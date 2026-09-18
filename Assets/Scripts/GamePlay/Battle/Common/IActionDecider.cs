using UnityEngine;

public interface IActionDecider
{
    BattleAction Decide(BattleUnit caster);
}
