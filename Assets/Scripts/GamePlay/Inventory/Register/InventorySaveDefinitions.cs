using TapTap21.SaveSystem.dyh;

namespace Game.Inventory
{
    public static class InventorySaveDefinitions
    {
        public static readonly SaveDefinition<InventoryData> Inventory =
            SaveSystem.Register<InventoryData>("player_inventory", "player_inventory.json", 1);
    }
}
