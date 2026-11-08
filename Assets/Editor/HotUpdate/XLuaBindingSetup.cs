using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Provides the normal XLua binding-generation menu and a one-shot import hook.</summary>
[InitializeOnLoad]
public static class XLuaBindingSetup
{
    private const string RequestPath = "Temp/XLuaGenerateBindings.request";

    static XLuaBindingSetup()
    {
        EditorApplication.delayCall += RunRequestedGeneration;
    }

    [MenuItem("Tools/HotUpdate/Generate XLua IL2CPP Bindings")]
    public static void GenerateBindings()
    {
        CSObjectWrapEditor.Generator.GenAll();
    }

    private static void RunRequestedGeneration()
    {
        if (!File.Exists(RequestPath))
            return;

        // Remove the request before generating: the generated files trigger another domain reload.
        File.Delete(RequestPath);
        try
        {
            GenerateBindings();
            Debug.Log("XLua project bindings generated for the current C# API set.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }
}
