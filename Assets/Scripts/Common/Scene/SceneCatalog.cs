using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>编辑器登记场景资源，运行时按 SceneId 取得已同步的完整路径。</summary>
[CreateAssetMenu(fileName = "SceneCatalog", menuName = "Game/Scenes/Scene Catalog")]
public sealed class SceneCatalog : ScriptableObject
{
    public const string ResourcePath = "Bootstrap/SceneCatalog";

    [SerializeField] private SceneEntry[] entries = Array.Empty<SceneEntry>();

    public IReadOnlyList<SceneEntry> Entries => entries;

    public bool TryGetPath(SceneId id, out string path)
    {
        path = null;
        if (id == SceneId.None || !Enum.IsDefined(typeof(SceneId), id) || entries == null)
            return false;

        foreach (SceneEntry entry in entries)
        {
            if (entry == null || entry.Id != id)
                continue;

            // 重复登记时拒绝加载，避免悄悄采用错误场景。
            if (path != null || string.IsNullOrWhiteSpace(entry.Path))
            {
                path = null;
                return false;
            }

            path = entry.Path;
        }

        return path != null;
    }

    public bool TryGetId(string path, out SceneId id)
    {
        id = SceneId.None;
        if (string.IsNullOrWhiteSpace(path) || entries == null)
            return false;

        foreach (SceneEntry entry in entries)
        {
            if (entry == null || !string.Equals(entry.Path, path, StringComparison.OrdinalIgnoreCase))
                continue;

            if (id != SceneId.None || !TryGetPath(entry.Id, out _))
            {
                id = SceneId.None;
                return false;
            }

            id = entry.Id;
        }

        return id != SceneId.None;
    }

    [Serializable]
    public sealed class SceneEntry
    {
        [SerializeField] private SceneId id = SceneId.None;
#if UNITY_EDITOR
        [SerializeField] private UnityEditor.SceneAsset scene;
#endif
        [SerializeField, HideInInspector] private string scenePath;

        public SceneId Id => id;
        public string Path => scenePath;
    }
}
