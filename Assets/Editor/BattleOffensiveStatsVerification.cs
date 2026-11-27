using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Battle.Editor.ConfigImport;
using Battle.Logic;
using Game.Characters;
using Game.Inventory;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class BattleOffensiveStatsVerification
{
    [MenuItem("Tools/Battle/Verify Offensive Attributes")]
    public static void Run()
    {
        ConfigImportService.EnsureEditorReady();
        var checks = new List<string>();
        var backups = new List<string>();
        byte[] previousReport = File.Exists(ConfigImportService.LastReport) ? File.ReadAllBytes(ConfigImportService.LastReport) : null;
        string root = "Assets/__BattleOffensiveStatsVerification_" + Guid.NewGuid().ToString("N");
        string temp = "Temp/BattleOffensiveStatsVerification";
        string error = null;
        UnitConfig config = null;
        CharacterGrowthConfig growth = null;
        void Check(bool value, string label)
        { if (!value) throw new InvalidOperationException(label); checks.Add(label); }
        bool Near(float a, float b) => Mathf.Abs(a - b) < 0.00001f;
        bool Reject(Action action)
        { try { action(); return false; } catch (ArgumentOutOfRangeException) { return true; } }
        try
        {
            Directory.CreateDirectory(temp);
            config = ScriptableObject.CreateInstance<UnitConfig>();
            config.id = "offensive-enemy"; config.displayName = "属性验证";
            config.maxHealth = 100; config.attack = 40; config.defence = 10; config.speed = 100;
            Check(Near(config.OffensiveStats.CritRate, .05f) && Near(config.OffensiveStats.CritDamage, .5f), "new config uses 5% crit / 50% crit damage");
            JsonUtility.FromJsonOverwrite("{\"id\":\"offensive-enemy\",\"attack\":40}", config);
            Check(Near(config.OffensiveStats.CritRate, .05f), "legacy serialized data missing new field retains initializer");
            var defaults = BattleDataMaping.ToUnit(config, Faction.Enemy, 0, 0);
            Check(Near(defaults.CritRate, .05f) && Near(defaults.CritDamage, .5f) && Near(defaults.BreakEfficiencyMultiplier, 1f), "direct enemy mapping carries defaults; efficiency starts at 100%");
            Check((int)ElementType.Fire == 0 && (int)ElementType.Imaginary == 5 && (int)ElementType.Physical == 6, "existing element serialization IDs preserved");

            var values = new ElementDamageBonuses(.1f, .2f, .3f, .4f, .5f, .6f, .7f);
            var configured = new BattleOffensiveStats(1.2f, 1.6f, 2f, .5f, values);
            BattleTableSchema.WriteField(config, "offensiveStats", configured);
            var first = BattleDataMaping.ToUnit(config, Faction.OurSide, 0, 0);
            var second = BattleDataMaping.ToUnit(config, Faction.Enemy, 1, 1);
            Check(Near(first.CritRate, 1.2f) && Near(first.EffectiveCritRate, 1f), "raw crit overcap retained; effective chance capped at 100%");
            Check(Near(first.CritDamage, 1.6f) && Near(first.BreakEffect, 2f) && Near(first.BreakEfficiencyMultiplier, 1.5f), "crit damage break effect and efficiency are independent fractional values");
            ElementType[] elements = { ElementType.Physical, ElementType.Fire, ElementType.Ice, ElementType.Lightning, ElementType.Wind, ElementType.Quantum, ElementType.Imaginary };
            for (int i = 0; i < elements.Length; i++)
                Check(Near(first.GetElementDamageBonus(elements[i]), (i + 1) * .1f), "independent element bonus: " + elements[i]);
            first.SetOffensiveStats(default);
            Check(Near(first.CritRate, 0) && Near(second.CritRate, 1.2f) && Near(config.OffensiveStats.CritRate, 1.2f), "explicit zero allowed; changing runtime stats does not mutate asset or other unit");
            Check(Reject(() => new BattleOffensiveStats(float.NaN, .5f)), "reject NaN crit rate");
            Check(Reject(() => new BattleOffensiveStats(.05f, float.PositiveInfinity)), "reject infinite crit damage");
            Check(Reject(() => new BattleOffensiveStats(.05f, .5f, -1)), "reject negative base break effect");
            Check(Reject(() => new ElementDamageBonuses(fire: -.1f)), "reject negative base elemental bonus");
            Check(Reject(() => first.GetElementDamageBonus((ElementType)999)), "unknown element never silently reads another element");

            growth = ScriptableObject.CreateInstance<CharacterGrowthConfig>();
            growth.levelGrowth = .1f; config.growth = growth;
            var runtime = AssetDatabase.LoadAssetAtPath<PlayerRuntimeConfig>("Assets/Resources/Bootstrap/PlayerRuntimeConfig.asset");
            var inventory = new InventoryService(runtime.items, false);
            var progress = new CharacterProgress { unitId = config.id, level = 20 };
            var stats = CharacterStatCalculator.Calculate(config, progress, inventory, out _);
            Check(stats.attack > config.attack && Near(stats.OffensiveStats.CritRate, 1.2f) && Near(stats.OffensiveStats.BreakEffect, 2f), "level growth affects base attack without scaling percentage attributes");
            Check(stats.Level == 20, "calculated stats retain actual progression level");
            var snapshot = new BattleCharacterSnapshot(config, progress, inventory);
            progress.level = 80;
            BattleTableSchema.WriteField(config, "offensiveStats", BattleOffensiveStats.Default);
            var ally = snapshot.CreateUnit(0, 0);
            Check(ally.Level == 20, "battle level is captured at launch, not read from later mutable progress");
            Check(Near(ally.CritRate, 1.2f) && Near(ally.GetElementDamageBonus(ElementType.Imaginary), .7f), "ally battle snapshot retains values captured before later config edits");
            ally.SetOffensiveStats(default);
            Check(Near(snapshot.CreateUnit(1, 1).CritRate, 1.2f), "snapshot can spawn independent units without runtime state leakage");
            Check(new BattleCharacterSnapshot(config, progress, inventory).CreateUnit(0, 0).Level == 80,
                "next launch captures updated progression level");

            BattleTableSchema.WriteField(growth, "offensivePerLevel", new BattleOffensiveStats(.001f, .002f, .01f, 0,
                new ElementDamageBonuses(fire: .003f, physical: .002f)));
            BattleTableSchema.WriteField(growth, "offensivePerAscension", new BattleOffensiveStats(.02f, .05f, .1f, .05f,
                new ElementDamageBonuses(fire: .04f)));
            progress.level = 11; progress.ascension = 2;
            var grown = CharacterStatCalculator.Calculate(config, progress, inventory, out _);
            Check(grown.Level == 11 && Near(grown.OffensiveStats.CritRate, .10f), "base config + ten level increments + two ascensions determine crit rate");
            Check(Near(grown.OffensiveStats.CritDamage, .62f) && Near(grown.OffensiveStats.BreakEffect, .3f), "crit damage and break effect use their own additive growth");
            Check(Near(grown.OffensiveStats.BreakEfficiencyBonus, .1f) && Near(grown.OffensiveStats.ElementalDamage.Get(ElementType.Fire), .11f), "efficiency and elemental growth are independent");
            Check(Near(grown.OffensiveStats.ElementalDamage.Get(ElementType.Physical), .02f) && Near(grown.OffensiveStats.ElementalDamage.Get(ElementType.Ice), 0), "element growth affects only configured elements");
            var growthSnapshot = new BattleCharacterSnapshot(config, progress, inventory);
            BattleTableSchema.WriteField(growth, "offensivePerLevel", default(BattleOffensiveStats));
            var grownAlly = growthSnapshot.CreateUnit(0, 0);
            Check(grownAlly.Level == 11 && Near(grownAlly.CritRate, .10f) && Near(grownAlly.BreakEffect, .3f), "launch snapshots both actual level and evaluated growth, not live growth asset");
            progress.level = 1; progress.ascension = 0;
            Check(Near(CharacterStatCalculator.Calculate(config, progress, inventory, out _).OffensiveStats.CritRate, .05f), "level one and no ascension keep exact config baseline");
            progress.level = 0;
            Check(Reject(() => CharacterStatCalculator.Calculate(config, progress, inventory, out _)), "invalid level fails instead of silently using base values");
            progress.level = 1;
            config.growth = null;
            Check(CharacterStatCalculator.Calculate(config, progress, inventory, out _).Level == 1, "absent growth retains real level and base attributes");

            // Independent assets: actual Excel preview/apply, GUIDs and nested struct serialization.
            config.growth = null;
            ConfigImportService.EnsureAssetFolder(root + "/Enemies");
            AssetDatabase.CreateAsset(config, root + "/Enemies/Unit.asset");
            AssetDatabase.SaveAssetIfDirty(config);
            var adapter = new BattleConfigTableAdapter(root + "/Generated");
            string book = temp + "/attributes.xlsx";
            var exportMessages = ConfigImportService.Export(book, root, false, adapter);
            Check(!exportMessages.Any(m => m.Kind == ImportMessageKind.Error), "export new attribute columns");
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(config));
            var messages = new List<ImportMessage>();
            var data = adapter.Parse(book, messages);
            var row = data.Rows("Units").Single();
            Check(Near(row.Get<float>("critRate"), .05f) && Near(row.Get<float>("critDamage"), .5f), "Excel exports actual serialized defaults");
            row.Values["critRate"] = .8f; row.Values["critDamage"] = 1.5f; row.Values["breakEffect"] = 2.4f;
            row.Values["breakEfficiencyBonus"] = .6f;
            string[] columns = { "physicalDamageBonus", "fireDamageBonus", "iceDamageBonus", "lightningDamageBonus", "windDamageBonus", "quantumDamageBonus", "imaginaryDamageBonus" };
            for (int i = 0; i < columns.Length; i++) row.Values[columns[i]] = (i + 1) * .11f;
            ConfigWorkbook.Write(book, data, adapter.Schemas);
            var plan = ConfigImportService.Preview(book, root, adapter);
            Check(plan.IsValid && plan.Updates.Count == 1 && plan.Messages.Any(m => m.Detail.Contains("critRate:")), "preview locates nested attribute differences");
            string backupFolder = ConfigImportService.Apply(plan); backups.Add(backupFolder);
            Check(Near(config.OffensiveStats.CritRate, .8f) && Near(config.OffensiveStats.CritDamage, 1.5f) && Near(config.OffensiveStats.BreakEffect, 2.4f) && Near(config.OffensiveStats.BreakEfficiencyBonus, .6f), "apply writes nested scalar attributes back into asset");
            for (int i = 0; i < elements.Length; i++)
                Check(Near(config.OffensiveStats.ElementalDamage.Get(elements[i]), (i + 1) * .11f), "apply writes nested value-type element: " + elements[i]);
            Check(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(config)) == guid, "attribute import retains original GUID");
            Check(ConfigImportService.Preview(book, root, adapter).Updates.Count == 0, "second import is a no-op");

            row.Values["breakEffect"] = -.1f;
            ConfigWorkbook.Write(book, data, adapter.Schemas);
            var invalid = ConfigImportService.Preview(book, root, adapter);
            Check(!invalid.IsValid && invalid.Messages.Any(m => m.Kind == ImportMessageKind.Error && m.Sheet == "Units" && m.Row == 3 && m.Detail.Contains("breakEffect")), "invalid attribute accurately reported at worksheet row and field");
            Check(Near(config.OffensiveStats.BreakEffect, 2.4f), "invalid preview performs no asset writes");

            // Old workbook without the 11 new columns must not reset authored stats.
            var oldSchema = adapter.Schemas.Select(s => s.Name == "Units"
                ? new TableSchema("Units", s.Columns.Where(c => !c.OptionalColumn).ToArray()) : s).ToArray();
            string legacy = temp + "/legacy.xlsx";
            ConfigWorkbook.Write(legacy, data, oldSchema);
            var oldPlan = ConfigImportService.Preview(legacy, root, adapter);
            Check(oldPlan.IsValid && oldPlan.Updates.Count == 0 && oldPlan.Messages.Any(m => m.Kind == ImportMessageKind.Warning && m.Detail.Contains("旧表")), "legacy workbook preserves existing attributes with explicit warning");
            row.Values["unitId"] = "new-legacy-enemy";
            ConfigWorkbook.Write(legacy, data, oldSchema);
            var newPlan = ConfigImportService.Preview(legacy, root, adapter);
            Check(newPlan.IsValid, "legacy workbook can still create a new enemy");
            backupFolder = ConfigImportService.Apply(newPlan); backups.Add(backupFolder);
            var created = AssetDatabase.LoadAssetAtPath<UnitConfig>(newPlan.Updates.Single().Path);
            Check(Near(created.OffensiveStats.CritRate, .05f) && Near(created.OffensiveStats.CritDamage, .5f), "new unit from legacy workbook gets defaults rather than zero crit damage");

            var index = new ConfigAssetIndex(root, new List<ImportMessage>());
            var oldData = adapter.Parse(legacy, new List<ImportMessage>());
            var batch = ConfigBatchAuthoring.Build(oldData, index, new ConfigBatchRequest
            { Recipe = ConfigRecipe.CopyUnit, SourceId = "new-legacy-enemy", UnitPrefix = "copy_", CloneSkills = false });
            Check(Near(batch.Rows("Units").Single(r => r.Id == "copy_1").Get<float>("critRate"), .05f), "batch copy supports old workbook missing new columns");
            var template = ConfigBatchAuthoring.Build(new ConfigTableData(), index, new ConfigBatchRequest { Recipe = ConfigRecipe.NewUnit });
            Check(Near(template.Rows("Units").Single().Get<float>("critRate"), .05f) && Near(template.Rows("Units").Single().Get<float>("critDamage"), .5f), "new unit recipe uses same crit defaults as UnitConfig");
        }
        catch (Exception e) { error = e.ToString(); throw; }
        finally
        {
            if (AssetDatabase.IsValidFolder(root)) AssetDatabase.DeleteAsset(root);
            if (config != null && !AssetDatabase.Contains(config)) Object.DestroyImmediate(config);
            if (growth != null) Object.DestroyImmediate(growth);
            // Only the exact backup directories returned by this test's two applies.
            foreach (string backup in backups.Distinct())
                if (backup != null && Directory.Exists(backup) && backup.Replace('\\', '/').StartsWith(ConfigImportService.BackupRoot + "/", StringComparison.Ordinal))
                    Directory.Delete(backup, true);
            if (previousReport != null) File.WriteAllBytes(ConfigImportService.LastReport, previousReport);
            else if (File.Exists(ConfigImportService.LastReport)) File.Delete(ConfigImportService.LastReport);
            Directory.CreateDirectory("Docs/Verification");
            File.WriteAllText("Docs/Verification/BattleOffensiveStatsResults.json", Newtonsoft.Json.JsonConvert.SerializeObject(
                new { passed = error == null, error, completedAt = DateTimeOffset.Now, checkCount = checks.Count, checks }, Newtonsoft.Json.Formatting.Indented));
        }
        Debug.Log("Battle offensive stats verification passed: " + checks.Count);
    }
}
