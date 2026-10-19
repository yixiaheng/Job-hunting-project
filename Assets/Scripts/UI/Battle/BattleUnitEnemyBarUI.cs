

using UnityEngine;

/// <summary>
/// 敌人的血条UI显示
/// </summary>
public class BattleUnitEnemyBarUI : MonoBehaviour
{
    
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
       

        BattleUnitAllyUI battleUnitAllyUI = GetComponent<BattleUnitAllyUI>();
        if(battleUnitAllyUI != null)
            battleUnitAllyUI.Bind(u);
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
    }

    public void SetActing(bool acting)
    {
        InitializeScale();
        transform.localScale = acting ? restScale * 1.2f : restScale;
    }
}