using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class UpLoadAB
{
    // Deliberately unset: configure a project-owned publishing target before use.
    private const string ServerUser = "CONFIGURE_ME";
    private const string ServerIp = "CONFIGURE_ME";
    private const string ServerPath = "CONFIGURE_ME";
    private const string ManifestName = "ABCompareInfo.txt";

    [MenuItem("Tools/上传 AssetBundles 到服务器")]
    private static void Upload()
    {
        if (ServerUser == "CONFIGURE_ME" || ServerIp == "CONFIGURE_ME" || ServerPath == "CONFIGURE_ME")
        {
            UnityEngine.Debug.LogError("请先在 UpLoadAB.cs 中配置项目自己的发布目标；当前工具未绑定学习工程的旧服务器。");
            return;
        }

        string localPath = Path.Combine(Application.streamingAssetsPath, "AssetBundles");
        string manifestPath = Path.Combine(localPath, ManifestName);

        if (!Directory.Exists(localPath) || !File.Exists(manifestPath))
        {
            UnityEngine.Debug.LogError("请先构建 AssetBundle 和更新清单：" + localPath);
            return;
        }

        string privateKeyPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".ssh",
            "unity_ab_upload_v2"
        );

        if (!File.Exists(privateKeyPath))
        {
            UnityEngine.Debug.LogError("SSH 私钥不存在：" + privateKeyPath);
            return;
        }

        List<string> bundleFiles;
        try
        {
            bundleFiles = ReadBundleFileNames(manifestPath);
        }
        catch (Exception exception)
        {
            UnityEngine.Debug.LogError("更新清单格式错误：" + exception.Message);
            return;
        }

        // 先上传清单描述的所有 Bundle。只有这些文件全部成功后，才发布新清单。
        foreach (string fileName in bundleFiles)
        {
            string filePath = Path.Combine(localPath, fileName);
            if (!File.Exists(filePath) || !UploadFile(filePath, privateKeyPath))
            {
                UnityEngine.Debug.LogError("发布终止，新清单没有上传。失败文件：" + fileName);
                return;
            }
        }

        // ABCompareInfo.txt 是客户端看到新版本的入口，因此必须最后上传。
        if (!UploadFile(manifestPath, privateKeyPath))
        {
            UnityEngine.Debug.LogError("Bundle 已上传，但更新清单发布失败，客户端仍会使用旧清单。");
            return;
        }

        UnityEngine.Debug.Log($"AssetBundle 发布完成：{bundleFiles.Count} 个 Bundle，最后发布 {ManifestName}");
    }

    private static List<string> ReadBundleFileNames(string manifestPath)
    {
        List<string> result = new List<string>();
        foreach (string rawLine in File.ReadAllLines(manifestPath))
        {
            if (string.IsNullOrWhiteSpace(rawLine))
                continue;

            string[] values = rawLine.Split('|');
            if (values.Length != 3)
                throw new FormatException(rawLine);

            string fileName = values[0].Trim();
            // 当前学习项目使用平铺 Bundle 名称，禁止路径穿越或意外上传其他目录。
            if (string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName)
                throw new FormatException("非法文件名：" + fileName);

            result.Add(fileName);
        }

        return result;
    }

    private static bool UploadFile(string localFilePath, string privateKeyPath)
    {
        string destination = $"{ServerUser}@{ServerIp}:{ServerPath}";
        string arguments =
            "-o BatchMode=yes " +
            "-o IdentitiesOnly=yes " +
            "-i " + Quote(privateKeyPath) + " " +
            Quote(localFilePath) + " " +
            Quote(destination);

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = "scp.exe",
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        UnityEngine.Debug.Log("正在上传：" + Path.GetFileName(localFilePath));
        using Process process = Process.Start(startInfo);
        if (process == null)
        {
            UnityEngine.Debug.LogError("无法启动 scp.exe");
            return false;
        }

        // 同时读取两个输出流，避免其中一个缓冲区写满后进程互相等待。
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();

        string output = outputTask.GetAwaiter().GetResult();
        string error = errorTask.GetAwaiter().GetResult();
        if (process.ExitCode == 0)
            return true;

        UnityEngine.Debug.LogError(
            $"上传失败：{Path.GetFileName(localFilePath)}，退出码：{process.ExitCode}\n{output}\n{error}"
        );
        return false;
    }

    private static string Quote(string value) => "\"" + value + "\"";
}
