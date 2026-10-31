using UnityEngine;

namespace Game.UI
{
    [CreateAssetMenu(menuName = "Game/UI/UI Config", fileName = "UIConfig")]
    public class UIConfig : ScriptableObject
    {
        [Header("Identity")]
        public UIId id;
        public UIPanel prefab;

        [Header("Placement")]
        public UILayer layer = UILayer.Window;
        [Tooltip("Screen 挂到屏幕 UI 层；World 保留预制体的世界空间 Canvas 和坐标。")]
        public UISpace space = UISpace.Screen;

        [Header("Instance")]
        public UIInstanceMode instanceMode = UIInstanceMode.Single;
        public UICacheMode cacheMode = UICacheMode.Keep;
        public UILifetime lifetime = UILifetime.Scene;

        [Header("Navigation")]
        public bool handlesBack = true;
        public bool hideLowerWindows;

        [Header("Input")]
        public bool showCursor;
        public bool blockMovement;
        public bool blockCamera;

        [Header("Background")]
        public bool modal;
        public bool closeWhenClickMask;
        public bool blurBackground;

        private void OnValidate()
        {
            if (id == UIId.None)
                Debug.LogWarning($"UIConfig {name} 没有设置有效的 UIId", this);

            if (prefab == null)
                Debug.LogWarning($"UIConfig {name} 没有设置 UIPanel Prefab", this);
        }
    }
}
