using TMPro;
using UnityEngine;

public class BattleUnitView : MonoBehaviour
{
    public TextMeshProUGUI nameHp;
    BattleUnit unit;
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
        Refresh();
    }

    public void Refresh()
    {
        if (unit == null)
        {
            gameObject.SetActive(false);
            return;
        }
        gameObject.SetActive(unit.IsActive);
        if (nameHp != null)
            nameHp.text = $"{unit.displayName}  {unit.currentHealth}/{unit.maxHealth}";
    }

    public void SetActing(bool acting)
    {
        InitializeScale();
        transform.localScale = acting ? restScale * 1.2f : restScale;
    }
}
