using System;
using System.Collections.Generic;
using Battle.SaveData;

/// <summary>
/// A snapshot of the confirmed allies. Each index is the original formation slot.
/// </summary>
public sealed class BattleLaunchData
{
    private readonly UnitConfig[] allies;

    public IReadOnlyList<UnitConfig> Allies { get; }

    private BattleLaunchData(UnitConfig[] allies)
    {
        this.allies = allies;
        Allies = Array.AsReadOnly(allies);
    }

    public static bool TryCreate(IReadOnlyList<UnitConfig> slotUnits,
        out BattleLaunchData data, out string error)
    {
        data = null;
        error = null;

        if (slotUnits == null || slotUnits.Count > BattleFormation.MaxMembers)
        {
            error = "The ally formation must contain at most four slots.";
            return false;
        }

        var snapshot = new UnitConfig[BattleFormation.MaxMembers];
        var unitIds = new HashSet<string>(StringComparer.Ordinal);
        for (int position = 0; position < slotUnits.Count; position++)
        {
            UnitConfig config = slotUnits[position];
            if (config == null)
                continue;

            if (string.IsNullOrWhiteSpace(config.id) || !unitIds.Add(config.id))
            {
                error = $"Ally slot {position + 1} has an empty or duplicate unit ID.";
                return false;
            }

            if (config.prefab == null)
            {
                error = $"Ally slot {position + 1} ({config.id}) has no model prefab.";
                return false;
            }

            snapshot[position] = config;
        }

        if (unitIds.Count == 0)
        {
            error = "Select and confirm at least one ally before starting battle.";
            return false;
        }

        data = new BattleLaunchData(snapshot);
        return true;
    }

    public UnitConfig[] CopyAllies()
    {
        return (UnitConfig[])allies.Clone();
    }
}
