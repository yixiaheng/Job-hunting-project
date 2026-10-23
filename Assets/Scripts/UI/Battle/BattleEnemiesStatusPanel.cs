using System.Collections.Generic;
using Game.UI;
using UnityEngine;

/// <summary>管理本场战斗的敌方血条实例，将模型挂点投影到 HUD。</summary>
public class BattleEnemiesStatusPanel : UIPanel
{
    [SerializeField] private BattleUnitEnemyBarUI barPrefab;

    private sealed class Entry
    {
        public BattleUnitEnemyBarUI Bar;
        public RectTransform Rect;
        public BattleUnitActor Actor;
    }

    private readonly Dictionary<BattleUnit, Entry> entries = new();
    private RectTransform panelRect;
    private Canvas canvas;
    private Camera battleCamera;
    private BattleUnit actingUnit;

    public int BarCount => entries.Count;

    protected override void OnOpen(IUIContext context) => Bind(context);
    protected override void OnContextChanged(IUIContext context) => Bind(context);
    protected override void OnClosing() => Unbind();
    private void OnDisable() => Unbind();

    private void Bind(IUIContext context)
    {
        Unbind();
        if (context is not BattleEnemiesUIContext battleContext || battleContext.Units == null)
        {
            Debug.LogError("敌方血条需要 BattleEnemiesUIContext 和本场战斗的敌方单位。", this);
            return;
        }

        panelRect = (RectTransform)transform;
        canvas = GetComponentInParent<Canvas>()?.rootCanvas;
        battleCamera = battleContext.BattleCamera != null ? battleContext.BattleCamera : Camera.main;
        if (barPrefab == null || canvas == null)
        {
            Debug.LogError("敌方血条面板需要血条 prefab，并放在 Canvas 下。", this);
            return;
        }

        foreach (BattleUnit unit in battleContext.Units)
            AddUnit(unit);
        UpdatePositions();
    }

    // 增援/离场时由战斗系统调用，不根据 pos 挤入固定槽位，也不逐帧重建实例。
    public void AddUnit(BattleUnit unit)
    {
        if (unit == null || unit.faction != Faction.Enemy || entries.ContainsKey(unit)
            || barPrefab == null || panelRect == null || !IsOpen)
            return;

        BattleUnitEnemyBarUI bar = Instantiate(barPrefab, panelRect, false);
        bar.name = $"EnemyBar_{unit.displayName}_{unit.pos}";
        bar.SetVisible(false);
        bar.Bind(unit);
        bar.SetActing(unit == actingUnit);
        entries.Add(unit, new Entry { Bar = bar, Rect = (RectTransform)bar.transform });
    }

    public void RemoveUnit(BattleUnit unit)
    {
        if (unit == null || !entries.TryGetValue(unit, out Entry entry))
            return;

        Release(entry);
        entries.Remove(unit);
    }

    public void Refresh(BattleUnit currentUnit)
    {
        actingUnit = currentUnit;
        foreach (var pair in entries)
            pair.Value.Bar.SetActing(pair.Key == currentUnit);
    }

    private void LateUpdate() => UpdatePositions();

    private void UpdatePositions()
    {
        if (entries.Count == 0)
            return;
        if (battleCamera == null)
            battleCamera = Camera.main;

        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera : null;
        foreach (var pair in entries)
        {
            BattleUnit unit = pair.Key;
            Entry entry = pair.Value;
            bool visible = battleCamera != null && battleCamera.isActiveAndEnabled
                && canvas != null && unit.IsActive
                && unit.gameObject != null && unit.gameObject.activeInHierarchy;
            if (visible)
            {
                if (entry.Actor == null || entry.Actor.gameObject != unit.gameObject)
                    entry.Actor = unit.gameObject.GetComponent<BattleUnitActor>();

                visible = entry.Actor != null;
                if (visible)
                {
                    Vector3 worldPosition = entry.Actor.HealthBarPosition;
                    Vector3 viewport = battleCamera.WorldToViewportPoint(worldPosition);
                    visible = viewport.z >= battleCamera.nearClipPlane
                        && viewport.z <= battleCamera.farClipPlane
                        && viewport.x >= 0f && viewport.x <= 1f
                        && viewport.y >= 0f && viewport.y <= 1f;
                    if (visible)
                    {
                        Vector3 screen = battleCamera.WorldToScreenPoint(worldPosition);
                        visible = RectTransformUtility.ScreenPointToLocalPointInRectangle(
                            panelRect, screen, uiCamera, out Vector2 localPosition);
                        if (visible)
                            entry.Rect.anchoredPosition = localPosition;
                    }
                }
            }
            entry.Bar.SetVisible(visible);
        }
    }

    private void Unbind()
    {
        foreach (Entry entry in entries.Values)
            Release(entry);
        entries.Clear();
        actingUnit = null;
        battleCamera = null;
    }

    private void Release(Entry entry)
    {
        if (entry.Bar == null)
            return;
        entry.Bar.Unbind();
        // Destroy 在帧末执行，先禁用，防止切换战斗时旧血条多显示一帧。
        entry.Bar.gameObject.SetActive(false);
        Destroy(entry.Bar.gameObject);
    }
}
