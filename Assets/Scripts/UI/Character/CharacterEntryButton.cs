using Game.UI;
using UnityEngine;

namespace Game.Characters
{
    [RequireComponent(typeof(UIHUDEntryVisibility))]
    public sealed class CharacterEntryButton : MonoBehaviour
    {
        public void Open()
        {
            if (GetComponent<UIHUDEntryVisibility>().CanActivate)
                CharacterSceneNavigator.TryEnter();
        }
    }
}
