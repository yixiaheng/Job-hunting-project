using System;
using Game.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventoryItemView : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image icon;
    [SerializeField] private Image rarityBar;
    [SerializeField] private GameObject selection;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text amountLabel;
    [SerializeField] private TMP_Text lockLabel;

    private InventoryEntry entry;
    private Action<InventoryEntry> selected;

    private void Awake() { button.onClick.AddListener(Select); }

    public void Bind(InventoryEntry value, bool isSelected, Action<InventoryEntry> onSelected)
    {
        entry = value;
        selected = onSelected;
        icon.sprite = value.Config.icon;
        icon.enabled = icon.sprite != null;
        nameLabel.text = value.Config.displayName;
        amountLabel.text = value.Equipment == null ? value.Quantity.ToString() : "Lv." + value.Equipment.level;
        lockLabel.text = value.Equipment != null && value.Equipment.isLocked ? "已锁定" : string.Empty;
        rarityBar.color = GetRarityColor(value.Config.rarity);
        selection.SetActive(isSelected);
    }

    public static Color GetRarityColor(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Rare: return new Color(0.35f, 0.65f, 0.95f);
            case ItemRarity.Epic: return new Color(0.68f, 0.47f, 0.91f);
            case ItemRarity.Legendary: return new Color(0.96f, 0.75f, 0.34f);
            default: return new Color(0.68f, 0.72f, 0.76f);
        }
    }

    private void Select() { if (entry != null) selected?.Invoke(entry); }
}
