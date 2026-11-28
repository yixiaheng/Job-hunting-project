using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Game.Characters;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Battle.Editor.ConfigImport
{
    public static class GrowthConfigImportVerification
    {
        [MenuItem("Tools/Battle/Verify Growth Excel Import")]
        public static void Run()
        {
            ConfigImportService.EnsureEditorReady();
            string root = "Assets/__GrowthExcelVerification_" + Guid.NewGuid().ToString("N");
            string workbook = "Temp/GrowthExcelVerification/养成配置验证.xlsx";
            var checks = new List<string>(); var backups = new List<string>();
            string error = null;
            byte[] oldReport = File.Exists(ConfigImportService.LastReport) ? File.ReadAllBytes(ConfigImportService.LastReport) : null;
            void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); checks.Add(label); }
            bool Reject(Action action) { try { action(); return false; } catch (InvalidOperationException) { return true; } }
            T Asset<T>(string name) where T : ScriptableObject
            { var a = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(a, root + "/" + name + ".asset"); return a; }
            try
            {
                ConfigImportService.EnsureAssetFolder(root + "/Enemies");
                var curve = Asset<CharacterGrowthTable>("Curve"); GrowthConfigTables.SharedForVerification = curve;
                var growth = Asset<CharacterGrowthConfig>("Growth");
                BattleTableSchema.WriteField(growth, "configId", "test-growth");
                growth.effectGrowth.Add(new SkillEffectGrowth { effectKey = "sample.hit", perSkillLevel = .1f });
                var skill = Asset<SkillConfig>("Skill"); skill.id = 993001; skill.Name = "测试技能";
                skill.effectConfigs = new List<EffectConfig> { new() { key = "sample.hit", effectType = EffectType.Damage, value = 1, growsWithSkill = true } };
                var unit = Asset<UnitConfig>("Enemies/Unit"); unit.id = "growth-enemy"; unit.displayName = "成长测试";
                unit.growth = growth; unit.skills.Add(skill); UnitBaseStatsAuthoring.Ensure(unit);
                foreach (var a in new Object[] { curve, growth, skill, unit, unit.BaseStats }) { EditorUtility.SetDirty(a); AssetDatabase.SaveAssetIfDirty(a); }
                var adapter = new BattleConfigTableAdapter(root + "/Generated");
                Check(!ConfigImportService.Export(workbook, root, false, adapter).Any(m => m.Kind == ImportMessageKind.Error), "export five growth sheets and unit binding");
                var messages = new List<ImportMessage>(); var data = adapter.Parse(workbook, messages);
                Check(!messages.Any(m => m.Kind == ImportMessageKind.Error) && data.Rows("CharacterGrowthStages").Count == 7, "Chinese filename parses and seven stages roundtrip");
                Check(ConfigImportService.Preview(workbook, root, adapter).Updates.Count == 0, "unchanged export performs no writes");
                string growthPath = AssetDatabase.GetAssetPath(growth), curvePath = AssetDatabase.GetAssetPath(curve);
                string guid = AssetDatabase.AssetPathToGUID(growthPath), unitGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(unit));
                void Write() => ConfigWorkbook.Write(workbook, data, adapter.Schemas);
                ConfigImportPlan Preview() => ConfigImportService.Preview(workbook, root, adapter);
                void Apply()
                {
                    var p = Preview(); Check(p.IsValid, "valid preview: " + string.Join(";", p.Messages.Where(m => m.Kind == ImportMessageKind.Error)));
                    backups.Add(ConfigImportService.Apply(p, true));
                }
                var parent = data.Rows("CharacterGrowth").Single();
                parent.Values["experienceBase"] = 120;
                data.Rows("CharacterGrowthStages")[1].Values["ascensionCost"] = 9;
                data.Rows("CharacterRanks")[0].Values["shardCost"] = 15;
                data.Rows("SkillEffectGrowth")[0].Values["perSkillLevel"] = .25f;
                data.Rows("GrowthStages")[6].Values["endMultiplier"] = 7.55f;
                Write(); string pure = EditorJsonUtility.ToJson(growth), pureCurve = EditorJsonUtility.ToJson(curve);
                var plan = Preview();
                Check(plan.IsValid && plan.Updates.Count == 2 && plan.Messages.Any(m => m.Detail.Contains("experienceBase:")), "field and child differences include shared and per-character assets");
                Check(pure == EditorJsonUtility.ToJson(growth) && pureCurve == EditorJsonUtility.ToJson(curve), "preview does not mutate growth or curve");
                Apply();
                Check(growth.experienceBase == 120 && growth.ascensionCosts[1] == 9 && growth.shardCosts[0] == 15
                    && growth.effectGrowth[0].perSkillLevel == .25f && Mathf.Approximately(curve.Evaluate(80,6),7.55f), "all growth tables update their intended runtime data");
                Check(AssetDatabase.AssetPathToGUID(growthPath) == guid && unit.growth == growth && AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(unit)) == unitGuid, "growth and owner GUID/references retained");
                Check(Preview().Updates.Count == 0, "second identical import is a no-op");

                // Sorting physical rows must not reorder semantic lists.
                data.MutableRows("GrowthStages").Reverse(); data.MutableRows("CharacterGrowthStages").Reverse(); data.MutableRows("CharacterRanks").Reverse(); Write();
                Check(Preview().IsValid && Preview().Updates.Count == 0, "row order does not change stage/rank order");
                var firstStage = data.Rows("GrowthStages").First(r => r.Get<int>("stage") == 0);
                firstStage.Values["maxLevel"] = 21; Write();
                Check(!Preview().IsValid && Preview().Messages.Any(m => m.Sheet == "GrowthStages" && m.Row >= 3), "noncontiguous growth endpoints reject with row location");
                firstStage.Values["maxLevel"] = 20;
                parent.Values["characterExperience"] = "missing_material"; Write();
                Check(!Preview().IsValid && Preview().Messages.Any(m => m.Detail.Contains("missing_material")), "unknown material is rejected");
                parent.Values["characterExperience"] = "character_exp";
                var effect = data.Rows("SkillEffectGrowth")[0]; effect.Values["requiredRank"] = 6; Write();
                Check(!Preview().IsValid, "impossible rank requirement rejected"); effect.Values["requiredRank"] = 0;
                var stageRow = data.MutableRows("CharacterGrowthStages").Last(); data.MutableRows("CharacterGrowthStages").Remove(stageRow); Write();
                Check(!Preview().IsValid, "missing stage cannot silently shorten skill caps"); data.Add(stageRow);
                var duplicate = ConfigBatchAuthoring.Copy(parent); data.Add(duplicate); Write();
                Check(!Preview().IsValid, "duplicate growth ID rejected"); data.MutableRows("CharacterGrowth").Remove(duplicate);

                // New skill key and its growth key may migrate together in one workbook.
                data.Rows("SkillEffects")[0].Values["effectKey"] = "sample.renamed"; Write();
                Check(!Preview().IsValid, "effect removal with dangling growth is rejected");
                effect.Values["effectKey"] = "sample.renamed"; Write(); Apply();
                Check(skill.effectConfigs[0].key == "sample.renamed" && growth.effectGrowth[0].effectKey == "sample.renamed", "same-import skill/growth key migration succeeds");

                var newParent = ConfigBatchAuthoring.Copy(parent); newParent.Values["growthId"] = "test-new-growth"; data.Add(newParent);
                foreach (string sheet in new[] { "CharacterGrowthStages", "CharacterRanks", "SkillEffectGrowth" })
                    foreach (var source in data.Rows(sheet).Where(r => r.Id == "test-growth").ToArray())
                    { var copy = ConfigBatchAuthoring.Copy(source); copy.Values["growthId"] = "test-new-growth"; data.Add(copy); }
                var newUnit = ConfigBatchAuthoring.Copy(data.Rows("Units")[0]); newUnit.Values["unitId"] = "new-growth-unit"; newUnit.Values["growthId"] = "test-new-growth"; data.Add(newUnit);
                foreach (var source in data.Rows("UnitSkills").ToArray()) { var copy = ConfigBatchAuthoring.Copy(source); copy.Values["unitId"] = newUnit.Id; data.Add(copy); }
                Write(); Apply();
                var index = new ConfigAssetIndex(root, new());
                Check(index.Units[newUnit.Id].growth == index.Growths["test-new-growth"], "new unit binds same-workbook new growth asset");
                Check(Preview().Updates.Count == 0, "new dependency chain is repeatable without writes");
                var batch = ConfigBatchAuthoring.Build(data, index, new ConfigBatchRequest { Recipe = ConfigRecipe.CopyUnit, SourceId = unit.id, UnitPrefix = "copied", CloneSkills = true, StartSkillId = 993100 });
                var batchMessages = new List<ImportMessage>(); adapter.Validate(batch, index, batchMessages);
                Check(!batchMessages.Any(m => m.Kind == ImportMessageKind.Error) && batch.Rows("CharacterGrowth").Any(r => r.Id == "growth.copied1"), "batch unit/skill copy creates matching growth and remaps effect keys");

                // v2 may never treat missing sheets as clears. Genuine v1 keeps all growth data.
                string legacy = "Temp/GrowthExcelVerification/旧版.xlsx";
                File.Copy(workbook, legacy, true);
                using (var doc = SpreadsheetDocument.Open(legacy, true))
                {
                    var sheet = doc.WorkbookPart.Workbook.Sheets.Elements<Sheet>().Single(s => s.Name == "CharacterRanks");
                    doc.WorkbookPart.DeletePart(sheet.Id); sheet.Remove(); doc.Save();
                }
                Check(!ConfigImportService.Preview(legacy, root, adapter).IsValid, "v2 missing growth sheet rejected");
                File.Copy(workbook, legacy, true);
                using (var doc = SpreadsheetDocument.Open(legacy, true))
                {
                    var book = doc.WorkbookPart;
                    foreach (var sheet in book.Workbook.Sheets.Elements<Sheet>().Where(s => GrowthConfigTables.IsSheet(s.Name)).ToArray()) { book.DeletePart(sheet.Id); sheet.Remove(); }
                    var meta = book.Workbook.Sheets.Elements<Sheet>().Single(s => s.Name == "Meta");
                    var cell = ((WorksheetPart)book.GetPartById(meta.Id)).Worksheet.Descendants<Cell>().Single(c => c.CellReference == "B3");
                    cell.RemoveAllChildren(); cell.DataType = CellValues.InlineString; cell.InlineString = new InlineString(new Text("1"));
                    var units = book.Workbook.Sheets.Elements<Sheet>().Single(s => s.Name == "Units");
                    var ws = ((WorksheetPart)book.GetPartById(units.Id)).Worksheet;
                    string col = ConfigWorkbook.Letters(ws.Descendants<Row>().First().Elements<Cell>().Single(c => ConfigWorkbookReferences.CellText(book,c) == "growthId").CellReference);
                    foreach (var c in ws.Descendants<Cell>().Where(c => ConfigWorkbook.Letters(c.CellReference) == col).ToArray()) c.Remove();
                    doc.Save();
                }
                var legacyPlan = ConfigImportService.Preview(legacy, root, adapter);
                Check(legacyPlan.IsValid && legacyPlan.Updates.Count == 0, "v1 missing new sheets and growthId preserves existing growth bindings and values");

                parent.Values["experienceBase"] = 130; Write(); plan = Preview();
                growth.experienceBase = 140; EditorUtility.SetDirty(growth);
                Check(Reject(() => ConfigImportService.Apply(plan)) && !Preview().IsValid, "unsaved growth invalidates preview/apply");
                growth.experienceBase = 120; EditorUtility.SetDirty(growth); AssetDatabase.SaveAssetIfDirty(growth);
                data.Rows("GrowthStages").Single(r => r.Get<int>("stage") == 6).Values["endMultiplier"] = 7.65f; Write();
                string growthBytes = ConfigImportService.FileHash(growthPath), curveBytes = ConfigImportService.FileHash(curvePath);
                plan = Preview(); Check(plan.IsValid && plan.Updates.Count == 2, "rollback fixture updates exactly shared table and growth");
                ConfigImportService.AfterWriteForVerification = n => { if (n == 2) throw new IOException("simulated growth write failure"); };
                Check(Reject(() => ConfigImportService.Apply(plan)), "write failure triggers rollback"); ConfigImportService.AfterWriteForVerification = null;
                Check(ConfigImportService.FileHash(growthPath) == growthBytes && ConfigImportService.FileHash(curvePath) == curveBytes
                    && AssetDatabase.AssetPathToGUID(growthPath) == guid, "rollback restores both growth and shared-curve bytes/GUID");
            }
            catch (Exception e) { error = e.ToString(); throw; }
            finally
            {
                ConfigImportService.AfterWriteForVerification = null; GrowthConfigTables.SharedForVerification = null;
                if (AssetDatabase.IsValidFolder(root)) AssetDatabase.DeleteAsset(root);
                foreach (string backup in backups) if (backup != null && Directory.Exists(backup) && backup.StartsWith(ConfigImportService.BackupRoot + "/", StringComparison.Ordinal)) Directory.Delete(backup, true);
                if (oldReport != null) File.WriteAllBytes(ConfigImportService.LastReport, oldReport);
                Directory.CreateDirectory("Docs/Verification");
                File.WriteAllText("Docs/Verification/GrowthExcelResults.json", Newtonsoft.Json.JsonConvert.SerializeObject(
                    new { passed = error == null, error, completedAt = DateTimeOffset.Now, checkCount = checks.Count, checks }, Newtonsoft.Json.Formatting.Indented));
            }
            Debug.Log("Growth Excel checks passed: " + checks.Count);
        }
    }
}
