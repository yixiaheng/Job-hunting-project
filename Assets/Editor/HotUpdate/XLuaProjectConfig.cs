using System;
using System.Collections.Generic;
using Game.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using XLua;

/// <summary>
/// Project-specific XLua AOT bindings for the existing UI framework.
/// Run XLua/Generate Code again whenever Lua-exposed C# APIs change.
/// </summary>
public static class XLuaProjectConfig
{
    [LuaCallCSharp]
    public static List<Type> LuaCallCSharp = new List<Type>
    {
        typeof(GameObject),
        typeof(Component),
        typeof(Transform),
        typeof(RectTransform),
        typeof(CanvasGroup),
        typeof(Vector2),
        typeof(Vector3),
        typeof(Quaternion),
        typeof(Mathf),
        typeof(Button),
        typeof(Selectable),
        typeof(Image),
        typeof(Graphic),
        typeof(TMP_Text),
        typeof(UnityEvent),
        typeof(UIManager),
        typeof(UIPanel),
        typeof(UIHandle),
        typeof(UIId),
        typeof(UILayer),
        typeof(UISpace),
        typeof(UIState),
        typeof(UIInstanceMode),
        typeof(UICacheMode),
        typeof(UILifetime),
        typeof(IUIContext)
    };

    [CSharpCallLua]
    public static List<Type> CSharpCallLua = new List<Type>
    {
        typeof(Action),
        typeof(Action<bool>),
        typeof(Action<int>),
        typeof(Action<string>),
        typeof(Func<bool>),
        typeof(UnityAction),
        typeof(UnityAction<bool>)
    };
}
