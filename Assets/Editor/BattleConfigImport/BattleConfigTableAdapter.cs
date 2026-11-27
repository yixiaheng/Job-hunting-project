using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Game.Characters;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Battle.Editor.ConfigImport
{
    public sealed class ConfigAssetIndex
    {
        public string Scope { get; }
        internal readonly Dictionary<string, UnitConfig> Units = new(StringComparer.Ordinal);
        internal readonly Dictionary<int, SkillConfig> Skills = new();
        internal readonly Dictionary<int, BuffConfig> Buffs = new();
        internal readonly Dictionary<string, UnitTableKind> Kinds = new(StringComparer.Ordinal);
        internal readonly List<Object> Watched = new();
        internal UnitCatalog Catalog;
        public ConfigAssetIndex(string scope, List<ImportMessage> messages)
        {
            Scope = scope.Replace('\\', '/').TrimEnd('/');
            if (!Scope.StartsWith("Assets/", StringComparison.Ordinal) || !AssetDatabase.IsValidFolder(Scope))
            { messages.Add(new(ImportMessageKind.Error, "Index", 0, "", Scope, "索引范围必须是存在的Assets子目录。")); return; }
            var catalogs = Find<UnitCatalog>().ToArray();
            Watched.AddRange(catalogs);
            if (catalogs.Length == 1) Catalog = catalogs[0];
            if (catalogs.Length > 1) Error("UnitCatalog", "范围内有多个角色目录，无法确定登记目标；请缩小索引范围。");
            foreach (var a in Find<UnitConfig>())
            {
                Watched.Add(a);
                if (string.IsNullOrWhiteSpace(a.id) || a.id != a.id.Trim() || Units.ContainsKey(a.id)) { Error(a.name, "单位ID为空、含首尾空格或重复：" + a.id + "；" + AssetDatabase.GetAssetPath(a)); continue; }
                Units.Add(a.id, a);
                string path = AssetDatabase.GetAssetPath(a);
                bool ally = path.Contains("/Allies/") || (Catalog != null && Catalog.Units.Contains(a));
                bool enemy = path.Contains("/Enemies/");
                if (ally == enemy) Error(a.id, "单位类别无法明确判断；请将资产移入Allies/Enemies（保留meta），并检查UnitCatalog。");
                else Kinds.Add(a.id, ally ? UnitTableKind.Ally : UnitTableKind.Enemy);
            }
            foreach (var a in Find<SkillConfig>())
            { Watched.Add(a); if (a.id <= 0 || Skills.ContainsKey(a.id)) Error(a.name, "技能ID非正或重复：" + a.id + "；" + AssetDatabase.GetAssetPath(a) + (Skills.TryGetValue(a.id, out var other) ? " 与 " + AssetDatabase.GetAssetPath(other) : "")); else Skills.Add(a.id, a); }
            foreach (var a in Find<BuffConfig>())
            { Watched.Add(a); if (a.id <= 0 || Buffs.ContainsKey(a.id)) Error(a.name, "Buff ID非正或重复：" + a.id + "；" + AssetDatabase.GetAssetPath(a) + (Buffs.TryGetValue(a.id, out var other) ? " 与 " + AssetDatabase.GetAssetPath(other) : "")); else Buffs.Add(a.id, a); }
            // Growth dependencies outside the import scope are still protected.
            Watched.AddRange(AssetDatabase.FindAssets("t:CharacterGrowthConfig", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<CharacterGrowthConfig>));
            Watched.AddRange(AssetDatabase.FindAssets("t:UnitConfig", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<UnitConfig>));
            void Error(string id, string detail) => messages.Add(new(ImportMessageKind.Error, "Index", 0, "", id, detail));
        }
        internal IEnumerable<T> Find<T>() where T : Object => AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { Scope })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, StringComparer.Ordinal).Select(AssetDatabase.LoadAssetAtPath<T>);
        internal string Fingerprint()
        {
            var text = new StringBuilder(Scope);
            foreach (var obj in Watched.Where(o => o != null).Distinct().OrderBy(AssetDatabase.GetAssetPath, StringComparer.Ordinal))
            {
                string p = AssetDatabase.GetAssetPath(obj);
                text.Append(p).Append(EditorJsonUtility.ToJson(obj)).Append(ConfigImportService.FileHash(p)).Append(ConfigImportService.FileHash(p + ".meta"));
            }
            return ConfigImportService.Hash(Encoding.UTF8.GetBytes(text.ToString()));
        }
    }

    /// <summary>One asset update, generated only from validated data; dependencies resolved after all assets exist.</summary>
    public sealed class ConfigAssetUpdate
    {
        public string Path { get; internal set; }
        public string Description { get; internal set; }
        public bool IsNew { get; internal set; }
        public bool RequiresClearConfirmation { get; internal set; }
        internal Object Existing;
        internal Type AssetType;
        internal ConfigRow Row;
        internal IReadOnlyList<ConfigRow> Children;
        internal Action<Dictionary<string, Object>> CustomApply;
        internal string Key;
        /// <summary>Extension updates must list every affected asset to participate in backup and recovery.</summary>
        public static ConfigAssetUpdate ForExisting(Object asset, string description, Action<Dictionary<string, Object>> apply) =>
            new() { Existing = asset, AssetType = asset.GetType(), Path = AssetDatabase.GetAssetPath(asset), Description = description, CustomApply = apply };
        public static ConfigAssetUpdate ForNew<T>(string key, string path, string description, Action<Dictionary<string, Object>> apply) where T : ScriptableObject =>
            new() { IsNew = true, AssetType = typeof(T), Path = path, Key = key, Description = description, CustomApply = apply };
    }

    public sealed class BattleConfigTableAdapter : IConfigTableAdapter
    {
        private readonly string destinationRoot;
        public BattleConfigTableAdapter() : this("Assets/Config/Battle") { }
        internal BattleConfigTableAdapter(string destinationRoot) { this.destinationRoot = destinationRoot; }
        public IReadOnlyList<TableSchema> Schemas => BattleTableSchema.All;
        public ConfigTableData Parse(string path, List<ImportMessage> messages) => ConfigWorkbook.Read(path, Schemas, messages);
        public ConfigTableData Export(ConfigAssetIndex index, List<ImportMessage> messages)
        {
            var data = new ConfigTableData();
            foreach (var schema in Schemas) data.MutableRows(schema.Name);
            foreach (var item in index.Units.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                var unit = item.Value;
                if (!index.Kinds.TryGetValue(item.Key, out var kind)) continue;
                var r = BattleTableSchema.FromObject("Units", unit); r.Values["unitType"] = kind; data.MutableRows("Units").Add(r);
                for (int i = 0; i < (unit.skills?.Count ?? 0); i++) Relation(unit, unit.skills[i], UnitSkillRole.Normal, i);
                if (unit.talentAction != null) Relation(unit, unit.talentAction, UnitSkillRole.TalentAction, 0);
            }
            void Relation(UnitConfig u, SkillConfig s, UnitSkillRole role, int order)
            {
                if (s == null || !index.Skills.TryGetValue(s.id, out var known) || known != s)
                { messages.Add(new(ImportMessageKind.Error, "UnitSkills", 0, "skillId", u.id, "技能引用为空或在索引范围外；扩大范围或修复引用。")); return; }
                var r = new ConfigRow { Sheet = "UnitSkills" };
                r.Values["unitId"] = u.id; r.Values["skillId"] = s.id; r.Values["role"] = role; r.Values["order"] = order;
                data.MutableRows("UnitSkills").Add(r);
            }
            foreach (var item in index.Skills.OrderBy(p => p.Key))
            {
                var s = item.Value; data.MutableRows("Skills").Add(BattleTableSchema.FromObject("Skills", s));
                var used = new HashSet<string>((s.effectConfigs ?? new()).Where(e => e != null && !string.IsNullOrWhiteSpace(e.key)).Select(e => e.key), StringComparer.Ordinal);
                for (int i = 0; i < (s.effectConfigs?.Count ?? 0); i++)
                {
                    var e = s.effectConfigs[i];
                    if (e == null) { messages.Add(new(ImportMessageKind.Error, "SkillEffects", 0, "", s.id.ToString(), "存在空效果；请先修复。")); continue; }
                    var r = BattleTableSchema.FromObject("SkillEffects", e);
                    r.Values["skillId"] = s.id; r.Values["order"] = i;
                    if (string.IsNullOrWhiteSpace(e.key))
                    {
                        string key = "legacy." + s.id + "." + i;
                        while (!used.Add(key)) key += "_";
                        r.Values["effectKey"] = key;
                        messages.Add(new(ImportMessageKind.Warning, "SkillEffects", 0, "effectKey", s.id.ToString(), $"旧效果[{i}]没有key；导出候选 {key}，导入时会显示新增标识。"));
                    }
                    if (e.buff != null && (!index.Buffs.TryGetValue(e.buff.id, out var b) || b != e.buff))
                        messages.Add(new(ImportMessageKind.Error, "SkillEffects", 0, "buffId", s.id.ToString(), "Buff引用在索引范围外或ID重复。"));
                    if (e.buff != null && e.buff.id != e.buffId)
                        messages.Add(new(ImportMessageKind.Warning, "SkillEffects", 0, "buffId", s.id.ToString(), "现有Buff引用与数字ID不一致；导出采用真实引用ID，导入时同步。"));
                    r.Values["buffId"] = e.buff != null ? e.buff.id : e.buffId;
                    data.MutableRows("SkillEffects").Add(r);
                }
            }
            foreach (var b in index.Buffs.OrderBy(p => p.Key)) data.MutableRows("Buffs").Add(BattleTableSchema.FromObject("Buffs", b.Value));
            foreach (var rows in data.Tables.Values)
                for (int i = 0; i < rows.Count; i++) rows[i].Number = i + 3;
            Validate(data, index, messages);
            return data;
        }

        public void Validate(ConfigTableData data, ConfigAssetIndex index, List<ImportMessage> messages)
        {
            var units = data.Rows("Units"); var skills = data.Rows("Skills"); var buffs = data.Rows("Buffs");
            var effects = data.Rows("SkillEffects"); var links = data.Rows("UnitSkills");
            void Error(ConfigRow r, string c, string m) => messages.Add(r.Message(ImportMessageKind.Error, c, m));
            void Warning(ConfigRow r, string c, string m) => messages.Add(r.Message(ImportMessageKind.Warning, c, m));
            void Unique(IEnumerable<ConfigRow> rows, Func<ConfigRow, string> key, string column)
            { foreach (var g in rows.GroupBy(key, StringComparer.Ordinal).Where(g => g.Count() > 1)) foreach (var r in g) Error(r, column, "重复身份：" + g.Key + "；必须唯一。"); }
            Unique(units, r => r.Get<string>("unitId"), "unitId"); Unique(skills, r => r.Id, "skillId"); Unique(buffs, r => r.Id, "buffId");
            Unique(effects, r => r.Get<int>("skillId") + "/" + r.Get<string>("effectKey"), "effectKey");
            Unique(links, r => r.Id + "/" + r.Get<UnitSkillRole>("role") + "/" + r.Get<int>("order"), "order");
            var unitIds = new HashSet<string>(units.Select(r => r.Id), StringComparer.Ordinal);
            var skillIds = new HashSet<int>(skills.Select(r => r.Get<int>("skillId")));
            var buffIds = new HashSet<int>(buffs.Select(r => r.Get<int>("buffId")));
            foreach (var r in units)
            {
                foreach (var column in BattleTableSchema.Get("Units").Columns.Where(c => c.OptionalColumn))
                {
                    float value = (float)(r.Values.TryGetValue(column.Name, out var configured) ? configured :
                        index.Units.TryGetValue(r.Id, out var existing) ? BattleTableSchema.ReadField(existing, column.AssetField) : BattleTableSchema.CombatDefault(column));
                    if (float.IsNaN(value) || float.IsInfinity(value) || value < 0)
                        Error(r, column.Name, "基础伤害属性必须是非负有限小数；0.2表示20%。");
                }
                if (r.Get<int>("startEnergy") > r.Get<int>("maxEnergy")) Error(r, "startEnergy", "初始能量不能超过能量上限。");
                if (index.Kinds.TryGetValue(r.Id, out var old) && old != r.Get<UnitTableKind>("unitType")) Error(r, "unitType", "首版禁止转换已有单位类别；需要单独迁移角色登记和引用。");
                if (!index.Units.ContainsKey(r.Id))
                {
                    Warning(r, "unitId", "新单位：请在Unity绑定prefab、icon及我方growth。修改旧ID将新增资产，旧配置不会删除；请检查存档和演出映射。");
                    if (r.Get<UnitTableKind>("unitType") == UnitTableKind.Ally && index.Catalog == null) Error(r, "unitType", "新增我方角色需要索引范围内恰好一个UnitCatalog。");
                }
            }
            foreach (var r in effects)
            {
                if (!skillIds.Contains(r.Get<int>("skillId"))) Error(r, "skillId", "子表所属技能必须出现在Skills；不能单独更新子表。");
                int id = r.Get<int>("buffId");
                if (id != 0 && !buffIds.Contains(id) && !index.Buffs.ContainsKey(id)) Error(r, "buffId", "Buff不存在；补充Buffs行或扩大索引范围。");
                if (r.Get<EffectType>("effectType") == EffectType.ApplyBuff && (id == 0 || r.Get<int>("stack") < 1)) Error(r, "buffId", "ApplyBuff要求有效buffId和至少1层。");
            }
            foreach (var r in links)
            {
                if (!unitIds.Contains(r.Id)) Error(r, "unitId", "子表所属单位必须出现在Units；不能单独更新子表。");
                int id = r.Get<int>("skillId");
                if (!skillIds.Contains(id) && !index.Skills.ContainsKey(id)) Error(r, "skillId", "引用的技能不存在；补充Skills或扩大索引范围。");
            }
            void Order(IEnumerable<ConfigRow> rows)
            {
                var sorted = rows.OrderBy(r => r.Get<int>("order")).ToArray();
                for (int i = 0; i < sorted.Length; i++) if (sorted[i].Get<int>("order") != i) Error(sorted[i], "order", "order必须从0连续且不能重复；预期 " + i);
            }
            foreach (var group in effects.GroupBy(r => r.Get<int>("skillId"))) Order(group);
            foreach (var group in links.GroupBy(r => new { r.Id, Role = r.Get<UnitSkillRole>("role") }))
            {
                Order(group);
                if (group.Key.Role == UnitSkillRole.TalentAction && group.Count() > 1) Error(group.First(), "role", "每个单位最多一个TalentAction。");
            }
            // Protect each growth owner's reachable key set after ALL link/effect edits, including shared skills.
            var allOwners = AssetDatabase.FindAssets("t:UnitConfig", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<UnitConfig>);
            foreach (var owner in allOwners.Where(u => u != null && u.growth != null))
            {
                var beforeSkills = (owner.skills ?? new()).Concat(owner.talentAction != null ? new[] { owner.talentAction } : Array.Empty<SkillConfig>()).Where(s => s != null).ToArray();
                var before = new HashSet<string>(beforeSkills.SelectMany(s => s.effectConfigs ?? new()).Where(e => e != null).Select(e => e.key), StringComparer.Ordinal);
                var afterSkills = index.Units.TryGetValue(owner.id ?? "", out var indexed) && indexed == owner && unitIds.Contains(owner.id)
                    ? links.Where(r => r.Id == owner.id).Select(r => r.Get<int>("skillId")).ToArray() : beforeSkills.Select(s => s.id).ToArray();
                var after = new HashSet<string>(StringComparer.Ordinal);
                foreach (int id in afterSkills)
                {
                    if (skillIds.Contains(id)) after.UnionWith(effects.Where(r => r.Get<int>("skillId") == id).Select(r => r.Get<string>("effectKey")));
                    else
                    {
                        var skill = beforeSkills.FirstOrDefault(s => s.id == id) ?? (index.Skills.TryGetValue(id, out var known) ? known : null);
                        if (skill != null) after.UnionWith((skill.effectConfigs ?? new()).Where(e => e != null).Select(e => e.key));
                    }
                }
                foreach (var growth in owner.growth.effectGrowth ?? new())
                    if (growth != null && before.Contains(growth.effectKey) && !after.Contains(growth.effectKey))
                        messages.Add(new(ImportMessageKind.Error, "Growth", 0, "effectKey", owner.id, $"删除/替换后成长引用悬空：{growth.effectKey}；来源 {AssetDatabase.GetAssetPath(owner.growth)}。先调整成长配置。"));
            }
            if (!messages.Any(m => m.Kind == ImportMessageKind.Error)) ConfigAuthoringSupport.Warn(data, index, messages);
        }

        public IReadOnlyList<ConfigAssetUpdate> BuildUpdates(ConfigTableData data, ConfigAssetIndex index, List<ImportMessage> messages)
        {
            // Normalize only the detached import data; Validate remains a read-only query.
            BattleTableSchema.CompleteLegacyUnitColumns(data, index);
            var updates = new List<ConfigAssetUpdate>();
            foreach (var r in data.Rows("Buffs")) Add(r, index.Buffs.TryGetValue(r.Get<int>("buffId"), out var b) ? b : null, typeof(BuffConfig), "Buffs", null);
            foreach (var r in data.Rows("Skills")) Add(r, index.Skills.TryGetValue(r.Get<int>("skillId"), out var s) ? s : null, typeof(SkillConfig), "Skills",
                data.Rows("SkillEffects").Where(e => e.Get<int>("skillId") == r.Get<int>("skillId")).OrderBy(e => e.Get<int>("order")).ToArray());
            foreach (var r in data.Rows("Units")) Add(r, index.Units.TryGetValue(r.Id, out var u) ? u : null, typeof(UnitConfig),
                "Units/" + (r.Get<UnitTableKind>("unitType") == UnitTableKind.Ally ? "Allies" : "Enemies"),
                data.Rows("UnitSkills").Where(l => l.Id == r.Id).OrderBy(l => l.Get<UnitSkillRole>("role")).ThenBy(l => l.Get<int>("order")).ToArray());
            var newAllies = data.Rows("Units").Where(r => r.Get<UnitTableKind>("unitType") == UnitTableKind.Ally && !index.Units.ContainsKey(r.Id)).Select(r => r.Id).ToArray();
            if (newAllies.Length > 0 && index.Catalog != null)
            {
                updates.Add(new ConfigAssetUpdate { Path = AssetDatabase.GetAssetPath(index.Catalog), Existing = index.Catalog, AssetType = typeof(UnitCatalog), Description = "UnitCatalog追加：" + string.Join(", ", newAllies),
                    CustomApply = map =>
                    {
                        var so = new SerializedObject(index.Catalog); var list = so.FindProperty("units");
                        foreach (string id in newAllies) { int i = list.arraySize; list.InsertArrayElementAtIndex(i); list.GetArrayElementAtIndex(i).objectReferenceValue = map["Units/" + id]; }
                        so.ApplyModifiedPropertiesWithoutUndo();
                    } });
                messages.Add(new(ImportMessageKind.Modified, "UnitCatalog", 0, "units", "", "保留已有顺序，追加：" + string.Join(", ", newAllies)));
            }
            return updates.AsReadOnly();

            void Add(ConfigRow row, Object asset, Type type, string folder, IReadOnlyList<ConfigRow> children)
            {
                string id = row.Id, key = row.Sheet + "/" + id;
                var differences = new List<string>();
                bool clears = false;
                if (asset != null)
                {
                    foreach (var c in BattleTableSchema.Get(row.Sheet).Columns.Where(c => c.AssetField != null))
                    {
                        object old = BattleTableSchema.ReadField(asset, c.AssetField), next = row.Values[c.Name];
                        if (!Equals(old, next)) differences.Add(c.Name + ": " + BattleTableSchema.Text(old) + " → " + BattleTableSchema.Text(next));
                    }
                    if (asset is SkillConfig skill)
                    {
                        var old = skill.effectConfigs ?? new();
                        differences.AddRange(ConfigAuthoringSupport.EffectDiff(old, children, index));
                        clears = old.Count > 0 && children.Count == 0;
                        if (!old.Select(e => (e?.key ?? "") + "/" + e?.effectType).SequenceEqual(children.Select(e => e.Get<string>("effectKey") + "/" + e.Get<EffectType>("effectType"))))
                            messages.Add(row.Message(ImportMessageKind.Warning, "skillId", "效果key/顺序/数量变化：BattleImpactMarker.effectGroup按下标匹配；请复查Timeline命中标记。"));
                    }
                    if (asset is UnitConfig unit)
                    {
                        differences.AddRange(ConfigAuthoringSupport.LinkDiff(unit, children, data, index));
                        clears = (unit.skills?.Count > 0 && !children.Any(c => c.Get<UnitSkillRole>("role") == UnitSkillRole.Normal)) ||
                            (unit.talentAction != null && !children.Any(c => c.Get<UnitSkillRole>("role") == UnitSkillRole.TalentAction));
                    }
                }
                else differences.Add("新建配置；" + string.Join(", ", row.Values.Select(v => v.Key + "=" + BattleTableSchema.Text(v.Value))));
                if (differences.Count == 0) { messages.Add(row.Message(ImportMessageKind.Unchanged, "", "无变化；不保存资产。")); return; }
                string path = asset != null ? AssetDatabase.GetAssetPath(asset) : NewPath(destinationRoot, folder, id);
                if (asset == null && (File.Exists(path) || File.Exists(path + ".meta")))
                    messages.Add(row.Message(ImportMessageKind.Error, "", "新资源路径已被占用：" + path));
                string detail = path + "\n" + string.Join("\n", differences);
                if (differences.Any(d => d.StartsWith("[删除]"))) messages.Add(row.Message(ImportMessageKind.Warning, "", "将移除子列表项（不删除SO）：\n" + string.Join("\n", differences.Where(d => d.StartsWith("[删除]")))));
                if (clears) messages.Add(row.Message(ImportMessageKind.Warning, "", "[清空确认] 现有技能列表、天赋行动或效果列表将被清空；应用前需明确确认。"));
                messages.Add(row.Message(asset == null ? ImportMessageKind.Added : ImportMessageKind.Modified, "", detail));
                updates.Add(new ConfigAssetUpdate { Path = path, Description = detail, IsNew = asset == null, Existing = asset, AssetType = type, Row = row, Children = children, Key = key, RequiresClearConfirmation = clears });
            }
        }
        internal static string NewPath(string root, string folder, string id) => root + "/" + folder + "/" +
            new string(id.Take(40).Select(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_').ToArray()) + "_" + ConfigImportService.Hash(Encoding.UTF8.GetBytes(id)).Substring(0, 8) + ".asset";
        private static string EffectText(EffectConfig e)
        {
            if (e == null) return "null";
            var r = BattleTableSchema.FromObject("SkillEffects", e);
            return EffectText(r) + (e.buff == null && e.buffId != 0 ? " [缺少Buff引用]" : e.buff != null && e.buff.id != e.buffId ? " [Buff引用ID=" + e.buff.id + "]" : "");
        }
        private static string EffectText(ConfigRow r) => string.Join("; ", BattleTableSchema.Get("SkillEffects").Columns.Where(c => c.AssetField != null).Select(c => c.Name + "=" + BattleTableSchema.Text(r.Values[c.Name])));
    }
}
