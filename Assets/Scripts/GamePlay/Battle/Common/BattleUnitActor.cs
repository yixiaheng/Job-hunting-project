using UnityEngine;

/// <summary>
/// 放在场景中的对象身上，用于对象标识，战斗时检测，然后获取BattleUnit
/// </summary>
public class BattleUnitActor : MonoBehaviour
{
    public BattleUnit Unit { get; private set; }
    public void Bind(BattleUnit u) => Unit = u;
}
