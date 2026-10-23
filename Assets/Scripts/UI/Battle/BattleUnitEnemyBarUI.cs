using System.Collections;
using Battle.Events;
using Game;
using Game.Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 单个敌人的名字和血量显示。位置跟随和实例管理由外部负责。
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class BattleUnitEnemyBarUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameHp;
    [SerializeField] private Image hp_bar;
    [SerializeField] private Image delay_bar;

    public BattleUnit Unit { get; private set; }

    private readonly SubscriptionBag subscriptions = new();
    private CanvasGroup canvasGroup;
    private Coroutine delayCoroutine;
    private Vector3 restScale;
    private bool initialized;
    private bool positionVisible = true;

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        Subscribe();
        Refresh();
        SnapLifeBars(GetHealthFill());
    }

    private void OnDisable()
    {
        subscriptions.Clear();
        StopDelayCoroutine();
    }

    private void OnDestroy()
    {
        subscriptions.Clear();
        StopDelayCoroutine();
        Unit = null;
    }

    private void Initialize()
    {
        if (initialized)
            return;

        canvasGroup = GetComponent<CanvasGroup>();
        // 血条只负责显示，不拦截选择敌人模型的鼠标射线。
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        restScale = transform.localScale;
        initialized = true;
    }

    public void Bind(BattleUnit unit)
    {
        Initialize();
        subscriptions.Clear();
        StopDelayCoroutine();

        Unit = unit;
        SetActing(false);
        Refresh();
        // 首次绑定或切换敌人时，两条血条直接同步，避免从旧血量开始播放动画。
        SnapLifeBars(GetHealthFill());
        Subscribe();
    }

    public void Unbind()
    {
        Bind(null);
    }

    private void Subscribe()
    {
        subscriptions.Clear();
        if (Unit != null && isActiveAndEnabled)
            subscriptions.Add(GameEvents.Battle.Subscribe<HealthChangedEvent>(OnHealthChanged));
    }

    public void Refresh()
    {
        Initialize();
        if (nameHp != null)
            nameHp.text = Unit == null
                ? string.Empty
                : $"{Unit.displayName}  {Unit.currentHealth}/{Unit.maxHealth}";

        // 隐藏显示内容，保留根物体运行，供后续的位置跟随逻辑使用。
        UpdateVisibility();
    }

    public void SetVisible(bool visible)
    {
        Initialize();
        positionVisible = visible;
        UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        canvasGroup.alpha = positionVisible && Unit != null && Unit.IsActive ? 1f : 0f;
    }

    public void SetActing(bool acting)
    {
        Initialize();
        transform.localScale = acting && Unit != null && Unit.IsActive
            ? restScale * 1.2f
            : restScale;
    }

    private void OnHealthChanged(HealthChangedEvent changedEvent)
    {
        if (Unit == null || Unit != changedEvent.Unit || Unit.BattleId != changedEvent.BattleId)
            return;

        Refresh();
        float targetFill = GetHealthFill();
        if (!Unit.IsActive)
        {
            SetActing(false);
            SnapLifeBars(targetFill);
        }
        else if (changedEvent.CurrentHealth > changedEvent.PreviousHealth)
        {
            // 治疗时两条血条一起增加，不保留之前的扣血残影。
            SnapLifeBars(targetFill);
        }
        else
        {
            UpdateLifeBar(targetFill);
        }
    }

    private float GetHealthFill()
    {
        return Unit != null && Unit.maxHealth > 0
            ? Mathf.Clamp01((float)Unit.currentHealth / Unit.maxHealth)
            : 0f;
    }

    private void UpdateLifeBar(float targetFill)
    {
        StopDelayCoroutine();
        if (hp_bar != null)
            hp_bar.fillAmount = targetFill;

        if (delay_bar == null)
            return;

        if (!isActiveAndEnabled)
        {
            delay_bar.fillAmount = targetFill;
            return;
        }

        delayCoroutine = StartCoroutine(DelayBarLerp(targetFill));
    }

    private IEnumerator DelayBarLerp(float targetFill)
    {
        yield return new WaitForSeconds(0.2f);

        float startFill = delay_bar.fillAmount;
        const float duration = 0.25f;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            delay_bar.fillAmount = Mathf.Lerp(startFill, targetFill, elapsed / duration);
            yield return null;
        }

        delay_bar.fillAmount = targetFill;
        delayCoroutine = null;
    }

    private void SnapLifeBars(float fill)
    {
        StopDelayCoroutine();
        if (hp_bar != null) hp_bar.fillAmount = fill;
        if (delay_bar != null) delay_bar.fillAmount = fill;
    }

    private void StopDelayCoroutine()
    {
        if (delayCoroutine == null)
            return;

        StopCoroutine(delayCoroutine);
        delayCoroutine = null;
    }
}
