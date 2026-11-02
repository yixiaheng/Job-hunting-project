using System.Collections.Generic;
using Game.Inventory;
using UnityEngine;

namespace Game.Characters
{
    [CreateAssetMenu(menuName = "Characters/Player Runtime", fileName = "PlayerRuntimeConfig")]
    public sealed class PlayerRuntimeConfig : ScriptableObject
    {
        public UnitCatalog units;
        public ItemCatalog items;
        public TMPro.TMP_FontAsset uiFont;
        public string saveSlot = "slot_0";
        public List<string> startingCharacters = new List<string>();
        public List<ItemAmount> startingItems = new List<ItemAmount>();
    }
}
