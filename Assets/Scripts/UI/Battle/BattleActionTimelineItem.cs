using System;
using System.Collections;
using Battle.Logic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
[RequireComponent(typeof(CanvasGroup))]
public sealed class BattleActionTimelineItem : MonoBehaviour
{
    [SerializeField] private Image portrait;
    [SerializeField] private Image factionFrame;
    [SerializeField] private Image currentHighlight;
    [SerializeField] private TMP_Text actionValueText;
    [SerializeField] private TMP_Text fallbackText;

    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private Coroutine transition;
    private int transitionVersion;

    public BattleUnit Unit { get; private set; }
    public RectTransform RectTransform => rectTransform != null
        ? rectTransform : rectTransform = (RectTransform)transform;
    public float Alpha => Group.alpha;

    private CanvasGroup Group => canvasGroup != null
        ? canvasGroup : canvasGroup = GetComponent<CanvasGroup>();

    public void Bind(BattleActionOrderEntry entry, Color allyColor,
        Color enemyColor, Color currentColor)
    {
        Unit = entry.Unit;
        bool hasPortrait = entry.Unit != null && entry.Unit.Icon != null;
        if (portrait != null)
        {
            portrait.sprite = hasPortrait ? entry.Unit.Icon : null;
            portrait.color = hasPortrait ? Color.white : new Color(0.08f, 0.11f, 0.16f, 0.96f);
            portrait.preserveAspect = true;
        }
        if (fallbackText != null)
        {
            fallbackText.gameObject.SetActive(!hasPortrait);
            fallbackText.text = Initial(entry.Unit?.displayName);
        }
        if (actionValueText != null)
            actionValueText.text = entry.IsQueuedAction
                ? $"{OriginLabel(entry.Origin)}\n0"
                : entry.RemainingActionValue.ToString();
        if (factionFrame != null)
            factionFrame.color = entry.Unit != null && entry.Unit.faction == Faction.OurSide
                ? allyColor : enemyColor;
        if (currentHighlight != null)
        {
            currentHighlight.gameObject.SetActive(entry.IsCurrent);
            currentHighlight.color = currentColor;
        }
        gameObject.name = entry.Unit == null
            ? "ActionOrderItem"
            : $"ActionOrder_{entry.Origin}_{entry.Unit.displayName}_{entry.EntryId}";
    }

    public void SetImmediate(Vector2 position, float alpha)
    {
        CancelTransition();
        RectTransform.anchoredPosition = position;
        Group.alpha = alpha;
        gameObject.SetActive(true);
    }

    public void AnimateTo(Vector2 position, float alpha, float duration, Action completed = null)
    {
        CancelTransition();
        gameObject.SetActive(true);
        int version = transitionVersion;
        if (!isActiveAndEnabled || duration <= 0f)
        {
            RectTransform.anchoredPosition = position;
            Group.alpha = alpha;
            completed?.Invoke();
            return;
        }
        transition = StartCoroutine(Transition(position, alpha, duration, version, completed));
    }

    public void Release()
    {
        CancelTransition();
        Unit = null;
        if (portrait != null) portrait.sprite = null;
        if (actionValueText != null) actionValueText.text = string.Empty;
        if (fallbackText != null) fallbackText.text = string.Empty;
        Group.alpha = 0f;
        gameObject.SetActive(false);
    }

    private IEnumerator Transition(Vector2 targetPosition, float targetAlpha,
        float duration, int version, Action completed)
    {
        Vector2 startPosition = RectTransform.anchoredPosition;
        float startAlpha = Group.alpha;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (version != transitionVersion)
                yield break;
            elapsed += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - normalized, 3f);
            RectTransform.anchoredPosition = Vector2.LerpUnclamped(startPosition, targetPosition, eased);
            Group.alpha = Mathf.LerpUnclamped(startAlpha, targetAlpha, eased);
            yield return null;
        }
        RectTransform.anchoredPosition = targetPosition;
        Group.alpha = targetAlpha;
        transition = null;
        completed?.Invoke();
    }

    private void CancelTransition()
    {
        transitionVersion++;
        if (transition == null)
            return;
        StopCoroutine(transition);
        transition = null;
    }

    private static string Initial(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return "?";
        string trimmed = displayName.Trim();
        return trimmed.Substring(0, 1);
    }

    private static string OriginLabel(BattleActionOrigin origin) => origin switch
    {
        BattleActionOrigin.Ultimate => "ULT",
        BattleActionOrigin.FollowUp => "FUP",
        BattleActionOrigin.Counter => "CTR",
        BattleActionOrigin.ExtraTurn => "EXT",
        BattleActionOrigin.Scripted => "SYS",
        _ => string.Empty
    };
}
}
