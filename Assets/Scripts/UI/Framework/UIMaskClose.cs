using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.UI
{
    /// <summary>
    /// Attach to the panel's background graphic; 
    /// the UIConfig decides whether clicks close it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIMaskClose : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                GetComponentInParent<UIPanel>()?.HandleMaskClick();
        }
    }
}
