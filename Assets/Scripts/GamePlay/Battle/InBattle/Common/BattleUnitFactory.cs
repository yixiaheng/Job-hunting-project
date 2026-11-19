using System.Collections.Generic;
using Battle.Logic;
using UnityEngine;

/// <summary>创建本场逻辑单位，并集中释放只属于本场的技能和 Buff 配置。</summary>
public sealed class BattleUnitFactory
{
    private readonly BattleLaunchData launchData;
    private readonly List<BattleUnit> units = new();

    public IReadOnlyList<BattleUnit> Units => units;

    public BattleUnitFactory(BattleLaunchData launchData = null)
    {
        this.launchData = launchData;
    }

    public BattleUnit Create(UnitConfig config, Faction faction, int position, int tieBreaker)
    {
        if (config == null)
            return null;

        BattleUnit unit = faction == Faction.OurSide && launchData != null
            ? launchData.CreateAlly(position, tieBreaker)
            : BattleDataMaping.ToUnit(config, faction, position, tieBreaker);
        if (unit != null)
            units.Add(unit);
        return unit;
    }

    public void Release(BattleUnit unit)
    {
        if (unit == null || !units.Remove(unit))
            return;

        if (unit.ownsRuntimeSkills && unit.skills != null)
        {
            var released = new HashSet<SkillConfig>();
            foreach (SkillConfig skill in unit.skills)
                if (skill != null && released.Add(skill))
                    DestroyOwned(skill);
        }

        if (unit.buffs != null)
        {
            var released = new HashSet<BuffConfig>();
            foreach (BuffInstance buff in unit.buffs)
                if (buff != null && buff.ownsConfig && buff.config != null && released.Add(buff.config))
                    DestroyOwned(buff.config);
        }
    }

    public void ReleaseAll()
    {
        for (int i = units.Count - 1; i >= 0; i--)
            Release(units[i]);
    }

    private static void DestroyOwned(Object asset)
    {
        if (asset == null)
            return;
        if (Application.isPlaying)
            Object.Destroy(asset);
        else
            Object.DestroyImmediate(asset);
    }
}
