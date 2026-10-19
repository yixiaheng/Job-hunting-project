using System.Collections.Generic;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// UI组件Config的注册，id与Config相绑定
    /// </summary>
    [CreateAssetMenu(menuName = "Game/UI/UI Registry", fileName = "UIRegistry")]
    public class UIRegistry : ScriptableObject
    {
        [SerializeField] private List<UIConfig> configs = new();

        private readonly Dictionary<UIId, UIConfig> lookup = new();

        private void OnEnable()
        {
            Rebuild();
        }

        public void Rebuild()
        {
            lookup.Clear();

            foreach (UIConfig config in configs)
            {
                if (config == null || config.id == UIId.None)
                    continue;

                if (lookup.ContainsKey(config.id))
                {
                    Debug.LogError($"UIRegistry 中存在重复 UIId：{config.id}", this);
                    continue;
                }

                lookup.Add(config.id, config);
            }
        }

        public bool TryGet(UIId id, out UIConfig config)
        {
            if (lookup.Count != configs.Count)
                Rebuild();

            return lookup.TryGetValue(id, out config);
        }
    }
}
