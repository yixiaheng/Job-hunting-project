using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Game.Characters;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Battle.Editor.ConfigImport
{
    [InitializeOnLoad]
    public static class BattleConfigImportVerification
    {
        private const string Folder = "Temp/BattleConfigImport";
        private static readonly List<string> checks = new();
        static BattleConfigImportVerification() => EditorApplication.update += Poll;
        private static void Poll()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Folder + "/request.txt")) return;
            string command;
            try { command = File.ReadAllText(Folder + "/request.txt").Trim(); } catch (IOException) { return; }
            File.Delete(Folder + "/request.txt");
            try
            {
                if (command == "test") Run();
                else if (command == "export") ExportProject();
                else if (command == "regression")
                {
                    RunRegression();
                }
                else if (command == "open") BattleConfigImportWindow.Open();
                else if (command == "audit")
                    File.WriteAllLines(Folder + "/dirty-assets.txt", AssetDatabase.FindAssets("", new[] { "Assets/Config" }).Select(AssetDatabase.GUIDToAssetPath)
                        .Select(AssetDatabase.LoadMainAssetAtPath).Where(o => o != null && EditorUtility.IsDirty(o)).Select(AssetDatabase.GetAssetPath));
                else if (command == "project-roundtrip") ProjectRoundtrip();
                else if (command == "upgrade-workbooks")
                {
                    var reports = new List<string>();
                    foreach (string path in new[] { ConfigImportService.DefaultWorkbook, "ConfigTables/BattleConfigTemplate.xlsx" })
                        reports.Add(path + "\n" + string.Join("\n", ConfigWorkbookReferences.Refresh(path, "Assets/Config")));
                    File.WriteAllText(Folder + "/upgrade-workbooks.txt", string.Join("\n\n", reports));
                }
                else if (command == "preview-live")
                {
                    var preview = ConfigImportService.Preview(ConfigImportService.DefaultWorkbook, "Assets/Config");
                    File.WriteAllText(Folder + "/live-preview.txt", "Valid=" + preview.IsValid + "\n" + string.Join("\n", preview.Messages));
                }
                else throw new InvalidOperationException("Unknown command: " + command);
            }
            catch (Exception e) { Directory.CreateDirectory(Folder); File.WriteAllText(Folder + "/command-error.txt", e.ToString()); Debug.LogException(e); }
        }

        [MenuItem("Tools/Battle/Verify Config Excel Import")]
        public static void Run()
        {
            ConfigImportService.EnsureEditorReady(); checks.Clear(); Directory.CreateDirectory(Folder);
            string root = "Assets/__BattleConfigImportVerification_" + Guid.NewGuid().ToString("N");
            var createdBackups = new List<string>();
            try
            {
                ConfigImportService.EnsureAssetFolder(root + "/Allies");
                var adapter = new BattleConfigTableAdapter(root + "/Generated");
                T Asset<T>(string name) where T : ScriptableObject
                { var a = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(a, root + "/" + name + ".asset"); return a; }
                var buff = Asset<BuffConfig>("Buff"); buff.id = 991001; buff.Name = "测试减防"; buff.durationTimes = 2; buff.maxStack = 2; buff.value = -0.2f; buff.isDebuff = true;
                var skill = Asset<SkillConfig>("Skill"); skill.id = 991001; skill.Name = "测试攻击"; skill.kind = SkillKind.Skill; skill.targetSide = TargetSide.OtherSide;
                skill.effectConfigs = new List<EffectConfig>
                {
                    new() { key = "test.hit", value = 1.2f, effectType = EffectType.Damage, growsWithSkill = true },
                    new() { key = "test.buff", effectType = EffectType.ApplyBuff, buffId = buff.id, buff = buff, stack = 1 }
                };
                var growth = Asset<CharacterGrowthConfig>("Growth"); growth.effectGrowth.Add(new SkillEffectGrowth { effectKey = "test.hit" });
                var unit = Asset<UnitConfig>("Allies/Unit"); unit.id = "test-unit"; unit.displayName = "测试角色"; UnitBaseStatsAuthoring.Set(unit, "health", 100f); UnitBaseStatsAuthoring.Set(unit, "attack", 20f); UnitBaseStatsAuthoring.Set(unit, "defence", 10f); UnitBaseStatsAuthoring.Set(unit, "speed", 100f); UnitBaseStatsAuthoring.Set(unit, "maxEnergy", 100);
                unit.skills = new List<SkillConfig> { skill }; unit.talentAction = skill; unit.growth = growth;
                var go = new GameObject("test prefab");
                try { unit.prefab = PrefabUtility.SaveAsPrefabAsset(go, root + "/test.prefab"); } finally { Object.DestroyImmediate(go); }
                var texture = new Texture2D(2, 2); File.WriteAllBytes(root + "/icon.png", texture.EncodeToPNG()); Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(root + "/icon.png"); var importer = (TextureImporter)AssetImporter.GetAtPath(root + "/icon.png"); importer.textureType = TextureImporterType.Sprite; importer.SaveAndReimport();
                unit.icon = AssetDatabase.LoadAssetAtPath<Sprite>(root + "/icon.png");
                var catalog = Asset<UnitCatalog>("Catalog"); var serial = new SerializedObject(catalog); serial.FindProperty("units").arraySize = 1; serial.FindProperty("units").GetArrayElementAtIndex(0).objectReferenceValue = unit; serial.ApplyModifiedPropertiesWithoutUndo();
                foreach (var a in new Object[] { buff, skill, unit, growth, catalog }) { EditorUtility.SetDirty(a); AssetDatabase.SaveAssetIfDirty(a); }
                string workbook = Folder + "/verification.xlsx";
                var exportMessages = ConfigImportService.Export(workbook, root, false, adapter);
                Check(!exportMessages.Any(m => m.Kind == ImportMessageKind.Error), "export valid fixture: " + Errors(exportMessages));
                var messages = new List<ImportMessage>(); var data = adapter.Parse(workbook, messages);
                Check(messages.Count == 0 && data.Rows("Units").Count == 1, "xlsx read preserves rows and types");
                var p = ConfigImportService.Preview(workbook, root, adapter);
                Check(p.IsValid && p.Updates.Count == 0, "unchanged roundtrip preserves references, keys and list order: " + Errors(p.Messages));
                using (var officeHandle = new FileStream(workbook, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
                {
                    bool oldReadFails = false;
                    try { File.ReadAllBytes(workbook); } catch (IOException) { oldReadFails = true; }
                    Check(oldReadFails, "reproduced Office-style read/write handle sharing violation with old reader");
                    var openPreview = ConfigImportService.Preview(workbook, root, adapter);
                    Check(openPreview.IsValid && openPreview.Updates.Count == 0, "preview works while workbook is open for editing");
                    ConfigImportService.Apply(openPreview);
                    Check(true, "apply freshness check works while workbook is open for editing");
                }
                using (var exclusiveHandle = new FileStream(workbook, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    var lockedPreview = ConfigImportService.Preview(workbook, root, adapter);
                    Check(!lockedPreview.IsValid && lockedPreview.Messages.Any(m => m.Detail.Contains("独占锁定")), "exclusive lock reports actionable Chinese guidance");
                }
                string unitPath = AssetDatabase.GetAssetPath(unit), guid = AssetDatabase.AssetPathToGUID(unitPath), originalHash = ConfigImportService.FileHash(unitPath);
                ConfigImportService.Apply(p);
                Check(originalHash == ConfigImportService.FileHash(unitPath), "no-op import leaves original file bytes unchanged");
                var prefab = unit.prefab; var icon = unit.icon;
                data.Rows("Units")[0].Values["attack"] = 29;
                data.Rows("SkillEffects")[0].Values["value"] = 1.7f;
                Write();
                using (var officeHandle = new FileStream(workbook, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
                {
                    Apply();
                    Check(ConfigImportService.FileHash(createdBackups.Last() + "/Source.xlsx") == ConfigImportService.Hash(ConfigImportService.ReadWorkbookSnapshot(workbook)), "write and backup use validated snapshot while workbook is open");
                }
                Check(unit.BaseStats.Attack == 29 && skill.effectConfigs[0].value == 1.7f, "primitive update applied");
                var runtimeFactory = new BattleUnitFactory();
                try { var battleUnit = runtimeFactory.Create(unit, Faction.OurSide, 0, 0); Check(battleUnit.attack == 29 && battleUnit.skills[0].effectConfigs[0].value == 1.7f, "battle factory reads imported SO values"); }
                finally { runtimeFactory.ReleaseAll(); }
                Check(unit.prefab == prefab && unit.icon == icon && unit.growth == growth && unit.skills[0] == skill && unit.talentAction == skill && AssetDatabase.AssetPathToGUID(unitPath) == guid,
                    "prefab/icon/growth/skill/talent/GUID preserved");
                Check(ConfigImportService.Preview(workbook, root, adapter).Updates.Count == 0, "second identical import has no writes");

                // New same-workbook dependency chain: buff -> skill -> ally + enemy; only ally appended.
                ConfigRow Copy(ConfigRow source)
                { var r = new ConfigRow { Sheet = source.Sheet, Number = 8 }; foreach (var v in source.Values) r.Values[v.Key] = v.Value; return r; }
                var newBuff = Copy(data.Rows("Buffs")[0]); newBuff.Values["buffId"] = 991002; data.MutableRows("Buffs").Add(newBuff);
                var newSkill = Copy(data.Rows("Skills")[0]); newSkill.Values["skillId"] = 991002; data.MutableRows("Skills").Add(newSkill);
                var newEffect = Copy(data.Rows("SkillEffects")[1]); newEffect.Values["skillId"] = 991002; newEffect.Values["effectKey"] = "new.buff"; newEffect.Values["order"] = 0; newEffect.Values["buffId"] = 991002; data.MutableRows("SkillEffects").Add(newEffect);
                var newAlly = Copy(data.Rows("Units")[0]); newAlly.Values["unitId"] = "test-new-ally"; newAlly.Values["growthId"] = ""; data.MutableRows("Units").Add(newAlly);
                var newEnemy = Copy(newAlly); newEnemy.Values["unitId"] = "test-new-enemy"; newEnemy.Values["unitType"] = UnitTableKind.Enemy; data.MutableRows("Units").Add(newEnemy);
                foreach (var r in new[] { newAlly, newEnemy })
                { var link = Copy(data.Rows("UnitSkills").First(l => l.Get<UnitSkillRole>("role") == UnitSkillRole.Normal)); link.Values["unitId"] = r.Id; link.Values["skillId"] = 991002; data.MutableRows("UnitSkills").Add(link); }
                Write(); Apply();
                var idx = new ConfigAssetIndex(root, new());
                Check(idx.Units["test-new-ally"].skills[0] == idx.Skills[991002] && idx.Skills[991002].effectConfigs[0].buff == idx.Buffs[991002] && idx.Skills[991002].effectConfigs[0].buffId == 991002, "new cross-table references resolve in two passes");
                Check(catalog.Units.Count == 2 && catalog.Units[0] == unit && catalog.Units[1].id == "test-new-ally" && !catalog.Units.Any(u => u.id == "test-new-enemy"), "catalog appends ally, preserves order, excludes enemy");
                Check(idx.Units["test-new-ally"].prefab == null && idx.Units["test-new-ally"].growth == null, "new unit does not copy other character bindings");

                // Swap physical rows and columns while keeping explicit order; include hidden records and shared strings.
                using (var doc = SpreadsheetDocument.Open(workbook, true))
                {
                    var ws = Sheet(doc, "SkillEffects"); var sd = ws.GetFirstChild<SheetData>();
                    var a = sd.Elements<Row>().First(r => r.RowIndex == 3); var b = sd.Elements<Row>().First(r => r.RowIndex == 4);
                    var av = a.Elements<Cell>().Select(c => (Cell)c.CloneNode(true)).ToArray(); var bv = b.Elements<Cell>().Select(c => (Cell)c.CloneNode(true)).ToArray();
                    a.RemoveAllChildren(); b.RemoveAllChildren();
                    foreach (var c in bv) { c.CellReference = ConfigWorkbook.Letters(c.CellReference) + "3"; a.Append(c); }
                    foreach (var c in av) { c.CellReference = ConfigWorkbook.Letters(c.CellReference) + "4"; b.Append(c); }
                    a.Hidden = true;
                    foreach (var row in sd.Elements<Row>())
                    {
                        var first = row.Elements<Cell>().First(c => ConfigWorkbook.Letters(c.CellReference) == "A");
                        var second = row.Elements<Cell>().First(c => ConfigWorkbook.Letters(c.CellReference) == "B");
                        first.CellReference = "B" + row.RowIndex; second.CellReference = "A" + row.RowIndex;
                        var sorted = row.Elements<Cell>().OrderBy(c => ((string)c.CellReference).Length).ThenBy(c => (string)c.CellReference, StringComparer.Ordinal).ToArray();
                        foreach (var cell in sorted) cell.Remove(); row.Append(sorted);
                    }
                    var shared = doc.WorkbookPart.AddNewPart<SharedStringTablePart>(); shared.SharedStringTable = new SharedStringTable(new SharedStringItem(new Text("测试角色")));
                    var nameCell = Sheet(doc, "Units").Descendants<Cell>().First(c => c.CellReference == "B3"); nameCell.RemoveAllChildren(); nameCell.DataType = CellValues.SharedString; nameCell.CellValue = new CellValue("0");
                    doc.Save();
                }
                p = ConfigImportService.Preview(workbook, root, adapter);
                Check(p.IsValid && p.Updates.Count == 0, "reordered columns/rows, hidden rows and shared strings preserve meaning: " + Errors(p.Messages));

                // Invalid inputs fail before any asset write, with exact coordinates.
                void InvalidCell(string sheet, string cell, Action<Cell> change, string expected)
                {
                    Write(); using (var doc = SpreadsheetDocument.Open(workbook, true)) { change(Sheet(doc, sheet).Descendants<Cell>().First(c => c.CellReference == cell)); doc.Save(); }
                    var invalid = ConfigImportService.Preview(workbook, root, adapter);
                    Check(!invalid.IsValid && invalid.Messages.Any(m => m.Kind == ImportMessageKind.Error && m.Sheet == sheet && m.Row == int.Parse(new string(cell.Where(char.IsDigit).ToArray())) && m.Column == ConfigWorkbook.Letters(cell)), expected + " locates " + sheet + "!" + cell);
                    string before = ConfigImportService.FileHash(unitPath); bool refused = false;
                    try { ConfigImportService.Apply(invalid); } catch (InvalidOperationException) { refused = true; }
                    Check(refused && before == ConfigImportService.FileHash(unitPath), expected + " rejects all writes");
                }
                void Text(Cell c, string v) { c.RemoveAllChildren(); c.DataType = CellValues.InlineString; c.InlineString = new InlineString(new Text(v)); }
                InvalidCell("Units", "G3", c => { c.CellFormula = new CellFormula("1+2"); c.CellValue = new CellValue("3"); }, "formula");
                InvalidCell("Units", "D3", c => Text(c, "NotAnElement"), "unknown enum");
                InvalidCell("Units", "G3", c => Text(c, "NaN"), "nonfinite number");
                InvalidCell("Units", "A4", c => Text(c, "test-unit"), "duplicate ID");
                InvalidCell("SkillEffects", "L4", c => Text(c, "999999"), "missing buff");
                InvalidCell("SkillEffects", "C4", c => Text(c, "8"), "noncontiguous order");
                InvalidCell("Units", "C3", c => Text(c, "Enemy"), "existing category conversion");
                InvalidCell("Units", "G3", c => Text(c, "-1"), "negative base stat");
                InvalidCell("Units", "A3", c => Text(c, ""), "empty ID");
                InvalidCell("SkillEffects", "F3", c => Text(c, "Infinity"), "nonfinite floating point");
                InvalidCell("UnitSkills", "B3", c => Text(c, "999999"), "missing skill reference");
                InvalidCell("Buffs", "A4", c => Text(c, "991001"), "duplicate Buff ID");
                var parent = data.MutableRows("Skills")[0]; data.MutableRows("Skills").RemoveAt(0); Write();
                Check(!ConfigImportService.Preview(workbook, root, adapter).IsValid, "child effects cannot update an omitted skill parent");
                data.MutableRows("Skills").Insert(0, parent);
                var duplicateTalent = Copy(data.Rows("UnitSkills").First(r => r.Get<UnitSkillRole>("role") == UnitSkillRole.TalentAction));
                duplicateTalent.Values["order"] = 1; data.MutableRows("UnitSkills").Add(duplicateTalent); Write();
                Check(!ConfigImportService.Preview(workbook, root, adapter).IsValid, "multiple talent action links rejected");
                data.MutableRows("UnitSkills").Remove(duplicateTalent);
                Write(); using (var doc = SpreadsheetDocument.Open(workbook, true)) { Sheet(doc, "Units").InsertAfter(new MergeCells(new MergeCell { Reference = "A3:B3" }), Sheet(doc, "Units").GetFirstChild<SheetData>()); doc.Save(); }
                Check(!ConfigImportService.Preview(workbook, root, adapter).IsValid, "merged business cells rejected");
                Write(); using (var doc = SpreadsheetDocument.Open(workbook, true)) { var s = doc.WorkbookPart.Workbook.Sheets.Elements<Sheet>().First(s => s.Name == "UnitSkills"); doc.WorkbookPart.DeletePart(s.Id); s.Remove(); doc.Save(); }
                Check(!ConfigImportService.Preview(workbook, root, adapter).IsValid, "missing required sheet is not an empty list");
                data.Rows("SkillEffects")[0].Values["effectKey"] = "renamed.hit"; Write();
                Check(ConfigImportService.Preview(workbook, root, adapter).Messages.Any(m => m.Kind == ImportMessageKind.Error && m.Sheet == "SkillEffectGrowth"), "renamed effect key blocks dangling growth references");
                data.Rows("SkillEffects")[0].Values["effectKey"] = "test.hit";
                data.Rows("SkillEffects")[0].Values["order"] = 1; data.Rows("SkillEffects")[1].Values["order"] = 0; Write();
                Check(ConfigImportService.Preview(workbook, root, adapter).Messages.Any(m => m.Kind == ImportMessageKind.Warning && m.Detail.Contains("BattleImpactMarker")), "effect order warns about Timeline index binding");
                data.Rows("SkillEffects")[0].Values["order"] = 0; data.Rows("SkillEffects")[1].Values["order"] = 1;
                var removed = data.MutableRows("SkillEffects")[1]; data.MutableRows("SkillEffects").RemoveAt(1); Write();
                p = ConfigImportService.Preview(workbook, root, adapter);
                Check(p.IsValid && p.Messages.Any(m => m.Kind == ImportMessageKind.Modified && m.Detail.Contains("effectConfigs")), "explicit effect deletion is visible");
                data.MutableRows("SkillEffects").Insert(1, removed);
                var saveEnemy = data.MutableRows("Units").Last(); data.MutableRows("Units").Remove(saveEnemy);
                var saveLinks = data.MutableRows("UnitSkills").Where(r => r.Id == saveEnemy.Id).ToArray(); foreach (var r in saveLinks) data.MutableRows("UnitSkills").Remove(r);
                Write(); p = ConfigImportService.Preview(workbook, root, adapter);
                Check(p.IsValid && p.Updates.Count == 0 && idx.Units[saveEnemy.Id] != null, "omitted parent does not delete or modify existing SO");
                data.MutableRows("Units").Add(saveEnemy); data.MutableRows("UnitSkills").AddRange(saveLinks);

                Write(); p = ConfigImportService.Preview(workbook, root, adapter);
                data.Rows("Units")[0].Values["attack"] = 30; Write();
                CheckThrows(() => ConfigImportService.Apply(p), "changed workbook invalidates preview");
                p = ConfigImportService.Preview(workbook, root, adapter); UnitBaseStatsAuthoring.Set(unit, "attack", 31f); EditorUtility.SetDirty(unit);
                CheckThrows(() => ConfigImportService.Apply(p), "asset mutation invalidates preview");
                Check(!ConfigImportService.Preview(workbook, root, adapter).IsValid, "affected dirty asset blocks preview/application");
                UnitBaseStatsAuthoring.Set(unit, "attack", 29f); EditorUtility.SetDirty(unit); AssetDatabase.SaveAssetIfDirty(unit);

                // Simulated write failure after existing asset write + new asset creation, then recover exact bytes/meta.
                newBuff = Copy(data.Rows("Buffs")[0]); newBuff.Values["buffId"] = 991003; data.MutableRows("Buffs").Add(newBuff);
                data.Rows("Buffs")[0].Values["value"] = -0.4f; Write();
                string buffPath = AssetDatabase.GetAssetPath(buff), bHash = ConfigImportService.FileHash(buffPath), bMeta = ConfigImportService.FileHash(buffPath + ".meta");
                string unaffected = ConfigImportService.FileHash(AssetDatabase.GetAssetPath(skill));
                p = ConfigImportService.Preview(workbook, root, adapter); Check(p.IsValid, "rollback fixture validates");
                string[] newPaths = p.Updates.Where(u => u.IsNew).Select(u => u.Path).ToArray();
                ConfigImportService.AfterWriteForVerification = n => { if (n == 1) throw new IOException("simulated write failure"); };
                CheckThrows(() => ConfigImportService.Apply(p), "simulated failure triggers rollback");
                ConfigImportService.AfterWriteForVerification = null;
                Check(ConfigImportService.FileHash(buffPath) == bHash && ConfigImportService.FileHash(buffPath + ".meta") == bMeta && ConfigImportService.FileHash(AssetDatabase.GetAssetPath(skill)) == unaffected && newPaths.All(pth => !File.Exists(pth) && !File.Exists(pth + ".meta")), "rollback restores exact bytes/GUID and deletes only new assets");
                Check(ConfigImportService.FindPendingBackup() == null, "rollback clears interrupted transaction");
                // Simulate process interruption by marking a completed transaction Prepared, then recover from disk.
                data.MutableRows("Buffs").Remove(newBuff); data.Rows("Buffs")[0].Values["value"] = -0.2f; Write();
                p = ConfigImportService.Preview(workbook, root, adapter);
                string interrupted = ConfigImportService.Apply(p);
                string manifestPath = interrupted + "/manifest.json";
                File.WriteAllText(manifestPath, File.ReadAllText(manifestPath).Replace("\"Completed\"", "\"Prepared\""));
                Check(Path.GetFullPath(ConfigImportService.FindPendingBackup()) == Path.GetFullPath(interrupted), "pending transaction discovered from manifest after interruption");
                CheckThrows(() => ConfigImportService.Apply(ConfigImportService.Preview(workbook, root, adapter)), "pending transaction blocks later imports");
                ConfigImportService.Restore(interrupted);
                Check(ConfigImportService.FindPendingBackup() == null && ConfigImportService.FileHash(unitPath) == ConfigImportService.FileHash(interrupted + "/files/" + unitPath), "explicit recovery restores interrupted update");
                CheckThrows(() => ConfigImportService.ValidateEditorState(true, false, false), "Play Mode gate rejects application");
                CheckThrows(() => ConfigImportService.ValidateEditorState(false, true, false), "compilation gate rejects application");
                CheckThrows(() => ConfigImportService.ValidateEditorState(false, false, true), "asset refresh gate rejects application");
                foreach (string dll in Directory.GetFiles("Assets/Editor/BattleConfigImport/ThirdParty/Managed", "*.dll"))
                {
                    var plugin = (PluginImporter)AssetImporter.GetAtPath(dll);
                    Check(plugin.GetCompatibleWithEditor() && !plugin.GetCompatibleWithAnyPlatform() && !plugin.GetCompatibleWithPlatform(BuildTarget.StandaloneWindows64), "Editor-only dependency: " + Path.GetFileName(dll));
                }
                using (var doc = SpreadsheetDocument.Open(workbook, false))
                {
                    var errors = new DocumentFormat.OpenXml.Validation.OpenXmlValidator().Validate(doc).Take(5).ToArray();
                    Check(errors.Length == 0, "generated xlsx passes OpenXML schema validation: " + string.Join(";", errors.Select(e => e.Description)));
                }
                VerifyAuthoring(root, adapter, workbook, unit, skill);

                void Write() => ConfigWorkbook.Write(workbook, data, adapter.Schemas);
                void Apply()
                {
                    var preview = ConfigImportService.Preview(workbook, root, adapter);
                    Check(preview.IsValid, "fixture preview valid: " + Errors(preview.Messages));
                    createdBackups.Add(ConfigImportService.Apply(preview));
                }
                WriteResults(null);
            }
            catch (Exception e) { WriteResults(e.ToString()); throw; }
            finally
            {
                ConfigImportService.AfterWriteForVerification = null;
                AssetDatabase.DeleteAsset(root);
                // Keep test backup evidence outside the user's operational recovery list.
                if (Directory.Exists(ConfigImportService.BackupRoot))
                    foreach (var file in Directory.GetFiles(ConfigImportService.BackupRoot, "manifest.json", SearchOption.AllDirectories))
                    {
                        string content = File.ReadAllText(file);
                        if (!content.Contains(root) || content.Contains("\"Prepared\"")) continue;
                        string directory = Path.GetDirectoryName(file);
                        Directory.CreateDirectory(Folder + "/TestBackups");
                        Directory.Move(directory, Folder + "/TestBackups/" + Path.GetFileName(directory));
                    }
            }
            Debug.Log("Battle Config Excel verification passed: " + checks.Count);
        }
        private static Worksheet Sheet(SpreadsheetDocument doc, string name) => ((WorksheetPart)doc.WorkbookPart.GetPartById(doc.WorkbookPart.Workbook.Sheets.Elements<Sheet>().Single(s => s.Name == name).Id)).Worksheet;
        private static void VerifyAuthoring(string root, BattleConfigTableAdapter adapter, string workbook, UnitConfig unit, SkillConfig skill)
        {
            var m = new List<ImportMessage>(); var index = new ConfigAssetIndex(root, m);
            var data = adapter.Export(index, m);
            string test = Folder + "/authoring.xlsx";
            ConfigWorkbook.Write(test, data, adapter.Schemas);
            var parsed = adapter.Parse(test, new());
            Check(parsed.Metadata["projectId"] == PlayerSettings.productGUID.ToString() && parsed.Metadata.ContainsKey("workbookId"), "metadata records project identity and workbook identity");
            var effectRows = data.Rows("SkillEffects").Where(r => r.Get<int>("skillId") == skill.id).OrderBy(r => r.Get<int>("order")).ToArray();
            effectRows[0].Values["value"] = 9f;
            var diff = ConfigAuthoringSupport.EffectDiff(skill.effectConfigs, effectRows, index).ToArray();
            Check(diff.Length == 1 && diff[0].Contains("[修改]") && diff[0].Contains(".value"), "one changed effect field produces one readable field diff");
            effectRows[0].Values["value"] = skill.effectConfigs[0].value;
            diff = ConfigAuthoringSupport.EffectDiff(skill.effectConfigs, effectRows.Reverse().ToArray(), index).ToArray();
            Check(diff.Length == 2 && diff.All(d => d.StartsWith("[移动]")), "effect reordering is reported as moves rather than full-list replacement");
            diff = ConfigAuthoringSupport.EffectDiff(skill.effectConfigs, effectRows.Take(1).ToArray(), index).ToArray();
            Check(diff.Length == 1 && diff[0].StartsWith("[删除]"), "effect deletion names exact removed key");

            // Clear a fixture with no growth owner, without actually applying the deletion.
            var emptyTarget = data.Rows("Skills").First(s => s.Get<int>("skillId") != skill.id);
            var removed = data.MutableRows("SkillEffects").Where(e => e.Get<int>("skillId") == emptyTarget.Get<int>("skillId")).ToArray();
            foreach (var e in removed) data.MutableRows("SkillEffects").Remove(e);
            ConfigWorkbook.Write(test, data, adapter.Schemas);
            var plan = ConfigImportService.Preview(test, root, adapter);
            Check(plan.IsValid && plan.RequiresClearConfirmation, "empty existing effect list requires explicit clear confirmation");
            string unchanged = ConfigImportService.FileHash(AssetDatabase.GetAssetPath(skill));
            CheckThrows(() => ConfigImportService.Apply(plan), "service prevents unconfirmed clearing, not only the UI");
            Check(ConfigImportService.FileHash(AssetDatabase.GetAssetPath(skill)) == unchanged, "clear rejection writes no business assets");
            data.MutableRows("SkillEffects").AddRange(removed);

            var options = new ConfigBatchRequest { Recipe = ConfigRecipe.CopyUnit, SourceId = unit.id, Count = 2, UnitPrefix = "copy-", StartSkillId = 992000, NamePrefix = "复制角色", Kind = UnitTableKind.Enemy };
            var batch = ConfigBatchAuthoring.Build(data, index, options);
            Check(batch.Rows("Units").Count == data.Rows("Units").Count + 2 && batch.Rows("Skills").Count == data.Rows("Skills").Count + 2, "batch clones units and deduplicates shared normal/talent skill within each unit");
            Check(batch.Rows("UnitSkills").Where(r => r.Id == "copy-1").Select(r => r.Get<int>("skillId")).Distinct().Single() == 992000, "batch normal/talent relationships resolve to same cloned skill");
            Check(batch.Rows("SkillEffects").Where(r => r.Get<int>("skillId") == 992000).All(r => r.Get<string>("effectKey").StartsWith("skill.992000.")), "batch regenerates explicit independent effect keys");
            Check(data.Rows("Units").All(r => r.Id != "copy-1"), "batch generation does not mutate source data");
            options.StartSkillId = skill.id;
            CheckThrows(() => ConfigBatchAuthoring.Build(data, index, options), "batch rejects occupied skill ID without silent renumbering");
            options.StartSkillId = 992000; options.CloneSkills = false;
            var shared = ConfigBatchAuthoring.Build(data, index, options);
            Check(shared.Rows("Skills").Count == data.Rows("Skills").Count && shared.Rows("UnitSkills").Where(r => r.Id == "copy-1").All(r => r.Get<int>("skillId") == skill.id), "reuse-skills option shares exact original skill IDs");
            options.Recipe = ConfigRecipe.CopySkill; options.SourceId = skill.id.ToString();
            Check(ConfigBatchAuthoring.Build(data, index, options).Rows("Skills").Count == data.Rows("Skills").Count + 2, "standalone skill batch copies effects");
            foreach (ConfigRecipe recipe in new[] { ConfigRecipe.SingleDamage, ConfigRecipe.AreaDamage, ConfigRecipe.AllyHeal, ConfigRecipe.NewUnit })
            {
                options.Recipe = recipe; options.Count = 1;
                var template = ConfigBatchAuthoring.Build(data, index, options);
                string file = Folder + "/recipe-" + recipe + ".xlsx";
                ConfigWorkbook.Write(file, template, adapter.Schemas);
                Check(ConfigImportService.Preview(file, root, adapter).IsValid, "template validates actual cells and dependencies: " + recipe);
            }
            ConfigWorkbook.Write(test, batch, adapter.Schemas);
            ConfigWorkbookReferences.Enrich(test, batch, index);
            using (var doc = SpreadsheetDocument.Open(test, true))
            {
                Check(Sheet(doc, "RefSkills").Descendants<Cell>().Any(c => ConfigWorkbookReferences.CellText(doc.WorkbookPart, c) == "992000"), "reference helpers include newly authored workbook IDs");
                Check(doc.WorkbookPart.Workbook.GetFirstChild<DefinedNames>().Elements<DefinedName>().Count(n => n.Name == "BattleSkillsIds") == 1, "reference named ranges are unique after repeated enrichment");
                Check(Sheet(doc, "UnitSkills").Descendants<DataValidation>().Any(v => v.Formula1?.Text == "BattleSkillsIds" && v.ShowErrorMessage == false), "foreign-key dropdown permits newly typed IDs");
                ConfigWorkbook.AddSheet(doc.WorkbookPart, "UserCalculations", new[] { "value" }, new[] { "额外表" }, new[] { new object[] { 123 } }, null, false);
                doc.Save();
            }
            string businessXml, extraXml;
            using (var doc = SpreadsheetDocument.Open(test, false)) { businessXml = Sheet(doc, "Units").OuterXml; extraXml = Sheet(doc, "UserCalculations").OuterXml; }
            var refresh = ConfigWorkbookReferences.Refresh(test, root);
            Check(!refresh.Any(x => x.Kind == ImportMessageKind.Error), "reference refresh succeeds");
            using (var doc = SpreadsheetDocument.Open(test, false))
            {
                Check(Sheet(doc, "Units").OuterXml == businessXml && Sheet(doc, "UserCalculations").OuterXml == extraXml, "reference refresh preserves business cells and extra calculation sheets");
                var errors = new DocumentFormat.OpenXml.Validation.OpenXmlValidator().Validate(doc).ToArray();
                Check(errors.Length == 0, "enriched workbook passes OpenXML validation: " + string.Join(";", errors.Take(3).Select(e => e.Description)));
            }
            string destination = Folder + "/publish.xlsx";
            File.WriteAllText(destination, "original workbook sentinel"); string original = ConfigImportService.FileHash(destination);
            using (var handle = new FileStream(destination, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            {
                bool locked = false; try { ConfigWorkbookFiles.Publish(test, destination, original); } catch (IOException e) { locked = e.Message.Contains("Excel/WPS"); }
                Check(locked, "export refuses Office-open destination with actionable message");
            }
            Check(ConfigImportService.FileHash(destination) == original, "failed export leaves original bytes intact");
            CheckThrows(() => ConfigWorkbookFiles.Publish(test, destination, "stale"), "changed destination invalidates export generation");
            ConfigWorkbookFiles.Publish(test, destination, original);
            Check(Directory.GetFiles(Folder + "/.BattleWorkbookBackups", "publish-*.xlsx").Any(f => ConfigImportService.FileHash(f) == original), "successful overwrite retains exact old workbook backup");
            Check(ConfigImportService.FileHash(test) == ConfigImportService.FileHash(destination), "safe publication writes complete validated output");
            var warningData = adapter.Export(index, new());
            warningData.Rows("Skills").First(r => r.Get<int>("skillId") == skill.id).Values["energyCost"] = unit.BaseStats.MaxEnergy + 1;
            var buffEffect = warningData.Rows("SkillEffects").First(r => r.Get<EffectType>("effectType") == EffectType.ApplyBuff);
            buffEffect.Values["stack"] = 999;
            var warnings = new List<ImportMessage>(); adapter.Validate(warningData, index, warnings);
            Check(warnings.Any(w => w.Detail.Contains("能量消耗")) && warnings.Any(w => w.Detail.Contains("超过Buff上限")) && !warnings.Any(w => w.Kind == ImportMessageKind.Error), "domain suspicious combinations warn without banning legal custom rules");
            Check(Directory.GetFiles(ConfigImportService.BackupRoot + "/History", "*.json").Length > 0, "successful and no-op imports create durable history records");
        }
        private static string Errors(IEnumerable<ImportMessage> messages) => string.Join(" | ", messages.Where(m => m.Kind == ImportMessageKind.Error));
        private static void Check(bool condition, string label) { if (!condition) throw new InvalidOperationException("FAILED: " + label); checks.Add(label); }
        private static void CheckThrows(Action action, string label) { bool threw = false; try { action(); } catch (InvalidOperationException) { threw = true; } Check(threw, label); }
        private static void WriteResults(string error) => File.WriteAllText(Folder + "/results.json", Newtonsoft.Json.JsonConvert.SerializeObject(new { passed = error == null, error, checkCount = checks.Count, checks, completedAt = DateTimeOffset.Now }, Newtonsoft.Json.Formatting.Indented));
        private static void RunRegression()
        {
            string battleError = null, characterError = null;
            var remaining = new Dictionary<string, string>();
            try { global::BattleActionVerification.Run(); }
            catch (Exception e)
            {
                battleError = e.ToString();
                // Do not alter or silently waive the original suite. If its UI check stops execution,
                // report that failure and exercise later independent sections with separate results.
                if (e.StackTrace != null && e.StackTrace.Contains("ActionTimelineResourceChecks"))
                    foreach (string name in new[] { "ServiceChecks", "ResourceChecks", "EnemyDecisionChecks", "ResolutionChecks", "WaveChecks", "PersistenceChecks" })
                        try
                        {
                            typeof(global::BattleActionVerification).GetMethod(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null, null);
                            remaining[name] = "Passed";
                        }
                        catch (Exception sectionError) { remaining[name] = (sectionError.InnerException ?? sectionError).ToString(); }
            }
            try { global::CharacterProgressionVerification.Run(); } catch (Exception e) { characterError = e.ToString(); }
            File.WriteAllText(Folder + "/regression.json", Newtonsoft.Json.JsonConvert.SerializeObject(new
            { battlePassed = battleError == null, battleError, remainingSections = remaining, characterPassed = characterError == null, characterError, completedAt = DateTimeOffset.Now }, Newtonsoft.Json.Formatting.Indented));
        }
        private static void ExportProject()
        {
            var m = ConfigImportService.Export(ConfigImportService.DefaultWorkbook, "Assets/Config", false);
            File.WriteAllText(Folder + "/export-report.txt", string.Join("\n", m));
            if (m.Any(x => x.Kind == ImportMessageKind.Error)) throw new InvalidOperationException(Errors(m));
            m = ConfigImportService.Export("ConfigTables/BattleConfigTemplate.xlsx", "Assets/Config", true);
            if (m.Any(x => x.Kind == ImportMessageKind.Error)) throw new InvalidOperationException(Errors(m));
            var p = ConfigImportService.Preview(ConfigImportService.DefaultWorkbook, "Assets/Config");
            File.WriteAllText(Folder + "/project-preview.txt", "Valid=" + p.IsValid + "; updates=" + p.Updates.Count + "\n" + string.Join("\n", p.Messages));
            if (!p.IsValid) throw new InvalidOperationException(Errors(p.Messages));
            // Read real data through the game's existing construction and stat calculation code in regression.
            var runtime = AssetDatabase.LoadAssetAtPath<PlayerRuntimeConfig>("Assets/Resources/Bootstrap/PlayerRuntimeConfig.asset");
            if (runtime == null || runtime.units == null || runtime.units.Units.Any(u => u == null || u.skills.Any(s => s == null)))
                throw new InvalidOperationException("实际角色目录或技能引用读取失败。");
            var inventory = new Game.Inventory.InventoryService(runtime.items, false);
            var lines = new List<string>();
            foreach (var unit in runtime.units.Units)
            {
                var battle = new BattleCharacterSnapshot(unit, new CharacterProgress { unitId = unit.id }, inventory).CreateUnit(0, 0);
                try
                {
                    if (battle.maxHealth <= 0 || battle.speed <= 0 || battle.skills.Count != unit.skills.Count || battle.skills.Any(s => s == null))
                        throw new InvalidOperationException("实际战斗单位构造失败：" + unit.id);
                    lines.Add($"{unit.id}: HP={battle.maxHealth}, ATK={battle.attack}, SPD={battle.speed}, Skills={battle.skills.Count}");
                }
                finally { foreach (var skill in battle.skills) Object.DestroyImmediate(skill); }
            }
            var factory = new BattleUnitFactory();
            try
            {
                var index = new ConfigAssetIndex("Assets/Config", new());
                foreach (var enemy in index.Units.Values.Where(u => index.Kinds[u.id] == UnitTableKind.Enemy))
                {
                    var battle = factory.Create(enemy, Faction.Enemy, 0, 0);
                    if (battle == null || battle.attack != Mathf.RoundToInt(enemy.BaseStats.Attack) || battle.maxHealth != Mathf.RoundToInt(enemy.BaseStats.Health)) throw new InvalidOperationException("敌人配置读取失败：" + enemy.id);
                    lines.Add($"Enemy {enemy.id}: HP={battle.maxHealth}, ATK={battle.attack}, Skills={battle.skills.Count}");
                }
            }
            finally { factory.ReleaseAll(); }
            File.WriteAllText(Folder + "/project-read.txt", "PlayerRuntimeConfig → BattleCharacterSnapshot / BattleUnitFactory 读取成功\n" + string.Join("\n", lines) + "\n" + DateTimeOffset.Now);
        }
        private static void ProjectRoundtrip()
        {
            var plan = ConfigImportService.Preview(ConfigImportService.DefaultWorkbook, "Assets/Config");
            if (!plan.IsValid) throw new InvalidOperationException(Errors(plan.Messages));
            var paths = plan.Index.Watched.Where(o => o != null).Select(AssetDatabase.GetAssetPath).Distinct().ToArray();
            var before = paths.ToDictionary(p => p, p => ConfigImportService.FileHash(p) + ConfigImportService.FileHash(p + ".meta"));
            string backup = null;
            bool repeatedNoChanges = false, restored = false;
            try
            {
                backup = ConfigImportService.Apply(plan);
                var second = ConfigImportService.Preview(ConfigImportService.DefaultWorkbook, "Assets/Config");
                repeatedNoChanges = second.IsValid && second.Updates.Count == 0;
                if (!repeatedNoChanges) throw new InvalidOperationException("实际配置二次导入并非无变化。");
                var factory = new BattleUnitFactory();
                try
                {
                    foreach (var u in plan.Index.Units.Values)
                    {
                        var battle = factory.Create(u, plan.Index.Kinds[u.id] == UnitTableKind.Ally ? Faction.OurSide : Faction.Enemy, 0, 0);
                        if (battle == null || battle.attack != Mathf.RoundToInt(u.BaseStats.Attack) || battle.skills.Count != u.skills.Count) throw new InvalidOperationException("导入后战斗配置读取失败：" + u.id);
                    }
                }
                finally { factory.ReleaseAll(); }
            }
            finally
            {
                if (backup != null && plan.Updates.Count > 0) ConfigImportService.Restore(backup);
                restored = before.All(p => p.Value == ConfigImportService.FileHash(p.Key) + ConfigImportService.FileHash(p.Key + ".meta"));
                File.WriteAllText(Folder + "/project-roundtrip.json", Newtonsoft.Json.JsonConvert.SerializeObject(new
                { repeatedNoChanges, restoredExactOriginalFiles = restored, changedAssets = plan.Updates.Count, watchedAssets = paths.Length, backup, completedAt = DateTimeOffset.Now }, Newtonsoft.Json.Formatting.Indented));
            }
            if (!restored) throw new InvalidOperationException("实际配置恢复未能通过文件/Meta一致性检查。");
        }
    }
}
