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

    public IReadOnlyList<UnitConfig> AvailableAllies => unitCatalog != null
        ? unitCatalog.Units
        : Array.Empty<UnitConfig>();

    // Return a snapshot so UI cannot modify the real formation directly.
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
    }

    public UnitConfig FindConfig(string unitId)
    {
        return unitCatalog != null ? unitCatalog.FindById(unitId) : null;
    }

    public bool AddOrReplace(UnitConfig config, int position)
    {
        if (config == null || FindConfig(config.id) != config)
            return false;

        if (!formation.TryReplace(config.id, position))
            return false;

        PublishFormationChanged();
        return true;
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

        formation = next;
        PublishFormationChanged();
        return true;
    }

    public bool RemoveAt(int position)
    {
        if (!formation.ContainsPosition(position))
            return false;

        formation.RemoveAtPosition(position);
        PublishFormationChanged();
        return true;
    }

    public void ClearFormation()
    {
        if (formation.members.Count == 0)
            return;

        formation.Clear();
        PublishFormationChanged();
    }

    private void PublishFormationChanged()
    {
        GameEvents.Battle.Publish(new PartyFormationChangedEvent(this));
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
