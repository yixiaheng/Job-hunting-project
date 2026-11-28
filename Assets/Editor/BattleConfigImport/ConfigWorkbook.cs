using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace Battle.Editor.ConfigImport
{
    public static class ConfigWorkbook
    {
        public const string Version = "2";
        public static ConfigTableData Read(string path, IReadOnlyList<TableSchema> schemas, List<ImportMessage> messages)
        {
            var data = new ConfigTableData();
            using var document = SpreadsheetDocument.Open(path, false);
            var book = document.WorkbookPart ?? throw new InvalidDataException("缺少 workbook.xml");
            var sheets = book.Workbook.Sheets.Elements<Sheet>().ToArray();
            string Value(Cell cell)
            {
                if (cell == null) return "";
                string raw = cell.CellValue?.Text ?? "";
                if (cell.DataType?.Value == CellValues.SharedString)
                    return book.SharedStringTablePart?.SharedStringTable.Elements<SharedStringItem>().ElementAt(int.Parse(raw)).InnerText ?? "";
                if (cell.DataType?.Value == CellValues.InlineString) return cell.InlineString?.InnerText ?? "";
                if (cell.DataType?.Value == CellValues.Boolean) return raw == "1" ? "TRUE" : "FALSE";
                return raw;
            }
            var meta = sheets.FirstOrDefault(s => s.Name == "Meta");
            if (meta == null) messages.Add(new(ImportMessageKind.Error, "Meta", 0, "", "", "缺少格式版本表；请重新导出模板。"));
            else
            {
                var cells = ((WorksheetPart)book.GetPartById(meta.Id)).Worksheet.Descendants<Cell>().ToArray();
                foreach (var row in ((WorksheetPart)book.GetPartById(meta.Id)).Worksheet.Descendants<Row>().Where(r => r.RowIndex >= 3))
                {
                    string key = Value(row.Elements<Cell>().FirstOrDefault(c => Letters(c.CellReference) == "A"));
                    if (key.Length > 0) data.Metadata[key] = Value(row.Elements<Cell>().FirstOrDefault(c => Letters(c.CellReference) == "B"));
                }
                if (Value(cells.FirstOrDefault(c => c.CellReference == "A3")) != "formatVersion" ||
                    (Value(cells.FirstOrDefault(c => c.CellReference == "B3")) != Version && Value(cells.FirstOrDefault(c => c.CellReference == "B3")) != "1"))
                    messages.Add(new(ImportMessageKind.Error, "Meta", 3, "B", "", "不支持的格式版本；使用当前工具导出的模板。"));
            }
            bool legacy = data.Metadata.TryGetValue("formatVersion", out string version) && version == "1";
            bool hasGrowth = sheets.Any(s => GrowthConfigTables.IsSheet(s.Name));
            if (legacy && !hasGrowth) messages.Add(new(ImportMessageKind.Warning, "Growth", 0, "", "", "旧版工作簿无成长表，保留现有成长规则；导出新版后可配置。"));
            foreach (var schema in schemas)
            {
                if (legacy && !hasGrowth && GrowthConfigTables.IsSheet(schema.Name)) continue;
                var matches = sheets.Where(s => s.Name == schema.Name).ToArray();
                if (matches.Length != 1)
                { messages.Add(new(ImportMessageKind.Error, schema.Name, 0, "", "", "必需表缺失或重复；缺表不能解释为清空。")); continue; }
                var ws = ((WorksheetPart)book.GetPartById(matches[0].Id)).Worksheet;
                if (ws.Descendants<MergeCell>().Any()) messages.Add(new(ImportMessageKind.Error, schema.Name, 0, "", "", "禁止合并单元格；请取消合并。"));
                var rows = ws.GetFirstChild<SheetData>()?.Elements<Row>().ToArray() ?? Array.Empty<Row>();
                var headers = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var c in rows.FirstOrDefault(r => r.RowIndex == 1)?.Elements<Cell>() ?? Enumerable.Empty<Cell>())
                {
                    string key = Value(c), col = Letters(c.CellReference);
                    if (key.Length == 0) continue;
                    if (c.CellFormula != null || headers.ContainsKey(key)) messages.Add(new(ImportMessageKind.Error, schema.Name, 1, col, "", "表头公式或重复字段：" + key));
                    else headers.Add(key, col);
                    if (!schema.Columns.Any(x => x.Name == key)) messages.Add(new(ImportMessageKind.Warning, schema.Name, 1, col, "", "额外列不参与导入：" + key));
                }
                foreach (var c in schema.Columns.Where(c => !headers.ContainsKey(c.Name)))
                    messages.Add(new(c.OptionalColumn ? ImportMessageKind.Warning : ImportMessageKind.Error, schema.Name, 1, c.Name, "",
                        c.OptionalColumn ? "旧表缺少新列：保留已有单位对应属性或成长引用，新单位使用默认值。重新导出可编辑完整配置。" : "缺少必需字段；请恢复表头。"));
                var output = data.MutableRows(schema.Name);
                foreach (var excelRow in rows.Where(r => r.RowIndex >= 3))
                {
                    var cells = excelRow.Elements<Cell>().ToDictionary(c => Letters(c.CellReference));
                    if (!cells.Values.Any(c => !string.IsNullOrEmpty(Value(c)) || c.CellFormula != null)) continue;
                    var row = new ConfigRow { Sheet = schema.Name, Number = (int)excelRow.RowIndex.Value };
                    bool valid = true;
                    foreach (var cell in cells.Values.Where(c => c.CellFormula != null && !schema.Columns.Any(s => headers.TryGetValue(s.Name, out string column) && column == Letters(c.CellReference))))
                    { valid = false; messages.Add(new(ImportMessageKind.Error, schema.Name, row.Number, Letters(cell.CellReference), "", "业务表额外列也不允许公式；请移到独立计算表。")); }
                    foreach (var c in schema.Columns)
                    {
                        if (!headers.TryGetValue(c.Name, out string col)) { if (!c.OptionalColumn) valid = false; continue; }
                        row.Locations[c.Name] = col;
                        cells.TryGetValue(col, out var cell);
                        string raw = Value(cell);
                        string error = null;
                        object parsed = null;
                        if (cell?.CellFormula != null) error = "业务数据不允许公式；请粘贴为值。";
                        else if (cell?.DataType?.Value == CellValues.Error) error = "Excel错误值；请填写确定值。";
                        else if (c.AllowEmpty && raw.Length == 0) parsed = "";
                        else if (string.IsNullOrWhiteSpace(raw)) error = "必填值为空；没有值的数字请显式填0。";
                        else if (c.Type == typeof(string))
                        {
                            parsed = raw;
                            if (raw != raw.Trim()) error = "首尾有空格；请删除空格，ID按精确值匹配。";
                        }
                        else if (c.Type == typeof(int))
                        {
                            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) || double.IsNaN(n) || double.IsInfinity(n) || n != Math.Truncate(n) || n > int.MaxValue || n < c.Minimum)
                                error = $"需要整数，范围 {c.Minimum} 至 {int.MaxValue}。";
                            else parsed = (int)n;
                        }
                        else if (c.Type == typeof(float))
                        {
                            if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float n) || float.IsNaN(n) || float.IsInfinity(n) || n < c.Minimum)
                                error = $"需要有限数值，最小值 {c.Minimum}；百分比请填底层小数。";
                            else parsed = n;
                        }
                        else if (c.Type == typeof(bool))
                        {
                            if (bool.TryParse(raw, out bool b)) parsed = b;
                            else if (raw == "1" || raw == "0") parsed = raw == "1";
                            else error = "请填写 TRUE 或 FALSE。";
                        }
                        else if (Enum.GetNames(c.Type).Contains(raw)) parsed = Enum.Parse(c.Type, raw);
                        else error = "未知枚举；可选：" + string.Join(", ", Enum.GetNames(c.Type));
                        if (error != null) { valid = false; messages.Add(row.Message(ImportMessageKind.Error, c.Name, c.Name + "：" + error)); }
                        else row.Values[c.Name] = parsed;
                    }
                    if (valid) output.Add(row);
                }
            }
            return data;
        }

        internal static string Letters(string reference) => new((reference ?? "").TakeWhile(char.IsLetter).ToArray());
        internal static string Column(int number)
        { string name = ""; for (; number > 0; number = (number - 1) / 26) name = (char)('A' + (number - 1) % 26) + name; return name; }

        public static void Write(string path, ConfigTableData data, IReadOnlyList<TableSchema> schemas)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string temp = path + ".writing";
            try
            {
                using (var doc = SpreadsheetDocument.Create(temp, SpreadsheetDocumentType.Workbook))
                {
                    var book = doc.AddWorkbookPart();
                    book.Workbook = new Workbook(new Sheets());
                    var styles = book.AddNewPart<WorkbookStylesPart>();
                    styles.Stylesheet = Styles();
                    AddSheet(book, "Instructions", new[] { "topic", "description" }, new[] { "主题", "填写规则" }, new object[][]
                    {
                        new object[] { "流程", "编辑并保存 → 校验与预览 → 检查差异 → 应用导入；下次导入以Excel管理字段为准。" },
                        new object[] { "表头", "第1行字段名不可重命名；第2行中文说明；第3行起数据。允许排序、换列、隐藏；隐藏行仍导入。" },
                        new object[] { "数值", "业务表不允许公式或合并单元格。0.2代表20%；允许百分比格式。计算后粘贴为值。" },
                        new object[] { "身份", "ID是稳定关联，不是名称。改ID视为新增，旧资产保留；涉及存档和技能表现映射。" },
                        new object[] { "完整子列表", "Units对应的UnitSkills、Skills对应的SkillEffects为完整替换；缺子行表示清空。未列主体不修改。" },
                        new object[] { "资源", "模型、头像与技能演出在Unity配置。Units.growthId绑定CharacterGrowth；基础属性不等于养成后属性。" },
                        new object[] { "基础成长", "Units的生命/攻击/防御为1级值，允许小数，写入单位独占的BaseStats子资源。玩家按JSON等级与突破阶段查全局成长表；速度、双暴、元素/击破不自然增长。敌人暂直接读取基础值。" },
                        new object[] { "成长", "GrowthStages有行时完整替换全局七阶段；空表保留。CharacterGrowth出现的主体完整替换其阶段/升阶/效果成长子表。旧v1缺全部成长表时保留原配置。" },
                        new object[] { "顺序", "order从0连续；排序Excel行不影响执行顺序。效果下标关联命中标记，改动时请复查动画。" },
                        new object[] { "普攻", "保留现有规则：零消耗普攻skillPointGain=0时仍可能默认恢复1点战技点。" },
                        new object[] { "来源", "由当前Unity项目SO导出；索引范围和生成时间见Meta。额外计算工作表不参与导入。" }
                    }, null, true);
                    foreach (var schema in schemas)
                        AddSheet(book, schema.Name, schema.Columns.Select(c => c.Name).ToArray(), schema.Columns.Select(c => c.Description).ToArray(),
                            data.Rows(schema.Name).Select(r => schema.Columns.Select(c => r.Values[c.Name]).ToArray()), schema, false);
                    var enums = schemas.SelectMany(s => s.Columns).Where(c => c.Type.IsEnum).Select(c => c.Type).Distinct();
                    AddSheet(book, "Enums", new[] { "type", "code", "description" }, new[] { "枚举类型", "表格使用代码", "中文含义" },
                        enums.SelectMany(t => Enum.GetNames(t).Select(n => new object[] { t.Name, n, EnumDescription(t, n) })), null, true);
                    var metadata = new Dictionary<string, string>(data.Metadata);
                    metadata["formatVersion"] = Version;
                    metadata["exportedAt"] = DateTimeOffset.Now.ToString("O");
                    metadata["source"] = "Unity ScriptableObject / " + ConfigImportService.ExportScope;
                    metadata["reader"] = "DocumentFormat.OpenXml 3.5.1";
                    metadata["project"] = UnityEngine.Application.productName;
                    metadata["projectId"] = UnityEditor.PlayerSettings.productGUID.ToString();
                    metadata["scope"] = ConfigImportService.ExportScope;
                    if (!metadata.ContainsKey("workbookId")) metadata["workbookId"] = Guid.NewGuid().ToString("N");
                    AddSheet(book, "Meta", new[] { "key", "value" }, new[] { "格式元数据", "不要修改formatVersion" },
                        new[] { new object[] { "formatVersion", Version } }.Concat(metadata.Where(p => p.Key != "formatVersion").Select(p => new object[] { p.Key, p.Value })), null, true);
                    ConfigWorkbookReferences.Update(book, data, null);
                    book.Workbook.Save();
                }
                if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }

        internal static void AddSheet(WorkbookPart book, string name, string[] headers, string[] descriptions, IEnumerable<object[]> rows, TableSchema schema, bool prose)
        {
            var part = book.AddNewPart<WorksheetPart>();
            var data = new SheetData();
            var views = new SheetViews(new SheetView(new Pane { HorizontalSplit = 1, VerticalSplit = 2, TopLeftCell = "B3", ActivePane = PaneValues.BottomRight, State = PaneStateValues.Frozen })
                { WorkbookViewId = 0, ShowGridLines = false });
            var cols = new Columns();
            for (int i = 0; i < headers.Length; i++) cols.Append(new Column { Min = (uint)i + 1, Max = (uint)i + 1,
                Width = name.StartsWith("Ref", StringComparison.Ordinal) ? (i == 3 ? 100 : i == 1 ? 38 : name == "RefCharacterGrowth" && i == 0 ? 44 : 25) : name == "Enums" ? (i == 2 ? 32 : 25) : prose ? (i == 0 ? 27 : 105) : headers[i] == "effectKey" ? 48 : headers[i] == "growthId" ? 44 : Math.Max(19, Math.Min(38, headers[i].Length + 7)), CustomWidth = true });
            part.Worksheet = new Worksheet(views, new SheetFormatProperties { DefaultRowHeight = 22 }, cols, data);
            void Row(object[] values, uint index, uint style, double height)
            {
                var row = new Row { RowIndex = index, Height = height, CustomHeight = true };
                for (int i = 0; i < values.Length; i++)
                {
                    var cell = new Cell { CellReference = Column(i + 1) + index, StyleIndex = style };
                    object v = values[i];
                    if (v is int || v is float) { cell.DataType = CellValues.Number; cell.CellValue = new CellValue(BattleTableSchema.Text(v)); }
                    else { cell.DataType = CellValues.InlineString; cell.InlineString = new InlineString(new Text(BattleTableSchema.Text(v)) { Space = SpaceProcessingModeValues.Preserve }); }
                    row.Append(cell);
                }
                data.Append(row);
            }
            Row(headers.Cast<object>().ToArray(), 1, 1, 26);
            Row(descriptions.Cast<object>().ToArray(), 2, 2, prose ? 30 : 64);
            uint r = 3;
            foreach (var values in rows) Row(values, r++, prose ? 2u : 0u, prose && name != "Enums" ? 42 : 24);
            part.Worksheet.Append(new AutoFilter { Reference = "A2:" + Column(headers.Length) + Math.Max(3, r - 1) });
            if (schema != null)
            {
                var validations = new DataValidations();
                for (int i = 0; i < schema.Columns.Count; i++)
                {
                    var t = schema.Columns[i].Type;
                    if (!t.IsEnum && t != typeof(bool)) continue;
                    string choices = t == typeof(bool) ? "TRUE,FALSE" : string.Join(",", Enum.GetNames(t));
                    validations.Append(new DataValidation(new Formula1("\"" + choices + "\""))
                    { Type = DataValidationValues.List, AllowBlank = false, ShowErrorMessage = true, ErrorTitle = "无效选项", Error = "请从下拉列表选择代码。",
                        SequenceOfReferences = new ListValue<StringValue> { InnerText = Column(i + 1) + "3:" + Column(i + 1) + "1048576" } });
                }
                validations.Count = (uint)validations.ChildElements.Count;
                if (validations.Count > 0) part.Worksheet.Append(validations);
            }
            part.Worksheet.Save();
            book.Workbook.Sheets.Append(new Sheet { Name = name, Id = book.GetIdOfPart(part), SheetId = book.Workbook.Sheets.Elements<Sheet>().Select(s => s.SheetId.Value).DefaultIfEmpty(0u).Max() + 1 });
        }

        private static Stylesheet Styles() => new(
            new Fonts(new Font { FontSize = new FontSize { Val = 11 }, FontName = new FontName { Val = "Microsoft YaHei" }, Color = new Color { Rgb = "FF243247" } },
                new Font { FontSize = new FontSize { Val = 11 }, FontName = new FontName { Val = "Microsoft YaHei" }, Bold = new Bold(), Color = new Color { Rgb = "FFFFFFFF" } }),
            new Fills(new Fill(new PatternFill { PatternType = PatternValues.None }), new Fill(new PatternFill { PatternType = PatternValues.Gray125 }),
                new Fill(new PatternFill(new ForegroundColor { Rgb = "FF253B58" }) { PatternType = PatternValues.Solid }),
                new Fill(new PatternFill(new ForegroundColor { Rgb = "FFEAF0F6" }) { PatternType = PatternValues.Solid })),
            new Borders(new Border()), new CellStyleFormats(new CellFormat()),
            new CellFormats(new CellFormat(new Alignment { Vertical = VerticalAlignmentValues.Center }) { FontId = 0, FillId = 0, BorderId = 0, FormatId = 0 },
                new CellFormat(new Alignment { Vertical = VerticalAlignmentValues.Center, Horizontal = HorizontalAlignmentValues.Center }) { FontId = 1, FillId = 2, BorderId = 0, ApplyFill = true, ApplyFont = true },
                new CellFormat(new Alignment { Vertical = VerticalAlignmentValues.Center, WrapText = true }) { FontId = 0, FillId = 3, BorderId = 0, ApplyFill = true }),
            new CellStyles(new CellStyle { Name = "Normal", FormatId = 0, BuiltinId = 0 }));

        private static string EnumDescription(Type type, string code)
        {
            var map = new Dictionary<string, string>
            {
                ["Ally"]="我方角色", ["Enemy"]="敌人", ["Normal"]="普通技能列表", ["TalentAction"]="天赋行动",
                ["None"]="无天赋", ["Aegis"]="岩壁守卫天赋", ["Verdant"]="春风医者天赋", ["VoltHunt"]="雷鸣猎手天赋", ["FrostNova"]="寒星术士天赋", ["UmbralHex"]="暮影诅咒师天赋", ["AstralChord"]="星弦指挥家天赋",
                ["Fire"]="火", ["Ice"]="冰", ["Lightning"]="雷", ["Wind"]="风", ["Quantum"]="量子", ["Imaginary"]="虚数", ["Physical"]="物理",
                ["Preservation"]="存护", ["Abundance"]="丰饶", ["Hunt"]="巡猎", ["Erudition"]="智识", ["Nihility"]="虚无", ["Harmony"]="同谐",
                ["Basic"]="普通攻击", ["Skill"]="战技", ["Ultimate"]="终结技", ["OurSide"]="施法者一方", ["OtherSide"]="施法者对面",
                ["Single"]="单体", ["Spread"]="扩散", ["AoE"]="全体", ["Self"]="自身", ["ActiveSkill"]="主动技能", ["Talent"]="天赋", ["Equipment"]="装备",
                ["Damage"]="伤害", ["Heal"]="治疗", ["AddShield"]="添加护盾", ["ApplyBuff"]="施加Buff", ["GainEnergy"]="回复能量", ["AdvanceAction"]="行动提前",
                ["RemoveDeBuff"]="移除负面Buff", ["RemoveBuff"]="移除Buff", ["GainSkillPoint"]="回复战技点", ["Attack"]="攻击", ["Health"]="生命", ["Speed"]="速度", ["Defence"]="防御", ["StatMod"]="属性修改"
            };
            return map.TryGetValue(code, out string s) ? s : type.Name + "/" + code;
        }
    }
}
