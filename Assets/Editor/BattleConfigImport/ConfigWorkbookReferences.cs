using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using UnityEditor;

namespace Battle.Editor.ConfigImport
{
    internal static class ConfigWorkbookReferences
    {
        internal static void Enrich(string path, ConfigTableData data, ConfigAssetIndex index)
        {
            using var doc = SpreadsheetDocument.Open(path, true);
            Update(doc.WorkbookPart, data, index); doc.Save();
        }

        internal static IReadOnlyList<ImportMessage> Refresh(string path, string scope)
        {
            ConfigImportService.EnsureEditorReady();
            var messages = new List<ImportMessage>();
            var index = new ConfigAssetIndex(scope, messages);
            byte[] bytes = ConfigImportService.ReadWorkbookSnapshot(path);
            Directory.CreateDirectory("Temp/BattleConfigImport");
            string temp = "Temp/BattleConfigImport/references-" + Guid.NewGuid().ToString("N") + ".xlsx";
            try
            {
                File.WriteAllBytes(temp, bytes);
                var data = ConfigWorkbook.Read(temp, BattleTableSchema.All, messages);
                if (messages.Any(m => m.Kind == ImportMessageKind.Error)) return messages.AsReadOnly();
                Enrich(temp, data, index);
                ConfigWorkbookFiles.Publish(temp, path, ConfigImportService.Hash(bytes));
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
            return messages.AsReadOnly();
        }

        internal static void Update(WorkbookPart book, ConfigTableData data, ConfigAssetIndex index)
        {
            var names = book.Workbook.GetFirstChild<DefinedNames>();
            if (names == null) { names = new DefinedNames(); book.Workbook.AddChild(names, true); }
            foreach (string table in new[] { "Units", "Skills", "Buffs", "CharacterGrowth" })
            {
                string helper = "Ref" + table, rangeName = "Battle" + table + "Ids";
                var values = new Dictionary<string, object[]>(StringComparer.Ordinal);
                if (index != null)
                {
                    if (table == "CharacterGrowth") foreach (var p in index.Growths) values[p.Key] = new object[] { p.Key, p.Value.name, "SO", AssetDatabase.GetAssetPath(p.Value) };
                    if (table == "Units") foreach (var p in index.Units) values[p.Key] = new object[] { p.Key, p.Value.displayName, "SO", AssetDatabase.GetAssetPath(p.Value) };
                    if (table == "Skills") foreach (var p in index.Skills) values[p.Key.ToString()] = new object[] { p.Key, p.Value.Name, "SO", AssetDatabase.GetAssetPath(p.Value) };
                    if (table == "Buffs") foreach (var p in index.Buffs) values[p.Key.ToString()] = new object[] { p.Key, p.Value.Name, "SO", AssetDatabase.GetAssetPath(p.Value) };
                }
                string idField = table == "Units" ? "unitId" : table == "Skills" ? "skillId" : table == "Buffs" ? "buffId" : "growthId";
                foreach (var row in data.Rows(table))
                {
                    values.TryGetValue(row.Id, out var old);
                    string label = table != "CharacterGrowth" ? row.Get<string>("name") : old != null ? (string)old[1] : row.Id;
                    if (table == "CharacterGrowth")
                    {
                        var owners = data.Rows("Units").Where(r => r.Values.TryGetValue("growthId", out var id) && (string)id == row.Id)
                            .Select(r => r.Get<string>("name")).ToArray();
                        if (owners.Length > 0) label = string.Join(" / ", owners);
                    }
                    values[row.Id] = new object[] { row.Values[idField], label, "Workbook", old != null ? old[3] : "待导入创建" };
                }
                if (table == "Buffs") values["0"] = new object[] { 0, "无Buff引用", "保留值", "" };
                var sheet = book.Workbook.Sheets.Elements<Sheet>().FirstOrDefault(s => s.Name == helper);
                if (sheet != null) { book.DeletePart(sheet.Id); sheet.Remove(); }
                ConfigWorkbook.AddSheet(book, helper, new[] { "id", "name", "source", "assetPath" },
                    new[] { "实际填写的ID", "名称仅供查询，不参与匹配", "工作簿新配置优先", "Unity资源路径；此表仅辅助" },
                    values.OrderBy(v => v.Key, StringComparer.Ordinal).Select(v => v.Value), null, false);
                foreach (var n in names.Elements<DefinedName>().Where(n => n.Name == rangeName).ToArray()) n.Remove();
                names.Append(new DefinedName { Name = rangeName, Text = "'" + helper + "'!$A$3:$A$" + Math.Max(3, values.Count + 2) });
            }
            foreach (var schema in BattleTableSchema.All.Where(s => s.Name == "SkillEffects" || s.Name == "UnitSkills" || s.Name == "Units" || s.Name == "CharacterGrowthStages" || s.Name == "CharacterRanks" || s.Name == "SkillEffectGrowth"))
            {
                var sheet = book.Workbook.Sheets.Elements<Sheet>().FirstOrDefault(s => s.Name == schema.Name);
                if (sheet == null) continue;
                var ws = ((WorksheetPart)book.GetPartById(sheet.Id)).Worksheet;
                var validations = ws.GetFirstChild<DataValidations>();
                if (validations == null) { validations = new DataValidations(); ws.AddChild(validations, true); }
                foreach (var v in validations.Elements<DataValidation>().Where(v => (v.Formula1?.Text ?? "").StartsWith("Battle", StringComparison.Ordinal)).ToArray()) v.Remove();
                var header = ws.GetFirstChild<SheetData>().Elements<Row>().FirstOrDefault(r => r.RowIndex == 1);
                foreach (var cell in header?.Elements<Cell>() ?? Enumerable.Empty<Cell>())
                {
                    string field = CellText(book, cell);
                    string table = field == "unitId" ? "Units" : field == "skillId" ? "Skills" : field == "buffId" ? "Buffs" : field == "growthId" ? "CharacterGrowth" : null;
                    if (table == null) continue;
                    string col = ConfigWorkbook.Letters(cell.CellReference);
                    validations.Append(new DataValidation(new Formula1("Battle" + table + "Ids"))
                    { Type = DataValidationValues.List, AllowBlank = field == "growthId" && schema.Name == "Units", ShowErrorMessage = false, ShowInputMessage = true,
                        PromptTitle = "ID引用", Prompt = "名称见Ref" + table + "；允许手填新增ID。保存后用工作台刷新引用辅助表。",
                        SequenceOfReferences = new ListValue<StringValue> { InnerText = col + "3:" + col + "1048576" } });
                }
                validations.Count = (uint)validations.ChildElements.Count; ws.Save();
            }
            var metaSheet = book.Workbook.Sheets.Elements<Sheet>().FirstOrDefault(s => s.Name == "Meta");
            if (metaSheet != null)
            {
                var ws = ((WorksheetPart)book.GetPartById(metaSheet.Id)).Worksheet;
                var rows = ws.GetFirstChild<SheetData>();
                var additions = new Dictionary<string, string>
                {
                    ["referenceRefreshedAt"] = DateTimeOffset.Now.ToString("O"), ["authoringToolsVersion"] = "2"
                };
                // Older workbooks did not record project identity; do not invent their origin on refresh.
                foreach (var p in additions)
                {
                    var row = rows.Elements<Row>().FirstOrDefault(r => r.RowIndex >= 3 && CellText(book, r.Elements<Cell>().FirstOrDefault(c => ConfigWorkbook.Letters(c.CellReference) == "A")) == p.Key);
                    if (row == null)
                    {
                        uint number = rows.Elements<Row>().Select(r => r.RowIndex.Value).DefaultIfEmpty(2u).Max() + 1;
                        row = new Row { RowIndex = number }; rows.Append(row);
                        row.Append(new Cell { CellReference = "A" + number, DataType = CellValues.InlineString, InlineString = new InlineString(new Text(p.Key)) });
                    }
                    var cell = row.Elements<Cell>().FirstOrDefault(c => ConfigWorkbook.Letters(c.CellReference) == "B");
                    if (cell == null) { cell = new Cell { CellReference = "B" + row.RowIndex }; row.Append(cell); }
                    cell.RemoveAllChildren(); cell.DataType = CellValues.InlineString; cell.InlineString = new InlineString(new Text(p.Value));
                }
                ws.Save();
            }
            book.Workbook.Save();
        }

        internal static string CellText(WorkbookPart book, Cell cell)
        {
            if (cell == null) return "";
            if (cell.DataType?.Value == CellValues.SharedString) return book.SharedStringTablePart.SharedStringTable.Elements<SharedStringItem>().ElementAt(int.Parse(cell.CellValue.Text)).InnerText;
            return cell.InlineString?.InnerText ?? cell.CellValue?.Text ?? "";
        }
    }
}
