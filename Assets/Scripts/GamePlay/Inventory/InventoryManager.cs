using System;
using System.Collections.Generic;
using TapTap21.SaveSystem.dyh;
using UnityEngine;

namespace Game.Inventory
{
    /// <summary>
    /// 背包统筹管理，负责初始化背包，保存背包，物品调用
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class InventoryManager : MonoBehaviour
    {
        public static InventoryManager Instance { get; private set; }
        //物品目录
        [SerializeField] private ItemCatalog catalog;
        //永久跨场景
        [SerializeField] private bool persistAcrossScenes = true;
        //存储存档，直接默认slot_0
        //SaveSystem中额外提供了不同存档说法，所以使用需要注意
        [SerializeField] private string saveSlot = "slot_0";
        //物品栏存储，每一个ItemAmount存储着物品id与数量
        [SerializeField] private List<ItemAmount> startingItems = new List<ItemAmount>();

        //dirty 脏，被修改过
        private bool dirty;
        private bool canSave;
        private float nextSaveTime;
        public InventoryService Service { get; private set; }
        public string LastMessage { get; private set; }
        public event Action<ItemUseRequest> UseRequested;
        public event Action<string> EquipRequested;
        public event Action<string> ProgressionRequested;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (persistAcrossScenes) DontDestroyOnLoad(gameObject);
            try
            {
                Service = new InventoryService(catalog);
                LoadInventory();
                Service.Changed += HandleChanged;
            }
            catch (Exception exception)
            {
                LastMessage = exception.Message;
                Debug.LogError("背包初始化失败：" + exception.Message, this);
            }
        }

        //加载背包资源
        public bool LoadInventory()
        {
            if (Service == null) return false;
            canSave = false;
            dirty = false;
            //存档系统检查
            if (!SaveSlotId.TryFrom(saveSlot, out var slot)) 
                return Fail("背包存档槽位无效。");

            var loaded = SaveSystem.Load(InventorySaveDefinitions.Inventory, slot);

            //存档读取成功
            if (loaded.Success)
            {
                //判断存档数据是否符合背包数据要求
                if (!Service.TryRestore(loaded.Data, out string reason)) 
                    return Fail(reason);
                //不存在修改，允许保存
                dirty = false;
                canSave = true;
                //判断loaded数据是使用的备份，还是真正读取成功了
                LastMessage = loaded.UsedBackup ? "背包已从备份恢复。" : "背包已读取。";
                //返回
                return true;
            }
            //如果是NotFound，则可能是没有创建背包
            //反之，则是背包读取失败
            if (loaded.Status != SaveStatus.NotFound) 
                return Fail("背包读取失败：" + loaded.Message);
            //新建InventoryData，尝试存储
            if (!Service.TryRestore(new InventoryData(), out string restoreError)) 
                return Fail(restoreError);
            
            //新创建的背包：发放初始物品
            if (startingItems != null && startingItems.Count > 0 
                && !Service.TryAddRewards(startingItems, out string error))
                return Fail(error);
            //允许保存，并标记为已经修改
            canSave = true;
            dirty = true;
            //保存一下
            return SaveInventory();
        }
        //保存背包资源
        public bool SaveInventory()
        {
            if (Service == null || !canSave) return Fail("背包未成功读取，暂不覆盖存档。");

            //数据保存到具体的存档中，传入的数据为背包数据的快照
            var saved = SaveSystem.Save(InventorySaveDefinitions.Inventory, saveSlot, Service.CreateSnapshot());

            if (!saved.Success)
            {
                //5s后再次尝试保存，配合LateUpdate使用
                nextSaveTime = Time.unscaledTime + 5f;
                return Fail("背包保存失败：" + saved.Message);
            }
            dirty = false;
            LastMessage = "背包已保存。";
            return true;
        }
        /// <summary>
        /// 尝试使用
        /// </summary>
        /// <param name="configId">使用的物品ID</param>
        /// <param name="quantity">使用物品的数量</param>
        /// <param name="targetId">物品作用对象ID</param>
        /// <param name="reason">返回的reason数据</param>
        /// <returns></returns>
        public bool TryUse(string configId, int quantity, string targetId, out string reason)
        {
            reason = "背包尚未初始化。";
            //检查InventorySerive是否为空
            if (Service == null) 
                return false;
            //调用Serive中的TryUse尝试使用物品
            bool success = Service.TryUse(configId, quantity, targetId, out reason);
            //使用成功就保存背包数据
            if (success) 
                SaveInventory();
            return success;
        }

        /// <summary>
        /// 请求使用物品，并把请求使用交给外部系统
        /// </summary>
        /// <param name="configId"></param>
        /// <param name="quantity"></param>
        /// <param name="reason"></param>
        /// <returns></returns>
        public bool RequestUse(string configId, int quantity, out string reason)
        {
            reason = "对应的使用界面尚未接入。";
            //如果Service服务不存在，或者无法成功获取使用的物品Config，返回false
            if (Service == null || !Service.TryGetConfig(configId, out var config)) 
                return false;
            //如果物品是武器或者没有明确的使用方法，返回false;
            if (config.IsEquipment || config.useKind == ItemUseKind.None)
            {
                reason = "该物品不能直接使用。";
                return false;
            }
            //使用数量不正常或者超过物品数量
            if (quantity <= 0 || quantity > Service.GetQuantity(configId))
            {
                reason = "使用数量无效。";
                return false;
            }
            //UseRequest是一个可以被订阅的事件，也就是将物品是实际使用和作用效果，交给外部有监听的部分
            if (UseRequested == null) 
                return false;

            
            UseRequested.Invoke(new ItemUseRequest(config, quantity, null));

            reason = null;
            return true;
        }
        /// <summary>
        /// 请求武器，并把请求使用交给外部系统
        /// </summary>
        /// <param name="instanceId"></param>
        /// <returns></returns>
        public bool RequestEquip(string instanceId)
        {
            //背包服务不存在/找不到这份装备(out _ 意为直接丢弃out结果)/装备事件订阅为空
            if (Service == null || !Service.TryGetEquipment(instanceId, out _) || EquipRequested == null) 
                return false;
            EquipRequested.Invoke(instanceId);
            return true;
        }
        /// <summary>
        /// 检查选中的条目是否可以发起养成请求，然后通知外部养成界面。
        /// </summary>
        /// <param name="itemKey"></param>
        /// <returns></returns>
        public bool RequestProgression(string itemKey)
        {
            //Todo 逻辑修改，待定

            //背包服务不存在/事件为空
            if (Service == null || ProgressionRequested == null) 
                return false;
            //itemKey对应的词条是装备
            bool isEquipment = Service.TryGetEquipment(itemKey, out _);
            //itemKey对应的词条是材料，并且数量不为0
            bool isMaterial = Service.TryGetConfig(itemKey, out var config)
                && config.category == ItemCategory.Material && Service.GetQuantity(itemKey) > 0;
            
            if (!isEquipment && !isMaterial) 
                return false;

            //调用事件
            ProgressionRequested.Invoke(itemKey);
            return true;
        }

        //当物品变动时调用，进行处理
        private void HandleChanged() 
        { 
            dirty = true; 
        }
        //LateUpdate
        private void LateUpdate() 
        { 
            //如果物品有变动_可保存_时间
            if (dirty && canSave 
                && Time.unscaledTime >= nextSaveTime) 
                SaveInventory(); 
        }
        //应用暂停时保存
        private void OnApplicationPause(bool paused) 
        { 
            if (paused && dirty && canSave) 
            SaveInventory(); 
        }
        //应用退出时保存·1
        private void OnApplicationQuit() 
        { 
            if (dirty && canSave) 
            SaveInventory(); 
        }
        //报错消息
        private bool Fail(string message)
        {
            LastMessage = message;
            Debug.LogWarning(message, this);
            return false;
        }
        //物品销毁时
        private void OnDestroy()
        {
            if (Service != null) Service.Changed -= HandleChanged;
            if (Instance == this) Instance = null;
        }
    }
}
