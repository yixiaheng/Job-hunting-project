using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(BattleUnitActor))]
public class BattleUnitAllyUI : MonoBehaviour
{
    private BattleUnitActor actor;
    [SerializeField] private Image hp_bar;
    [SerializeField] private Image delay_bar;
    private int maxHp;
    private int currentHp;
    private Coroutine delayCoroutine;




    private void Awake()
    {
        actor = GetComponent<BattleUnitActor>();
        if (maxHp <= 0)
        {
            Debug.LogError("maxHp 必须大于 0，已临时设置为 1");
            maxHp = 1;
        }

        SetBarsImmediately(1f);
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

    private void SetBarsImmediately(float fillAmount)
    {
        if (hp_bar != null) 
            hp_bar.fillAmount = fillAmount;
        if (delay_bar != null) 
            delay_bar.fillAmount = fillAmount;
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
}
