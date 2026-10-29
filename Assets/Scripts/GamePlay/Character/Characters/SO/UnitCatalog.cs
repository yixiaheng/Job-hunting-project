using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "UnitCatalog",
    menuName = "Battle/UnitCatalog")]
public sealed class UnitCatalog : ScriptableObject
{
    [SerializeField] private List<UnitConfig> units = new();

    public IReadOnlyList<UnitConfig> Units => units;

    public UnitConfig FindById(string unitId)
    {
        if (string.IsNullOrWhiteSpace(unitId))
            return null;

        foreach (UnitConfig config in units)
        {
            if (config != null && config.id == unitId)
                return config;
        }

        return null;
    }
}