using System.Collections.Generic;

/// <summary>只管理波次配置游标，不拥有逻辑单位和模型。</summary>
public sealed class BattleWaveController
{
    private readonly List<BattleWave> waves = new();
    private int nextIndex;

    public int CurrentWaveNumber { get; private set; }
    public int WaveCount => waves.Count;
    public bool HasNext => nextIndex < waves.Count;

    public BattleWaveController(UnitConfig[] fallbackFirstWave,
        BattleEncounterConfig encounter = null)
    {
        if (encounter?.waves?.battleWaves != null)
        {
            foreach (BattleWave wave in encounter.waves.battleWaves)
                if (HasEnemies(wave))
                    waves.Add(wave);
        }
        if (waves.Count == 0 && fallbackFirstWave != null)
            waves.Add(new BattleWave { enemies = fallbackFirstWave });
    }

    public bool TryTakeNext(out UnitConfig[] enemies)
    {
        enemies = null;
        if (!HasNext)
            return false;
        BattleWave wave = waves[nextIndex++];
        CurrentWaveNumber = nextIndex;
        enemies = wave.enemies;
        return enemies != null && enemies.Length > 0;
    }

    private static bool HasEnemies(BattleWave wave)
    {
        if (wave?.enemies == null)
            return false;
        foreach (UnitConfig enemy in wave.enemies)
            if (enemy != null)
                return true;
        return false;
    }
}
