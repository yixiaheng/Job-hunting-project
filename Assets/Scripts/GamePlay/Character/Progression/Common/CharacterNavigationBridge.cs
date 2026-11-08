using Game.Inventory;
using UnityEngine;
using System.Linq;

namespace Game.Characters
{
    /// <summary>背包只发意图，选择角色和实际培养仍统一走养成服务。</summary>
    public sealed class CharacterNavigationBridge : MonoBehaviour
    {
        private InventoryManager inventory;
        private void Start()
        {
            inventory = InventoryManager.Instance;
            if (inventory == null)
                return;
            inventory.EquipRequested += OpenEquipment;
            inventory.ProgressionRequested += OpenProgression;
        }

        private void OpenEquipment(string id) => OpenProgression(id);
        private void OpenProgression(string key)
        {
            var runtime = PlayerRuntime.Instance;
            if (runtime.Inventory.TryGetEquipment(key, out var instance))
            {
                runtime.Inventory.TryGetConfig(instance.configId, out var config);
                var equipment = config as EquipmentConfig;
                string id = runtime.Characters.EquipmentOwner(key) ?? runtime.Characters.OwnedCharacters.FirstOrDefault(unit => CharacterProgressionService.Matches(unit, equipment, equipment.equipmentKind == EquipmentKind.LightCone ? EquipmentSlot.LightCone : equipment.slot))?.id;
                CharacterSceneNavigator.TryEnter(id, equipment.equipmentKind == EquipmentKind.LightCone ? CharacterTab.LightCone : CharacterTab.Relics, key);
            }
            else
            {
                var shardOwner = runtime.Characters.OwnedCharacters.FirstOrDefault(unit => unit.growth.shardItem == key);
                bool skillMaterial = runtime.Characters.OwnedCharacters.Any(unit => unit.growth.skillMaterial == key);
                runtime.Inventory.TryGetConfig(key, out var material);
                CharacterSceneNavigator.TryEnter(shardOwner?.id, shardOwner != null ? CharacterTab.Rank : skillMaterial ? CharacterTab.Skills : material?.useKind == ItemUseKind.EquipmentExperience || material?.useKind == ItemUseKind.EquipmentAscension ? CharacterTab.LightCone : CharacterTab.Attributes);
            }
        }

        private void OnDestroy()
        {
            if (inventory == null)
                return;
            inventory.EquipRequested -= OpenEquipment;
            inventory.ProgressionRequested -= OpenProgression;
        }
    }
}
