using System.Collections.Generic;
using Battle.Logic;
using UnityEngine;

namespace Game.UI
{
public sealed class BattleActionTimelinePanel : UIPanel
{
    [SerializeField] private RectTransform viewport;
    [SerializeField] private RectTransform itemsRoot;
    [SerializeField] private BattleActionTimelineItem itemPrefab;
    [SerializeField, Min(1f)] private float itemHeight = 148f;
    [SerializeField, Min(0f)] private float spacing = 12f;
    [SerializeField, Min(1f)] private float fallbackViewportHeight = 1420f;
    [SerializeField, Min(0f)] private float bottomFadeHeight = 240f;
    [SerializeField, Min(0f)] private float transitionDuration = 0.22f;
    [SerializeField] private Color allyColor = new(0.14f, 0.83f, 1f, 1f);
    [SerializeField] private Color enemyColor = new(1f, 0.29f, 0.2f, 1f);
    [SerializeField] private Color currentColor = new(1f, 0.91f, 0.48f, 1f);

    private readonly Dictionary<long, BattleActionTimelineItem> activeItems = new();
    private readonly Stack<BattleActionTimelineItem> pooledItems = new();
    private readonly List<long> removalBuffer = new();
    private IBattleActionOrderSource source;
    private bool holding;

    public int VisibleEntryCount => activeItems.Count;
    public bool IsHolding => holding;

    protected override void OnOpen(IUIContext context) => Bind(context);
    protected override void OnContextChanged(IUIContext context) => Bind(context);
    protected override void OnClosing() => Clear();
    protected override void OnClosed() => source = null;
    private void OnDisable() { if (!IsOpen) Clear(); }

    public void Refresh(BattleUnit displayedActor, bool animate = true)
    {
        if (holding || source == null)
            return;
        Apply(source.GetActionOrderSnapshot(displayedActor), animate);
    }

    public void Hold(BattleUnit displayedActor)
    {
        if (source == null)
            return;
        if (!holding)
            Apply(source.GetActionOrderSnapshot(displayedActor), false);
        holding = true;
    }

    public void AdvanceAndHold(BattleUnit displayedActor)
    {
        if (source == null)
            return;
        holding = false;
        Apply(source.GetActionOrderSnapshot(displayedActor), true);
        holding = true;
    }

    public void ReleaseAndRefresh(BattleUnit displayedActor, bool animate = true)
    {
        holding = false;
        Refresh(displayedActor, animate);
    }

    private void Bind(IUIContext context)
    {
        Clear();
        source = (context as BattleActionTimelineUIContext)?.Source;
        if (source == null)
        {
            Debug.LogError("Battle action timeline requires BattleActionTimelineUIContext.", this);
            return;
        }
        Apply(source.GetActionOrderSnapshot(), false);
    }

    private void Apply(IReadOnlyList<BattleActionOrderEntry> snapshot, bool animate)
    {
        if (snapshot == null || itemPrefab == null || itemsRoot == null)
            return;

        removalBuffer.Clear();
        foreach (var pair in activeItems)
        {
            bool retained = false;
            for (int index = 0; index < snapshot.Count; index++)
                if (snapshot[index].EntryId == pair.Key)
                { retained = true; break; }
            if (!retained)
                removalBuffer.Add(pair.Key);
        }

        foreach (long entryId in removalBuffer)
        {
            BattleActionTimelineItem item = activeItems[entryId];
            activeItems.Remove(entryId);
            if (animate)
            {
                Vector2 exit = item.RectTransform.anchoredPosition + new Vector2(0f, 28f);
                item.AnimateTo(exit, 0f, transitionDuration, () => Return(item));
            }
            else Return(item);
        }

        float step = itemHeight + spacing;
        for (int index = 0; index < snapshot.Count; index++)
        {
            BattleActionOrderEntry entry = snapshot[index];
            if (entry.Unit == null)
                continue;
            bool isNew = !activeItems.TryGetValue(entry.EntryId, out BattleActionTimelineItem item);
            if (isNew)
            {
                item = Rent();
                activeItems.Add(entry.EntryId, item);
            }

            item.Bind(entry, allyColor, enemyColor, currentColor);
            item.transform.SetAsLastSibling();
            Vector2 destination = new(0f, -index * step);
            float alpha = AlphaAt(index, step);
            if (!animate)
                item.SetImmediate(destination, alpha);
            else
            {
                if (isNew)
                    item.SetImmediate(destination + new Vector2(0f, -Mathf.Min(itemHeight, 48f)), 0f);
                item.AnimateTo(destination, alpha, transitionDuration);
            }
        }
    }

    private BattleActionTimelineItem Rent()
    {
        BattleActionTimelineItem item = pooledItems.Count > 0
            ? pooledItems.Pop() : Instantiate(itemPrefab, itemsRoot, false);
        item.gameObject.SetActive(true);
        return item;
    }

    private void Return(BattleActionTimelineItem item)
    {
        if (item == null)
            return;
        item.Release();
        item.transform.SetParent(itemsRoot, false);
        pooledItems.Push(item);
    }

    private float AlphaAt(int index, float step)
    {
        float viewportHeight = viewport != null && viewport.rect.height > 1f
            ? viewport.rect.height : fallbackViewportHeight;
        float center = index * step + itemHeight * 0.5f;
        float fadeStart = Mathf.Max(0f, viewportHeight - bottomFadeHeight);
        if (bottomFadeHeight <= 0f || center <= fadeStart)
            return center <= viewportHeight ? 1f : 0f;
        return 1f - Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(fadeStart, viewportHeight, center));
    }

    private void Clear()
    {
        holding = false;
        foreach (BattleActionTimelineItem item in activeItems.Values)
            if (item != null) item.Release();
        activeItems.Clear();
        if (itemsRoot != null)
            for (int index = itemsRoot.childCount - 1; index >= 0; index--)
            {
                GameObject child = itemsRoot.GetChild(index).gameObject;
                child.GetComponent<BattleActionTimelineItem>()?.Release();
                Destroy(child);
            }
        pooledItems.Clear();
    }
}
}
