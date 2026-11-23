using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

public sealed class HotUpdatePublishResult
{
    public HotUpdatePublishResult(string version, string remotePath, int fileCount)
    {
        Version = version;
        RemotePath = remotePath;
        FileCount = fileCount;
    }

    public string Version { get; }
    public string RemotePath { get; }
    public int FileCount { get; }
}

public static class HotUpdatePublisher
{
    private const string ManifestName = "ABCompareInfo.txt";
    private static readonly Regex HostPattern =
        new Regex("^[A-Za-z0-9.-]+$", RegexOptions.CultureInvariant);
    private static readonly Regex UserPattern =
        new Regex("^[A-Za-z_][A-Za-z0-9_-]*$", RegexOptions.CultureInvariant);
    private static readonly Regex RemoteRootPattern =
        new Regex("^/[A-Za-z0-9._/-]+$", RegexOptions.CultureInvariant);

    public static IReadOnlyList<HotUpdateManifestEntry> ValidateLocal(
        HotUpdatePublishSettingsData settings,
        string bundleDirectory)
    {
        ValidateSettings(settings);
        return HotUpdateManifestUtility.ReadAndValidate(bundleDirectory, ManifestName);
    }

    public static Task<HotUpdatePublishResult> PublishAsync(
        HotUpdatePublishSettingsData settings,
        string bundleDirectory,
        Action<string> report)
    {
        HotUpdatePublishSettingsData snapshot = CloneAndValidate(settings);
        IReadOnlyList<HotUpdateManifestEntry> entries =
            HotUpdateManifestUtility.ReadAndValidate(bundleDirectory, ManifestName);
        string manifestPath = Path.Combine(bundleDirectory, ManifestName);

        return Task.Run(
            () => PublishCore(snapshot, entries, manifestPath, report));
    }

    private static HotUpdatePublishResult PublishCore(
        HotUpdatePublishSettingsData settings,
        IReadOnlyList<HotUpdateManifestEntry> entries,
        string manifestPath,
        Action<string> report)
    {
        string root = settings.remoteRoot.TrimEnd('/');
        string uploadName = "upload-" + settings.version + "-"
                            + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-"
                            + Guid.NewGuid().ToString("N").Substring(0, 8);
        string uploadPath = root + "/uploads/" + uploadName;
        string stagingPath = root + "/staging/" + settings.version;
        bool temporaryDirectoryCreated = false;

        try
        {
            report?.Invoke("检查远端目标并创建独立上传目录……");
            string prepare =
                "set -eu; "
                + "if [ -e " + stagingPath + " ]; then "
                + "echo 'staging version already exists: " + settings.version + "' >&2; "
                + "exit 23; fi; "
                + "mkdir -p " + root + "/uploads " + root + "/staging; "
                + "mkdir " + uploadPath;
            RunSsh(settings, prepare, 30000);
            temporaryDirectoryCreated = true;

            int completed = 0;
            foreach (HotUpdateManifestEntry entry in entries)
            {
                completed++;
                report?.Invoke($"上传 {entry.Name} ({completed}/{entries.Count})……");
                RunScp(settings, entry.LocalPath, uploadPath + "/", 180000);
            }

            report?.Invoke("上传 ABCompareInfo.txt……");
            RunScp(settings, manifestPath, uploadPath + "/", 30000);

            report?.Invoke("在服务器上复核文件大小和 MD5……");
            StringBuilder verify = new StringBuilder("set -eu;");
            foreach (HotUpdateManifestEntry entry in entries)
            {
                string remoteFile = uploadPath + "/" + entry.Name;
                verify.Append(" test $(stat -c %s ")
                    .Append(remoteFile)
                    .Append(") -eq ")
                    .Append(entry.Size)
                    .Append(';');
                verify.Append(" test $(md5sum ")
                    .Append(remoteFile)
                    .Append(" | cut -d' ' -f1) = ")
                    .Append(entry.Md5.ToLowerInvariant())
                    .Append(';');
            }

            verify.Append(" printf '%s\\n' ")
                .Append(settings.version)
                .Append(" > ")
                .Append(uploadPath)
                .Append("/version.txt;")
                .Append(" chmod 664 ")
                .Append(uploadPath)
                .Append("/*;")
                .Append(" test ! -e ")
                .Append(stagingPath)
                .Append(';')
                .Append(" mv -T ")
                .Append(uploadPath)
                .Append(' ')
                .Append(stagingPath);
            RunSsh(settings, verify.ToString(), 60000);
            temporaryDirectoryCreated = false;

            report?.Invoke("Staging 上传完成：" + stagingPath);
            return new HotUpdatePublishResult(
                settings.version,
                stagingPath,
                entries.Count + 2);
        }
        catch
        {
            if (temporaryDirectoryCreated)
            {
                try
                {
                    RunSsh(settings, "rm -rf " + uploadPath, 30000);
                }
                catch
                {
                    report?.Invoke("远端临时目录未能自动清理：" + uploadPath);
                }
            }

            throw;
        }
    }

    private static HotUpdatePublishSettingsData CloneAndValidate(
        HotUpdatePublishSettingsData source)
    {
        ValidateSettings(source);
        HotUpdateBootstrap.TryNormalizeVersion(source.version, out string version);
        return new HotUpdatePublishSettingsData
        {
            host = source.host.Trim(),
            port = source.port,
            user = source.user.Trim(),
            remoteRoot = source.remoteRoot.Trim().TrimEnd('/'),
            privateKeyPath = Path.GetFullPath(source.privateKeyPath.Trim()),
            version = version
        };
    }

    private static void ValidateSettings(HotUpdatePublishSettingsData settings)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));
        if (string.IsNullOrWhiteSpace(settings.host)
            || !HostPattern.IsMatch(settings.host.Trim()))
        {
            throw new InvalidDataException("SSH 主机名或 IP 格式无效。");
        }

        if (settings.port < 1 || settings.port > 65535)
            throw new InvalidDataException("SSH 端口必须在 1 到 65535 之间。");
        if (string.IsNullOrWhiteSpace(settings.user)
            || !UserPattern.IsMatch(settings.user.Trim()))
        {
            throw new InvalidDataException("SSH 用户名格式无效。");
        }

        string root = settings.remoteRoot == null
            ? string.Empty
            : settings.remoteRoot.Trim().TrimEnd('/');
        if (!RemoteRootPattern.IsMatch(root)
            || root.Contains("//")
            || root.Contains("/../")
            || root.EndsWith("/..", StringComparison.Ordinal))
        {
            throw new InvalidDataException("远端根目录必须是简单的绝对 Linux 路径。");
        }

        if (string.IsNullOrWhiteSpace(settings.privateKeyPath)
            || !File.Exists(settings.privateKeyPath.Trim()))
        {
            throw new FileNotFoundException("SSH 私钥不存在。", settings.privateKeyPath);
        }

        if (!HotUpdateBootstrap.TryNormalizeVersion(settings.version, out _))
            throw new InvalidDataException("版本号必须是三段数字，例如 0.1.0。");
    }

    private static void RunSsh(
        HotUpdatePublishSettingsData settings,
        string remoteCommand,
        int timeoutMilliseconds)
    {
        string arguments = CommonArguments(settings, false)
                           + " " + settings.user + "@" + settings.host
                           + " " + Quote(remoteCommand);
        RunProcess(ResolveOpenSshTool("ssh.exe"), arguments, timeoutMilliseconds);
    }

    private static void RunScp(
        HotUpdatePublishSettingsData settings,
        string localFile,
        string remoteDirectory,
        int timeoutMilliseconds)
    {
        string arguments = CommonArguments(settings, true)
                           + " " + Quote(localFile)
                           + " " + settings.user + "@" + settings.host
                           + ":" + remoteDirectory;
        RunProcess(ResolveOpenSshTool("scp.exe"), arguments, timeoutMilliseconds);
    }

    private static string CommonArguments(
        HotUpdatePublishSettingsData settings,
        bool scp)
    {
        return (scp ? "-P " : "-p ") + settings.port
               + " -o BatchMode=yes"
               + " -o IdentitiesOnly=yes"
               + " -o StrictHostKeyChecking=accept-new"
               + " -o ConnectTimeout=15"
               + " -i " + Quote(settings.privateKeyPath);
    }

    private static void RunProcess(
        string fileName,
        string arguments,
        int timeoutMilliseconds)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using Process process = Process.Start(startInfo);
        if (process == null)
            throw new InvalidOperationException("无法启动进程：" + fileName);

        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(timeoutMilliseconds))
        {
            process.Kill();
            throw new TimeoutException("SSH/SCP 操作超时。");
        }

        string output = outputTask.GetAwaiter().GetResult().Trim();
        string error = errorTask.GetAwaiter().GetResult().Trim();
        if (process.ExitCode == 0)
            return;

        throw new InvalidOperationException(
            $"{Path.GetFileName(fileName)} 退出码 {process.ExitCode}。\n{output}\n{error}".Trim());
    }

    private static string ResolveOpenSshTool(string fileName)
    {
        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string systemTool = Path.Combine(windows, "System32", "OpenSSH", fileName);
        return File.Exists(systemTool) ? systemTool : fileName;
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }
}
