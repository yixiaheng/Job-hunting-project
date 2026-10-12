using System.Collections.Generic;
using UnityEngine;

public interface IActionDecider
{
    BattleAction Decide(BattleUnit caster, IReadOnlyList<BattleUnit> units);
}
