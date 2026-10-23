using System.Collections;
using Battle.Events;
using Game;
using Game.Events;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(BattleUnitView))]
public class BattleUnitAllyUI : MonoBehaviour
{
    [SerializeField] private Image hp_bar;
    [SerializeField] private Image delay_bar;
    private BattleUnit Unit;
    private Coroutine delayCoroutine;
    private readonly SubscriptionBag subscriptions = new();

    void OnEnable()
    {
        subscriptions.Clear();
        subscriptions.Add(GameEvents.Battle.Subscribe<HealthChangedEvent>(OnHealthChanged));
        if (Unit != null)
            SnapLifeBars(GetHealthFill());
    }
    void OnDisable()
    {
        subscriptions.Clear();
        StopDelayCoroutine();
    }

    private void OnHealthChanged(HealthChangedEvent changedEvent)
    {
        if(Unit == null || Unit.BattleId != changedEvent.BattleId)
            return;
        if(Unit != changedEvent.Unit)
            return;
        if(changedEvent.MaxHealth <= 0)
            return;
        float targetFill = changedEvent.CurrentHealth / (float)changedEvent.MaxHealth;
        UpdateLifeBar(targetFill);
    }

    private void UpdateLifeBar(float targetFill)
    {
        targetFill = Mathf.Clamp01(targetFill);
        if (!isActiveAndEnabled)
        {
            SnapLifeBars(targetFill);
            return;
        }

        if (hp_bar != null) hp_bar.fillAmount = targetFill;
        StopDelayCoroutine();
        delayCoroutine = StartCoroutine(DelayBarLerp(targetFill));
    }

    private IEnumerator DelayBarLerp(float targetFill)
    {
        yield return new WaitForSeconds(0.2f);
        float startFill = delay_bar != null ? delay_bar.fillAmount : targetFill;
        const float duration = 0.25f;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            if (delay_bar != null)
                delay_bar.fillAmount = Mathf.Lerp(startFill, targetFill, elapsed / duration);
            yield return null;
        }
        if (delay_bar != null) delay_bar.fillAmount = targetFill;
        delayCoroutine = null;
    }
    private void StopDelayCoroutine()
    {
        if (delayCoroutine == null) return;
        StopCoroutine(delayCoroutine);
        delayCoroutine = null;
    }

    private void OnDestroy()
    {
        subscriptions.Clear();
        StopDelayCoroutine();
    }

    public void Bind(BattleUnit unit)
    {
        Unit = unit;
        SnapLifeBars(GetHealthFill());
    }

    private float GetHealthFill()
    {
        return Unit != null && Unit.maxHealth > 0
            ? Mathf.Clamp01((float)Unit.currentHealth / Unit.maxHealth)
            : 0f;
    }

    private void SnapLifeBars(float fill)
    {
        StopDelayCoroutine();
        if (hp_bar != null) hp_bar.fillAmount = fill;
        if (delay_bar != null) delay_bar.fillAmount = fill;
    }
}
