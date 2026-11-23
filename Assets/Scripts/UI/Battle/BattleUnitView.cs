using TMPro;
using UnityEngine;
using Battle.Presentation;

public class BattleUnitView : MonoBehaviour
{
    public TextMeshProUGUI nameHp;
    
    public BattleUnit unit;
    Vector3 restScale;
    bool scaleInitialized;

    void Awake()
    {
        InitializeScale();
    }

    void InitializeScale()
    {
        if (scaleInitialized)
            return;

        restScale = transform.localScale;
        scaleInitialized = true;
    }

    public void Bind(BattleUnit u)
    {
        InitializeScale();
        unit = u;
        SetActing(false);

        BattleUnitAllyUI battleUnitAllyUI = GetComponent<BattleUnitAllyUI>();
        if(battleUnitAllyUI != null)
            battleUnitAllyUI.Bind(u);
        Refresh();
    }

    public void Refresh()
    {
        if (unit == null)
        {
            if (nameHp != null)
                nameHp.text = string.Empty;
            gameObject.SetActive(false);
            return;
        }
        gameObject.SetActive(BattleDisplayState.Visible(unit));
        if (nameHp != null)
            nameHp.text = $"{unit.displayName}  {BattleDisplayState.Health(unit)}/{unit.maxHealth}";
    }

    public void SetActing(bool acting)
    {
        InitializeScale();
        transform.localScale = acting ? restScale * 1.2f : restScale;
    }
}
