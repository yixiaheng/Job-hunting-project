using System.Collections.Generic;
using DialogueSystem;
using Game.Inventory;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class InventoryUIController : MonoBehaviour
{
    [SerializeField] private GameObject backPanel;
    [SerializeField] private InventoryManager inventory;
    [SerializeField] private Transform itemRoot;
    [SerializeField] private InventoryItemView itemPrefab;
    [SerializeField] private TMP_Dropdown categoryDropdown;
    [SerializeField] private TMP_Dropdown rarityDropdown;
    [SerializeField] private TMP_Dropdown sortDropdown;
    [SerializeField] private TMP_InputField searchInput;
    [SerializeField] private Image detailIcon;
    [SerializeField] private TMP_Text detailTitle;
    [SerializeField] private TMP_Text detailDescription;
    [SerializeField] private TMP_Text detailStats;
    [SerializeField] private TMP_Text countLabel;
    [SerializeField] private TMP_Text quantityLabel;
    [SerializeField] private TMP_Text statusLabel;
    [SerializeField] private Button useButton;
    [SerializeField] private Button lockButton;
    [SerializeField] private Button equipButton;
    [SerializeField] private Button progressionButton;
    [SerializeField] private Button decreaseButton;
    [SerializeField] private Button increaseButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button saveButton;

    private readonly List<InventoryItemView> views = new List<InventoryItemView>();
    private InventoryService service;
    private InventoryEntry selected;
    private int useQuantity = 1;
    private bool isOpen;
    private PlayerInputHandler playerInput;
    private CinemachineInputAxisController cameraInput;
    private bool previousMovement;
    private bool previousCameraInput;

    public InventoryEntry Selected => selected;
    public bool IsOpen => isOpen;

    private void Start()
    {
        if (inventory == null) 
            inventory = InventoryManager.Instance;
        service = inventory == null ? null : inventory.Service;

        //Change订阅
        if (service != null) service.Changed += Refresh;

        //给界面上的各种控件（下拉框、输入框、按钮）绑定事件监听，让它们在用户操作时触发对应的方法
        if (categoryDropdown != null) categoryDropdown.onValueChanged.AddListener(FilterChanged);
        if (rarityDropdown != null) rarityDropdown.onValueChanged.AddListener(FilterChanged);
        if (sortDropdown != null) sortDropdown.onValueChanged.AddListener(FilterChanged);
        if (searchInput != null) searchInput.onValueChanged.AddListener(SearchChanged);
        if (useButton != null) useButton.onClick.AddListener(UseSelected);
        if (lockButton != null) lockButton.onClick.AddListener(ToggleLock);
        if (equipButton != null) equipButton.onClick.AddListener(EquipSelected);
        if (progressionButton != null) progressionButton.onClick.AddListener(OpenProgression);
        if (closeButton != null) closeButton.onClick.AddListener(CloseInventory);
        if (saveButton != null) saveButton.onClick.AddListener(Save);
        if (decreaseButton != null) decreaseButton.onClick.AddListener(DecreaseQuantity);
        if (increaseButton != null) increaseButton.onClick.AddListener(IncreaseQuantity);

        //关闭背包
        CloseInventory();
    }

    private void Update()
    {
        //处理按键输入
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;
        //ESC处理退出背包
        if (isOpen && keyboard.escapeKey.wasPressedThisFrame)
        {
            CloseInventory();
            return;
        }
        if (searchInput != null && searchInput.isFocused) return;
        //B键处理背包交互
        if (keyboard.bKey.wasPressedThisFrame)
        {
            if (isOpen) CloseInventory();
            else OpenInventory();
        }
    }

    public void OpenInventory()
    {
        //下面情况返回： 已经打开/缺失Panel/正在对话中
        if (isOpen || backPanel == null) 
            return;
        if (DialogueRunner.Instance != null && DialogueRunner.Instance.isDialoguing) 
            return;
        
        if (InventoryManager.Instance != null) 
            inventory = InventoryManager.Instance;
        if (inventory != null && service != inventory.Service)
        {
            //重新注册Refresh到service.Changed
            if (service != null) 
                service.Changed -= Refresh;
            service = inventory.Service;
            if (service != null) 
                service.Changed += Refresh;
        }
        playerInput = FindFirstObjectByType<PlayerInputHandler>();
        cameraInput = FindFirstObjectByType<CinemachineInputAxisController>();
        if (playerInput != null)
        {
            //禁止移动
            //保存原有状态
            previousMovement = playerInput.CanMove;
            playerInput.SetMoveEnable(false);
        }
        if (cameraInput != null)
        {
            //禁用相机输入
            //保存原有状态
            previousCameraInput = cameraInput.enabled;
            cameraInput.enabled = false;
        }
        isOpen = true;
        backPanel.SetActive(true);
        SetStatus(service == null ? "背包尚未初始化，请检查物品目录。" : "选择物品查看详情");
        Refresh();
    }

    public void CloseInventory()
    {
        if (backPanel != null) 
            backPanel.SetActive(false);

        RestoreControls();

        //记得保存
        if (isOpen && inventory != null) 
            inventory.SaveInventory();
        isOpen = false;
    }

    //背包刷新
    public void Refresh()
    {
        //没有打开/背包服务不存在/item根和prefab不存在
        if (!isOpen || service == null || itemRoot == null || itemPrefab == null) 
            return;

        //分类下滑菜单
        int categoryIndex = categoryDropdown switch
        {
            null => 0 ,
            _ =>categoryDropdown.value
        };

        ItemCategory? category = categoryIndex switch
        {
            1 => ItemCategory.Material,
            2 => ItemCategory.Food,
            >=3 => ItemCategory.Equipment,
            _ => null
        };
        EquipmentKind? kind = categoryIndex switch
        {
            3 => EquipmentKind.LightCone,
            4 => EquipmentKind.Instrument,
            _ => null
        };

        //排序方式，默认按照品质排序
        InventorySort sort = sortDropdown switch
        {
            null => InventorySort.Rarity,
            //数字转Enum
            _ => (InventorySort)sortDropdown.value
        };

        //获取仓库数据
        //category:分类
        //kind:装备子类型
        //sort:排序方式
        List<InventoryEntry> entries = service.GetEntries(category, kind, sort);

        //搜索框的字符
        //Trim()去除空格
        string search = searchInput == null ? string.Empty : searchInput.text.Trim();
        //品质筛选
        int rarity = rarityDropdown == null ? 0 : rarityDropdown.value;

        //按照品质和名称进行过滤
        entries.RemoveAll(
                entry => rarity > 0 && (int)entry.Config.rarity != rarity - 1   //品质不合适
                || search.Length > 0 && entry.Config.displayName.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) < 0 //搜索不合适
        );
        //保存刷新之前的选中标识
        string selectedKey = selected == null ? null : selected.Key;
        //刷新之后原来选中的标识是否仍然存在
        selected = entries.Find(entry => entry.Key == selectedKey);
        //不存在就变成第一个
        if (selected == null && entries.Count > 0) 
            selected = entries[0];

        //创建和复用物品格
        for (int index = 0; index < entries.Count; index++)
        {
            //如果格子不够
            if (index >= views.Count) 
                views.Add(Instantiate(itemPrefab, itemRoot));

            views[index].gameObject.SetActive(true);
            //数据绑定
            //Select是回调方法
            views[index].Bind(entries[index], selected != null && entries[index].Key == selected.Key, Select);
        }
        //隐藏多余的
        for (int index = entries.Count; index < views.Count; index++) 
            views[index].gameObject.SetActive(false);

        countLabel.text = entries.Count == 0 ? "该分类暂无物品" : "共 " + entries.Count + " 个条目";
        //展示细节
        ShowDetails();
    }

    private void Select(InventoryEntry entry)
    {
        selected = entry;   //选择变成了entry
        useQuantity = 1;    //重置使用数目
        Refresh();          //刷新
    }


    //展示细节
    private void ShowDetails()
    {
        bool hasSelection = selected != null;
        //图标
        detailIcon.enabled = hasSelection && selected.Config.icon != null;
        detailIcon.sprite = hasSelection ? selected.Config.icon : null;
        //标题
        detailTitle.text = hasSelection ? selected.Config.displayName : "暂无物品";
        //描述
        detailDescription.text = hasSelection ? selected.Config.description : "获得的材料、食物和装备会显示在这里。";
        //状态
        detailStats.text = string.Empty;

        bool isEquipment = hasSelection && selected.Equipment != null;
        bool isFood = hasSelection && selected.Config.category == ItemCategory.Food;

        //通过isFood isEquipment来确认按钮的显示和隐藏
        useButton.gameObject.SetActive(isFood);
        lockButton.gameObject.SetActive(isEquipment);
        equipButton.gameObject.SetActive(isEquipment);
        progressionButton.gameObject.SetActive(hasSelection && !isFood);
        decreaseButton.gameObject.SetActive(isFood);
        increaseButton.gameObject.SetActive(isFood);
        quantityLabel.gameObject.SetActive(isFood);

        if (!hasSelection) return;

        //根据稀有度显示颜色
        detailTitle.color = InventoryItemView.GetRarityColor(selected.Config.rarity);
        //使用数量
        useQuantity = Mathf.Clamp(useQuantity, 1, selected.Quantity);
        quantityLabel.text = useQuantity.ToString();

        if (isEquipment)
        {
            //武器的其它显示
            var config = (EquipmentConfig)selected.Config;
            //根据等级计算属性值
            EquipmentStats stats = config.GetStats(selected.Equipment.level);
            //装备者
            string owner = service.EquipmentUsage == null ? null : service.EquipmentUsage.GetOwnerName(selected.Key);
            //属性文本
            detailStats.text = $"等级 {selected.Equipment.level}/{config.maxLevel}    突破 {selected.Equipment.ascension}\n生命 +{stats.health}    攻击 +{stats.attack}\n防御 +{stats.defence}    速度 +{stats.speed}\n增伤 +{stats.damageBonus:P0}\n"
                + (string.IsNullOrEmpty(owner) ? "未穿戴" : "已穿戴：" + owner);
            //锁定按钮文本
            lockButton.GetComponentInChildren<TMP_Text>().text = selected.Equipment.isLocked ? "解除锁定" : "锁定装备";
        }
        else
        {   
            //不是武器则显示出数量
            detailStats.text = "持有数量：" + selected.Quantity + "\n" + GetUseText(selected.Config.useKind);
        }
    }

    //使用选择的物品
    private void UseSelected()
    {
        if (selected == null) 
            return;
        string configId = selected.Config.id;

        //是否可以使用
        if (service.CanUse(configId, useQuantity, null, out _))
        {
            //使用，used代表是否使用成功，used失败则展示reason原因
            bool used = inventory.TryUse(configId, useQuantity, null, out string reason);
            SetStatus(used ? "物品已使用。" : reason);
        }
        else if(inventory.RequestUse(configId, useQuantity, out string reason))
        {
            SetStatus("请选择使用目标。");
        }
        else
        {
            SetStatus(reason);
        }
    }

    //装备锁定按钮
    private void ToggleLock()
    {
        if (selected == null || selected.Equipment == null) 
            return;
        bool locked = !selected.Equipment.isLocked;
        //尝试设置锁定
        bool changed = service.TrySetLocked(selected.Key, locked, out string reason);
        SetStatus(changed ? locked ? "装备已锁定。" : "装备已解锁。" : reason);
    }
    //装备选择
    private void EquipSelected()
    {
        if (selected != null) 
            SetStatus(inventory.RequestEquip(selected.Key) ? "请选择装备角色。" 
            : "角色装备界面尚未接入。");
    }
    //打开养成界面
    private void OpenProgression()
    {
        if (selected != null) 
            SetStatus(inventory.RequestProgression(selected.Key) ? "已请求打开养成界面。" 
            : "养成界面尚未接入，材料不会被扣除。");
    }
    //保存按钮
    private void Save() 
    { 
        if (inventory != null) 
        { 
            inventory.SaveInventory(); 
            SetStatus(inventory.LastMessage); 
        } 
    }
    //筛选条件已更改
    private void FilterChanged(int value) 
    { 
        useQuantity = 1; 
        Refresh(); 
    }
    //搜索条件改变
    private void SearchChanged(string value) { Refresh(); }
    //数量减少
    private void DecreaseQuantity() 
    { 
        useQuantity = Mathf.Max(1, useQuantity - 1); 
        ShowDetails(); 
    }
    //数量增加
    private void IncreaseQuantity() 
    { 
        if (selected != null) 
        { 
            useQuantity = Mathf.Min(selected.Quantity, useQuantity + 1); 
            ShowDetails(); 
        } 
    }
    //设置Status
    private void SetStatus(string message) { if (statusLabel != null) statusLabel.text = message; }

    //通过ItemUseKind获取文字说明
    private static string GetUseText(ItemUseKind kind)
    {
        switch (kind)
        {
            case ItemUseKind.CharacterExperience: return "在角色养成界面使用";
            case ItemUseKind.EquipmentExperience: return "在装备强化界面使用";
            case ItemUseKind.CharacterAscension: return "用于角色突破";
            case ItemUseKind.EquipmentAscension: return "用于装备突破";
            case ItemUseKind.Revive: return "选择倒下的角色使用";
            case ItemUseKind.TechniquePoints: return "恢复队伍共享秘技点";
            case ItemUseKind.RestoreHealth: return "选择受伤的角色使用";
            case ItemUseKind.AttackBoost: return "下一场战斗获得攻击加成";
            default: return string.Empty;
        }
    }
    //恢复打开背包前的操作设置
    private void RestoreControls()
    {
        if (!isOpen) 
            return;

        bool dialogueLocked = DialogueRunner.Instance != null && DialogueRunner.Instance.isDialoguing;

        if (playerInput != null) 
            playerInput.SetMoveEnable(previousMovement && !dialogueLocked);

        if (cameraInput != null) 
            cameraInput.enabled = previousCameraInput && !dialogueLocked;
    }

    private void OnDisable() 
    { 
        CloseInventory(); 
    }
    private void OnDestroy() 
    { 
        if (service != null)    
            service.Changed -= Refresh; 
    }
}
