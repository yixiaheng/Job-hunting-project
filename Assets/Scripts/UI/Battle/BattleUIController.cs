using System;
using UnityEngine;
using Battle.Logic;
public class BattleUIController : MonoBehaviour
{
    public void OnStartBattle() => BattleManager.Instance.StartBattle();
    public void OnBasic() => BattleManager.Instance.ChooseBasic();
    public void OnSkill() => BattleManager.Instance.ChooseSkill();
    public void OnUlt() => BattleManager.Instance.ChooseUlt();


}
