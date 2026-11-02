using UnityEngine;

namespace Game.Characters
{
    public sealed class CharacterEntryButton : MonoBehaviour
    {
        public void Open() => CharacterSceneNavigator.TryEnter();
    }
}
