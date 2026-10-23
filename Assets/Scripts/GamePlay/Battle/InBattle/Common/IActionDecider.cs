using System.Collections.Generic;
using UnityEngine;

// 自动决策战斗行为
public interface IActionDecider
{
    BattleAction Decide(BattleUnit caster, IReadOnlyList<BattleUnit> units);
}
