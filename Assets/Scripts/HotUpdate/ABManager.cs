using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// AssetBundle 加载、依赖处理和缓存管理器。
/// 使用前请先让 HotUpdateManager 完成更新。
/// </summary>
public class ABManager : MonoBehaviour
{
    public static ABManager Instance { get; private set; }

    [SerializeField] private string bundleFolder = "AssetBundles";
    private string configuredRoot;

    private readonly Dictionary<string, AssetBundle> loadedBundles =
        new Dictionary<string, AssetBundle>(StringComparer.OrdinalIgnoreCase);

    private AssetBundle mainBundle;
    private AssetBundleManifest manifest;
    public bool IsInitialized => manifest != null;

    private string RootPath => string.IsNullOrEmpty(configuredRoot)
        ? Path.Combine(Application.persistentDataPath, bundleFolder)
        : configuredRoot;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>Sets the writable bundle root before Initialize is called.</summary>
    public void Configure(string rootPath)
    {
        if (manifest != null)
            throw new InvalidOperationException("ABManager must be configured before initialization.");
        configuredRoot = rootPath;
    }

    /// <summary>加载主包并读取依赖清单。</summary>
    public bool Initialize()
    {
        if (manifest != null)
            return true;

        string path = GetBundlePath("AssetBundles");
        mainBundle = AssetBundle.LoadFromFile(path);
        if (mainBundle == null)
        {
            Debug.LogError("主包加载失败: " + path);
            return false;
        }

        manifest = mainBundle.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
        if (manifest == null)
            Debug.LogError("主包中找不到 AssetBundleManifest。");

        return manifest != null;
    }

    /// <summary>同步加载资源，管理器会自动加载全部依赖。</summary>
    public T LoadAsset<T>(string bundleName, string assetName) where T : UnityEngine.Object
    {
        AssetBundle bundle = LoadBundleWithDependencies(bundleName);
        if (bundle == null)
            return null;

        T asset = bundle.LoadAsset<T>(assetName);
        if (asset == null)
            Debug.LogError($"资源不存在: {bundleName}/{assetName}");
        return asset;
    }

    /// <summary>异步加载资源，避免大资源加载时阻塞主线程。</summary>
    public void LoadAssetAsync<T>(string bundleName, string assetName, Action<T> onComplete)
        where T : UnityEngine.Object
    {
        StartCoroutine(LoadAssetAsyncCoroutine(bundleName, assetName, onComplete));
    }

    private IEnumerator LoadAssetAsyncCoroutine<T>(string bundleName, string assetName, Action<T> onComplete)
        where T : UnityEngine.Object
    {
        yield return LoadBundleWithDependenciesAsync(bundleName, bundle =>
        {
            if (bundle == null)
            {
                onComplete?.Invoke(null);
                return;
            }

            StartCoroutine(LoadAssetFromBundleAsync(bundle, assetName, onComplete));
        });
    }

    private IEnumerator LoadAssetFromBundleAsync<T>(AssetBundle bundle, string assetName, Action<T> onComplete)
        where T : UnityEngine.Object
    {
        AssetBundleRequest request = bundle.LoadAssetAsync<T>(assetName);
        yield return request;
        T asset = request.asset as T;
        if (asset == null)
            Debug.LogError($"异步资源不存在: {assetName}");
        onComplete?.Invoke(asset);
    }

    private AssetBundle LoadBundleWithDependencies(string bundleName)
    {
        if (!Initialize())
            return null;

        string[] dependencies = manifest.GetAllDependencies(bundleName);
        foreach (string dependency in dependencies)
        {
            if (LoadBundle(dependency) == null)
                return null;
        }

        return LoadBundle(bundleName);
    }

    private IEnumerator LoadBundleWithDependenciesAsync(string bundleName, Action<AssetBundle> onComplete)
    {
        if (!Initialize())
        {
            onComplete?.Invoke(null);
            yield break;
        }

        string[] dependencies = manifest.GetAllDependencies(bundleName);
        foreach (string dependency in dependencies)
        {
            yield return LoadBundleAsync(dependency);
            if (!loadedBundles.ContainsKey(dependency))
            {
                onComplete?.Invoke(null);
                yield break;
            }
        }

        yield return LoadBundleAsync(bundleName);
        loadedBundles.TryGetValue(bundleName, out AssetBundle bundle);
        onComplete?.Invoke(bundle);
    }

    private AssetBundle LoadBundle(string bundleName)
    {
        if (loadedBundles.TryGetValue(bundleName, out AssetBundle cached))
            return cached;

        string path = GetBundlePath(bundleName);
        AssetBundle bundle = AssetBundle.LoadFromFile(path);
        if (bundle == null)
        {
            Debug.LogError("Bundle 加载失败: " + path);
            return null;
        }

        loadedBundles.Add(bundleName, bundle);
        return bundle;
    }

    private IEnumerator LoadBundleAsync(string bundleName)
    {
        if (loadedBundles.ContainsKey(bundleName))
            yield break;

        string path = GetBundlePath(bundleName);
        AssetBundleCreateRequest request = AssetBundle.LoadFromFileAsync(path);
        yield return request;

        if (request.assetBundle == null)
        {
            Debug.LogError("异步 Bundle 加载失败: " + path);
            yield break;
        }

        loadedBundles[bundleName] = request.assetBundle;
    }

    public string GetBundlePath(string bundleName) => Path.Combine(RootPath, bundleName);

    public void UnloadBundle(string bundleName, bool unloadAllLoadedObjects = false)
    {
        if (!loadedBundles.TryGetValue(bundleName, out AssetBundle bundle))
            return;

        bundle.Unload(unloadAllLoadedObjects);
        loadedBundles.Remove(bundleName);
    }

    public void UnloadAll(bool unloadAllLoadedObjects = false)
    {
        foreach (AssetBundle bundle in loadedBundles.Values)
            bundle.Unload(unloadAllLoadedObjects);

        loadedBundles.Clear();
        if (mainBundle != null)
            mainBundle.Unload(unloadAllLoadedObjects);

        mainBundle = null;
        manifest = null;
    }
}
