using System;
using System.Collections;
using System.IO;
using UnityEngine;

/// <summary>Creates and starts the persistent hot-update, AssetBundle and Lua runtime services.</summary>
public sealed class HotUpdateBootstrap : MonoBehaviour
{
    public const string SettingsResourcePath = "Bootstrap/HotUpdateSettings";
    private const string SeedBundleFolder = "AssetBundles";

    public static HotUpdateBootstrap Instance { get; private set; }
    public bool IsReady { get; private set; }
    public string StartupError { get; private set; }

    private HotUpdateSettings settings;
    private ABManager abManager;
    private HotUpdateManager hotUpdateManager;
    private bool started;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

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
            Debug.LogError("缺少 Resources/Bootstrap/HotUpdateSettings.asset，热更新启动已停止。", this);
            enabled = false;
            return;
        }

        abManager = gameObject.AddComponent<ABManager>();
        hotUpdateManager = gameObject.AddComponent<HotUpdateManager>();
        string bundleRoot = Path.Combine(Application.persistentDataPath, settings.bundleFolder);
        abManager.Configure(bundleRoot);
        hotUpdateManager.Configure(settings, bundleRoot);
        StartCoroutine(InitializeRuntime());
    }

    private IEnumerator InitializeRuntime()
    {
        if (!started)
        {
            started = true;
            string bundleRoot = Path.Combine(Application.persistentDataPath, settings.bundleFolder);
            if (!Directory.Exists(Path.Combine(bundleRoot, SeedBundleFolder)))
            {
                if (!settings.copyStreamingAssetsOnFirstLaunch)
                {
                    StopWithError("本地 AssetBundle 不存在，且配置禁止从 StreamingAssets 复制。");
                    yield break;
                }

                yield return CopyStreamingAssetsBundles(bundleRoot);
                if (!Directory.Exists(Path.Combine(bundleRoot, SeedBundleFolder)))
                {
                    StopWithError("本地首包 AssetBundle 不存在；请先构建 Windows AssetBundles。");
                    yield break;
                }
            }

            if (settings.tryRemoteUpdate && !string.IsNullOrWhiteSpace(settings.serverRoot))
            {
                yield return hotUpdateManager.UpdateCoroutine();
                if (!hotUpdateManager.LastUpdateSucceeded)
                    Debug.LogWarning("远程更新不可用，继续使用本地 AssetBundle：" + hotUpdateManager.LastError, this);
            }
            else
            {
                Debug.Log("远程更新未配置，使用本地 AssetBundle。", this);
            }
        }

        if (!abManager.Initialize())
        {
            StopWithError("AssetBundle 初始化失败，Lua 启动已停止。");
            yield break;
        }

        LuaManager.ConfigureEditorAssetBundleLoading(settings.loadLuaFromAssetBundlesInEditor);
        try
        {
            LuaManager runtime = LuaManager.Instance;
            if (!string.IsNullOrWhiteSpace(settings.luaEntry))
                runtime.DoLuaFile(settings.luaEntry);
            IsReady = true;
            Debug.Log("热更新运行环境初始化完成。", this);
        }
        catch (Exception exception)
        {
            StartupError = exception.Message;
            Debug.LogException(exception, this);
        }
    }

    private void StopWithError(string error)
    {
        StartupError = error;
        Debug.LogError(error, this);
    }

    private IEnumerator CopyStreamingAssetsBundles(string destinationRoot)
    {
        string sourceRoot = Path.Combine(Application.streamingAssetsPath, SeedBundleFolder);
        if (Directory.Exists(sourceRoot))
        {
            CopyDirectory(sourceRoot, destinationRoot);
        }
        else
        {
            Debug.LogError("找不到安装包首包目录：" + sourceRoot, this);
        }

        yield break;
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
        foreach (string directory in Directory.GetDirectories(source))
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
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

        if (LuaManager.IsCreated)
            LuaManager.Instance.Dispose();
        if (abManager != null)
            abManager.UnloadAll();
        Instance = null;
    }
}
