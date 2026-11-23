using System;
using System.IO;
using UnityEngine;

[Serializable]
public sealed class HotUpdatePublishSettingsData
{
    public string host = string.Empty;
    public int port = 22;
    public string user = string.Empty;
    public string remoteRoot = "/srv/game-server/update";
    public string privateKeyPath = string.Empty;
    public string version = "0.1.0";
}

public static class HotUpdateEditorSettings
{
    public const string RelativePath = "UserSettings/HotUpdatePublishSettings.json";

    public static HotUpdatePublishSettingsData Load()
    {
        string path = GetPath();
        if (!File.Exists(path))
            return new HotUpdatePublishSettingsData();

        try
        {
            HotUpdatePublishSettingsData data =
                JsonUtility.FromJson<HotUpdatePublishSettingsData>(File.ReadAllText(path));
            return data ?? new HotUpdatePublishSettingsData();
        }
        catch (Exception exception)
        {
            Debug.LogWarning("读取热更新上传设置失败：" + exception.Message);
            return new HotUpdatePublishSettingsData();
        }
    }

    public static void Save(HotUpdatePublishSettingsData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        string path = GetPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, JsonUtility.ToJson(data, true));
    }

    public static string GetPath()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", RelativePath));
    }
}
