using System.IO;
using UnityEngine;
using XLua;


public class LuaManager
{
    public LuaEnv luaEnv;
    public event System.Action Disposing;
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
    /// 加载 Lua 模块，并取得模块 return 的对象。
    /// require 自带缓存，同一个模块不会被重复执行。
    /// </summary>
    public T Require<T>(string moduleName) where T : class
    {
        if (string.IsNullOrWhiteSpace(moduleName))
            throw new System.ArgumentException(
                "Lua module name cannot be empty.",
                nameof(moduleName));

        //获取lua中的require函数
        LuaFunction require = luaEnv.Global.Get<LuaFunction>("require");
        if (require == null)
            throw new System.InvalidOperationException(
                "Lua global function 'require' was not found.");

        try
        {
            //在lua中调用require函数 传入的是moduleName
            object[] results = require.Call(moduleName);

            if (results == null || results.Length == 0)
            {
                throw new System.InvalidOperationException(
                    $"Lua module '{moduleName}' did not return a value.");
            }

            //如果result[0]是T类型，那么赋值给result
            //lua的require只能返回第一个值
            if (results[0] is T result)
                return result;

            //得到result返回值的真正类型是什么，用于异常抛出
            string actualType = results[0]?.GetType().Name ?? "null";

            throw new System.InvalidOperationException(
                $"Lua module '{moduleName}' returned {actualType}, " +
                $"but {typeof(T).Name} was expected.");
        }
        finally
        {
            require.Dispose();
        }
    }

    /// <summary>
    /// 创建一张独立的 Lua table。
    /// 后续用于把预制体中的组件引用传给 Lua。
    /// </summary>
    public LuaTable CreateTable()
    {
        if (luaEnv == null)
            throw new System.ObjectDisposedException(nameof(LuaManager));

        return luaEnv.NewTable();
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

        System.Delegate[] listeners = Disposing?.GetInvocationList();
        Disposing = null;
        if (listeners != null)
        {
            foreach (System.Action listener in listeners)
            {
                try
                {
                    listener();
                }
                catch (System.Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

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
