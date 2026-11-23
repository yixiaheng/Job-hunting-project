using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class HotUpdatePublisherWindow : EditorWindow
{
    private HotUpdatePublishSettingsData publishSettings;
    private HotUpdateEditorPlaySettingsData playSettings;
    private readonly ConcurrentQueue<string> pendingMessages =
        new ConcurrentQueue<string>();
    private Task<HotUpdatePublishResult> publishTask;
    private Vector2 scroll;
    private string activityLog = string.Empty;

    [MenuItem("Tools/Hot Update/Publisher", priority = 120)]
    public static void Open()
    {
        GetWindow<HotUpdatePublisherWindow>("Hot Update").Show();
    }

    private void OnEnable()
    {
        publishSettings = HotUpdateEditorSettings.Load();
        playSettings = HotUpdateEditorPlaySettings.Load();
        if (string.IsNullOrWhiteSpace(playSettings.version))
            playSettings.version = publishSettings.version;
    }

    private void OnGUI()
    {
        if (publishSettings == null || playSettings == null)
            OnEnable();

        EditorGUILayout.LabelField("AssetBundle 构建与 Staging 上传", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        publishSettings.version = EditorGUILayout.TextField("版本号", publishSettings.version);
        publishSettings.host = EditorGUILayout.TextField("SSH 主机", publishSettings.host);
        publishSettings.port = EditorGUILayout.IntField("SSH 端口", publishSettings.port);
        publishSettings.user = EditorGUILayout.TextField("SSH 用户", publishSettings.user);
        publishSettings.remoteRoot = EditorGUILayout.TextField("远端更新根目录", publishSettings.remoteRoot);
        EditorGUILayout.BeginHorizontal();
        publishSettings.privateKeyPath = EditorGUILayout.TextField(
            "SSH 私钥",
            publishSettings.privateKeyPath);
        if (GUILayout.Button("选择", GUILayout.Width(56f)))
        {
            string selected = EditorUtility.OpenFilePanel(
                "选择 SSH 私钥",
                Path.GetDirectoryName(publishSettings.privateKeyPath),
                string.Empty);
            if (!string.IsNullOrEmpty(selected))
                publishSettings.privateKeyPath = selected;
        }
        EditorGUILayout.EndHorizontal();
        if (EditorGUI.EndChangeCheck())
        {
            playSettings.version = publishSettings.version;
            SaveSettings();
        }

        string bundleDirectory = Path.Combine(
            Application.streamingAssetsPath,
            "AssetBundles");
        EditorGUILayout.LabelField("本地产物", bundleDirectory);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("应用项目设置"))
                RunAndLog(HotUpdateProjectSetup.Apply);
            if (GUILayout.Button("构建 AssetBundles"))
                RunAndLog(BuildAssetBundles.Build);
            if (GUILayout.Button("校验本地产物"))
                ValidateLocal(bundleDirectory);
        }

        using (new EditorGUI.DisabledScope(publishTask != null))
        {
            if (GUILayout.Button("上传到 Staging", GUILayout.Height(30f)))
                BeginPublish(bundleDirectory);
        }

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Editor 运行来源", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        playSettings.source = (HotUpdateSourceKind)EditorGUILayout.EnumPopup(
            "资源来源",
            playSettings.source);
        playSettings.version = EditorGUILayout.TextField("测试版本", playSettings.version);
        playSettings.stagingBaseUrl = EditorGUILayout.TextField(
            "Staging HTTP 根地址",
            playSettings.stagingBaseUrl);
        if (EditorGUI.EndChangeCheck())
        {
            publishSettings.version = playSettings.version;
            SaveSettings();
        }

        HotUpdateSettings runtimeSettings = Resources.Load<HotUpdateSettings>(
            HotUpdateBootstrap.SettingsResourcePath);
        string productionUrl = runtimeSettings == null
            ? "未找到 HotUpdateSettings"
            : string.IsNullOrWhiteSpace(runtimeSettings.productionBaseUrl)
                ? "尚未配置"
                : runtimeSettings.productionBaseUrl;
        EditorGUILayout.LabelField("正式 HTTPS 根地址", productionUrl);
        EditorGUILayout.HelpBox(
            "Unity 只上传 staging/<版本>。Release 迁移与 current 切换继续由服务器管理员手动执行。",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("用所选来源进入 Play", GUILayout.Height(28f)))
            {
                SaveSettings();
                RunAndLog(() => HotUpdatePlayModeLauncher.Play(playSettings));
            }

            if (GUILayout.Button("验证项目设置", GUILayout.Height(28f)))
                RunAndLog(HotUpdateVerification.Run);
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("操作日志", EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(130f));
        EditorGUILayout.SelectableLabel(
            string.IsNullOrEmpty(activityLog) ? "等待操作。" : activityLog,
            EditorStyles.textArea,
            GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private void Update()
    {
        bool changed = false;
        while (pendingMessages.TryDequeue(out string message))
        {
            AppendLog(message);
            changed = true;
        }

        if (publishTask != null && publishTask.IsCompleted)
        {
            try
            {
                HotUpdatePublishResult result = publishTask.GetAwaiter().GetResult();
                AppendLog(
                    $"完成：{result.RemotePath}，共发布 {result.FileCount} 个文件。\n"
                    + "releases 和 current 未修改。");
                Debug.Log("热更新 Staging 上传完成：" + result.RemotePath);
            }
            catch (Exception exception)
            {
                Exception error = exception.GetBaseException();
                AppendLog("上传失败：" + error.Message);
                Debug.LogException(error);
            }

            publishTask = null;
            changed = true;
        }

        if (changed)
            Repaint();
    }

    private void ValidateLocal(string bundleDirectory)
    {
        RunAndLog(
            () =>
            {
                int count = HotUpdatePublisher
                    .ValidateLocal(publishSettings, bundleDirectory)
                    .Count;
                AppendLog($"本地产物校验通过：{count} 个 Bundle。");
            });
    }

    private void BeginPublish(string bundleDirectory)
    {
        try
        {
            SaveSettings();
            AppendLog("开始上传版本 " + publishSettings.version + "……");
            publishTask = HotUpdatePublisher.PublishAsync(
                publishSettings,
                bundleDirectory,
                message => pendingMessages.Enqueue(message));
        }
        catch (Exception exception)
        {
            AppendLog("无法开始上传：" + exception.Message);
            Debug.LogException(exception);
        }
    }

    private void SaveSettings()
    {
        HotUpdateEditorSettings.Save(publishSettings);
        HotUpdateEditorPlaySettings.Save(playSettings);
    }

    private void RunAndLog(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            AppendLog(exception.Message);
            Debug.LogException(exception);
        }
    }

    private void AppendLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;
        activityLog += (string.IsNullOrEmpty(activityLog) ? string.Empty : "\n")
                       + "[" + DateTime.Now.ToString("HH:mm:ss") + "] "
                       + message.Trim();
        scroll.y = float.MaxValue;
    }
}

[InitializeOnLoad]
public static class HotUpdatePlayModeLauncher
{
    private const string ArmedKey = "HotUpdate.PlayModeStartScene.Armed";
    private const string PreviousSceneKey = "HotUpdate.PlayModeStartScene.Previous";

    static HotUpdatePlayModeLauncher()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        if (SessionState.GetBool(ArmedKey, false)
            && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += RestoreStartScene;
        }
    }

    public static void Play(HotUpdateEditorPlaySettingsData settings)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Unity 正在编译或切换 Play Mode，请稍后重试。");

        SceneAsset startup = AssetDatabase.LoadAssetAtPath<SceneAsset>(
            HotUpdateProjectSetup.StartupScenePath);
        if (startup == null)
            throw new FileNotFoundException("请先执行 Apply Project Setup 创建热更新启动场景。");

        HotUpdateEditorPlaySettings.Save(settings);
        string previous = EditorSceneManager.playModeStartScene == null
            ? string.Empty
            : AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene);
        SessionState.SetString(PreviousSceneKey, previous);
        SessionState.SetBool(ArmedKey, true);
        EditorSceneManager.playModeStartScene = startup;
        EditorApplication.isPlaying = true;
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
            RestoreStartScene();
    }

    private static void RestoreStartScene()
    {
        if (!SessionState.GetBool(ArmedKey, false))
            return;

        string previous = SessionState.GetString(PreviousSceneKey, string.Empty);
        EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previous)
            ? null
            : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
        SessionState.SetBool(ArmedKey, false);
        SessionState.EraseString(PreviousSceneKey);
    }
}
