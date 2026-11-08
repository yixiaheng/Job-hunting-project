using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 基础 AssetBundle 热更新管理器。
/// 服务器清单格式：文件名|文件大小|MD5（每行一个文件）。
/// </summary>
public class HotUpdateManager : MonoBehaviour
{
    public static HotUpdateManager Instance;

    [SerializeField] private string serverRoot = "";
    [SerializeField] private string manifestName = "ABCompareInfo.txt";
    [SerializeField] private int requestTimeoutSeconds = 30;
    private string configuredLocalRoot;

    public event Action<float> ProgressChanged;
    public event Action<string> Downloading;

    public bool LastUpdateSucceeded { get; private set; }
    public string LastError { get; private set; }
    public bool IsUpdating { get; private set; }
    public bool HasCompleted { get; private set; }

    private string LocalRoot => string.IsNullOrEmpty(configuredLocalRoot)
        ? Path.Combine(Application.persistentDataPath, "AssetBundles")
        : configuredLocalRoot;
    private string StagingRoot => LocalRoot + ".staging";
    private string BackupRoot => LocalRoot + ".backup";
    private string LocalManifestPath => Path.Combine(LocalRoot, manifestName);

    void Awake()
    {
        if(Instance == null)
            Instance = this;
    }

    public void Configure(HotUpdateSettings settings, string localRoot)
    {
        if (IsUpdating)
            throw new InvalidOperationException("Cannot reconfigure HotUpdateManager while an update is running.");
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        serverRoot = settings.serverRoot == null ? "" : settings.serverRoot.Trim();
        manifestName = string.IsNullOrWhiteSpace(settings.manifestName)
            ? "ABCompareInfo.txt"
            : settings.manifestName.Trim();
        requestTimeoutSeconds = Mathf.Max(1, settings.requestTimeoutSeconds);
        configuredLocalRoot = localRoot;
    }

    public void StartUpdate() => StartCoroutine(UpdateCoroutine());

    public IEnumerator UpdateCoroutine()
    {
        IsUpdating = true;
        HasCompleted = false;
        LastUpdateSucceeded = false;
        LastError = null;
        ProgressChanged?.Invoke(0f);

        Directory.CreateDirectory(LocalRoot);
        RecreateDirectory(StagingRoot);

        string manifestText;
        string manifestUrl = CombineUrl(serverRoot, manifestName);
        using (UnityWebRequest request = UnityWebRequest.Get(manifestUrl))
        {
            request.timeout = requestTimeoutSeconds;
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Fail($"下载服务器更新清单失败：{request.error}\n{manifestUrl}");
                DeleteDirectoryIfExists(StagingRoot);
                yield break;
            }

            manifestText = request.downloadHandler.text;
        }

        Dictionary<string, BundleInfo> serverManifest;
        try
        {
            serverManifest = ParseManifest(manifestText);
        }
        catch (Exception exception)
        {
            Fail("解析服务器更新清单失败：" + exception.Message);
            DeleteDirectoryIfExists(StagingRoot);
            yield break;
        }

        List<BundleInfo> needDownload = new List<BundleInfo>();
        List<BundleInfo> keepFiles = new List<BundleInfo>();
        foreach (BundleInfo info in serverManifest.Values)
        {
            string localPath = GetSafeFilePath(LocalRoot, info.Name);
            if (IsFileCurrent(localPath, info))
                keepFiles.Add(info);
            else
                needDownload.Add(info);
        }

        List<string> obsoleteFiles = FindObsoleteFiles(serverManifest);
        Debug.Log($"资源比较完成：下载 {needDownload.Count}，保留 {keepFiles.Count}，清理 {obsoleteFiles.Count}");

        // 未变化的文件也复制进暂存版本。这样暂存目录会成为一个完整、可独立使用的新版本。
        try
        {
            foreach (BundleInfo info in keepFiles)
            {
                string source = GetSafeFilePath(LocalRoot, info.Name);
                string destination = GetSafeFilePath(StagingRoot, info.Name);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(source, destination, true);
            }
        }
        catch (Exception exception)
        {
            Fail("准备暂存版本失败：" + exception.Message);
            DeleteDirectoryIfExists(StagingRoot);
            yield break;
        }

        // 变化文件全部下载到 staging，正式目录此时完全不动。
        for (int index = 0; index < needDownload.Count; index++)
        {
            BundleInfo info = needDownload[index];
            Downloading?.Invoke(info.Name);

            string stagedPath = GetSafeFilePath(StagingRoot, info.Name);
            string temporaryPath = stagedPath + ".tmp";
            Directory.CreateDirectory(Path.GetDirectoryName(stagedPath));
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);

            string url = CombineUrl(serverRoot, info.Name);
            using UnityWebRequest request = UnityWebRequest.Get(url);
            request.timeout = requestTimeoutSeconds;
            request.downloadHandler = new DownloadHandlerFile(temporaryPath)
            {
                removeFileOnAbort = true
            };

            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                float fileProgress = Mathf.Max(0f, request.downloadProgress);
                ProgressChanged?.Invoke((index + fileProgress) / Math.Max(1, needDownload.Count));
                yield return null;
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                Fail($"下载 AssetBundle 失败：{info.Name}，{request.error}");
                DeleteDirectoryIfExists(StagingRoot);
                yield break;
            }

            // 先快速检查长度，再计算 MD5。任何一项不一致都不能进入正式版本。
            if (!IsFileCurrent(temporaryPath, info))
            {
                Fail($"AssetBundle 大小或 MD5 校验失败：{info.Name}");
                DeleteDirectoryIfExists(StagingRoot);
                yield break;
            }

            File.Move(temporaryPath, stagedPath);
            ProgressChanged?.Invoke((index + 1f) / Math.Max(1, needDownload.Count));
        }

        // 在切换前再检查一遍暂存版本，保证其中每个清单文件都完整存在。
        foreach (BundleInfo info in serverManifest.Values)
        {
            if (!IsFileCurrent(GetSafeFilePath(StagingRoot, info.Name), info))
            {
                Fail("暂存版本最终校验失败：" + info.Name);
                DeleteDirectoryIfExists(StagingRoot);
                yield break;
            }
        }

        File.WriteAllText(Path.Combine(StagingRoot, manifestName), manifestText, new UTF8Encoding(false));

        try
        {
            CommitStagingVersion();
        }
        catch (Exception exception)
        {
            Fail("切换正式资源版本失败：" + exception.Message);
            yield break;
        }

        // 早期学习代码把清单放在 persistentDataPath 根目录；新流程已把它放进版本目录。
        string legacyManifestPath = Path.Combine(Application.persistentDataPath, manifestName);
        if (File.Exists(legacyManifestPath))
            File.Delete(legacyManifestPath);

        LastUpdateSucceeded = true;
        IsUpdating = false;
        HasCompleted = true;
        ProgressChanged?.Invoke(1f);
        Debug.Log(
            $"热更新完成：下载 {needDownload.Count}，复用 {keepFiles.Count}，清理 {obsoleteFiles.Count}，资源目录：{LocalRoot}"
        );
    }

    public string GetLocalBundlePath(string bundleName) => GetSafeFilePath(LocalRoot, bundleName);

    private void CommitStagingVersion()
    {
        DeleteDirectoryIfExists(BackupRoot);

        bool oldVersionMoved = false;
        if (Directory.Exists(LocalRoot))
        {
            Directory.Move(LocalRoot, BackupRoot);
            oldVersionMoved = true;
        }

        try
        {
            Directory.Move(StagingRoot, LocalRoot);
        }
        catch
        {
            if (oldVersionMoved && Directory.Exists(BackupRoot) && !Directory.Exists(LocalRoot))
                Directory.Move(BackupRoot, LocalRoot);
            throw;
        }

        // 新版本已经切换成功。删除备份，也就同时清理了服务器清单中不存在的旧资源。
        try
        {
            DeleteDirectoryIfExists(BackupRoot);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("新版本已生效，但旧版本备份清理失败：" + exception.Message);
        }
    }

    private List<string> FindObsoleteFiles(Dictionary<string, BundleInfo> serverManifest)
    {
        List<string> result = new List<string>();
        if (!Directory.Exists(LocalRoot))
            return result;

        foreach (string path in Directory.GetFiles(LocalRoot, "*", SearchOption.AllDirectories))
        {
            string relative = path.Substring(LocalRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            relative = relative.Replace(Path.DirectorySeparatorChar, '/');
            if (!relative.Equals(manifestName, StringComparison.OrdinalIgnoreCase) &&
                !serverManifest.ContainsKey(relative))
            {
                result.Add(relative);
            }
        }

        return result;
    }

    private Dictionary<string, BundleInfo> ParseManifest(string text)
    {
        Dictionary<string, BundleInfo> result =
            new Dictionary<string, BundleInfo>(StringComparer.OrdinalIgnoreCase);

        string[] lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string rawLine in lines)
        {
            string[] values = rawLine.Trim().Split('|');
            if (values.Length != 3 || !long.TryParse(values[1], out long size) || size < 0)
                throw new FormatException(rawLine);

            string name = values[0].Trim();
            if (string.IsNullOrWhiteSpace(name) || Path.GetFileName(name) != name)
                throw new FormatException("非法 Bundle 文件名：" + name);

            // 兼容旧版构建脚本生成的不干净清单。重新构建后这些条目不会再出现。
            if (name.Equals(manifestName, StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(".manifest", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string md5 = values[2].Trim();
            if (md5.Length != 32)
                throw new FormatException("非法 MD5：" + rawLine);

            result.Add(name, new BundleInfo(name, size, md5));
        }

        if (result.Count == 0)
            throw new FormatException("服务器清单为空。");

        return result;
    }

    private static bool IsFileCurrent(string path, BundleInfo info)
    {
        if (!File.Exists(path))
            return false;

        FileInfo file = new FileInfo(path);
        return file.Length == info.Size &&
               string.Equals(CalculateMD5(path), info.Md5, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetSafeFilePath(string root, string fileName)
    {
        string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string fullPath = Path.GetFullPath(Path.Combine(root, fileName));
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("文件路径超出热更新目录：" + fileName);
        return fullPath;
    }

    private static string CombineUrl(string root, string fileName) =>
        root.TrimEnd('/') + "/" + Uri.EscapeDataString(fileName.TrimStart('/'));

    private static string CalculateMD5(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using MD5 md5 = MD5.Create();
        StringBuilder result = new StringBuilder();
        foreach (byte value in md5.ComputeHash(stream))
            result.Append(value.ToString("X2"));
        return result.ToString();
    }

    private static void RecreateDirectory(string path)
    {
        DeleteDirectoryIfExists(path);
        Directory.CreateDirectory(path);
    }

    private static void DeleteDirectoryIfExists(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, true);
    }

    private void Fail(string message)
    {
        LastError = message;
        LastUpdateSucceeded = false;
        IsUpdating = false;
        HasCompleted = true;
        Debug.LogError(message);
    }

    private readonly struct BundleInfo
    {
        public readonly string Name;
        public readonly long Size;
        public readonly string Md5;

        public BundleInfo(string name, long size, string md5)
        {
            Name = name;
            Size = size;
            Md5 = md5;
        }
    }
}
