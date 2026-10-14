namespace Game.Inventory
{


    /// <summary>
    /// 物品使用请求消息，作为信息传递
    /// </summary>
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

    /// <summary>
    /// 物品使用处理
    /// </summary>
    public interface IItemUseHandler
    {
        bool CanUse(ItemUseRequest request, out string reason);
        bool TryUse(ItemUseRequest request, out string reason);
    }


    /// <summary>
    /// 装备使用信息提供者
    /// </summary>
    public interface IEquipmentUsageProvider
    {
        bool IsEquipped(string instanceId);     //是否被装备
        string GetOwnerName(string instanceId); //获取装备者Name
    }
}
