using Game.Inventory;
using UnityEngine;
using System.Linq;

namespace Game.Characters
{
    /// <summary>
    /// 背包只发意图，选择角色和实际培养仍统一走养成服务。
    /// </summary>
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
                if (!(config is EquipmentConfig equipment))
                    return;
                bool lightCone = equipment.equipmentKind == EquipmentKind.LightCone;
                EquipmentSlot slot = lightCone ? EquipmentSlot.LightCone : equipment.slot;
                string id = runtime.Characters.EquipmentOwner(key)
                    ?? runtime.Characters.OwnedCharacters.FirstOrDefault(unit => CharacterProgressionService.Matches(unit, equipment, slot))?.id;
                CharacterTab tab = lightCone ? CharacterTab.LightCone : CharacterTab.Relics;
                CharacterSceneNavigator.TryEnter(id, tab, key);
            }
            else
            {
                var shardOwner = runtime.Characters.OwnedCharacters.FirstOrDefault(unit => unit.growth.shardItem == key);
                bool skillMaterial = runtime.Characters.OwnedCharacters.Any(unit => unit.growth.skillMaterial == key);
                runtime.Inventory.TryGetConfig(key, out var material);
                CharacterTab tab = CharacterTab.Attributes;
                if (shardOwner != null)
                    tab = CharacterTab.Rank;
                else if (skillMaterial)
                    tab = CharacterTab.Skills;
                else if (material?.useKind == ItemUseKind.EquipmentExperience || material?.useKind == ItemUseKind.EquipmentAscension)
                    tab = CharacterTab.LightCone;
                CharacterSceneNavigator.TryEnter(shardOwner?.id, tab);
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
