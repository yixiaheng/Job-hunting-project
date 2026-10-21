using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// UI根节点HUD、弹窗、窗口等
    /// </summary>
    public class UIRoot : MonoBehaviour
    {
        [SerializeField] private Transform hudRoot;
        [SerializeField] private Transform windowRoot;
        [SerializeField] private Transform popupRoot;
        [SerializeField] private Transform toastRoot;
        [SerializeField] private Transform overlayRoot;

        public Transform GetLayerRoot(UILayer layer)
        {
            Transform result = layer switch
            {
                UILayer.HUD => hudRoot,
                UILayer.Window => windowRoot,
                UILayer.Popup => popupRoot,
                UILayer.Toast => toastRoot,
                UILayer.Overlay => overlayRoot,
                _ => null
            };

            if (result == null)
            {
                Debug.LogWarning($"UIRoot 没有配置 {layer} 层，暂时使用 UIRoot 自身作为挂载点", this);
                result = transform;
            }

            return result;
        }
    }
}
