using System;
using System.Collections.Generic;
using Battle.SaveData;
using Game.Characters;
using Battle.Logic;

/// <summary>
/// A snapshot of the confirmed allies. Each index is the original formation slot.
/// </summary>
public sealed class BattleLaunchData
{
    private readonly UnitConfig[] allies;
    private readonly BattleCharacterSnapshot[] progression;

    public IReadOnlyList<UnitConfig> Allies { get; }

    private BattleLaunchData(UnitConfig[] allies)
    {
        this.allies = allies;
        Allies = Array.AsReadOnly(allies);
        progression = new BattleCharacterSnapshot[allies.Length];
        PlayerRuntime runtime = PlayerRuntime.Instance;
        if (runtime != null && runtime.IsReady)
        {
            for (int i = 0; i < allies.Length; i++)
                if (allies[i] != null)
                    progression[i] = new BattleCharacterSnapshot(allies[i], runtime.Characters.GetCharacter(allies[i].id), runtime.Inventory);
        }
    }

    public static bool TryCreate(IReadOnlyList<UnitConfig> slotUnits,
        out BattleLaunchData data, out string error)
    {
        data = null;
        error = null;
        if (PlayerRuntime.Instance != null && !PlayerRuntime.Instance.IsReady)
        { error = "玩家养成数据未成功读取，不能开始战斗。"; return false; }

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
            if (PlayerRuntime.Instance != null && !PlayerRuntime.Instance.Characters.Owns(config.id))
            { error = "尚未拥有角色：" + config.displayName; return false; }
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

    public BattleUnit CreateAlly(int position, int tieBreaker) => progression[position] != null
        ? progression[position].CreateUnit(position, tieBreaker)
        : BattleDataMaping.ToUnit(allies[position], Faction.OurSide, position, tieBreaker);
}
