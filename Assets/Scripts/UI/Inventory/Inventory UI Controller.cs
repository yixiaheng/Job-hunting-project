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
        if (inventory == null) inventory = InventoryManager.Instance;
        service = inventory == null ? null : inventory.Service;
        if (service != null) service.Changed += Refresh;
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
        CloseInventory();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (isOpen && keyboard.escapeKey.wasPressedThisFrame)
        {
            CloseInventory();
            return;
        }
        if (searchInput != null && searchInput.isFocused) return;
        if (keyboard.bKey.wasPressedThisFrame)
        {
            if (isOpen) CloseInventory();
            else OpenInventory();
        }
    }

    public void OpenInventory()
    {
        if (isOpen || backPanel == null) return;
        if (DialogueRunner.Instance != null && DialogueRunner.Instance.isDialoguing) return;
        if (InventoryManager.Instance != null) inventory = InventoryManager.Instance;
        if (inventory != null && service != inventory.Service)
        {
            if (service != null) service.Changed -= Refresh;
            service = inventory.Service;
            if (service != null) service.Changed += Refresh;
        }
        playerInput = FindFirstObjectByType<PlayerInputHandler>();
        cameraInput = FindFirstObjectByType<CinemachineInputAxisController>();
        if (playerInput != null)
        {
            previousMovement = playerInput.CanMove;
            playerInput.SetMoveEnable(false);
        }
        if (cameraInput != null)
        {
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
        if (backPanel != null) backPanel.SetActive(false);
        RestoreControls();
        if (isOpen && inventory != null) inventory.SaveInventory();
        isOpen = false;
    }

    public void Refresh()
    {
        if (!isOpen || service == null || itemRoot == null || itemPrefab == null) return;
        int categoryIndex = categoryDropdown == null ? 0 : categoryDropdown.value;
        ItemCategory? category = categoryIndex == 1 ? ItemCategory.Material : categoryIndex == 2
            ? ItemCategory.Food : categoryIndex >= 3 ? ItemCategory.Equipment : (ItemCategory?)null;
        EquipmentKind? kind = categoryIndex == 3 ? EquipmentKind.LightCone
            : categoryIndex == 4 ? EquipmentKind.Instrument : (EquipmentKind?)null;
        InventorySort sort = sortDropdown == null ? InventorySort.Rarity : (InventorySort)sortDropdown.value;
        List<InventoryEntry> entries = service.GetEntries(category, kind, sort);
        string search = searchInput == null ? string.Empty : searchInput.text.Trim();
        int rarity = rarityDropdown == null ? 0 : rarityDropdown.value;
        entries.RemoveAll(entry => rarity > 0 && (int)entry.Config.rarity != rarity - 1
            || search.Length > 0 && entry.Config.displayName.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) < 0);
        string selectedKey = selected == null ? null : selected.Key;
        selected = entries.Find(entry => entry.Key == selectedKey);
        if (selected == null && entries.Count > 0) selected = entries[0];
        for (int index = 0; index < entries.Count; index++)
        {
            if (index >= views.Count) views.Add(Instantiate(itemPrefab, itemRoot));
            views[index].gameObject.SetActive(true);
            views[index].Bind(entries[index], selected != null && entries[index].Key == selected.Key, Select);
        }
        for (int index = entries.Count; index < views.Count; index++) views[index].gameObject.SetActive(false);
        countLabel.text = entries.Count == 0 ? "该分类暂无物品" : "共 " + entries.Count + " 个条目";
        ShowDetails();
    }

    private void Select(InventoryEntry entry)
    {
        selected = entry;
        useQuantity = 1;
        Refresh();
    }

    private void ShowDetails()
    {
        bool hasSelection = selected != null;
        detailIcon.enabled = hasSelection && selected.Config.icon != null;
        detailIcon.sprite = hasSelection ? selected.Config.icon : null;
        detailTitle.text = hasSelection ? selected.Config.displayName : "暂无物品";
        detailDescription.text = hasSelection ? selected.Config.description : "获得的材料、食物和装备会显示在这里。";
        detailStats.text = string.Empty;
        bool isEquipment = hasSelection && selected.Equipment != null;
        bool isFood = hasSelection && selected.Config.category == ItemCategory.Food;
        useButton.gameObject.SetActive(isFood);
        lockButton.gameObject.SetActive(isEquipment);
        equipButton.gameObject.SetActive(isEquipment);
        progressionButton.gameObject.SetActive(hasSelection && !isFood);
        decreaseButton.gameObject.SetActive(isFood);
        increaseButton.gameObject.SetActive(isFood);
        quantityLabel.gameObject.SetActive(isFood);
        if (!hasSelection) return;
        detailTitle.color = InventoryItemView.GetRarityColor(selected.Config.rarity);
        useQuantity = Mathf.Clamp(useQuantity, 1, selected.Quantity);
        quantityLabel.text = useQuantity.ToString();
        if (isEquipment)
        {
            var config = (EquipmentConfig)selected.Config;
            EquipmentStats stats = config.GetStats(selected.Equipment.level);
            string owner = service.EquipmentUsage == null ? null : service.EquipmentUsage.GetOwnerName(selected.Key);
            detailStats.text = $"等级 {selected.Equipment.level}/{config.maxLevel}    突破 {selected.Equipment.ascension}\n生命 +{stats.health}    攻击 +{stats.attack}\n防御 +{stats.defence}    速度 +{stats.speed}\n增伤 +{stats.damageBonus:P0}\n"
                + (string.IsNullOrEmpty(owner) ? "未穿戴" : "已穿戴：" + owner);
            lockButton.GetComponentInChildren<TMP_Text>().text = selected.Equipment.isLocked ? "解除锁定" : "锁定装备";
        }
        else detailStats.text = "持有数量：" + selected.Quantity + "\n" + GetUseText(selected.Config.useKind);
    }

    private void UseSelected()
    {
        if (selected == null) return;
        string configId = selected.Config.id;
        if (service.CanUse(configId, useQuantity, null, out _))
        {
            bool used = inventory.TryUse(configId, useQuantity, null, out string reason);
            SetStatus(used ? "物品已使用。" : reason);
        }
        else SetStatus(inventory.RequestUse(configId, useQuantity, out string reason) ? "请选择使用目标。" : reason);
    }

    private void ToggleLock()
    {
        if (selected == null || selected.Equipment == null) return;
        bool locked = !selected.Equipment.isLocked;
        bool changed = service.TrySetLocked(selected.Key, locked, out string reason);
        SetStatus(changed ? locked ? "装备已锁定。" : "装备已解锁。" : reason);
    }

    private void EquipSelected()
    {
        if (selected != null) SetStatus(inventory.RequestEquip(selected.Key) ? "请选择装备角色。" : "角色装备界面尚未接入。");
    }

    private void OpenProgression()
    {
        if (selected != null) SetStatus(inventory.RequestProgression(selected.Key) ? "已请求打开养成界面。" : "养成界面尚未接入，材料不会被扣除。");
    }

    private void Save() { if (inventory != null) { inventory.SaveInventory(); SetStatus(inventory.LastMessage); } }
    private void FilterChanged(int value) { useQuantity = 1; Refresh(); }
    private void SearchChanged(string value) { Refresh(); }
    private void DecreaseQuantity() { useQuantity = Mathf.Max(1, useQuantity - 1); ShowDetails(); }
    private void IncreaseQuantity() { if (selected != null) { useQuantity = Mathf.Min(selected.Quantity, useQuantity + 1); ShowDetails(); } }
    private void SetStatus(string message) { if (statusLabel != null) statusLabel.text = message; }

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

    private void RestoreControls()
    {
        if (!isOpen) return;
        bool dialogueLocked = DialogueRunner.Instance != null && DialogueRunner.Instance.isDialoguing;
        if (playerInput != null) playerInput.SetMoveEnable(previousMovement && !dialogueLocked);
        if (cameraInput != null) cameraInput.enabled = previousCameraInput && !dialogueLocked;
    }

    private void OnDisable() { CloseInventory(); }
    private void OnDestroy() { if (service != null) service.Changed -= Refresh; }
}
