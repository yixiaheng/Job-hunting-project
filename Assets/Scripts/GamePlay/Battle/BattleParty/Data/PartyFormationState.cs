using System;
using Battle.SaveData;
using UnityEngine;

/// <summary>Confirmed formation for this application session; no scene references.</summary>
/// <summary>
/// 跨场景记住玩家已经确认的配队。
/// </summary>
public static class PartyFormationState
{
    private static BattleFormation saved;

    public static void Save(BattleFormation formation)
    {
        saved = Copy(formation ?? throw new ArgumentNullException(nameof(formation)));
    }

    public static bool TryRestore(out BattleFormation formation)
    {
        formation = saved != null ? Copy(saved) : null;
        return formation != null;
    }

    private static BattleFormation Copy(BattleFormation source)
    {
        var copy = new BattleFormation();
        foreach (FormationMember member in source.members)
        {
            if (member == null || !copy.TryAdd(member.unitId, member.position))
                throw new ArgumentException("Cannot save an invalid formation.", nameof(source));
        }

        return copy;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntime()
    {
        saved = null;
    }
}
