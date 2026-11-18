using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using XLua;

namespace Game.UI
{
    public enum LuaUIReferenceKind
    {
        Object,
        Button,
        TMPText,
        Image
    }

    /// <summary>
    /// 在 Inspector 中把一个名称映射到具体 Unity 组件或资源。
    /// Lua 通过 refs.xxx 获取这些对象。
    /// </summary>
    [Serializable]
    public sealed class LuaUIReference
    {
        [SerializeField] private string key;
        [SerializeField] private LuaUIReferenceKind kind;
        [SerializeField] private UnityEngine.Object target;

        public string Key => key;
        public LuaUIReferenceKind Kind => kind;

        public UnityEngine.Object Resolve(string panelName, string moduleName)
        {
            if (target == null)
                throw CreateError(panelName, moduleName, "does not have a target");

            UnityEngine.Object resolved = kind switch
            {
                LuaUIReferenceKind.Object => target,
                LuaUIReferenceKind.Button => ResolveComponent<Button>(),
                LuaUIReferenceKind.TMPText => ResolveComponent<TMP_Text>(),
                LuaUIReferenceKind.Image => ResolveComponent<Image>(),
                _ => null
            };

            if (resolved == null)
            {
                throw CreateError(
                    panelName,
                    moduleName,
                    $"cannot resolve a {kind} component from '{target.name}'");
            }

            return resolved;
        }

        private T ResolveComponent<T>() where T : Component
        {
            if (target is T component)
                return component;
            if (target is GameObject gameObject)
                return gameObject.GetComponent<T>();
            if (target is Component owner)
                return owner.GetComponent<T>();
            return null;
        }

        private InvalidOperationException CreateError(
            string panelName,
            string moduleName,
            string reason)
        {
            return new InvalidOperationException(
                $"LuaUIPanel '{panelName}' module '{moduleName}' reference " +
                $"'{key}' {reason}.");
        }
    }

    /// <summary>
    /// UIManager 与 Lua 面板逻辑之间的桥接组件。
    /// UIManager 仍负责面板实例、层级、输入和生命周期；
    /// Lua 负责面板内部的交互与显示逻辑。
    /// </summary>
    public sealed class LuaUIPanel : UIPanel
    {
        [Header("Lua")]
        [SerializeField] private string luaModule;

        [Header("References")]
        [SerializeField] private LuaUIReference[] references = 
                Array.Empty<LuaUIReference>();

        private LuaTable luaInstance;
        private LuaTable referenceTable;

        private LuaFunction initializeCallback;
        private LuaFunction openCallback;
        private LuaFunction openedCallback;
        private LuaFunction contextChangedCallback;
        private LuaFunction closingCallback;
        private LuaFunction closedCallback;
        private LuaFunction coveredChangedCallback;
        private LuaFunction destroyCallback;
        private bool subscribedToLuaDisposal;

        protected override void OnInitialize()
        {
            CreateLuaInstance();
            initializeCallback?.Call(luaInstance);
        }

        protected override void OnOpen(IUIContext context)
        {
            InvokeRuntimeCallback(openCallback, "OnOpen", context);
        }

        protected override void OnOpened()
        {
            InvokeRuntimeCallback(openedCallback, "OnOpened");
        }

        protected override void OnContextChanged(IUIContext context)
        {
            InvokeRuntimeCallback(contextChangedCallback, "OnContextChanged", context);
        }

        protected override void OnClosing()
        {
            InvokeRuntimeCallback(closingCallback, "OnClosing");
        }

        protected override void OnClosed()
        {
            InvokeRuntimeCallback(closedCallback, "OnClosed");
        }

        protected override void OnCoveredChanged(bool value)
        {
            InvokeRuntimeCallback(coveredChangedCallback, "OnCoveredChanged", value);
        }

        /// <summary>
        /// 提供给 Lua 的普通关闭入口。
        /// Lua 不直接操作 GameObject 的激活状态。
        /// </summary>
        public void CloseSelf()
        {
            if (Owner != null && Handle.IsValid)
                Owner.Close(Handle);
        }

        private void CreateLuaInstance()
        {
            if (luaInstance != null)
                return;

            if (!LuaManager.IsCreated)
            {
                throw new InvalidOperationException(
                    $"LuaUIPanel '{name}' cannot load module '{luaModule}': " +
                    "LuaManager has not been initialized.");
            }

            if (string.IsNullOrWhiteSpace(luaModule))
            {
                throw new InvalidOperationException(
                    $"LuaUIPanel '{name}' does not have a Lua module.");
            }

            //建立引用关系
            referenceTable = CreateReferenceTable();

            LuaTable module = null;
            LuaFunction createFunction = null;

            try
            {
                //require
                module = LuaManager.Instance.Require<LuaTable>(luaModule);
                //获取lua中的create函数
                createFunction = module.Get<LuaFunction>("Create");

                if (createFunction == null)
                {
                     throw new InvalidOperationException(
                        $"Lua module '{luaModule}' must provide " +
                        "a Create(panel, refs) function.");
                }

                //获取create函数的返回值，标准返回的是instance, 这个panel专用的
                object[] results = createFunction.Call(this, referenceTable);
                if (results == null ||
                    results.Length == 0 ||
                    results[0] is not LuaTable instance)
                {
                    throw new InvalidOperationException(
                        $"Lua module '{luaModule}'.Create must return a table.");
                }
                luaInstance = instance;
                // 获取 Lua 中用于接收 UIPanel 生命周期的函数。
                CacheCallbacks();
                LuaManager.Instance.Disposing += HandleLuaManagerDisposing;
                subscribedToLuaDisposal = true;
            }
            catch
            {
                DisposeLuaState();
                throw;
            }
            finally
            {
                createFunction?.Dispose();
                module?.Dispose();
            }
        }

        //建立引用关系
        private LuaTable CreateReferenceTable()
        {
            LuaTable table = LuaManager.Instance.CreateTable();
            var keys = new HashSet<string>();

            foreach (LuaUIReference reference in references)
            {
                if (reference == null ||
                    string.IsNullOrWhiteSpace(reference.Key))
                {
                    table.Dispose();

                    throw new InvalidOperationException(
                        $"LuaUIPanel '{name}' contains an empty reference key.");
                }

                if (!keys.Add(reference.Key))
                {
                    table.Dispose();

                    throw new InvalidOperationException(
                        $"LuaUIPanel '{name}' contains duplicate reference " +
                        $"key '{reference.Key}'.");
                }

                try
                {
                    table.Set(reference.Key, reference.Resolve(name, luaModule));
                }
                catch
                {
                    table.Dispose();
                    throw;
                }
            }

            return table;
        }

        private void CacheCallbacks()
        {
            initializeCallback =
                luaInstance.Get<LuaFunction>("OnInitialize");

            openCallback =
                luaInstance.Get<LuaFunction>("OnOpen");

            openedCallback =
                luaInstance.Get<LuaFunction>("OnOpened");

            contextChangedCallback =
                luaInstance.Get<LuaFunction>("OnContextChanged");

            closingCallback =
                luaInstance.Get<LuaFunction>("OnClosing");

            closedCallback =
                luaInstance.Get<LuaFunction>("OnClosed");

            coveredChangedCallback =
                luaInstance.Get<LuaFunction>("OnCoveredChanged");

            destroyCallback =
                luaInstance.Get<LuaFunction>("OnDestroy");
        }

        private void InvokeRuntimeCallback(
            LuaFunction callback,
            string callbackName,
            params object[] arguments)
        {
            if (callback == null || luaInstance == null)
                return;

            object[] callArguments = new object[arguments.Length + 1];
            callArguments[0] = luaInstance;
            Array.Copy(arguments, 0, callArguments, 1, arguments.Length);

            try
            {
                callback.Call(callArguments);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"LuaUIPanel '{name}' module '{luaModule}' callback " +
                    $"'{callbackName}' failed.\n{exception}",
                    this);
            }
        }

        private void OnDestroy()
        {
            ReleaseLuaState();
        }

        private void HandleLuaManagerDisposing()
        {
            ReleaseLuaState();
        }

        private void ReleaseLuaState()
        {
            if (subscribedToLuaDisposal && LuaManager.IsCreated)
                LuaManager.Instance.Disposing -= HandleLuaManagerDisposing;
            subscribedToLuaDisposal = false;

            if (luaInstance != null && LuaManager.IsCreated)
            {
                try
                {
                    destroyCallback?.Call(luaInstance);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }

            DisposeLuaState();
        }

        private void DisposeLuaState()
        {
            initializeCallback?.Dispose();
            openCallback?.Dispose();
            openedCallback?.Dispose();
            contextChangedCallback?.Dispose();
            closingCallback?.Dispose();
            closedCallback?.Dispose();
            coveredChangedCallback?.Dispose();
            destroyCallback?.Dispose();

            initializeCallback = null;
            openCallback = null;
            openedCallback = null;
            contextChangedCallback = null;
            closingCallback = null;
            closedCallback = null;
            coveredChangedCallback = null;
            destroyCallback = null;

            luaInstance?.Dispose();
            referenceTable?.Dispose();

            luaInstance = null;
            referenceTable = null;
        }
    }
}
