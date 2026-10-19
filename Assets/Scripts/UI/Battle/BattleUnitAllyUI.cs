using System;
using System.Collections;
using System.ComponentModel;
using Battle.Events;
using Game;
using Game.Events;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(BattleUnitView))]
public class BattleUnitAllyUI : MonoBehaviour
{
    [SerializeField] private Image hp_bar;
    [SerializeField] private Image delay_bar;
    private BattleUnit Unit;
    private Coroutine delayCoroutine;
    private SubscriptionBag subscriptions = new ();

    void OnEnable()
    {
        subscriptions.Clear();
        subscriptions.Add(GameEvents.Battle.Subscribe<Battle.Events.HealthChangedEvent>(OnHealthChanged));
    }
    void OnDisable()
    {
        subscriptions.Clear();
    }


    private void Awake()
    {
        
    }

    private void OnHealthChanged(HealthChangedEvent changedEvent)
    {
        if(Unit.BattleId != changedEvent.BattleId)
            return;
        if(Unit != changedEvent.Unit)
            return;
        if(changedEvent.MaxHealth == 0)
            return;
        float targetFill = changedEvent.CurrentHealth / (float)changedEvent.MaxHealth;
        UpdateLifeBar(targetFill);
        Debug.Log("OnHealthChanged");
    }

    private void UpdateLifeBar(float targetFill)
    {
        targetFill = Mathf.Clamp01(targetFill);
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
        StopDelayCoroutine();
    }

    public void Bind(BattleUnit unit)
    {
        Unit = unit;
        UpdateLifeBar((float)Unit.currentHealth / Unit.maxHealth);
    }
}
