#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;

public static class HotUpdateEditorPlaySettings
{
    public const string RelativePath = "UserSettings/HotUpdatePlaySettings.json";

    public static HotUpdateEditorPlaySettingsData Load()
    {
        string path = GetPath();
        if (!File.Exists(path))
            return new HotUpdateEditorPlaySettingsData();

        try
        {
            HotUpdateEditorPlaySettingsData settings =
                JsonUtility.FromJson<HotUpdateEditorPlaySettingsData>(File.ReadAllText(path));
            return settings ?? new HotUpdateEditorPlaySettingsData();
        }
        catch (Exception exception)
        {
            Debug.LogWarning("读取 Editor 热更新测试配置失败：" + exception.Message);
            return new HotUpdateEditorPlaySettingsData();
        }
    }

    public static void Save(HotUpdateEditorPlaySettingsData settings)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        string path = GetPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, JsonUtility.ToJson(settings, true));
    }

    public static string GetPath()
    {
        return Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", RelativePath));
    }
}
#endif
