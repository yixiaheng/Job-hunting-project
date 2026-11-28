using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Characters;
using UnityEditor;
using UnityEngine;

namespace Battle.Editor.ConfigImport
{
    /// <summary>养成表的语义层。仅生成受导入事务保护的资产操作，预览不修改SO。</summary>
    internal static class GrowthConfigTables
    {
        internal static CharacterGrowthTable SharedForVerification;
        internal static CharacterGrowthTable Shared => SharedForVerification != null ? SharedForVerification : CharacterGrowthTable.Shared;
        private static TableColumn S(string n, string d, string f = null, bool empty = false) => new(n, d, typeof(string), f, allowEmpty: empty);
        private static TableColumn I(string n, string d, string f = null, int min = 0) => new(n, d, typeof(int), f, min);
        private static TableColumn F(string n, string d, string f = null, float min = 0) => new(n, d, typeof(float), f, min);
        internal static readonly TableSchema[] Schemas =
        {
            new("GrowthStages", I("stage", "全局突破阶段；0—6连续；有数据时必须完整填写七行"),
                I("startLevel", "阶段起始等级；首次为1", min: 1), I("maxLevel", "阶段等级上限；末阶段80", min: 1),
                F("startMultiplier", "阶段起始三围系数；首次为1"), F("endMultiplier", "阶段结束三围系数；不是额外增量")),
            new("CharacterGrowth", S("growthId", "养成配置稳定ID；修改视为新增；允许多个单位引用"),
                I("experienceBase", "升级经验常数", "experienceBase"), I("experiencePerLevel", "每级经验增量；下一级需求=常数+增量×当前等级", "experiencePerLevel"),
                I("talentUnlockLevel", "现有天赋解锁等级", "talentUnlockLevel", 1), I("duplicateShards", "重复角色获得碎片数量", "duplicateShards"),
                S("characterExperience", "角色经验道具ID", "characterExperience"), S("characterAscension", "角色突破材料ID", "characterAscension"),
                S("skillMaterial", "技能升级材料ID", "skillMaterial"), I("skillCostPerLevel", "每级技能消耗系数", "skillCostPerLevel"),
                F("skillGrowth", "保留旧生成器默认成长值；实际按SkillEffectGrowth读取", "skillGrowth"),
                F("rankPrimaryFirst", "一阶主属性加成；0.05=5%", "rankPrimaryFirst"), F("rankHealth", "三阶生命加成", "rankHealth"),
                F("rankPrimaryLast", "五阶主属性加成", "rankPrimaryLast"), F("rankBasic", "旧普攻成长生成参数", "rankBasic"),
                F("rankSkill", "旧战技成长生成参数", "rankSkill"), F("rankUltimate", "旧终结技成长生成参数", "rankUltimate"),
                new TableColumn("primaryStat", "升阶主属性；限Health/Attack/Defence/Speed；代码见Enums", typeof(StatType), "primaryStat"),
                S("shardItem", "升阶碎片道具ID；未接入时可空并提示", "shardItem", true),
                new TableColumn("talent", "现有天赋类型；本次不重写天赋系统；代码见Enums", typeof(CharacterTalent), "talent"),
                F("talentValue", "现有天赋主参数", "talentValue"), F("talentSecondary", "现有天赋副参数", "talentSecondary"),
                I("talentEnergy", "现有天赋能量参数", "talentEnergy"), I("talentDuration", "现有天赋持续回合", "talentDuration")),
            new("CharacterGrowthStages", S("growthId", "主体必须在CharacterGrowth出现"), I("stage", "0—6连续；七行完整定义"),
                I("skillCap", "该突破阶段技能等级上限；不递减", min: 1), I("ascensionCost", "从本阶段突破到下一阶段的材料数量；最终阶段必须为0")),
            new("CharacterRanks", S("growthId", "主体必须在CharacterGrowth出现"), I("rank", "升到第几阶；当前规则为1—5完整定义", min: 1),
                I("shardCost", "此次升阶消耗碎片数量")),
            new("SkillEffectGrowth", S("growthId", "主体必须在CharacterGrowth出现"), S("effectKey", "匹配技能效果key；同养成配置内唯一", "effectKey"),
                I("order", "列表顺序；从0连续，移动行不改变列表顺序"), F("perSkillLevel", "每升一级技能增加倍率；0.1=10%", "perSkillLevel"),
                I("requiredRank", "达到此升阶等级时增加rankBonus；0表示无门槛", "requiredRank"), F("rankBonus", "达到门槛后的额外倍率", "rankBonus"))
        };

        internal static bool IsSheet(string name) => Schemas.Any(s => s.Name == name);
        internal static string Id(CharacterGrowthConfig config) => config == null ? "" :
            !string.IsNullOrWhiteSpace(config.ConfigId) ? config.ConfigId : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(config));
        internal static void Index(ConfigAssetIndex index, List<ImportMessage> messages)
        {
            index.Watched.Add(Shared);
            index.Watched.AddRange(AssetDatabase.FindAssets("t:ItemConfig", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<Game.Inventory.ItemConfig>).Where(c => c != null));
            foreach (var config in index.Find<CharacterGrowthConfig>())
            {
                string id = Id(config);
                index.Watched.Add(config);
                if (id.Length == 0 || id != id.Trim() || index.Growths.ContainsKey(id))
                    messages.Add(new(ImportMessageKind.Error, "CharacterGrowth", 0, "growthId", id, "养成ID为空、含空格或重复：" + AssetDatabase.GetAssetPath(config)));
                else index.Growths.Add(id, config);
            }
        }

        private static ConfigRow Row(string sheet, params object[] pairs)
        {
            var row = new ConfigRow { Sheet = sheet };
            for (int i = 0; i < pairs.Length; i += 2) row.Values[(string)pairs[i]] = pairs[i + 1];
            return row;
        }
        private static IEnumerable<ConfigRow> StageRows(CharacterGrowthTable table)
        {
            for (int i = 0; i < table.StageCount; i++)
            {
                var s = table.GetStage(i);
                yield return Row("GrowthStages", "stage", i, "startLevel", s.StartLevel, "maxLevel", s.MaxLevel,
                    "startMultiplier", s.StartMultiplier, "endMultiplier", s.EndMultiplier);
            }
        }
        private static IEnumerable<ConfigRow> Children(string id, CharacterGrowthConfig config)
        {
            for (int i = 0; i < config.skillCaps.Length; i++)
                yield return Row("CharacterGrowthStages", "growthId", id, "stage", i, "skillCap", config.skillCaps[i],
                    "ascensionCost", i < config.ascensionCosts.Length ? config.ascensionCosts[i] : 0);
            for (int i = 0; i < config.shardCosts.Length; i++)
                yield return Row("CharacterRanks", "growthId", id, "rank", i + 1, "shardCost", config.shardCosts[i]);
            for (int i = 0; i < (config.effectGrowth?.Count ?? 0); i++)
            {
                if (config.effectGrowth[i] == null) throw new InvalidDataException("养成效果列表含空项：" + id);
                var row = BattleTableSchema.FromObject("SkillEffectGrowth", config.effectGrowth[i]);
                row.Values["growthId"] = id; row.Values["order"] = i; yield return row;
            }
        }
        internal static void Export(ConfigTableData data, ConfigAssetIndex index)
        {
            foreach (var row in StageRows(Shared)) data.Add(row);
            foreach (var pair in index.Growths.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                var row = BattleTableSchema.FromObject("CharacterGrowth", pair.Value); row.Values["growthId"] = pair.Key;
                data.Add(row);
                if (pair.Value.skillCaps?.Length != 7 || pair.Value.ascensionCosts?.Length != 6 || pair.Value.shardCosts?.Length != 5)
                    throw new InvalidDataException("养成数组应为7个技能上限、6个突破消耗、5个升阶消耗：" + pair.Key);
                foreach (var child in Children(pair.Key, pair.Value)) data.Add(child);
            }
        }

        internal static string Reference(ConfigRow row, ConfigAssetIndex index) => row.Values.TryGetValue("growthId", out var value) ? (string)value :
            index.Units.TryGetValue(row.Id, out var unit) ? Id(unit.growth) : "";

        internal static void Validate(ConfigTableData data, ConfigAssetIndex index, List<ImportMessage> messages)
        {
            void Error(ConfigRow row, string field, string reason) => messages.Add(row.Message(ImportMessageKind.Error, field, reason));
            void Warn(ConfigRow row, string field, string reason) => messages.Add(row.Message(ImportMessageKind.Warning, field, reason));
            var stages = data.Rows("GrowthStages").OrderBy(r => r.Get<int>("stage")).ToArray();
            var parents = data.Rows("CharacterGrowth");
            var ids = new HashSet<string>(parents.Select(r => r.Id), StringComparer.Ordinal);
            foreach (var group in parents.GroupBy(r => r.Id).Where(g => g.Count() > 1))
                foreach (var row in group) Error(row, "growthId", "重复养成ID。");
            if (stages.Length > 0)
            {
                if (stages.Length != 7) Error(stages[0], "stage", "当前全局规则要求七阶段完整定义；不是局部补丁。");
                for (int i = 0; i < stages.Length; i++)
                {
                    var r = stages[i];
                    if (r.Get<int>("stage") != i) Error(r, "stage", "stage应从0连续且唯一。");
                    if (r.Get<int>("maxLevel") <= r.Get<int>("startLevel")) Error(r, "maxLevel", "等级上限必须大于起始等级。");
                    if (r.Get<float>("startMultiplier") <= 0 || r.Get<float>("endMultiplier") < r.Get<float>("startMultiplier")) Error(r, "endMultiplier", "成长系数必须为正且阶段内不下降。");
                    if (i == 0 && (r.Get<int>("startLevel") != 1 || r.Get<float>("startMultiplier") != 1)) Error(r, "startLevel", "首阶段必须从1级、1倍开始。");
                    if (i > 0 && (r.Get<int>("startLevel") != stages[i-1].Get<int>("maxLevel") || r.Get<float>("startMultiplier") < stages[i-1].Get<float>("endMultiplier")))
                        Error(r, "startLevel", "相邻阶段等级必须衔接；突破后系数不可降低。");
                }
                if (stages.Last().Get<int>("maxLevel") != 80) Error(stages.Last(), "maxLevel", "当前版本角色满级为80；改变总上限需另做存档迁移。");
            }
            foreach (string sheet in new[] { "CharacterGrowthStages", "CharacterRanks", "SkillEffectGrowth" })
                foreach (var r in data.Rows(sheet))
                    if (!ids.Contains(r.Id)) Error(r, "growthId", "子表不能单独更新，主体必须出现在CharacterGrowth。");
            var items = index.Watched.OfType<Game.Inventory.ItemConfig>().ToArray();
            var itemIds = new HashSet<string>(items.Select(i => i.id), StringComparer.Ordinal);
            foreach (var parent in parents)
            {
                var gs = data.Rows("CharacterGrowthStages").Where(r => r.Id == parent.Id).OrderBy(r => r.Get<int>("stage")).ToArray();
                var ranks = data.Rows("CharacterRanks").Where(r => r.Id == parent.Id).OrderBy(r => r.Get<int>("rank")).ToArray();
                var effects = data.Rows("SkillEffectGrowth").Where(r => r.Id == parent.Id).OrderBy(r => r.Get<int>("order")).ToArray();
                if (gs.Length != 7) Error(parent, "growthId", "CharacterGrowthStages必须完整提供七阶段。");
                for (int i = 0; i < gs.Length; i++)
                {
                    if (gs[i].Get<int>("stage") != i) Error(gs[i], "stage", "阶段应从0连续且唯一。");
                    if (i > 0 && gs[i].Get<int>("skillCap") < gs[i-1].Get<int>("skillCap")) Error(gs[i], "skillCap", "技能上限不可随突破降低。");
                    if (i == 6 && gs[i].Get<int>("ascensionCost") != 0) Error(gs[i], "ascensionCost", "最终阶段不再突破，消耗必须为0。");
                }
                if (ranks.Length != 5) Error(parent, "growthId", "当前升阶规则要求CharacterRanks完整提供1—5阶。");
                for (int i = 0; i < ranks.Length; i++) if (ranks[i].Get<int>("rank") != i + 1) Error(ranks[i], "rank", "阶数应从1连续且唯一。");
                var keys = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < effects.Length; i++)
                {
                    if (!keys.Add(effects[i].Get<string>("effectKey"))) Error(effects[i], "effectKey", "同养成配置内effectKey重复。");
                    if (effects[i].Get<int>("order") != i) Error(effects[i], "order", "order必须从0连续且唯一。");
                    if (effects[i].Get<int>("requiredRank") > 5) Error(effects[i], "requiredRank", "当前最多5阶。");
                }
                foreach (string field in new[] { "characterExperience", "characterAscension", "skillMaterial", "shardItem" })
                {
                    string id = parent.Get<string>(field);
                    if (id.Length == 0) Warn(parent, field, "尚未指定碎片道具，升阶前需补齐。");
                    else if (!itemIds.Contains(id)) Error(parent, field, "找不到道具ID：" + id + "；请在物品系统配置合法道具。");
                }
                if ((long)parent.Get<int>("experienceBase") + (long)parent.Get<int>("experiencePerLevel") * 80 > int.MaxValue
                    || parent.Get<int>("experienceBase") + (long)parent.Get<int>("experiencePerLevel") <= 0)
                    Error(parent, "experienceBase", "升级经验必须大于0且80级需求不能溢出整数。");
                if (parent.Get<int>("talentUnlockLevel") > 80) Warn(parent, "talentUnlockLevel", "天赋解锁等级超过满级，不会自然解锁。");
                var stat = parent.Get<StatType>("primaryStat");
                if (stat != StatType.Health && stat != StatType.Attack && stat != StatType.Defence && stat != StatType.Speed)
                    Error(parent, "primaryStat", "升阶主属性只能是生命、攻击、防御或速度。");
            }
            foreach (var row in data.Rows("Units"))
            {
                string id = Reference(row, index);
                if (id.Length > 0 && !ids.Contains(id) && !index.Growths.ContainsKey(id)) Error(row, "growthId", "养成配置不存在或在索引范围外：" + id);
                if (id.Length == 0 && row.Get<UnitTableKind>("unitType") == UnitTableKind.Ally) Warn(row, "growthId", "我方单位未绑定养成，正式玩家养成入口不可用；请补齐growthId。");
            }
            ValidateEffectReferences(data, index, messages);
        }

        private static void ValidateEffectReferences(ConfigTableData data, ConfigAssetIndex index, List<ImportMessage> messages)
        {
            var owners = AssetDatabase.FindAssets("t:UnitConfig", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<UnitConfig>).Where(u => u != null).ToList();
            var identities = owners.Select(u => (unit: u, row: index.Units.TryGetValue(u.id ?? "", out var known) && known == u ? data.Rows("Units").FirstOrDefault(r => r.Id == u.id) : null)).ToList();
            identities.AddRange(data.Rows("Units").Where(r => !index.Units.ContainsKey(r.Id)).Select(r => (unit: (UnitConfig)null, row: r)));
            foreach (var pair in identities)
            {
                var owner = pair.unit; var row = pair.row;
                string growthId = row != null ? Reference(row, index) : Id(owner.growth);
                if (growthId.Length == 0) continue;
                var oldSkills = owner == null ? Array.Empty<SkillConfig>() : (owner.skills ?? new()).Concat(owner.talentAction != null ? new[] {owner.talentAction} : Array.Empty<SkillConfig>()).Where(s => s != null).ToArray();
                var before = new HashSet<string>(oldSkills.SelectMany(s => s.effectConfigs ?? new()).Where(e => e != null).Select(e => e.key));
                var skillIds = row != null ? data.Rows("UnitSkills").Where(r => r.Id == row.Id).Select(r => r.Get<int>("skillId")) : oldSkills.Select(s => s.id);
                var after = new HashSet<string>();
                foreach (int id in skillIds)
                {
                    if (data.Rows("Skills").Any(r => r.Get<int>("skillId") == id)) after.UnionWith(data.Rows("SkillEffects").Where(r => r.Get<int>("skillId") == id).Select(r => r.Get<string>("effectKey")));
                    else
                    {
                        var skill = oldSkills.FirstOrDefault(s => s.id == id) ?? (index.Skills.TryGetValue(id, out var known) ? known : null);
                        if (skill != null) after.UnionWith((skill.effectConfigs ?? new()).Where(e => e != null).Select(e => e.key));
                    }
                }
                bool configured = data.Rows("CharacterGrowth").Any(r => r.Id == growthId);
                var growth = index.Growths.TryGetValue(growthId, out var g) ? g : owner?.growth;
                var keys = configured ? data.Rows("SkillEffectGrowth").Where(r => r.Id == growthId).Select(r => r.Get<string>("effectKey")) : (growth?.effectGrowth ?? new()).Where(e => e != null).Select(e => e.effectKey);
                foreach (string key in keys)
                    if (!after.Contains(key) && (configured || before.Contains(key) || row != null && Id(owner?.growth) != growthId))
                    {
                        string detail = "成长引用悬空：" + key + "；单位 " + (row?.Id ?? owner.id) + " 导入后的技能无法提供该效果。请同步调整技能或成长子表。";
                        var source = data.Rows("SkillEffectGrowth").FirstOrDefault(r => r.Id == growthId && r.Get<string>("effectKey") == key);
                        messages.Add(source != null ? source.Message(ImportMessageKind.Error, "effectKey", detail)
                            : new(ImportMessageKind.Error, "SkillEffectGrowth", 0, "effectKey", growthId, detail));
                    }
            }
        }

        private static string Text(ConfigRow row) => string.Join("; ", BattleTableSchema.Get(row.Sheet).Columns.Select(c => c.Name + "=" + BattleTableSchema.Text(row.Values[c.Name])));
        internal static IReadOnlyList<ConfigAssetUpdate> BuildUpdates(ConfigTableData data, ConfigAssetIndex index, string destination, List<ImportMessage> messages)
        {
            var result = new List<ConfigAssetUpdate>();
            var stages = data.Rows("GrowthStages").OrderBy(r => r.Get<int>("stage")).ToArray();
            if (stages.Length > 0 && !StageRows(Shared).Select(Text).SequenceEqual(stages.Select(Text)))
            {
                var target = Shared;
                string detail = "共享成长表（影响所有角色）\n原值：\n" + string.Join("\n", StageRows(target).Select(Text)) + "\n新值：\n" + string.Join("\n", stages.Select(Text));
                result.Add(ConfigAssetUpdate.ForExisting(target, detail, _ => BattleTableSchema.WriteField(target, "stages",
                    stages.Select(r => new CharacterGrowthStage(r.Get<int>("startLevel"), r.Get<int>("maxLevel"), r.Get<float>("startMultiplier"), r.Get<float>("endMultiplier"))).ToArray())));
                messages.Add(new(ImportMessageKind.Modified, "GrowthStages", 0, "", "Shared", detail));
                messages.Add(new(ImportMessageKind.Warning, "GrowthStages", 0, "", "Shared", "共用表影响所有角色。等级边界变化可能使已有存档失效；请检查已保存的等级/突破阶段。本工具不修改玩家JSON。"));
            }
            foreach (var row in data.Rows("CharacterGrowth"))
            {
                index.Growths.TryGetValue(row.Id, out var old);
                var gs = data.Rows("CharacterGrowthStages").Where(r => r.Id == row.Id).OrderBy(r => r.Get<int>("stage")).ToArray();
                var ranks = data.Rows("CharacterRanks").Where(r => r.Id == row.Id).OrderBy(r => r.Get<int>("rank")).ToArray();
                var effects = data.Rows("SkillEffectGrowth").Where(r => r.Id == row.Id).OrderBy(r => r.Get<int>("order")).ToArray();
                var children = gs.Concat(ranks).Concat(effects).ToArray();
                var diff = new List<string>();
                if (old == null) diff.Add("新建养成配置：" + Text(row));
                else
                {
                    foreach (var c in Schemas.Single(s => s.Name == "CharacterGrowth").Columns.Where(c => c.AssetField != null))
                    {
                        object value = BattleTableSchema.ReadField(old, c.AssetField);
                        // Serialized strings may be null while an empty Excel cell parses as "".
                        if (!Equals(value ?? "", row.Values[c.Name])) diff.Add(c.Name + ": " + BattleTableSchema.Text(value) + " → " + BattleTableSchema.Text(row.Values[c.Name]));
                    }
                }
                var previous = old == null ? Array.Empty<ConfigRow>() : Children(row.Id, old).ToArray();
                foreach (string sheet in new[] { "CharacterGrowthStages", "CharacterRanks", "SkillEffectGrowth" })
                {
                    var before = previous.Where(r => r.Sheet == sheet).Select(Text).ToArray();
                    var next = children.Where(r => r.Sheet == sheet).Select(Text).ToArray();
                    if (!before.SequenceEqual(next)) diff.Add(sheet + "\n原值：" + string.Join("\n", before) + "\n新值：" + string.Join("\n", next));
                }
                if (diff.Count == 0) { messages.Add(row.Message(ImportMessageKind.Unchanged, "", "养成配置无变化。")); continue; }
                string path = old != null ? AssetDatabase.GetAssetPath(old) : BattleConfigTableAdapter.NewPath(destination, "Growth", row.Id);
                if (old == null && (File.Exists(path) || File.Exists(path + ".meta"))) messages.Add(row.Message(ImportMessageKind.Error, "growthId", "新养成路径已占用：" + path));
                Action<Dictionary<string, UnityEngine.Object>> apply = map =>
                {
                    var target = old != null ? old : (CharacterGrowthConfig)map["Growth/" + row.Id];
                    BattleTableSchema.Assign(row, target); BattleTableSchema.WriteField(target, "configId", row.Id);
                    target.skillCaps = gs.Select(r => r.Get<int>("skillCap")).ToArray();
                    target.ascensionCosts = gs.Take(6).Select(r => r.Get<int>("ascensionCost")).ToArray();
                    target.shardCosts = ranks.Select(r => r.Get<int>("shardCost")).ToArray();
                    target.effectGrowth = effects.Select(r => { var e = new SkillEffectGrowth(); BattleTableSchema.Assign(r, e); return e; }).ToList();
                };
                var update = old == null ? ConfigAssetUpdate.ForNew<CharacterGrowthConfig>("Growth/" + row.Id, path, string.Join("\n", diff), apply)
                    : ConfigAssetUpdate.ForExisting(old, string.Join("\n", diff), apply);
                update.RequiresClearConfirmation = old?.effectGrowth?.Count > 0 && effects.Length == 0;
                result.Add(update);
                messages.Add(row.Message(old == null ? ImportMessageKind.Added : ImportMessageKind.Modified, "", path + "\n" + update.Description));
                if (update.RequiresClearConfirmation) messages.Add(row.Message(ImportMessageKind.Warning, "", "[清空确认] 将清空技能效果成长列表。"));
                messages.Add(row.Message(ImportMessageKind.Warning, "", "养成规则会影响所有引用该growthId的单位；降低技能上限或改变升级经验可能影响已有存档。"));
            }
            return result;
        }
    }
}
