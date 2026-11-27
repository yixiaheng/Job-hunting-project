using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Battle.Editor.ConfigImport
{
    internal static class ConfigAuthoringSupport
    {
        internal static IEnumerable<string> EffectDiff(IReadOnlyList<EffectConfig> old, IReadOnlyList<ConfigRow> next, ConfigAssetIndex index)
        {
            var used = new HashSet<int>();
            for (int n = 0; n < next.Count; n++)
            {
                var row = next[n]; string key = row.Get<string>("effectKey");
                int previous = Enumerable.Range(0, old.Count).Where(i => !used.Contains(i) && old[i]?.key == key).DefaultIfEmpty(-1).First();
                // Legacy unkeyed entries are compared at their old index, displaying the candidate key explicitly.
                if (previous < 0 && n < old.Count && old[n] != null && string.IsNullOrWhiteSpace(old[n].key) && !used.Contains(n)) previous = n;
                if (previous < 0)
                { yield return $"[新增] effectConfigs[{n}] {key}：" + string.Join(", ", row.Values.Select(v => v.Key + "=" + BattleTableSchema.Text(v.Value))); continue; }
                used.Add(previous);
                if (previous != n) yield return $"[移动] effectConfigs {key}：order {previous} → {n}";
                var before = BattleTableSchema.FromObject("SkillEffects", old[previous]);
                foreach (var c in BattleTableSchema.Get("SkillEffects").Columns.Where(c => c.AssetField != null))
                    if (!Equals(before.Values[c.Name], row.Values[c.Name])) yield return $"[修改] effectConfigs[{n}] {key}.{c.Name}：{BattleTableSchema.Text(before.Values[c.Name])} → {BattleTableSchema.Text(row.Values[c.Name])}";
                int buffId = row.Get<int>("buffId");
                var resolved = index.Buffs.TryGetValue(buffId, out var b) ? b : null;
                if (old[previous].buff != resolved || (buffId != 0 && resolved == null))
                    yield return $"[修改] effectConfigs[{n}] {key}.buff引用：{(old[previous].buff != null ? old[previous].buff.id.ToString() : "无")} → {(buffId == 0 ? "无" : buffId.ToString())}";
            }
            for (int i = 0; i < old.Count; i++) if (!used.Contains(i)) yield return $"[删除] effectConfigs[{i}] {old[i]?.key ?? "空效果"} ({old[i]?.effectType})";
        }

        internal static IEnumerable<string> LinkDiff(UnitConfig unit, IReadOnlyList<ConfigRow> rows, ConfigTableData data, ConfigAssetIndex index)
        {
            string Name(int id) => data.Rows("Skills").FirstOrDefault(r => r.Get<int>("skillId") == id)?.Get<string>("name") ??
                (index.Skills.TryGetValue(id, out var s) ? s.Name : "未找到");
            foreach (UnitSkillRole role in Enum.GetValues(typeof(UnitSkillRole)))
            {
                var before = role == UnitSkillRole.Normal ? (unit.skills ?? new()).ToArray() : unit.talentAction == null ? Array.Empty<SkillConfig>() : new[] { unit.talentAction };
                var after = rows.Where(r => r.Get<UnitSkillRole>("role") == role).ToArray();
                var used = new HashSet<int>();
                for (int n = 0; n < after.Length; n++)
                {
                    int id = after[n].Get<int>("skillId");
                    int previous = Enumerable.Range(0, before.Length).Where(i => !used.Contains(i) && before[i] != null && before[i].id == id).DefaultIfEmpty(-1).First();
                    if (previous < 0) yield return $"[新增] {role}[{n}] {id} · {Name(id)}";
                    else { used.Add(previous); if (previous != n) yield return $"[移动] {role} {id} · {Name(id)}：{previous} → {n}"; }
                }
                for (int i = 0; i < before.Length; i++) if (!used.Contains(i)) yield return $"[删除] {role}[{i}] {before[i]?.id} · {before[i]?.Name}";
            }
        }

        internal static void Warn(ConfigTableData data, ConfigAssetIndex index, List<ImportMessage> messages)
        {
            void W(ConfigRow r, string field, string text) => messages.Add(r.Message(ImportMessageKind.Warning, field, text));
            foreach (var s in data.Rows("Skills"))
            {
                if (!data.Rows("SkillEffects").Any(e => e.Get<int>("skillId") == s.Get<int>("skillId"))) W(s, "skillId", "技能没有效果；可能用于纯表现，请确认是有意配置。");
                if (s.Get<SkillKind>("kind") == SkillKind.Basic && s.Get<int>("skillPointCost") == 0 && s.Get<int>("skillPointGain") == 0)
                    W(s, "skillPointGain", "零消耗普攻填写0仍可能默认恢复1点战技点；本工具不改变运行时规则。");
            }
            foreach (var e in data.Rows("SkillEffects"))
            {
                int id = e.Get<int>("buffId");
                if (e.Get<EffectType>("effectType") == EffectType.ApplyBuff)
                {
                    int max = data.Rows("Buffs").FirstOrDefault(b => b.Get<int>("buffId") == id)?.Get<int>("maxStack") ?? (index.Buffs.TryGetValue(id, out var b) ? b.maxStack : int.MaxValue);
                    if (e.Get<int>("stack") > max) W(e, "stack", $"施加{e.Get<int>("stack")}层超过Buff上限{max}；运行时可能截断。");
                }
                else if (id != 0) W(e, "buffId", "非ApplyBuff效果填写了Buff引用，请确认用途。");
                var skill = data.Rows("Skills").First(s => s.Get<int>("skillId") == e.Get<int>("skillId"));
                if (!e.Get<bool>("overrideTarget") && (e.Get<TargetSide>("targetSide") != skill.Get<TargetSide>("targetSide") || e.Get<TargetRule>("targetRule") != skill.Get<TargetRule>("targetRule")))
                    W(e, "overrideTarget", "overrideTarget=FALSE：此行目标字段不生效，使用所属技能的目标规则。");
            }
            foreach (var unit in data.Rows("Units"))
            {
                var links = data.Rows("UnitSkills").Where(l => l.Id == unit.Id).ToArray();
                if (links.Length == 0) W(unit, "unitId", "单位没有任何技能关联；请确认能否正常行动。");
                foreach (var link in links)
                {
                    int id = link.Get<int>("skillId");
                    var s = data.Rows("Skills").FirstOrDefault(r => r.Get<int>("skillId") == id);
                    index.Skills.TryGetValue(id, out var existing);
                    int cost = s?.Get<int>("energyCost") ?? existing.energyCost;
                    if (cost > unit.Get<int>("maxEnergy")) W(link, "skillId", $"技能{id}能量消耗{cost}大于单位{unit.Id}基础能量上限{unit.Get<int>("maxEnergy")}；若无额外规则将无法释放。");
                    if (unit.Get<UnitTableKind>("unitType") != UnitTableKind.Ally) continue;
                    index.Units.TryGetValue(unit.Id, out var owner);
                    var keys = s == null ? (existing.effectConfigs ?? new()).Where(e => e != null && e.growsWithSkill).Select(e => e.key) :
                        data.Rows("SkillEffects").Where(e => e.Get<int>("skillId") == id && e.Get<bool>("growsWithSkill")).Select(e => e.Get<string>("effectKey"));
                    foreach (string key in keys)
                        if (owner?.growth == null || !(owner.growth.effectGrowth ?? new()).Any(g => g != null && g.effectKey == key))
                            W(link, "skillId", $"技能{id}效果{key}声明成长，但单位{unit.Id}尚无对应成长条目；需在Unity配置，否则不产生该条目的成长加成。");
                }
            }
        }

        internal static Object Resolve(ConfigAssetIndex index, string sheet, string id)
        {
            if (index == null) return null;
            if (sheet == "Units" || sheet == "UnitSkills" || sheet == "Growth") return index.Units.TryGetValue(id, out var u) ? u : null;
            if (!int.TryParse(id, out int number)) return null;
            if (sheet == "Skills" || sheet == "SkillEffects") return index.Skills.TryGetValue(number, out var s) ? s : null;
            return sheet == "Buffs" && index.Buffs.TryGetValue(number, out var b) ? b : null;
        }
    }
}
