using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Battle.Editor.ConfigImport
{
    internal enum ConfigRecipe { CopyUnit, CopySkill, SingleDamage, AreaDamage, AllyHeal, NewUnit }

    internal sealed class ConfigBatchRequest
    {
        internal ConfigRecipe Recipe;
        internal string SourceId, UnitPrefix = "NewUnit_", NamePrefix = "新配置";
        internal int Count = 1, StartUnitNumber = 1, StartSkillId = 10001;
        internal UnitTableKind Kind = UnitTableKind.Enemy;
        internal bool CloneSkills = true;
    }

    internal static class ConfigBatchAuthoring
    {
        // Builds an independent data copy. Neither the input workbook nor SOs are modified.
        internal static ConfigTableData Build(ConfigTableData source, ConfigAssetIndex index, ConfigBatchRequest request)
        {
            if (request.Count < 1 || request.Count > 100) throw new InvalidOperationException("批量数量必须为1—100。");
            if (string.IsNullOrWhiteSpace(request.NamePrefix) || request.NamePrefix != request.NamePrefix.Trim()) throw new InvalidOperationException("名称前缀不能为空或包含首尾空格。");
            var result = new ConfigTableData();
            foreach (var p in source.Metadata) result.Metadata[p.Key] = p.Value;
            result.Metadata["parentWorkbookId"] = source.Metadata.TryGetValue("workbookId", out var parent) ? parent : "SO";
            result.Metadata["workbookId"] = Guid.NewGuid().ToString("N");
            result.Metadata["authoringRecipe"] = request.Recipe.ToString();
            foreach (var schema in BattleTableSchema.All) foreach (var row in source.Rows(schema.Name)) result.Add(Copy(row));
            BattleTableSchema.CompleteLegacyUnitColumns(result, index);
            var unitIds = new HashSet<string>(index.Units.Keys.Concat(source.Rows("Units").Select(r => r.Id)), StringComparer.Ordinal);
            var skillIds = new HashSet<int>(index.Skills.Keys.Concat(source.Rows("Skills").Select(r => r.Get<int>("skillId"))));
            var diagnostics = new List<ImportMessage>();
            // The full SO export supplies sources referenced by partial workbooks.
            var assets = ConfigImportService.Adapter.Export(index, diagnostics);
            if (diagnostics.Any(m => m.Kind == ImportMessageKind.Error)) throw new InvalidOperationException(string.Join("\n", diagnostics.Where(m => m.Kind == ImportMessageKind.Error)));
            ConfigRow Find(string sheet, string id) => source.Rows(sheet).FirstOrDefault(r => r.Id == id) ?? assets.Rows(sheet).FirstOrDefault(r => r.Id == id)
                ?? throw new InvalidOperationException($"找不到复制来源：{sheet}/{id}");
            IEnumerable<ConfigRow> Children(string sheet, string parentSheet, string id) =>
                (source.Rows(parentSheet).Any(r => r.Id == id) ? source : assets).Rows(sheet).Where(r => r.Id == id);
            int nextSkill = request.StartSkillId;
            int NewSkillId()
            {
                int id = nextSkill;
                if (id <= 0 || id == int.MaxValue || !skillIds.Add(id)) throw new InvalidOperationException("新技能ID无效或已占用：" + id + "；调整起始ID，不会静默跳号。");
                nextSkill++;
                return id;
            }
            int CloneSkill(string sourceId, string name)
            {
                var row = Copy(Find("Skills", sourceId)); int id = NewSkillId(); row.Values["skillId"] = id; row.Values["name"] = name; result.Add(row);
                foreach (var original in Children("SkillEffects", "Skills", sourceId))
                {
                    var e = Copy(original); e.Values["skillId"] = id; e.Values["effectKey"] = "skill." + id + ".effect." + e.Get<int>("order"); result.Add(e);
                }
                return id;
            }
            int TemplateSkill(string name, ConfigRecipe recipe)
            {
                int id = NewSkillId();
                bool heal = recipe == ConfigRecipe.AllyHeal, area = recipe == ConfigRecipe.AreaDamage;
                var row = Defaults("Skills"); row.Values["skillId"] = id; row.Values["name"] = name;
                row.Values["kind"] = heal || area ? SkillKind.Skill : SkillKind.Basic;
                row.Values["skillPointCost"] = heal || area ? 1 : 0; row.Values["skillPointGain"] = heal || area ? 0 : 1;
                row.Values["targetSide"] = heal ? TargetSide.OurSide : TargetSide.OtherSide; row.Values["targetRule"] = area ? TargetRule.AoE : TargetRule.Single;
                result.Add(row);
                var effect = Defaults("SkillEffects"); effect.Values["skillId"] = id; effect.Values["effectKey"] = "skill." + id + ".effect.0";
                effect.Values["effectType"] = heal ? EffectType.Heal : EffectType.Damage; effect.Values["value"] = 1f;
                effect.Values["buffValueMultiplier"] = 1f; effect.Values["scaleStat"] = StatType.Attack;
                effect.Values["targetSide"] = row.Values["targetSide"]; effect.Values["targetRule"] = row.Values["targetRule"];
                result.Add(effect); return id;
            }
            for (int n = 0; n < request.Count; n++)
            {
                string name = request.NamePrefix + (request.Count > 1 ? "_" + (n + 1) : "");
                if (request.Recipe == ConfigRecipe.CopySkill) { CloneSkill(request.SourceId, name); continue; }
                if (request.Recipe != ConfigRecipe.CopyUnit && request.Recipe != ConfigRecipe.NewUnit) { TemplateSkill(name, request.Recipe); continue; }
                if (string.IsNullOrWhiteSpace(request.UnitPrefix) || request.UnitPrefix != request.UnitPrefix.Trim() || request.StartUnitNumber < 0 || (long)request.StartUnitNumber + request.Count > int.MaxValue)
                    throw new InvalidOperationException("单位ID前缀或起始序号无效。");
                string unitId = request.UnitPrefix + (request.StartUnitNumber + n);
                if (!unitIds.Add(unitId)) throw new InvalidOperationException("新单位ID已占用：" + unitId);
                var unit = request.Recipe == ConfigRecipe.CopyUnit ? Copy(Find("Units", request.SourceId)) : Defaults("Units");
                if (request.Recipe == ConfigRecipe.CopyUnit)
                    foreach (var column in BattleTableSchema.Get("Units").Columns.Where(c => c.OptionalColumn && !unit.Values.ContainsKey(c.Name)))
                        unit.Values[column.Name] = result.Rows("Units").First(r => r.Id == request.SourceId).Values[column.Name];
                unit.Values["unitId"] = unitId; unit.Values["name"] = name; unit.Values["unitType"] = request.Kind;
                if (request.Recipe == ConfigRecipe.NewUnit)
                {
                    unit.Values["maxHealth"] = 1000f; unit.Values["attack"] = 100f; unit.Values["defence"] = 100f;
                    unit.Values["speed"] = 100f; unit.Values["maxEnergy"] = 100;
                    var relation = Defaults("UnitSkills"); relation.Values["unitId"] = unitId; relation.Values["skillId"] = TemplateSkill(name + "普攻", ConfigRecipe.SingleDamage); result.Add(relation);
                }
                else
                {
                    var cloned = new Dictionary<int, int>();
                    foreach (var original in Children("UnitSkills", "Units", request.SourceId).OrderBy(r => r.Get<UnitSkillRole>("role")).ThenBy(r => r.Get<int>("order")))
                    {
                        var link = Copy(original); link.Values["unitId"] = unitId; int id = original.Get<int>("skillId");
                        if (request.CloneSkills)
                        {
                            if (!cloned.TryGetValue(id, out int newId)) { newId = CloneSkill(id.ToString(), name + "·" + Find("Skills", id.ToString()).Get<string>("name")); cloned.Add(id, newId); }
                            link.Values["skillId"] = newId;
                        }
                        result.Add(link);
                    }
                    string sourceGrowth = unit.Get<string>("growthId");
                    if (request.CloneSkills && sourceGrowth.Length > 0)
                    {
                        string newGrowth = "growth." + unitId;
                        if (index.Growths.ContainsKey(newGrowth) || result.Rows("CharacterGrowth").Any(r => r.Id == newGrowth))
                            throw new InvalidOperationException("复制养成ID已占用：" + newGrowth);
                        var growthParent = Copy(Find("CharacterGrowth", sourceGrowth)); growthParent.Values["growthId"] = newGrowth; result.Add(growthParent);
                        foreach (string sheet in new[] { "CharacterGrowthStages", "CharacterRanks" })
                            foreach (var original in Children(sheet, "CharacterGrowth", sourceGrowth))
                            { var child = Copy(original); child.Values["growthId"] = newGrowth; result.Add(child); }
                        int effectOrder = 0;
                        foreach (var original in Children("SkillEffectGrowth", "CharacterGrowth", sourceGrowth).OrderBy(r => r.Get<int>("order")))
                        {
                            foreach (var mapping in cloned)
                                foreach (var effect in Children("SkillEffects", "Skills", mapping.Key.ToString()).Where(r => r.Get<string>("effectKey") == original.Get<string>("effectKey")))
                                {
                                    var child = Copy(original); child.Values["growthId"] = newGrowth;
                                    child.Values["effectKey"] = "skill." + mapping.Value + ".effect." + effect.Get<int>("order");
                                    child.Values["order"] = effectOrder++; result.Add(child);
                                }
                        }
                        unit.Values["growthId"] = newGrowth;
                    }
                }
                result.Add(unit);
            }
            return result;
        }

        internal static ConfigRow Copy(ConfigRow row) => ConfigRow.Create(row.Sheet, row.Values);
        private static ConfigRow Defaults(string sheet)
        {
            var row = new ConfigRow { Sheet = sheet };
            foreach (var c in BattleTableSchema.Get(sheet).Columns)
                row.Values[c.Name] = c.OptionalColumn ? BattleTableSchema.CombatDefault(c) : c.Type == typeof(string) ? "New" : c.Type == typeof(int) ? (object)(int)Math.Max(0, c.Minimum) : c.Type == typeof(float) ? 0f : Activator.CreateInstance(c.Type);
            return row;
        }
    }

    public sealed class ConfigBatchAuthoringWindow : EditorWindow
    {
        private string sourcePath, scope, outputPath;
        private readonly ConfigBatchRequest request = new();
        private ConfigTableData generated;
        private ConfigAssetIndex index;
        private string sourceHash, assetHash, status = "选择来源与参数，先生成预览。输出为独立工作簿，不写入SO。";
        private Vector2 scroll;
        private string[] sourceIds = Array.Empty<string>(), sourceLabels = Array.Empty<string>();
        private static readonly string[] Recipes = { "复制单位（含技能关联）", "复制技能（含效果）", "单体伤害模板", "群体伤害模板", "我方治疗模板", "新单位＋普攻模板" };
        internal static void Open(string path, string scope)
        {
            var window = GetWindow<ConfigBatchAuthoringWindow>("配置模板与批量复制");
            window.sourcePath = path; window.scope = scope; window.generated = null; window.LoadSources();
            window.minSize = new Vector2(650, 480);
        }
        private ConfigTableData ReadSource(List<ImportMessage> messages)
        {
            if (string.IsNullOrWhiteSpace(sourcePath)) return ConfigImportService.Adapter.Export(index, messages);
            byte[] bytes = ConfigImportService.ReadWorkbookSnapshot(sourcePath); sourceHash = ConfigImportService.Hash(bytes);
            string temp = Path.GetTempFileName();
            try { File.WriteAllBytes(temp, bytes); return ConfigImportService.Adapter.Parse(temp, messages); }
            finally { File.Delete(temp); }
        }
        private void LoadSources()
        {
            try
            {
                var messages = new List<ImportMessage>(); index = new ConfigAssetIndex(scope, messages); var data = ReadSource(messages);
                if (messages.Any(m => m.Kind == ImportMessageKind.Error)) throw new InvalidOperationException(string.Join("\n", messages));
                string sheet = request.Recipe == ConfigRecipe.CopySkill ? "Skills" : "Units";
                var rows = data.Rows(sheet).ToArray(); sourceIds = rows.Select(r => r.Id).ToArray(); sourceLabels = rows.Select(r => r.Id + " · " + r.Get<string>("name")).ToArray();
                if (!sourceIds.Contains(request.SourceId)) request.SourceId = sourceIds.FirstOrDefault();
            }
            catch (Exception e) { status = e.Message; sourceIds = sourceLabels = Array.Empty<string>(); }
        }
        private void OnGUI()
        {
            EditorGUILayout.HelpBox("生成包含源业务数据与新增行的独立工作簿；原文件不变。额外计算表不复制。模板只提供战斗规则，不制作移动/动画。模型、头像和演出在Unity绑定；成长通过growthId配置。", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            sourcePath = EditorGUILayout.TextField("源工作簿（空=当前SO）", sourcePath);
            scope = EditorGUILayout.TextField("索引范围", scope);
            request.Recipe = (ConfigRecipe)EditorGUILayout.Popup("模板/复制方式", (int)request.Recipe, Recipes);
            if (EditorGUI.EndChangeCheck()) { generated = null; LoadSources(); }
            EditorGUI.BeginChangeCheck();
            if (request.Recipe == ConfigRecipe.CopyUnit || request.Recipe == ConfigRecipe.CopySkill)
            {
                int selected = EditorGUILayout.Popup("来源ID · 名称", Math.Max(0, Array.IndexOf(sourceIds, request.SourceId)), sourceLabels);
                if (selected >= 0 && selected < sourceIds.Length) request.SourceId = sourceIds[selected];
            }
            request.NamePrefix = EditorGUILayout.TextField("新名称前缀", request.NamePrefix);
            request.Count = EditorGUILayout.IntField("数量（1—100）", request.Count);
            if (request.Recipe == ConfigRecipe.CopyUnit || request.Recipe == ConfigRecipe.NewUnit)
            {
                request.Kind = (UnitTableKind)EditorGUILayout.EnumPopup("新单位类别", request.Kind);
                request.UnitPrefix = EditorGUILayout.TextField("新单位ID前缀", request.UnitPrefix);
                request.StartUnitNumber = EditorGUILayout.IntField("单位起始序号", request.StartUnitNumber);
                if (request.Recipe == ConfigRecipe.CopyUnit) request.CloneSkills = EditorGUILayout.Toggle("复制技能（关闭则共用）", request.CloneSkills);
            }
            request.StartSkillId = EditorGUILayout.IntField("新技能起始ID", request.StartSkillId);
            if (EditorGUI.EndChangeCheck()) generated = null;
            using (new EditorGUI.DisabledScope(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("生成预览")) Run(() =>
                {
                    ConfigImportService.EnsureEditorReady(); var messages = new List<ImportMessage>(); index = new ConfigAssetIndex(scope, messages);
                    var source = ReadSource(messages); ConfigImportService.Adapter.Validate(source, index, messages);
                    if (messages.Any(m => m.Kind == ImportMessageKind.Error)) throw new InvalidOperationException(string.Join("\n", messages));
                    generated = ConfigBatchAuthoring.Build(source, index, request); assetHash = index.Fingerprint();
                    messages.Clear(); ConfigImportService.Adapter.Validate(generated, index, messages);
                    if (messages.Any(m => m.Kind == ImportMessageKind.Error)) throw new InvalidOperationException(string.Join("\n", messages));
                    status = "以下为本次新生成的主体（原有业务行也将保留）：\n" + string.Join("\n", new[] { "Units", "Skills" }.SelectMany(s => generated.Rows(s).Where(r => !source.Rows(s).Any(o => o.Id == r.Id)).Select(r => s + " / " + r.Id + " · " + r.Get<string>("name")))) + "\n\n" + string.Join("\n", messages);
                });
                using (new EditorGUI.DisabledScope(generated == null)) if (GUILayout.Button("另存为工作簿（之后在工作台预览导入）"))
                {
                    outputPath = EditorUtility.SaveFilePanel("保存批量配置", Path.GetFullPath("ConfigTables"), "战斗配置_批量生成", "xlsx");
                    if (!string.IsNullOrEmpty(outputPath)) Run(() =>
                    {
                        if (!string.IsNullOrWhiteSpace(sourcePath) && Path.GetFullPath(outputPath).Equals(Path.GetFullPath(sourcePath), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("批量生成请使用新文件名，不覆盖来源工作簿。");
                        if (File.Exists(outputPath) && !EditorUtility.DisplayDialog("覆盖输出工作簿", "将备份旧输出文件再覆盖：\n" + outputPath, "备份并覆盖", "取消")) return;
                        if (!string.IsNullOrWhiteSpace(sourcePath) && ConfigWorkbookFiles.CurrentHash(sourcePath) != sourceHash) throw new InvalidOperationException("来源工作簿已变化，请重新生成预览。");
                        var messages = new List<ImportMessage>(); var current = new ConfigAssetIndex(scope, messages);
                        if (current.Fingerprint() != assetHash) throw new InvalidOperationException("SO已变化，请重新生成预览。");
                        string expected = ConfigWorkbookFiles.CurrentHash(outputPath), temp = Path.GetTempFileName();
                        try
                        {
                            ConfigImportService.ExportScope = scope; generated.Metadata["assetBaseline"] = assetHash;
                            ConfigWorkbook.Write(temp, generated, BattleTableSchema.All); ConfigWorkbookReferences.Enrich(temp, generated, current);
                            var parsed = ConfigImportService.Adapter.Parse(temp, messages); ConfigImportService.Adapter.Validate(parsed, current, messages);
                            if (messages.Any(m => m.Kind == ImportMessageKind.Error)) throw new InvalidOperationException(string.Join("\n", messages));
                            ConfigWorkbookFiles.Publish(temp, outputPath, expected);
                        }
                        finally { File.Delete(temp); }
                        status = "已生成：" + outputPath + "\n在配置表工作台选择此文件，校验与预览后再应用。";
                        EditorUtility.RevealInFinder(outputPath);
                    });
                }
            }
            scroll = EditorGUILayout.BeginScrollView(scroll); GUILayout.Label(status, EditorStyles.wordWrappedLabel); EditorGUILayout.EndScrollView();
        }
        private void Run(Action action) { try { action(); } catch (Exception e) { generated = null; status = e.Message; } }
    }
}
