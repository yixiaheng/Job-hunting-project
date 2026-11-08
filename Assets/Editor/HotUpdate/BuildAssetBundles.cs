using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class BuildAssetBundles
{
    [MenuItem("Tools/Build AssetBundles")]
    private static void Build()
    {
        string outputPath = Path.Combine(Application.streamingAssetsPath, "AssetBundles");
        if (Directory.Exists(outputPath))
            Directory.Delete(outputPath, true);
        Directory.CreateDirectory(outputPath);

        BuildPipeline.BuildAssetBundles(
            outputPath,
            BuildAssetBundleOptions.ChunkBasedCompression,
            EditorUserBuildSettings.activeBuildTarget
        );

        StringBuilder compareInfo = new StringBuilder();
        foreach (string filePath in Directory.GetFiles(outputPath, "*", SearchOption.AllDirectories))
        {
            if (filePath.EndsWith(".manifest") || filePath.EndsWith(".meta") ||
                Path.GetFileName(filePath) == "ABCompareInfo.txt")
                continue;

            FileInfo file = new FileInfo(filePath);
            string relativePath = filePath.Substring(outputPath.Length + 1)
                .Replace(Path.DirectorySeparatorChar, '/');
            compareInfo.AppendLine($"{relativePath}|{file.Length}|{CalculateMD5(filePath)}");
        }

        File.WriteAllText(
            Path.Combine(outputPath, "ABCompareInfo.txt"),
            compareInfo.ToString(),
            new UTF8Encoding(false)
        );

        AssetDatabase.Refresh();
        Debug.Log("AssetBundle 与更新清单已生成：" + outputPath);
    }

    private static string CalculateMD5(string filePath)
    {
        using FileStream stream = File.OpenRead(filePath);
        using MD5 md5 = MD5.Create();
        StringBuilder result = new StringBuilder();
        foreach (byte value in md5.ComputeHash(stream))
            result.Append(value.ToString("X2"));
        return result.ToString();
    }
}
