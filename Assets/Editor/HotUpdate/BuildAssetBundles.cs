using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

public static class BuildAssetBundles
{
    [MenuItem("Tools/Hot Update/Build AssetBundles", priority = 100)]
    public static void Build()
    {
        string outputPath = Path.Combine(Application.streamingAssetsPath, "AssetBundles");
        if (Directory.Exists(outputPath))
            Directory.Delete(outputPath, true);
        Directory.CreateDirectory(outputPath);

        AssetBundleBuild[] builds = CreateBuildMap();
        BuildPipeline.BuildAssetBundles(
            outputPath,
            builds,
            BuildAssetBundleOptions.ChunkBasedCompression,
            EditorUserBuildSettings.activeBuildTarget
        );

        ValidateLuaBundle(outputPath);

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

    private static void ValidateLuaBundle(string outputPath)
    {
        string bundlePath = Path.Combine(outputPath, "lua");
        AssetBundle bundle = AssetBundle.LoadFromFile(bundlePath);
        if (bundle == null)
            throw new BuildFailedException("Lua AssetBundle 构建后无法加载：" + bundlePath);

        try
        {
            var assetNames = new HashSet<string>(
                bundle.GetAllAssetNames(),
                System.StringComparer.OrdinalIgnoreCase);

            string[] requiredAssets =
            {
                "assets/lua/main.lua.txt",
                "assets/lua/ui/campaign/campaignpanel.lua.txt"
            };

            string[] missing = requiredAssets
                .Where(required => !assetNames.Contains(required))
                .ToArray();

            if (missing.Length > 0)
            {
                throw new BuildFailedException(
                    "Lua AssetBundle 缺少必要模块：" + string.Join(", ", missing));
            }
        }
        finally
        {
            bundle.Unload(true);
        }
    }

    private static AssetBundleBuild[] CreateBuildMap()
    {
        var builds = new List<AssetBundleBuild>();

        foreach (string bundleName in AssetDatabase.GetAllAssetBundleNames())
        {
            if (string.Equals(bundleName, "lua", System.StringComparison.OrdinalIgnoreCase))
                continue;

            string[] assets = AssetDatabase.GetAssetPathsFromAssetBundle(bundleName);
            if (assets.Length == 0)
                continue;

            builds.Add(new AssetBundleBuild
            {
                assetBundleName = bundleName,
                assetNames = assets
            });
        }

        string[] luaAssets = AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets/Lua" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => path.EndsWith(".lua.txt", System.StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, System.StringComparer.Ordinal)
            .ToArray();

        if (luaAssets.Length == 0)
            throw new BuildFailedException("Assets/Lua 下没有可构建的 .lua.txt 文件。");

        builds.Add(new AssetBundleBuild
        {
            assetBundleName = "lua",
            assetNames = luaAssets
        });

        return builds.ToArray();
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
