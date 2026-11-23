using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

public sealed class HotUpdateManifestEntry
{
    public HotUpdateManifestEntry(string name, long size, string md5, string localPath)
    {
        Name = name;
        Size = size;
        Md5 = md5;
        LocalPath = localPath;
    }

    public string Name { get; }
    public long Size { get; }
    public string Md5 { get; }
    public string LocalPath { get; }
}

public static class HotUpdateManifestUtility
{
    private static readonly Regex RemoteFileNamePattern =
        new Regex("^[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant);

    public static IReadOnlyList<HotUpdateManifestEntry> ReadAndValidate(
        string bundleDirectory,
        string manifestName = "ABCompareInfo.txt")
    {
        if (string.IsNullOrWhiteSpace(bundleDirectory))
            throw new ArgumentException("AssetBundle 目录不能为空。", nameof(bundleDirectory));
        if (!Directory.Exists(bundleDirectory))
            throw new DirectoryNotFoundException("AssetBundle 目录不存在：" + bundleDirectory);

        string manifestPath = Path.Combine(bundleDirectory, manifestName);
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException("找不到热更新清单。", manifestPath);

        List<HotUpdateManifestEntry> entries = new List<HotUpdateManifestEntry>();
        HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string rawLine in File.ReadAllLines(manifestPath))
        {
            if (string.IsNullOrWhiteSpace(rawLine))
                continue;

            string[] values = rawLine.Trim().Split('|');
            if (values.Length != 3
                || !long.TryParse(
                    values[1].Trim(),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out long expectedSize)
                || expectedSize < 0)
            {
                throw new FormatException("清单行格式错误：" + rawLine);
            }

            string name = values[0].Trim();
            if (string.IsNullOrWhiteSpace(name)
                || Path.GetFileName(name) != name
                || !RemoteFileNamePattern.IsMatch(name))
            {
                throw new FormatException("Bundle 文件名不适合安全发布：" + name);
            }

            if (!names.Add(name))
                throw new FormatException("清单包含重复文件：" + name);

            string md5 = values[2].Trim().ToUpperInvariant();
            if (md5.Length != 32 || !IsHex(md5))
                throw new FormatException("清单包含无效 MD5：" + rawLine);

            string localPath = Path.Combine(bundleDirectory, name);
            if (!File.Exists(localPath))
                throw new FileNotFoundException("清单所列 Bundle 不存在。", localPath);

            FileInfo file = new FileInfo(localPath);
            if (file.Length != expectedSize)
            {
                throw new InvalidDataException(
                    $"Bundle 大小不匹配：{name}，清单 {expectedSize}，实际 {file.Length}。");
            }

            string actualMd5 = CalculateMD5(localPath);
            if (!string.Equals(actualMd5, md5, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Bundle MD5 不匹配：{name}，清单 {md5}，实际 {actualMd5}。");
            }

            entries.Add(new HotUpdateManifestEntry(name, expectedSize, md5, localPath));
        }

        if (entries.Count == 0)
            throw new InvalidDataException("热更新清单为空。");

        return entries;
    }

    public static string CalculateMD5(string filePath)
    {
        using FileStream stream = File.OpenRead(filePath);
        using MD5 md5 = MD5.Create();
        StringBuilder result = new StringBuilder();
        foreach (byte value in md5.ComputeHash(stream))
            result.Append(value.ToString("X2"));
        return result.ToString();
    }

    private static bool IsHex(string value)
    {
        foreach (char character in value)
        {
            bool valid = character >= '0' && character <= '9'
                         || character >= 'A' && character <= 'F';
            if (!valid)
                return false;
        }

        return true;
    }
}
