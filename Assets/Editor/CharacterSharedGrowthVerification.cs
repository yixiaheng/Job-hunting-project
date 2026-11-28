using System;
using System.Collections.Generic;
using System.IO;
using Battle.Logic;
using Game.Characters;
using UnityEditor;
using UnityEngine;

public static class CharacterSharedGrowthVerification
{
    [MenuItem("Tools/Characters/Verify Shared Growth")]
    public static void Run()
    {
        var checks = new List<string>();
        var unit = ScriptableObject.CreateInstance<UnitConfig>();
        var stats = UnitBaseStatsAuthoring.Ensure(unit);
        string error = null;
        void Check(bool pass, string name)
        { if (!pass) throw new InvalidOperationException(name); checks.Add(name); }
        bool Near(float a, float b) => Mathf.Abs(a - b) < .0001f;
        bool Reject(Action action)
        { try { action(); return false; } catch (ArgumentException) { return true; } catch (InvalidOperationException) { return true; } }
        try
        {
            var table = CharacterGrowthTable.Shared;
            table.Validate();
            int[] starts = { 1, 20, 30, 40, 50, 60, 70 }, ends = { 20, 30, 40, 50, 60, 70, 80 };
            float[] first = { 1, 2.35f, 3.25f, 4.15f, 5.05f, 5.95f, 6.85f };
            float[] last = { 1.95f, 2.85f, 3.75f, 4.65f, 5.55f, 6.45f, 7.35f };
            for (int stage = 0; stage < starts.Length; stage++)
            {
                Check(Near(table.Evaluate(starts[stage], stage), first[stage]), "stage start " + stage);
                Check(Near(table.Evaluate(ends[stage], stage), last[stage]), "stage end " + stage);
                for (int level = starts[stage] + 1; level <= ends[stage]; level++)
                    Check(Near(table.Evaluate(level, stage) - table.Evaluate(level - 1, stage), .05f), "linear increment " + stage + "/" + level);
            }
            Check(Near(table.Evaluate(25, 1), 2.6f), "25 asc1 coefficient 2.6");
            Check(Reject(() => table.Evaluate(19, 1)) && Reject(() => table.Evaluate(21, 0)) && Reject(() => table.Evaluate(81, 6)), "reject levels outside ascension bounds");
            UnitBaseStatsAuthoring.Set(unit, "health", 150.75f);
            UnitBaseStatsAuthoring.Set(unit, "attack", 53.25f);
            UnitBaseStatsAuthoring.Set(unit, "defence", 23.5f);
            UnitBaseStatsAuthoring.Set(unit, "speed", 103f);
            UnitBaseStatsAuthoring.Set(unit, "offensiveStats", new BattleOffensiveStats(.17f, .83f, .31f, .22f, new ElementDamageBonuses(fire: .28f)));
            string before = EditorJsonUtility.ToJson(stats), curveBefore = EditorJsonUtility.ToJson(table);
            unit.id = "growth-verification";
            var progress = new CharacterProgress { unitId = unit.id, level = 20 };
            var pre = CharacterStatCalculator.Calculate(unit, progress, null, out _);
            progress.ascension = 1;
            var post = CharacterStatCalculator.Calculate(unit, progress, null, out _);
            Check(pre.attack == Mathf.RoundToInt(53.25f * 1.95f) && post.attack == Mathf.RoundToInt(53.25f * 2.35f), "same level before and after breakthrough differs");
            progress.level = 80; progress.ascension = 6;
            var final = CharacterStatCalculator.Calculate(unit, progress, null, out _);
            Check(final.health == Mathf.RoundToInt(150.75f * 7.35f) && final.attack == Mathf.RoundToInt(53.25f * 7.35f), "decimal level1 values preserved until final rounding");
            Check(final.speed == 103 && Near(final.OffensiveStats.CritRate, .17f) && Near(final.OffensiveStats.CritDamage, .83f), "speed and crit do not grow");
            Check(Near(final.OffensiveStats.BreakEffect, .31f) && Near(final.OffensiveStats.BreakEfficiencyBonus, .22f)
                && Near(final.OffensiveStats.ElementalDamage.Get(ElementType.Fire), .28f), "break and elemental attributes do not grow");
            Check(before == EditorJsonUtility.ToJson(stats) && curveBefore == EditorJsonUtility.ToJson(table), "calculation is pure and preserves config assets");
            var snapshot = new BattleCharacterSnapshot(unit, progress, null);
            progress.level = 1; progress.ascension = 0;
            UnitBaseStatsAuthoring.Set(unit, "attack", 1f);
            var battle = snapshot.CreateUnit(0, 0);
            Check(battle.Level == 80 && battle.attack == final.attack, "battle snapshots actual level and calculated attributes");
            var old = new PlayerProfileData { formatVersion = 2 };
            old.roster.characters.Add(new CharacterProgress { unitId = "old-20", level = 20, ascension = 1, experience = 55, skillLevel = 3 });
            old.roster.characters.Add(new CharacterProgress { unitId = "old-40", level = 40, ascension = 1 });
            string oldJson = JsonUtility.ToJson(old);
            var migrated = PlayerProfileStore.MigrateSharedGrowth(old);
            Check(migrated.formatVersion == 3 && migrated.roster.characters[0].ascension == 1 && migrated.roster.characters[0].experience == 55 && migrated.roster.characters[0].skillLevel == 3
                && migrated.roster.characters[1].ascension == 2, "save migration preserves level experience and pre/post-breakthrough meaning");
            Check(oldJson == JsonUtility.ToJson(old), "migration does not mutate source profile");
            old.roster.characters[0].level = 90; old.roster.characters[0].ascension = 3;
            Check(Reject(() => PlayerProfileStore.MigrateSharedGrowth(old)), "legacy above80 is rejected without clamping or overwriting");
        }
        catch (Exception e) { error = e.ToString(); throw; }
        finally
        {
            UnityEngine.Object.DestroyImmediate(unit);
            UnityEngine.Object.DestroyImmediate(stats);
            Directory.CreateDirectory("Docs/Verification");
            File.WriteAllText("Docs/Verification/CharacterSharedGrowthResults.json", Newtonsoft.Json.JsonConvert.SerializeObject(
                new { passed = error == null, error, completedAt = DateTimeOffset.Now, checkCount = checks.Count, checks }, Newtonsoft.Json.Formatting.Indented));
        }
        Debug.Log("Shared growth checks passed: " + checks.Count);
    }
}
