using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Downloads a complete AssetBundle version into a staging directory, verifies it,
/// then atomically replaces the active local version.
/// </summary>
public sealed class HotUpdateManager : MonoBehaviour
{
    public static HotUpdateManager Instance { get; private set; }

    private string manifestName = "ABCompareInfo.txt";
    private int requestTimeoutSeconds = 30;
    private string configuredLocalRoot;
    private HotUpdateEndpoint endpoint;

    public event Action<float> ProgressChanged;
    public event Action<string> Downloading;
    public event Action<HotUpdateStatus> StatusChanged;

    public bool LastUpdateSucceeded { get; private set; }
    public string LastError { get; private set; }
    public bool IsUpdating { get; private set; }
    public bool HasCompleted { get; private set; }
    public bool HasPendingActivation { get; private set; }
    public HotUpdateStatus CurrentStatus { get; private set; }

    private string LocalRoot => string.IsNullOrEmpty(configuredLocalRoot)
        ? Path.Combine(Application.persistentDataPath, "AssetBundles")
        : configuredLocalRoot;
    private string StagingRoot => LocalRoot + ".staging";
    private string BackupRoot => LocalRoot + ".backup";

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
    }

    public void Configure(HotUpdateSettings settings, string localRoot)
    {
        if (IsUpdating)
            throw new InvalidOperationException(
                "Cannot reconfigure HotUpdateManager while an update is running.");
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        manifestName = string.IsNullOrWhiteSpace(settings.manifestName)
            ? "ABCompareInfo.txt"
            : settings.manifestName.Trim();
        requestTimeoutSeconds = Mathf.Max(1, settings.requestTimeoutSeconds);
        configuredLocalRoot = localRoot;
    }

    public IEnumerator UpdateCoroutine(HotUpdateEndpoint selectedEndpoint)
    {
        endpoint = selectedEndpoint;
        IsUpdating = true;
        HasCompleted = false;
        LastUpdateSucceeded = false;
        LastError = null;
        ProgressChanged?.Invoke(0f);

        Directory.CreateDirectory(LocalRoot);
        RecreateDirectory(StagingRoot);

        Publish(HotUpdatePhase.DownloadingManifest, "正在下载更新清单");
        string manifestText;
        string manifestUrl = CombineUrl(endpoint.RootUrl, manifestName);
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

        Publish(HotUpdatePhase.ComparingFiles, "正在比较本地资源");
        List<BundleInfo> needDownload = new();
        List<BundleInfo> keepFiles = new();
        foreach (BundleInfo info in serverManifest.Values)
        {
            string localPath = GetSafeFilePath(LocalRoot, info.Name);
            if (IsFileCurrent(localPath, info))
                keepFiles.Add(info);
            else
                needDownload.Add(info);
        }

        ulong totalDownloadBytes = 0;
        foreach (BundleInfo info in needDownload)
            totalDownloadBytes += (ulong)info.Size;

        List<string> obsoleteFiles = FindObsoleteFiles(serverManifest);
        Debug.Log(
            $"资源比较完成：下载 {needDownload.Count}，保留 {keepFiles.Count}，" +
            $"清理 {obsoleteFiles.Count}");

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

        ulong completedDownloadBytes = 0;
        for (int index = 0; index < needDownload.Count; index++)
        {
            BundleInfo info = needDownload[index];
            Downloading?.Invoke(info.Name);
            PublishDownload(
                info.Name,
                index,
                needDownload.Count,
                completedDownloadBytes,
                totalDownloadBytes);

            string stagedPath = GetSafeFilePath(StagingRoot, info.Name);
            string temporaryPath = stagedPath + ".tmp";
            Directory.CreateDirectory(Path.GetDirectoryName(stagedPath));
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);

            string url = CombineUrl(endpoint.RootUrl, info.Name);
            using UnityWebRequest request = UnityWebRequest.Get(url);
            request.timeout = requestTimeoutSeconds;
            request.downloadHandler = new DownloadHandlerFile(temporaryPath)
            {
                removeFileOnAbort = true
            };

            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                ulong currentBytes = Math.Min((ulong)info.Size, request.downloadedBytes);
                PublishDownload(
                    info.Name,
                    index,
                    needDownload.Count,
                    completedDownloadBytes + currentBytes,
                    totalDownloadBytes);
                yield return null;
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                Fail($"下载 AssetBundle 失败：{info.Name}，{request.error}");
                DeleteDirectoryIfExists(StagingRoot);
                yield break;
            }

            Publish(
                HotUpdatePhase.VerifyingFiles,
                "正在校验 " + info.Name,
                info.Name,
                index,
                needDownload.Count,
                completedDownloadBytes,
                totalDownloadBytes);
            if (!IsFileCurrent(temporaryPath, info))
            {
                Fail($"AssetBundle 大小或 MD5 校验失败：{info.Name}");
                DeleteDirectoryIfExists(StagingRoot);
                yield break;
            }

            File.Move(temporaryPath, stagedPath);
            completedDownloadBytes += (ulong)info.Size;
        }

        Publish(
            HotUpdatePhase.VerifyingFiles,
            "正在校验完整资源版本",
            string.Empty,
            needDownload.Count,
            needDownload.Count,
            completedDownloadBytes,
            totalDownloadBytes);
        foreach (BundleInfo info in serverManifest.Values)
        {
            if (!IsFileCurrent(GetSafeFilePath(StagingRoot, info.Name), info))
            {
                Fail("暂存版本最终校验失败：" + info.Name);
                DeleteDirectoryIfExists(StagingRoot);
                yield break;
            }
        }

        File.WriteAllText(
            Path.Combine(StagingRoot, manifestName),
            manifestText,
            new UTF8Encoding(false));
        File.WriteAllText(
            Path.Combine(StagingRoot, "version.txt"),
            endpoint.Version + Environment.NewLine,
            new UTF8Encoding(false));

        Publish(
            HotUpdatePhase.ActivatingVersion,
            "正在启用资源版本 " + endpoint.Version,
            string.Empty,
            needDownload.Count,
            needDownload.Count,
            completedDownloadBytes,
            totalDownloadBytes);
        try
        {
            CommitStagingVersion();
        }
        catch (Exception exception)
        {
            Fail("切换正式资源版本失败：" + exception.Message);
            yield break;
        }

        string legacyManifestPath = Path.Combine(
            Application.persistentDataPath,
            manifestName);
        if (File.Exists(legacyManifestPath))
            File.Delete(legacyManifestPath);

        LastUpdateSucceeded = true;
        IsUpdating = false;
        HasCompleted = true;
        ProgressChanged?.Invoke(1f);
        Publish(
            HotUpdatePhase.ActivatingVersion,
            $"资源版本 {endpoint.Version} 已启用",
            string.Empty,
            needDownload.Count,
            needDownload.Count,
            totalDownloadBytes,
            totalDownloadBytes);
        Debug.Log(
            $"热更新完成：下载 {needDownload.Count}，复用 {keepFiles.Count}，" +
            $"清理 {obsoleteFiles.Count}，资源目录：{LocalRoot}");
    }

    public string GetLocalBundlePath(string bundleName)
    {
        return GetSafeFilePath(LocalRoot, bundleName);
    }

    public void ConfirmActiveVersion()
    {
        if (!HasPendingActivation)
            return;

        try
        {
            DeleteDirectoryIfExists(BackupRoot);
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "新版本已生效，但旧版本备份清理失败：" + exception.Message);
        }

        HasPendingActivation = false;
    }

    public bool RestorePreviousVersion(out string error)
    {
        if (!HasPendingActivation || !Directory.Exists(BackupRoot))
        {
            error = "没有可以恢复的旧资源版本。";
            return false;
        }

        try
        {
            DeleteDirectoryIfExists(LocalRoot);
            Directory.Move(BackupRoot, LocalRoot);
            HasPendingActivation = false;
            error = null;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private void PublishDownload(
        string fileName,
        int completedFiles,
        int totalFiles,
        ulong downloadedBytes,
        ulong totalBytes)
    {
        Publish(
            HotUpdatePhase.DownloadingBundles,
            "正在下载 " + fileName,
            fileName,
            completedFiles,
            totalFiles,
            downloadedBytes,
            totalBytes);
        ProgressChanged?.Invoke(
            totalBytes == 0 ? 1f : Math.Min(1f, (float)downloadedBytes / totalBytes));
    }

    private void Publish(
        HotUpdatePhase phase,
        string message,
        string currentFile = "",
        int completedFiles = 0,
        int totalFiles = 0,
        ulong downloadedBytes = 0,
        ulong totalBytes = 0)
    {
        CurrentStatus = new HotUpdateStatus(
            phase,
            endpoint.Source,
            endpoint.Version,
            currentFile,
            completedFiles,
            totalFiles,
            downloadedBytes,
            totalBytes,
            message,
            string.Empty);
        StatusChanged?.Invoke(CurrentStatus);
    }

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
            HasPendingActivation = true;
        }
        catch
        {
            if (oldVersionMoved && Directory.Exists(BackupRoot) && !Directory.Exists(LocalRoot))
                Directory.Move(BackupRoot, LocalRoot);
            throw;
        }
    }

    private List<string> FindObsoleteFiles(
        Dictionary<string, BundleInfo> serverManifest)
    {
        List<string> result = new();
        if (!Directory.Exists(LocalRoot))
            return result;

        foreach (string path in Directory.GetFiles(
                     LocalRoot,
                     "*",
                     SearchOption.AllDirectories))
        {
            string relative = path.Substring(LocalRoot.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Replace(Path.DirectorySeparatorChar, '/');
            if (!relative.Equals(manifestName, StringComparison.OrdinalIgnoreCase)
                && !relative.Equals("version.txt", StringComparison.OrdinalIgnoreCase)
                && !serverManifest.ContainsKey(relative))
            {
                result.Add(relative);
            }
        }

        return result;
    }

    private Dictionary<string, BundleInfo> ParseManifest(string text)
    {
        Dictionary<string, BundleInfo> result =
            new(StringComparer.OrdinalIgnoreCase);

        string[] lines = text.Split(
            new[] { '\r', '\n' },
            StringSplitOptions.RemoveEmptyEntries);
        foreach (string rawLine in lines)
        {
            string[] values = rawLine.Trim().Split('|');
            if (values.Length != 3
                || !long.TryParse(values[1], out long size)
                || size < 0)
            {
                throw new FormatException(rawLine);
            }

            string name = values[0].Trim();
            if (string.IsNullOrWhiteSpace(name) || Path.GetFileName(name) != name)
                throw new FormatException("非法 Bundle 文件名：" + name);

            if (name.Equals(manifestName, StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".manifest", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string md5 = values[2].Trim();
            if (md5.Length != 32 || !IsHex(md5))
                throw new FormatException("非法 MD5：" + rawLine);
            if (result.ContainsKey(name))
                throw new FormatException("更新清单包含重复文件：" + name);

            result.Add(name, new BundleInfo(name, size, md5));
        }

        if (result.Count == 0)
            throw new FormatException("服务器清单为空。");

        return result;
    }

    private static bool IsHex(string value)
    {
        foreach (char character in value)
        {
            bool valid = character >= '0' && character <= '9'
                         || character >= 'a' && character <= 'f'
                         || character >= 'A' && character <= 'F';
            if (!valid)
                return false;
        }

        return true;
    }

    private static bool IsFileCurrent(string path, BundleInfo info)
    {
        if (!File.Exists(path))
            return false;

        FileInfo file = new(path);
        return file.Length == info.Size
               && string.Equals(
                   CalculateMD5(path),
                   info.Md5,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string GetSafeFilePath(string root, string fileName)
    {
        string fullRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        string fullPath = Path.GetFullPath(Path.Combine(root, fileName));
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "文件路径超出热更新目录：" + fileName);
        return fullPath;
    }

    public static string CombineUrl(string root, string fileName)
    {
        return root.TrimEnd('/') + "/" + Uri.EscapeDataString(fileName.TrimStart('/'));
    }

    private static string CalculateMD5(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using MD5 md5 = MD5.Create();
        StringBuilder result = new();
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
        CurrentStatus = new HotUpdateStatus(
            HotUpdatePhase.Failed,
            endpoint.Source,
            endpoint.Version,
            string.Empty,
            0,
            0,
            0,
            0,
            "热更新失败",
            message);
        StatusChanged?.Invoke(CurrentStatus);
        Debug.LogError(message);
    }

    private readonly struct BundleInfo
    {
        public BundleInfo(string name, long size, string md5)
        {
            Name = name;
            Size = size;
            Md5 = md5;
        }

        public string Name { get; }
        public long Size { get; }
        public string Md5 { get; }
    }
}
