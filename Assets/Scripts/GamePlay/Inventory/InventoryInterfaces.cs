namespace Game.Inventory
{
    public readonly struct ItemUseRequest
    {
        public ItemConfig Config { get; }
        public int Quantity { get; }
        public string TargetId { get; }

        public ItemUseRequest(ItemConfig config, int quantity, string targetId)
        {
            Config = config;
            Quantity = quantity;
            TargetId = targetId;
        }
    }

    public interface IItemUseHandler
    {
        bool CanUse(ItemUseRequest request, out string reason);
        bool TryUse(ItemUseRequest request, out string reason);
    }

    public interface IEquipmentUsageProvider
    {
        bool IsEquipped(string instanceId);
        string GetOwnerName(string instanceId);
    }
}
