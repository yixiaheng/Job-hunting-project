using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Battle.Editor.ConfigImport
{
    public sealed class BattleConfigImportWindow : EditorWindow
    {
        [SerializeField] private string workbook = ConfigImportService.DefaultWorkbook;
        [SerializeField] private string scope = "Assets/Config";
        [SerializeField] private int filter;
        private ConfigImportPlan plan;
        private IReadOnlyList<ImportMessage> messages = Array.Empty<ImportMessage>();
        private Vector2 scroll;
        private ConfigAssetIndex lookupIndex;
        private string search = "", pending;
        private double nextRecoveryScan;
        private bool showLookup;
        private string status = "选择工作簿后先校验与预览。所有表格管理字段：下次导入以Excel为准。";
        private static readonly string[] Filters = { "全部", "错误", "警告", "新增", "修改", "无变化" };

        [MenuItem("Tools/Battle/配置表工作台")]
        public static void Open() => GetWindow<BattleConfigImportWindow>("战斗配置表");
        private void OnEnable() { minSize = new Vector2(820, 560); nextRecoveryScan = 0; }
        private void OnGUI()
        {
            EditorGUILayout.LabelField("战斗配置 Excel 工作台", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Excel → 校验与差异预览 → 原位更新SO。可保持Excel/WPS打开，但请先保存；只读取磁盘上的已保存内容。模型、头像、成长和演出引用由Unity管理。修改ID视为新增。", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            using (new EditorGUILayout.HorizontalScope())
            {
                workbook = EditorGUILayout.TextField("工作簿 .xlsx", workbook);
                if (GUILayout.Button("选择文件", GUILayout.Width(80)))
                {
                    string selected = EditorUtility.OpenFilePanel("选择战斗配置表", Path.GetDirectoryName(Path.GetFullPath(workbook)), "xlsx");
                    if (!string.IsNullOrEmpty(selected)) { workbook = selected; plan = null; }
                }
            }
            scope = EditorGUILayout.TextField("配置索引范围", scope);
            if (EditorGUI.EndChangeCheck()) { plan = null; lookupIndex = null; }
            bool busy = EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating;
            using (new EditorGUI.DisabledScope(busy))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("导出现有配置")) Export(false);
                    if (GUILayout.Button("生成空模板")) Export(true);
                    if (GUILayout.Button("校验与预览")) Execute(() =>
                    {
                        plan = ConfigImportService.Preview(workbook, scope); messages = plan.Messages;
                        lookupIndex = plan.Index;
                        status = plan.IsValid ? $"校验通过：{plan.Updates.Count}个资产需要更新。请检查下面的字段前后值和警告。" : "校验失败；未写入业务资产。请按表、行、列修正后重新预览。";
                    });
                    using (new EditorGUI.DisabledScope(plan == null || !plan.IsValid))
                        if (GUILayout.Button("应用导入")) Execute(() =>
                        {
                            bool confirmed = !plan.RequiresClearConfirmation || EditorUtility.DisplayDialog("确认清空已有列表", "以下资产将清空技能列表、天赋行动或效果列表；不是仅删除一行显示：\n" +
                                string.Join("\n", plan.Updates.Where(u => u.RequiresClearConfirmation).Select(u => u.Path)), "确认清空并导入", "返回检查");
                            if (!confirmed) return;
                            string result = ConfigImportService.Apply(plan, confirmed);
                            messages = Array.Empty<ImportMessage>();
                            scroll = Vector2.zero;
                            filter = 0;
                            plan = null;
                            status = "导入完成，预览日志已清空。详细记录可通过“查看最近报告”查看。结果：" + result;
                            lookupIndex = new ConfigAssetIndex(scope, new List<ImportMessage>());
                        });
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("刷新工作簿引用辅助表"))
                    {
                        if (EditorUtility.DisplayDialog("刷新引用辅助表", "保存并关闭Excel/WPS中的该工作簿后操作。仅更新RefUnits/RefSkills/RefBuffs及关联下拉，保留业务单元格与额外计算表；旧文件自动备份。", "刷新", "取消"))
                            Execute(() => { messages = ConfigWorkbookReferences.Refresh(workbook, scope); plan = null; status = messages.Any(m => m.Kind == ImportMessageKind.Error) ? "刷新失败，请检查错误。" : "引用辅助表已刷新，包含工作簿中新增的配置。"; });
                    }
                    if (GUILayout.Button("配置模板与批量复制")) ConfigBatchAuthoringWindow.Open(workbook, scope);
                    if (GUILayout.Button("导入历史"))
                    { Directory.CreateDirectory(ConfigImportService.BackupRoot + "/History"); EditorUtility.RevealInFinder(Path.GetFullPath(ConfigImportService.BackupRoot + "/History")); }
                }
            }
            if (busy) EditorGUILayout.HelpBox("Play Mode、编译或资源导入期间不可操作。", MessageType.Warning);
            if (EditorApplication.timeSinceStartup >= nextRecoveryScan) { pending = ConfigImportService.FindPendingBackup(); nextRecoveryScan = EditorApplication.timeSinceStartup + 5; }
            if (pending != null) EditorGUILayout.HelpBox("检测到未完成导入，请恢复：" + pending, MessageType.Error);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("查看最近报告")) { if (File.Exists(ConfigImportService.LastReport)) EditorUtility.RevealInFinder(Path.GetFullPath(ConfigImportService.LastReport)); else status = "尚无导入报告。"; }
                if (GUILayout.Button("打开备份目录")) { Directory.CreateDirectory(ConfigImportService.BackupRoot); EditorUtility.RevealInFinder(Path.GetFullPath(ConfigImportService.BackupRoot)); }
                using (new EditorGUI.DisabledScope(busy))
                    if (GUILayout.Button(pending != null ? "恢复未完成导入" : "恢复最近备份"))
                    {
                        string folder = pending ?? ConfigImportService.LatestBackup();
                        if (folder == null) status = "尚无备份。";
                        else if (EditorUtility.DisplayDialog("恢复备份", "将恢复该次导入修改过的资产并删除该次新建资产。此后对这些资产的修改也会被覆盖。\n" + folder, "恢复", "取消"))
                            Execute(() => { ConfigImportService.Restore(folder); plan = null; status = "已恢复：" + folder; });
                    }
                if (GUILayout.Button("使用文档")) EditorUtility.RevealInFinder(Path.GetFullPath("Docs/BattleConfigExcelGuide.md"));
            }
            EditorGUILayout.HelpBox(status, MessageType.None);
            if (plan != null && plan.Metadata.Count > 0)
                EditorGUILayout.LabelField("来源：" + string.Join(" | ", plan.Metadata.Where(p => p.Key == "project" || p.Key == "scope" || p.Key == "formatVersion" || p.Key == "exportedAt").Select(p => p.Key + "=" + p.Value)), EditorStyles.wordWrappedLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                showLookup = EditorGUILayout.ToggleLeft("ID / 名称 / 路径查询", showLookup, GUILayout.Width(160));
                search = EditorGUILayout.TextField(search);
                if (GUILayout.Button("刷新资产索引", GUILayout.Width(110))) Execute(() => { var m = new List<ImportMessage>(); lookupIndex = new ConfigAssetIndex(scope, m); if (m.Count > 0) messages = m; });
            }
            filter = GUILayout.Toolbar(filter, Filters);
            EditorGUILayout.LabelField($"错误 {messages.Count(m => m.Kind == ImportMessageKind.Error)} / 警告 {messages.Count(m => m.Kind == ImportMessageKind.Warning)} / 新增 {messages.Count(m => m.Kind == ImportMessageKind.Added)} / 修改 {messages.Count(m => m.Kind == ImportMessageKind.Modified)} / 无变化 {messages.Count(m => m.Kind == ImportMessageKind.Unchanged)}");
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (showLookup) DrawLookup();
            foreach (var message in messages.Where(m => (filter == 0 || (int)m.Kind == filter - 1) && Matches(m.ToString())))
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField($"{Filters[(int)message.Kind + 1]} | {message.Sheet} | {message.Column}{(message.Row > 0 ? message.Row.ToString() : "")} | ID: {message.Id}", EditorStyles.boldLabel);
                    GUILayout.Label(message.Detail, EditorStyles.wordWrappedLabel);
                    var asset = ConfigAuthoringSupport.Resolve(lookupIndex ?? plan?.Index, message.Sheet, message.Id);
                    if (asset != null && GUILayout.Button("定位资源：" + asset.name, GUILayout.Width(300))) { Selection.activeObject = asset; EditorGUIUtility.PingObject(asset); }
                }
            }
            EditorGUILayout.EndScrollView();
        }
        private void Export(bool empty)
        {
            string output = EditorUtility.SaveFilePanel(empty ? "生成空模板" : "导出现有配置", Path.GetDirectoryName(Path.GetFullPath(workbook)), empty ? "BattleConfigTemplate" : Path.GetFileNameWithoutExtension(workbook), "xlsx");
            if (string.IsNullOrEmpty(output)) return;
            if (File.Exists(output) && !EditorUtility.DisplayDialog("覆盖工作簿", "导出将以当前SO重建工作簿，未导入的Excel修改和额外计算表不会合并。旧文件将备份到旁边的.BattleWorkbookBackups目录。\n" + output, "备份并覆盖", "取消")) return;
            Execute(() =>
            {
                messages = ConfigImportService.Export(output, scope, empty); plan = null;
                bool errors = messages.Any(m => m.Kind == ImportMessageKind.Error);
                status = errors ? "导出失败；先修复资源身份或字段问题。" : "已导出：" + output;
                if (!errors && !empty) workbook = output;
            });
        }
        private void Execute(Action action)
        {
            try { action(); nextRecoveryScan = 0; }
            catch (Exception error) { status = error.Message; plan = null; Debug.LogException(error); }
        }
        private bool Matches(string value) => string.IsNullOrWhiteSpace(search) || (value ?? "").IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        private void DrawLookup()
        {
            if (lookupIndex == null) { EditorGUILayout.HelpBox("请先校验与预览或刷新资产索引。", MessageType.Info); return; }
            var entries = lookupIndex.Units.Select(p => (Id: "Units/" + p.Key, Name: p.Value.displayName, Asset: (UnityEngine.Object)p.Value))
                .Concat(lookupIndex.Skills.Select(p => (Id: "Skills/" + p.Key, Name: p.Value.Name, Asset: (UnityEngine.Object)p.Value)))
                .Concat(lookupIndex.Buffs.Select(p => (Id: "Buffs/" + p.Key, Name: p.Value.Name, Asset: (UnityEngine.Object)p.Value)));
            foreach (var e in entries.Where(e => Matches(e.Id + " " + e.Name + " " + AssetDatabase.GetAssetPath(e.Asset))).Take(100))
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.SelectableLabel(e.Id + " · " + e.Name, GUILayout.Height(18));
                    if (GUILayout.Button("复制ID", GUILayout.Width(65))) EditorGUIUtility.systemCopyBuffer = e.Id.Substring(e.Id.IndexOf('/') + 1);
                    if (GUILayout.Button("定位", GUILayout.Width(50))) { Selection.activeObject = e.Asset; EditorGUIUtility.PingObject(e.Asset); }
                }
            if (plan != null) foreach (var u in plan.Updates.Where(u => u.IsNew && u.Row != null && Matches(u.Key + " " + u.Row.Get<string>("name"))))
                EditorGUILayout.LabelField("待创建 " + u.Key + " · " + u.Row.Get<string>("name"));
            EditorGUILayout.LabelField("资产查询最多显示100条；输入名称或ID缩小范围。", EditorStyles.miniLabel);
        }
    }
}
