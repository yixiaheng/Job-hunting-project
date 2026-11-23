using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.UI;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class HotUpdateVerification
{
    private const string ReportPath = "Docs/HotUpdateVerification.json";

    [Serializable]
    private sealed class VerificationReport
    {
        public string generatedAt;
        public bool passed;
        public List<string> checks = new List<string>();
        public List<string> warnings = new List<string>();
        public List<string> errors = new List<string>();
    }

    [MenuItem("Tools/Hot Update/Verify Project Setup", priority = 140)]
    public static void Run()
    {
        VerificationReport report = new VerificationReport
        {
            generatedAt = DateTimeOffset.Now.ToString("O")
        };

        CheckVersionAndUrlRules(report);
        CheckRuntimeSettings(report);
        CheckAssetBundleOutput(report);
        CheckProgressAssets(report);
        CheckStartupScene(report);
        CheckBuildSettings(report);

        report.passed = report.errors.Count == 0;
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
        AssetDatabase.Refresh();

        if (!report.passed)
        {
            throw new BuildFailedException(
                "热更新验证失败：\n" + string.Join("\n", report.errors));
        }

        Debug.Log(
            $"热更新项目验证通过：{report.checks.Count} 项检查，"
            + $"{report.warnings.Count} 项提示。报告：{ReportPath}");
    }

    private static void CheckVersionAndUrlRules(VerificationReport report)
    {
        Expect(
            HotUpdateBootstrap.TryNormalizeVersion("01.002.3", out string normalized)
            && normalized == "1.2.3",
            "版本号规范化",
            report);
        Expect(
            !HotUpdateBootstrap.TryNormalizeVersion("1.2", out _)
            && !HotUpdateBootstrap.TryNormalizeVersion("1.2.beta", out _),
            "非法版本号拒绝",
            report);
        Expect(
            HotUpdateManager.CombineUrl(
                "https://update.example.com/update/releases/0.1.0/",
                "ABCompareInfo.txt")
            == "https://update.example.com/update/releases/0.1.0/ABCompareInfo.txt",
            "版本固定 URL 拼接",
            report);

        HotUpdateStatus halfway = new HotUpdateStatus(
            HotUpdatePhase.DownloadingBundles,
            HotUpdateSourceKind.StagingVersion,
            "0.1.0",
            "uiprefabs",
            1,
            3,
            512,
            1024,
            "test",
            string.Empty);
        Expect(Mathf.Approximately(halfway.Progress, 0.5f), "字节进度计算", report);
    }

    private static void CheckRuntimeSettings(VerificationReport report)
    {
        HotUpdateSettings settings = AssetDatabase.LoadAssetAtPath<HotUpdateSettings>(
            HotUpdateProjectSetup.SettingsPath);
        Expect(settings != null, "HotUpdateSettings 资源存在", report);
        if (settings == null)
            return;

        Expect(settings.currentVersionPath == "current/version.txt", "current 版本入口", report);
        Expect(settings.releasesPath == "releases", "Release 根路径", report);
        Expect(settings.manifestName == "ABCompareInfo.txt", "清单名称", report);
        Expect(settings.bundleFolder == "AssetBundles", "本地 Bundle 目录", report);
        Expect(settings.luaEntry == "Main", "Lua 启动入口", report);
        if (string.IsNullOrWhiteSpace(settings.productionBaseUrl))
        {
            report.warnings.Add(
                "正式 HTTPS 地址为空；Caddy 与域名完成前 Player 远程更新保持关闭。 ");
            Expect(!settings.tryRemoteUpdate, "未配置 HTTPS 时关闭 Player 远程更新", report);
        }
    }

    private static void CheckAssetBundleOutput(VerificationReport report)
    {
        string root = Path.Combine(Application.streamingAssetsPath, "AssetBundles");
        string mainBundle = Path.Combine(root, "AssetBundles");
        Expect(File.Exists(mainBundle), "主 AssetBundle 是文件并且存在", report);
        Expect(!Directory.Exists(mainBundle), "主 AssetBundle 未被误判为目录", report);

        try
        {
            IReadOnlyList<HotUpdateManifestEntry> entries =
                HotUpdateManifestUtility.ReadAndValidate(root);
            Expect(entries.Count > 0, "本地清单、大小与 MD5", report);
            VerifyManifestRejections(entries[0], report);
        }
        catch (Exception exception)
        {
            report.errors.Add("AssetBundle 清单校验：" + exception.Message);
        }
    }

    private static void VerifyManifestRejections(
        HotUpdateManifestEntry source,
        VerificationReport report)
    {
        string temporary = Path.GetFullPath("Temp/HotUpdateManifestVerification");
        if (Directory.Exists(temporary))
            Directory.Delete(temporary, true);
        Directory.CreateDirectory(temporary);
        try
        {
            string copied = Path.Combine(temporary, source.Name);
            File.Copy(source.LocalPath, copied, true);
            string line = $"{source.Name}|{source.Size}|{source.Md5}";
            File.WriteAllText(
                Path.Combine(temporary, "ABCompareInfo.txt"),
                line + Environment.NewLine + line + Environment.NewLine);
            bool duplicateRejected = false;
            try
            {
                HotUpdateManifestUtility.ReadAndValidate(temporary);
            }
            catch (FormatException)
            {
                duplicateRejected = true;
            }

            Expect(duplicateRejected, "重复清单项拒绝", report);

            File.WriteAllText(
                Path.Combine(temporary, "ABCompareInfo.txt"),
                "../outside|0|00000000000000000000000000000000");
            bool pathRejected = false;
            try
            {
                HotUpdateManifestUtility.ReadAndValidate(temporary);
            }
            catch (FormatException)
            {
                pathRejected = true;
            }

            Expect(pathRejected, "越界清单路径拒绝", report);
        }
        finally
        {
            if (Directory.Exists(temporary))
                Directory.Delete(temporary, true);
        }
    }

    private static void CheckProgressAssets(VerificationReport report)
    {
        UIConfig config = AssetDatabase.LoadAssetAtPath<UIConfig>(
            HotUpdateProjectSetup.ProgressConfigPath);
        Expect(config != null, "Overlay UIConfig 存在", report);
        if (config == null)
            return;

        Expect(config.id == UIId.HotUpdateProgress, "热更新 UIId", report);
        Expect(config.layer == UILayer.Overlay, "热更新 UIConfig 位于 Overlay 层", report);
        Expect(config.space == UISpace.Screen, "热更新 UI 使用 Screen Space", report);
        Expect(config.lifetime == UILifetime.Global, "热更新 UI 生命周期为 Global", report);
        Expect(config.modal && config.blockMovement && config.blockCamera, "热更新 UI 阻断业务输入", report);
        Expect(!config.closeWhenClickMask && !config.handlesBack, "热更新 UI 不会被误关闭", report);
        Expect(
            AssetDatabase.GetAssetPath(config).StartsWith("Assets/Config/UI/Overlay/"),
            "UIConfig 目录与 UILayer 一致",
            report);

        HotUpdateProgressPanel panel = config.prefab as HotUpdateProgressPanel;
        Expect(panel != null, "进度面板预制体引用", report);
        if (panel != null)
        {
            Expect(
                AssetDatabase.GetAssetPath(panel).StartsWith(
                    "Assets/Prefabs/UIPrefabs/Overlay/"),
                "进度面板目录与 UILayer 一致",
                report);
            SerializedObject serialized = new SerializedObject(panel);
            string[] references =
            {
                "phaseLabel", "versionLabel", "fileLabel", "bytesLabel",
                "percentLabel", "progressBar", "retryButton"
            };
            foreach (string reference in references)
            {
                Expect(
                    serialized.FindProperty(reference).objectReferenceValue != null,
                    "面板引用：" + reference,
                    report);
            }
        }

        UIRegistry registry = AssetDatabase.LoadAssetAtPath<UIRegistry>(
            HotUpdateProjectSetup.RegistryPath);
        Expect(registry != null, "UIRegistry 存在", report);
        if (registry != null)
        {
            registry.Rebuild();
            Expect(
                registry.TryGet(UIId.HotUpdateProgress, out UIConfig registered)
                && registered == config,
                "UIRegistry 已登记热更新面板",
                report);
        }
    }

    private static void CheckStartupScene(VerificationReport report)
    {
        SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(
            HotUpdateProjectSetup.StartupScenePath);
        Expect(sceneAsset != null, "热更新启动场景存在", report);
        if (sceneAsset == null)
            return;

        Scene scene = SceneManager.GetSceneByPath(HotUpdateProjectSetup.StartupScenePath);
        bool wasLoaded = scene.IsValid() && scene.isLoaded;
        if (!wasLoaded)
        {
            scene = EditorSceneManager.OpenScene(
                HotUpdateProjectSetup.StartupScenePath,
                OpenSceneMode.Additive);
        }

        try
        {
            int controllers = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<HotUpdateStartupController>(true))
                .Count();
            Expect(controllers == 1, "启动场景包含唯一控制器", report);
        }
        finally
        {
            if (!wasLoaded && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void CheckBuildSettings(VerificationReport report)
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        Expect(
            scenes.Length > 0
            && scenes[0].enabled
            && scenes[0].path == HotUpdateProjectSetup.StartupScenePath,
            "启动场景位于 Build Settings 第一项",
            report);
        Expect(
            scenes.Any(scene => scene.enabled && scene.path == "Assets/Scenes/SampleScene.unity"),
            "业务 SampleScene 保留在 Build Settings",
            report);
    }

    private static void Expect(
        bool condition,
        string description,
        VerificationReport report)
    {
        if (condition)
            report.checks.Add(description);
        else
            report.errors.Add(description);
    }
}
