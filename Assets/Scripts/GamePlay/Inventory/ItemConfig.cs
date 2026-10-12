using UnityEngine;

namespace Game.Inventory
{
    public enum ItemCategory { Material, Food, Equipment }      //物品类型
    public enum ItemRarity { Common, Rare, Epic, Legendary }    //物品品质
    public enum ItemUseKind //物品的用途
    {
        None, CharacterExperience, EquipmentExperience, CharacterAscension,
        EquipmentAscension, Revive, TechniquePoints, RestoreHealth, AttackBoost
    }
    public enum EquipmentKind 
    { 
        LightCone,      //光锥
        Instrument      //仪器
    }
    public enum InventorySort { Rarity, Name, Quantity }

    [CreateAssetMenu(fileName = "ItemConfig", menuName = "Inventory/Item")]
    public class ItemConfig : ScriptableObject
    {
        public string id;
        public string displayName;              //显示名
        public Sprite icon;                     //图标
        [TextArea] public string description;   //描述
        public ItemCategory category;           //分类
        public ItemRarity rarity;               //稀有度
        public ItemUseKind useKind;             //用途类型
        [Min(0)] public float effectValue;      //效果值
        [Min(0)] public float effectRatio;      //效果比例
        public bool requiresTarget;             //需要指定目标

        //是否是装备
        public bool IsEquipment => this is EquipmentConfig;
    }
}
