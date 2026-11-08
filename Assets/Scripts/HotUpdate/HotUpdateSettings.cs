using UnityEngine;

[CreateAssetMenu(menuName = "Game/Hot Update Settings", fileName = "HotUpdateSettings")]
public sealed class HotUpdateSettings : ScriptableObject
{
    [Header("远程更新")]
    [Tooltip("关闭时完全跳过网络更新，直接使用本地首包资源。")]
    public bool tryRemoteUpdate = true;
    [Tooltip("留空时跳过网络更新并使用本地资源。")]
    public string serverRoot = "";
    public string manifestName = "ABCompareInfo.txt";
    [Min(1)] public int requestTimeoutSeconds = 30;

    [Header("本地资源")]
    public string bundleFolder = "AssetBundles";
    [Tooltip("从安装包 StreamingAssets/AssetBundles 首次复制资源到可写目录。")]
    public bool copyStreamingAssetsOnFirstLaunch = true;

    [Header("Lua")]
    [Tooltip("启动后执行的 Lua 模块名，留空则只创建 LuaEnv。")]
    public string luaEntry = "";
    [Tooltip("仅 Editor 生效；关闭后可在编辑器中验证从 AB 加载 Lua。")]
    public bool loadLuaFromAssetBundlesInEditor;
}
