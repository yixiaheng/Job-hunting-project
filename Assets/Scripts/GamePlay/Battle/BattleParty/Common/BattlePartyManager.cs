using System;
using System.Collections.Generic;
using Battle.Events;
using Battle.SaveData;
using Game;
using UnityEngine;

public class BattlePartyManager : MonoBehaviour
{
    public static BattlePartyManager Instance { get; private set; }

    [SerializeField] private UnitCatalog unitCatalog;

    private BattleFormation formation = new();

    //可选的上场角色，默认是全部，后续做成读取玩家现有情况
    public IReadOnlyList<UnitConfig> AvailableAllies => Game.Characters.PlayerRuntime.Instance != null
        ? Game.Characters.PlayerRuntime.Instance.IsReady ? Game.Characters.PlayerRuntime.Instance.Characters.OwnedCharacters : Array.Empty<UnitConfig>()
        : unitCatalog != null
        ? unitCatalog.Units
        : Array.Empty<UnitConfig>();


    // 返回一个站队快照，确保UI部分无法直接修复真正的站队信息
    public BattleFormation Formation
    {
        get
        {
            var snapshot = new BattleFormation();

            foreach (FormationMember member in formation.members)
                snapshot.TryAdd(member.unitId, member.position);

            return snapshot;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        BattleFormation remembered = null;
        if (Game.Characters.PlayerRuntime.Instance != null
            && Game.Characters.PlayerRuntime.Instance.IsReady)
            remembered = Game.Characters.PlayerRuntime.Instance.GetFormation();
        else
            PartyFormationState.TryRestore(out remembered);
        if (remembered != null)
        {
            foreach (FormationMember member in remembered.members)
            {
                if (FindConfig(member.unitId) != null)
                    formation.TryAdd(member.unitId, member.position);
            }
        }
    }

    public UnitConfig FindConfig(string unitId)
    {
        if (Game.Characters.PlayerRuntime.Instance != null && (!Game.Characters.PlayerRuntime.Instance.IsReady || !Game.Characters.PlayerRuntime.Instance.Characters.Owns(unitId))) return null;
        return unitCatalog != null ? unitCatalog.FindById(unitId) : null;
    }

    public bool TryCreateBattleLaunchData(out BattleLaunchData data, out string error)
    {
        var slots = new UnitConfig[BattleFormation.MaxMembers];
        for (int position = 0; position < slots.Length; position++)
        {
            FormationMember member = formation.GetMemberAtPosition(position);
            if (member == null)
                continue;

            UnitConfig config = FindConfig(member.unitId);
            if (config == null)
            {
                data = null;
                error = $"Ally slot {position + 1} references an unknown unit: {member.unitId}.";
                return false;
            }

            slots[position] = config;
        }

        return BattleLaunchData.TryCreate(slots, out data, out error);
    }

    public bool AddOrReplace(UnitConfig config, int position)
    {
        if (config == null || FindConfig(config.id) != config)
            return false;

        BattleFormation next = Copy(formation);
        if (!next.TryReplace(config.id, position))
            return false;
        return CommitFormation(next);
    }

    // Compatibility with existing Inspector callbacks.
    public bool AddorReplace(UnitConfig config, int position) => AddOrReplace(config, position);

    // Each index is a formation slot. Null entries remain empty on confirmation.
    public bool ApplySortedFormation(IReadOnlyList<UnitConfig> slotUnits)
    {
        if (slotUnits == null || slotUnits.Count > BattleFormation.MaxMembers)
            return false;

        var next = new BattleFormation();
        for (int i = 0; i < slotUnits.Count; i++)
        {
            UnitConfig config = slotUnits[i];
            if (config == null)
                continue;

            if (FindConfig(config.id) != config || !next.TryAdd(config.id, i))
                return false;
        }

        return CommitFormation(next);
    }

    public bool RemoveAt(int position)
    {
        if (!formation.ContainsPosition(position))
            return false;

        BattleFormation next = Copy(formation);
        next.RemoveAtPosition(position);
        return CommitFormation(next);
    }

    public void ClearFormation()
    {
        if (formation.members.Count == 0)
            return;

        CommitFormation(new BattleFormation());
    }

    private bool CommitFormation(BattleFormation next)
    {
        if (next == null)
            return false;
        var runtime = Game.Characters.PlayerRuntime.Instance;
        if (runtime != null && runtime.IsReady
            && !runtime.TrySetFormation(next, out string reason))
        {
            Debug.LogError("配队保存失败：" + reason, this);
            return false;
        }
        formation = Copy(next);
        PartyFormationState.Save(Formation);
        GameEvents.Battle.Publish(new PartyFormationChangedEvent(this));
        return true;
    }

    private static BattleFormation Copy(BattleFormation source)
    {
        var copy = new BattleFormation();
        if (source?.members == null)
            return copy;
        foreach (FormationMember member in source.members)
            if (member != null)
                copy.TryAdd(member.unitId, member.position);
        return copy;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
