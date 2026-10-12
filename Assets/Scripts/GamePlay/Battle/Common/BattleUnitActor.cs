using UnityEngine;

public class BattleUnitActor : MonoBehaviour
{
    public BattleUnit Unit { get; private set; }
    public void Bind(BattleUnit u) => Unit = u;
}
