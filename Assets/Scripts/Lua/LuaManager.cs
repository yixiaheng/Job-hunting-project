using System.IO;
using UnityEngine;
using XLua;

public class LuaManager
{
    public LuaEnv luaEnv;
    private static LuaManager instance;
    private static bool loadEditorScriptsFromAssetBundles;
    public static bool IsCreated => instance != null && instance.luaEnv != null;

    public static void ConfigureEditorAssetBundleLoading(bool enabled)
    {
        if (IsCreated)
            throw new System.InvalidOperationException("Lua loader must be configured before LuaManager is created.");
        loadEditorScriptsFromAssetBundles = enabled;
    }
    public static LuaManager Instance
    {
        get
        {
            if(instance == null)
                instance = new LuaManager();
            return instance;
        }
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    private LuaManager()
    {
        luaEnv = new LuaEnv();
    #if UNITY_EDITOR
        if (!loadEditorScriptsFromAssetBundles)
            luaEnv.AddLoader(LoadByFile);
    #endif
        luaEnv.AddLoader(LoadByAB);
    }
    /// <summary>
    /// 得到Lua中的_G
    /// </summary>
    public LuaTable Global
    {
        get
        {
            return luaEnv.Global;
        }
    }

    /// <summary>
    /// 执行lua文件
    /// </summary>
    /// <param name="fileName">lua文件名</param>
    /// <param name="formWhere"></param>
    public void DoLuaFile(string fileName, string formWhere = null)
    {
        string str = string.Format("require('{0}')", fileName);
        luaEnv.DoString(str);
        //这里是在require("其它的Lua文件")
    }
    
    /// <summary>
    /// 运行字符串中的lua脚本
    /// </summary>
    /// <param name="luaScript">lua脚本内容</param>
    /// <param name="fromWhere"></param>
    public void DoString(string luaScript, string fromWhere = null)
    {
        luaEnv.DoString(luaScript, fromWhere);
    }

    public void Tick()
    {
        luaEnv?.Tick();
    }

    public void Dispose()
    {
        if (luaEnv == null)
            return;
        luaEnv.Tick();
        luaEnv.Dispose();
        luaEnv = null;
        if (instance == this)
            instance = null;
    }
    /// <summary>
    /// 在Lua文件夹下加载(仅限在Editor下使用)
    /// </summary>
    /// <param name="filepath"></param>
    /// <returns></returns>
    private byte[] LoadByFile(ref string filepath)// ref是按引用传递
    {
        Debug.Log("loadByFile:" + filepath);
        string path = Application.dataPath + "/Lua/" + filepath + ".lua.txt";
        if(File.Exists(path))
        {
            return File.ReadAllBytes(path);
        }
        else
        {
            Debug.Log("在Editor下Lua文件加载失败");
            return null;
        }
    }


    private byte[] LoadByAB(ref string filePath)
    {
        if (ABManager.Instance == null)
        {
            Debug.LogError("Lua 加载失败：ABManager 尚未初始化");
            return null;
        }

        var file = ABManager.Instance.LoadAsset<TextAsset>(
            "lua",
            "Assets/Lua/" + filePath + ".lua.txt"
        );
        if(file != null)
        {
            return file.bytes;
        }
        else
        {
        Debug.LogWarning("从AB包加载失败");
        return null;
        }
    }
}
