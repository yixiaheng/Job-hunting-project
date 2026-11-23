using System;
using System.Collections;
using System.IO;
using Game.UI;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Owns the startup update, AssetBundle activation and Lua runtime lifecycle.
/// </summary>
public sealed class HotUpdateBootstrap : MonoBehaviour
{
    public const string SettingsResourcePath = "Bootstrap/HotUpdateSettings";
    private const string MainBundleName = "AssetBundles";

    public static HotUpdateBootstrap Instance { get; private set; }

    public event Action<HotUpdateStatus> StatusChanged;
    public event Action Ready;

    public bool IsReady { get; private set; }
    public string StartupError { get; private set; }
    public HotUpdateStatus CurrentStatus { get; private set; }

    private HotUpdateSettings settings;
    private ABManager abManager;
    private HotUpdateManager hotUpdateManager;
    private Coroutine initialization;
    private UIHandle progressHandle;
    private HotUpdateSourceKind selectedSource;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureInstance()
    {
        if (FindFirstObjectByType<HotUpdateBootstrap>() != null)
            return;

        new GameObject("[HotUpdateRoot]").AddComponent<HotUpdateBootstrap>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        settings = Resources.Load<HotUpdateSettings>(SettingsResourcePath);
        if (settings == null)
        {
            enabled = false;
            StartupError =
                "缺少 Resources/Bootstrap/HotUpdateSettings.asset，热更新启动已停止。";
            Debug.LogError(StartupError, this);
            return;
        }

        abManager = gameObject.AddComponent<ABManager>();
        hotUpdateManager = gameObject.AddComponent<HotUpdateManager>();
        hotUpdateManager.StatusChanged += ForwardManagerStatus;
    }

    private void Start()
    {
        BeginInitialization();
    }

    public void Retry()
    {
        if (initialization != null || IsReady || settings == null)
            return;

        StartupError = null;
        BeginInitialization();
    }

    private void BeginInitialization()
    {
        selectedSource = ResolveSelectedSource();

#if UNITY_EDITOR
        if (selectedSource != HotUpdateSourceKind.LocalOnly
            && FindFirstObjectByType<HotUpdateStartupController>() == null)
        {
            Fail(
                "Editor 远程更新测试必须通过 Tools/Hot Update 窗口的 " +
                "Play With Selected Source 启动。");
            return;
        }
#endif

        bool startupScene =
            FindFirstObjectByType<HotUpdateStartupController>() != null;
        if (startupScene || selectedSource != HotUpdateSourceKind.LocalOnly)
            OpenProgressPanel();

        initialization = StartCoroutine(InitializeRuntime());
    }

    private IEnumerator InitializeRuntime()
    {
        IsReady = false;
        Publish(
            HotUpdateStatus.Create(
                HotUpdatePhase.Preparing,
                selectedSource,
                message: "正在准备热更新环境"));

#if UNITY_EDITOR
        if (selectedSource == HotUpdateSourceKind.LocalOnly
            && !settings.loadLuaFromAssetBundlesInEditor)
        {
            if (!TryInitializeLua(false, out string directLuaError))
            {
                Fail(directLuaError);
                yield break;
            }

            Complete("Editor 正在使用本地 Lua 源码");
            yield break;
        }
#endif

        HotUpdateEndpoint endpoint = default;
        string resolveError = null;
        yield return ResolveEndpoint(
            selectedSource,
            value => endpoint = value,
            error => resolveError = error);

        string bundleRoot;
        if (!string.IsNullOrEmpty(resolveError))
        {
            bundleRoot = FindFallbackBundleRoot(selectedSource);
            if (!HasMainBundle(bundleRoot))
            {
                Fail(resolveError);
                yield break;
            }

            ConfigureManagers(bundleRoot);
            PublishFallback(resolveError);
            yield return null;
            if (!TryActivateRuntime(true, out string fallbackActivationError))
            {
                Fail(fallbackActivationError);
                yield break;
            }

            Complete("远程版本不可用，已使用本地资源");
            yield break;
        }

        bundleRoot = GetBundleRoot(endpoint);
        ConfigureManagers(bundleRoot);
        TryPrepareSeedBundles(bundleRoot);

        if (endpoint.Remote)
        {
            yield return hotUpdateManager.UpdateCoroutine(endpoint);
            if (!hotUpdateManager.LastUpdateSucceeded)
            {
                if (!HasMainBundle(bundleRoot))
                {
                    Fail(hotUpdateManager.LastError);
                    yield break;
                }

                PublishFallback(hotUpdateManager.LastError);
                yield return null;
            }
        }
        else if (!HasMainBundle(bundleRoot))
        {
            Fail("本地 AssetBundle 不存在，请先执行 Tools/Hot Update/Build AssetBundles。");
            yield break;
        }

        if (!TryActivateRuntime(true, out string activationError))
        {
            if (hotUpdateManager.HasPendingActivation)
            {
                DisposeActiveRuntime();
                if (hotUpdateManager.RestorePreviousVersion(out string restoreError)
                    && HasMainBundle(bundleRoot))
                {
                    ConfigureManagers(bundleRoot);
                    PublishFallback(
                        "新资源激活失败，已恢复上一版本：" + activationError);
                    yield return null;
                    if (TryActivateRuntime(true, out string restoredActivationError))
                    {
                        Complete("已恢复并使用上一资源版本");
                        yield break;
                    }

                    activationError =
                        restoredActivationError + "；恢复信息：" + restoreError;
                }
                else if (!string.IsNullOrEmpty(restoreError))
                {
                    activationError += "；恢复旧版本失败：" + restoreError;
                }
            }

            Fail(activationError);
            yield break;
        }

        hotUpdateManager.ConfirmActiveVersion();
        Complete(
            endpoint.Remote
                ? $"资源版本 {endpoint.Version} 已就绪"
                : "本地资源已就绪");
    }

    private IEnumerator ResolveEndpoint(
        HotUpdateSourceKind source,
        Action<HotUpdateEndpoint> completed,
        Action<string> failed)
    {
        if (source == HotUpdateSourceKind.LocalOnly)
        {
            completed(new HotUpdateEndpoint(source, "local", string.Empty, false));
            yield break;
        }

        string version;
        string baseUrl;
#if UNITY_EDITOR
        HotUpdateEditorPlaySettingsData editorSettings =
            HotUpdateEditorPlaySettings.Load();
#endif

        switch (source)
        {
            case HotUpdateSourceKind.CurrentRelease:
                baseUrl = NormalizeUrl(settings.productionBaseUrl);
                if (string.IsNullOrEmpty(baseUrl))
                {
                    failed("正式 HTTPS 更新地址尚未配置。");
                    yield break;
                }

                Publish(
                    HotUpdateStatus.Create(
                        HotUpdatePhase.ResolvingVersion,
                        source,
                        message: "正在查询当前正式资源版本"));
                string versionUrl = CombinePathUrl(
                    baseUrl,
                    settings.currentVersionPath);
                using (UnityWebRequest request = UnityWebRequest.Get(versionUrl))
                {
                    request.timeout = Mathf.Max(1, settings.requestTimeoutSeconds);
                    yield return request.SendWebRequest();
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        failed(
                            $"查询当前正式版本失败：{request.error}\n{versionUrl}");
                        yield break;
                    }

                    version = request.downloadHandler.text.Trim();
                }

                if (!TryNormalizeVersion(version, out version))
                {
                    failed("服务器返回了无效的正式版本号。");
                    yield break;
                }

                completed(
                    new HotUpdateEndpoint(
                        source,
                        version,
                        CombinePathUrl(
                            baseUrl,
                            settings.releasesPath,
                            version),
                        true));
                yield break;

            case HotUpdateSourceKind.SpecificRelease:
#if UNITY_EDITOR
                version = editorSettings.version;
                baseUrl = NormalizeUrl(settings.productionBaseUrl);
                if (!TryNormalizeVersion(version, out version))
                {
                    failed("指定 Release 版本号必须是三段数字，例如 0.1.0。");
                    yield break;
                }

                if (string.IsNullOrEmpty(baseUrl))
                {
                    failed("正式 HTTPS 更新地址尚未配置。");
                    yield break;
                }

                completed(
                    new HotUpdateEndpoint(
                        source,
                        version,
                        CombinePathUrl(
                            baseUrl,
                            settings.releasesPath,
                            version),
                        true));
                yield break;
#else
                failed("Player 不支持指定 Release 版本。");
                yield break;
#endif

            case HotUpdateSourceKind.StagingVersion:
#if UNITY_EDITOR
                version = editorSettings.version;
                baseUrl = NormalizeUrl(editorSettings.stagingBaseUrl);
                if (!TryNormalizeVersion(version, out version))
                {
                    failed("Staging 版本号必须是三段数字，例如 0.1.0。");
                    yield break;
                }

                if (string.IsNullOrEmpty(baseUrl))
                {
                    failed("Staging 下载地址尚未配置。");
                    yield break;
                }

                completed(
                    new HotUpdateEndpoint(
                        source,
                        version,
                        CombinePathUrl(baseUrl, version),
                        true));
                yield break;
#else
                failed("Player 不支持 Staging 版本。");
                yield break;
#endif

            default:
                failed("不支持的热更新来源：" + source);
                yield break;
        }
    }

    private HotUpdateSourceKind ResolveSelectedSource()
    {
#if UNITY_EDITOR
        return HotUpdateEditorPlaySettings.Load().source;
#else
        return settings.tryRemoteUpdate
            ? HotUpdateSourceKind.CurrentRelease
            : HotUpdateSourceKind.LocalOnly;
#endif
    }

    private string GetBundleRoot(HotUpdateEndpoint endpoint)
    {
#if UNITY_EDITOR
        if (endpoint.Source != HotUpdateSourceKind.LocalOnly)
        {
            return Path.Combine(
                Application.persistentDataPath,
                "EditorHotUpdate",
                endpoint.Source.ToString(),
                endpoint.Version,
                settings.bundleFolder);
        }
#endif
        return Path.Combine(
            Application.persistentDataPath,
            settings.bundleFolder);
    }

    private string FindFallbackBundleRoot(HotUpdateSourceKind source)
    {
#if UNITY_EDITOR
        if (source == HotUpdateSourceKind.CurrentRelease)
        {
            string parent = Path.Combine(
                Application.persistentDataPath,
                "EditorHotUpdate",
                source.ToString());
            if (Directory.Exists(parent))
            {
                string best = null;
                DateTime bestTime = DateTime.MinValue;
                foreach (string versionDirectory in Directory.GetDirectories(parent))
                {
                    string candidate = Path.Combine(
                        versionDirectory,
                        settings.bundleFolder);
                    string mainBundle = Path.Combine(candidate, MainBundleName);
                    if (!File.Exists(mainBundle))
                        continue;

                    DateTime changed = File.GetLastWriteTimeUtc(mainBundle);
                    if (changed > bestTime)
                    {
                        best = candidate;
                        bestTime = changed;
                    }
                }

                if (!string.IsNullOrEmpty(best))
                    return best;
            }
        }
#endif
        return Path.Combine(
            Application.persistentDataPath,
            settings.bundleFolder);
    }

    private void ConfigureManagers(string bundleRoot)
    {
        abManager.UnloadAll();
        abManager.Configure(bundleRoot);
        hotUpdateManager.Configure(settings, bundleRoot);
    }

    private bool TryActivateRuntime(bool loadLuaFromBundles, out string error)
    {
        Publish(
            HotUpdateStatus.Create(
                HotUpdatePhase.LoadingAssetBundles,
                selectedSource,
                ReadLocalVersion(),
                "正在初始化 AssetBundle"));
        if (!abManager.Initialize())
        {
            error = "AssetBundle 初始化失败。";
            return false;
        }

        Publish(
            HotUpdateStatus.Create(
                HotUpdatePhase.StartingLua,
                selectedSource,
                ReadLocalVersion(),
                "正在启动 Lua"));
        return TryInitializeLua(loadLuaFromBundles, out error);
    }

    private bool TryInitializeLua(bool loadFromBundles, out string error)
    {
        LuaManager.ConfigureEditorAssetBundleLoading(loadFromBundles);
        try
        {
            LuaManager runtime = LuaManager.Instance;
            if (!string.IsNullOrWhiteSpace(settings.luaEntry))
                runtime.DoLuaFile(settings.luaEntry);
            error = null;
            return true;
        }
        catch (Exception exception)
        {
            error = "Lua 启动失败：" + exception.Message;
            Debug.LogException(exception, this);
            return false;
        }
    }

    private void TryPrepareSeedBundles(string destinationRoot)
    {
        if (HasMainBundle(destinationRoot)
            || !settings.copyStreamingAssetsOnFirstLaunch)
        {
            return;
        }

        string sourceRoot = Path.Combine(
            Application.streamingAssetsPath,
            settings.bundleFolder);
        if (!Directory.Exists(sourceRoot))
        {
            Debug.LogWarning("找不到安装包首包目录：" + sourceRoot, this);
            return;
        }

        CopyDirectory(sourceRoot, destinationRoot);
    }

    private static bool HasMainBundle(string root)
    {
        return !string.IsNullOrWhiteSpace(root)
               && File.Exists(Path.Combine(root, MainBundleName));
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
        foreach (string directory in Directory.GetDirectories(source))
        {
            CopyDirectory(
                directory,
                Path.Combine(destination, Path.GetFileName(directory)));
        }
    }

    private void ForwardManagerStatus(HotUpdateStatus status)
    {
        Publish(status);
    }

    private void PublishFallback(string reason)
    {
        Publish(
            HotUpdateStatus.Create(
                HotUpdatePhase.Fallback,
                selectedSource,
                ReadLocalVersion(),
                "远程更新不可用，正在使用本地资源",
                reason));
        Debug.LogWarning("远程更新不可用，继续使用本地资源：" + reason, this);
    }

    private void Complete(string message)
    {
        IsReady = true;
        StartupError = null;
        initialization = null;
        HotUpdateStatus completed = HotUpdateStatus.Create(
            HotUpdatePhase.Completed,
            selectedSource,
            ReadLocalVersion(),
            message);
        Publish(completed);
        CloseProgressPanel();
        Ready?.Invoke();
        Debug.Log("热更新运行环境初始化完成。", this);
    }

    private void Fail(string error)
    {
        DisposeActiveRuntime();
        StartupError = error;
        initialization = null;
        Publish(
            HotUpdateStatus.Create(
                HotUpdatePhase.Failed,
                selectedSource,
                ReadLocalVersion(),
                "热更新启动失败",
                error));
        Debug.LogError(error, this);
    }

    private void Publish(HotUpdateStatus status)
    {
        CurrentStatus = status;
        StatusChanged?.Invoke(status);
    }

    private string ReadLocalVersion()
    {
        string path = hotUpdateManager == null
            ? null
            : hotUpdateManager.GetLocalBundlePath("version.txt");
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return string.Empty;

        return File.ReadAllText(path).Trim();
    }

    private void OpenProgressPanel()
    {
        UIRuntimeBootstrap.EnsureInitialized();
        UIManager ui = UIManager.Instance;
        if (ui == null || progressHandle.IsValid)
            return;

        progressHandle = ui.Open(
            UIId.HotUpdateProgress,
            new HotUpdateProgressContext(this),
            ownerSceneHandle: ui.gameObject.scene.handle);
    }

    private void CloseProgressPanel()
    {
        if (!progressHandle.IsValid || UIManager.Instance == null)
            return;

        UIManager.Instance.Close(progressHandle);
        progressHandle = default;
    }

    private void DisposeActiveRuntime()
    {
        if (LuaManager.IsCreated)
            LuaManager.Instance.Dispose();
        abManager?.UnloadAll();
    }

    private static string NormalizeUrl(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().TrimEnd('/');
    }

    private static string CombinePathUrl(string root, params string[] parts)
    {
        string result = NormalizeUrl(root);
        foreach (string part in parts)
        {
            if (string.IsNullOrWhiteSpace(part))
                continue;
            result += "/" + part.Trim().Trim('/');
        }

        return result;
    }

    public static bool TryNormalizeVersion(string value, out string normalized)
    {
        normalized = value == null ? string.Empty : value.Trim();
        string[] parts = normalized.Split('.');
        if (parts.Length != 3)
            return false;

        for (int index = 0; index < parts.Length; index++)
        {
            if (!int.TryParse(parts[index], out int component) || component < 0)
                return false;
            parts[index] = component.ToString();
        }

        normalized = string.Join(".", parts);
        return true;
    }

    private void Update()
    {
        if (LuaManager.IsCreated)
            LuaManager.Instance.Tick();
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        if (hotUpdateManager != null)
            hotUpdateManager.StatusChanged -= ForwardManagerStatus;
        DisposeActiveRuntime();
        Instance = null;
    }
}
