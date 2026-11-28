using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace Battle.Editor.ConfigImport
{
    public enum UnitTableKind { Ally, Enemy }
    public enum UnitSkillRole { Normal, TalentAction }
    public enum ImportMessageKind { Error, Warning, Added, Modified, Unchanged }

    public sealed class ImportMessage
    {
        public ImportMessageKind Kind { get; }
        public string Sheet { get; }
        public int Row { get; }
        public string Column { get; }
        public string Id { get; }
        public string Detail { get; }
        public ImportMessage(ImportMessageKind kind, string sheet, int row, string column, string id, string detail)
        { Kind = kind; Sheet = sheet; Row = row; Column = column; Id = id; Detail = detail; }
        public override string ToString() => $"[{Kind}] {Sheet} {Column}{(Row > 0 ? Row.ToString() : "")} [{Id}] {Detail}";
    }

    public sealed class TableColumn
    {
        public string Name { get; }
        public string Description { get; }
        public Type Type { get; }
        public string AssetField { get; }
        public double Minimum { get; }
        public bool OptionalColumn { get; }
        public bool AllowEmpty { get; }
        public TableColumn(string name, string description, Type type, string assetField = null, double minimum = double.NegativeInfinity, bool optionalColumn = false, bool allowEmpty = false)
        { Name = name; Description = description; Type = type; AssetField = assetField; Minimum = minimum; OptionalColumn = optionalColumn; AllowEmpty = allowEmpty; }
    }

    public sealed class TableSchema
    {
        public string Name { get; }
        public IReadOnlyList<TableColumn> Columns { get; }
        public TableSchema(string name, params TableColumn[] columns) { Name = name; Columns = Array.AsReadOnly(columns); }
    }

    // Parsed values have concrete CLR types. Location survives row/column rearrangement.
    public sealed class ConfigRow
    {
        public string Sheet { get; internal set; }
        public int Number { get; internal set; }
        internal readonly Dictionary<string, object> Values = new(StringComparer.Ordinal);
        internal readonly Dictionary<string, string> Locations = new(StringComparer.Ordinal);
        public T Get<T>(string field) => (T)Values[field];
        public static ConfigRow Create(string sheet, IReadOnlyDictionary<string, object> values)
        {
            var row = new ConfigRow { Sheet = sheet };
            foreach (var entry in values) row.Values.Add(entry.Key, entry.Value);
            return row;
        }
        public string Id => Values.TryGetValue("unitId", out var u) ? u.ToString() :
            Values.TryGetValue("growthId", out var g) ? g.ToString() : Values.TryGetValue("skillId", out var s) ? s.ToString() : Values.TryGetValue("buffId", out var b) ? b.ToString() : "";
        public ImportMessage Message(ImportMessageKind kind, string field, string detail) =>
            new(kind, Sheet, Number, Locations.TryGetValue(field, out string c) ? c : field, Id, detail);
    }

    public sealed class ConfigTableData
    {
        public readonly Dictionary<string, string> Metadata = new(StringComparer.Ordinal);
        internal readonly Dictionary<string, List<ConfigRow>> Tables = new(StringComparer.Ordinal);
        public IReadOnlyList<ConfigRow> Rows(string sheet) => Tables.TryGetValue(sheet, out var rows) ? rows.AsReadOnly() : Array.Empty<ConfigRow>();
        public void Add(ConfigRow row) => MutableRows(row.Sheet).Add(row);
        internal List<ConfigRow> MutableRows(string sheet)
        { if (!Tables.TryGetValue(sheet, out var list)) Tables.Add(sheet, list = new()); return list; }
    }

    /// <summary>Adapters own domain semantics; workbook IO only understands schemas and cells.</summary>
    public interface IConfigTableAdapter
    {
        IReadOnlyList<TableSchema> Schemas { get; }
        ConfigTableData Parse(string path, List<ImportMessage> messages);
        void Validate(ConfigTableData data, ConfigAssetIndex index, List<ImportMessage> messages);
        ConfigTableData Export(ConfigAssetIndex index, List<ImportMessage> messages);
        IReadOnlyList<ConfigAssetUpdate> BuildUpdates(ConfigTableData data, ConfigAssetIndex index, List<ImportMessage> messages);
    }

    public static class BattleTableSchema
    {
        private static TableColumn S(string n, string d, string f = null) => new(n, d, typeof(string), f);
        private static TableColumn I(string n, string d, string f = null, double min = 0) => new(n, d, typeof(int), f, min);
        private static TableColumn F(string n, string d, string f = null, double min = double.NegativeInfinity) => new(n, d, typeof(float), f, min);
        private static TableColumn B(string n, string d, string f) => new(n, d + "；TRUE/FALSE", typeof(bool), f);
        private static TableColumn E<T>(string n, string d, string f = null) => new(n, d + "；代码见 Enums", typeof(T), f);
        private static TableColumn Combat(string name, string description, string field) =>
            new(name, description + "；小数比例，0.2=20%；旧表缺列时保留已有值，新单位用默认值", typeof(float), "baseStats.offensiveStats." + field, 0, true);
        public static readonly IReadOnlyList<TableSchema> All = Array.AsReadOnly(new[]
        {
            new TableSchema("Units", S("unitId", "必填；稳定字符串ID，修改视为新增", "id"), S("name", "必填；显示名称", "displayName"),
                E<UnitTableKind>("unitType", "Ally 我方 / Enemy 敌方"), E<ElementType>("element", "元素", "element"), E<PathType>("path", "命途", "path"),
                F("maxHealth", "1级生命；支持小数；独立基础属性SO", "baseStats.health", 1), F("attack", "1级攻击；支持小数", "baseStats.attack", 0), F("defence", "1级防御；支持小数", "baseStats.defence", 0),
                F("speed", "固定基础速度；不随等级增长", "baseStats.speed", 1), I("startEnergy", "初始能量；不超过上限", "baseStats.startEnergy"), I("maxEnergy", "能量上限；正整数", "baseStats.maxEnergy", 1),
                Combat("critRate", "基础暴击率；默认0.05；允许溢出100%", "critRate"),
                Combat("critDamage", "基础暴击额外伤害；默认0.5，暴击倍率为1.5", "critDamage"),
                Combat("breakEffect", "击破特攻；默认0", "breakEffect"),
                Combat("breakEfficiencyBonus", "击破效率加成；默认0，总效率为1+此值", "breakEfficiencyBonus"),
                Combat("physicalDamageBonus", "物理伤害加成", "elementalDamage.physical"),
                Combat("fireDamageBonus", "火伤害加成", "elementalDamage.fire"),
                Combat("iceDamageBonus", "冰伤害加成", "elementalDamage.ice"),
                Combat("lightningDamageBonus", "雷伤害加成", "elementalDamage.lightning"),
                Combat("windDamageBonus", "风伤害加成", "elementalDamage.wind"),
                Combat("quantumDamageBonus", "量子伤害加成", "elementalDamage.quantum"),
                Combat("imaginaryDamageBonus", "虚数伤害加成", "elementalDamage.imaginary"),
                new TableColumn("growthId", "养成配置ID；空值明确解除引用；旧表缺列保留原引用", typeof(string), optionalColumn: true, allowEmpty: true)),
            new TableSchema("Skills", I("skillId", "稳定技能ID；正整数", "id", 1), S("name", "必填；技能名称", "Name"), E<SkillKind>("kind", "技能类型", "kind"),
                E<ElementType>("element", "元素", "element"), I("skillPointCost", "战技点消耗；非负", "skillPointCost"),
                I("skillPointGain", "战技点产出；零消耗普攻填0仍可能默认回1点", "skillPointGain"), I("energyCost", "能量消耗；非负", "energyCost"), I("energyGain", "能量回复；非负", "energyGain"),
                E<TargetSide>("targetSide", "OurSide 自己一方 / OtherSide 对面", "targetSide"), E<TargetRule>("targetRule", "目标规则", "targetRule")),
            new TableSchema("SkillEffects", I("skillId", "所属技能必须出现在 Skills", null, 1), S("effectKey", "必填；技能内唯一；成长匹配标识", "key"), I("order", "效果下标；从0连续"),
                E<EffectOrigin>("origin", "效果来源", "origin"), E<EffectType>("effectType", "效果类型", "effectType"), F("value", "数值或倍率；0.2表示20%；按效果语义", "value"),
                E<StatType>("scaleStat", "缩放属性", "scaleStat"), B("growsWithSkill", "是否参与技能成长", "growsWithSkill"), B("overrideTarget", "是否覆盖技能目标规则", "overrideTarget"),
                E<TargetSide>("targetSide", "覆盖目标阵营", "targetSide"), E<TargetRule>("targetRule", "覆盖目标规则", "target"), I("buffId", "Buff ID；无引用填0；ApplyBuff须有效ID", "buffId"),
                I("stack", "Buff层数；ApplyBuff至少1", "stack"), F("buffValueMultiplier", "Buff倍率；1为原值", "buffValueMultiplier", 0)),
            new TableSchema("Buffs", I("buffId", "稳定Buff ID；正整数", "id", 1), S("name", "必填；Buff名称", "Name"), E<BuffType>("buffType", "Buff类型", "buffType"),
                I("durationTimes", "持续回合；正整数", "durationTimes", 1), I("maxStack", "最大层数；正整数", "maxStack", 1), E<StatType>("stat", "影响属性", "stat"),
                F("value", "属性倍率；允许负值；-0.2表示降低20%", "value"), B("isDebuff", "是否负面", "isDebuff")),
            new TableSchema("UnitSkills", S("unitId", "所属单位必须出现在 Units"), I("skillId", "技能ID；允许引用已有配置", null, 1),
                E<UnitSkillRole>("role", "Normal 普通技能列表 / TalentAction 天赋行动"), I("order", "Normal从0连续；TalentAction只能一条且为0"))
        }.Concat(GrowthConfigTables.Schemas).ToArray());
        public static TableSchema Get(string name) => All.Single(s => s.Name == name);
        public static string Text(object value) => value is float f ? f.ToString("R", CultureInfo.InvariantCulture) :
            value is bool b ? (b ? "TRUE" : "FALSE") : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        internal static ConfigRow FromObject(string sheet, object source)
        {
            var row = new ConfigRow { Sheet = sheet };
            foreach (var c in Get(sheet).Columns.Where(c => c.AssetField != null))
                row.Values[c.Name] = ReadField(source, c.AssetField) ?? (c.Type == typeof(string) ? "" : null);
            return row;
        }
        internal static void Assign(ConfigRow row, object target)
        {
            foreach (var c in Get(row.Sheet).Columns.Where(c => c.AssetField != null))
                WriteField(target, c.AssetField, row.Values[c.Name]);
        }

        internal static object ReadField(object source, string path)
        {
            foreach (string name in path.Split('.'))
                source = Field(source, name).GetValue(source);
            return source;
        }

        // Nested value types are boxed; write each modified child back to its parent.
        internal static void WriteField(object target, string path, object value)
        {
            int separator = path.IndexOf('.');
            var field = Field(target, separator < 0 ? path : path.Substring(0, separator));
            if (separator < 0) { field.SetValue(target, value); return; }
            object child = field.GetValue(target);
            WriteField(child, path.Substring(separator + 1), value);
            field.SetValue(target, child);
        }

        private static FieldInfo Field(object source, string name) =>
            source.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("配置字段不存在：" + source.GetType().Name + "." + name);

        internal static object CombatDefault(TableColumn column)
        {
            if (column.Name == "growthId") return "";
            const string prefix = "baseStats.offensiveStats.";
            return ReadField(Battle.Logic.BattleOffensiveStats.Default, column.AssetField.Substring(prefix.Length));
        }

        internal static void CompleteLegacyUnitColumns(ConfigTableData data, ConfigAssetIndex index)
        {
            foreach (var row in data.Rows("Units"))
                foreach (var column in Get("Units").Columns.Where(c => c.OptionalColumn))
                    if (!row.Values.ContainsKey(column.Name))
                        row.Values[column.Name] = index.Units.TryGetValue(row.Id, out var unit)
                            ? column.Name == "growthId" ? GrowthConfigTables.Id(unit.growth) : ReadField(unit, column.AssetField) : CombatDefault(column);
        }
    }
}
