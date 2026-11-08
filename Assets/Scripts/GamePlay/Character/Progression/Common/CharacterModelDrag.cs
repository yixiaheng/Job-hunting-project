using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Characters
{
    public sealed class CharacterModelDrag : MonoBehaviour, IDragHandler
    {
        public CharacterSceneController Controller { get; set; }

        public void OnDrag(PointerEventData data) => Controller?.Rotate(data.delta.x);
    }
}
