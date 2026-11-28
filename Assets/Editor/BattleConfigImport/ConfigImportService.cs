using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Battle.Editor.ConfigImport
{
    public sealed class ConfigImportPlan
    {
        public string WorkbookPath { get; internal set; }
        public string Scope { get; internal set; }
        public IReadOnlyList<ImportMessage> Messages { get; internal set; }
        public IReadOnlyList<ConfigAssetUpdate> Updates { get; internal set; }
        public bool IsValid => !Messages.Any(m => m.Kind == ImportMessageKind.Error);
        public bool RequiresClearConfirmation => Updates.Any(u => u.RequiresClearConfirmation);
        public IReadOnlyDictionary<string, string> Metadata { get; internal set; } = new Dictionary<string, string>();
        internal string WorkbookHash, AssetHash;
        internal ConfigAssetIndex Index;
    }

    public static class ConfigImportService
    {
        public const string DefaultWorkbook = "ConfigTables/战斗配置.xlsx";
        public const string BackupRoot = "ConfigImportBackups";
        public const string LastReport = "ConfigImportBackups/LatestReport.txt";
        internal static string ExportScope = "Assets/Config";
        internal static Action<int> AfterWriteForVerification;
        public static IConfigTableAdapter Adapter { get; } = new BattleConfigTableAdapter();
        public static string Hash(byte[] bytes) { using var hash = SHA256.Create(); return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        public static string FileHash(string path) => File.Exists(path) ? Hash(File.ReadAllBytes(path)) : "missing";

        // Excel/WPS commonly keep a read/write handle with shared reads. Our reader must
        // permit that existing writer; parsing and backups use the resulting private snapshot.
        internal static byte[] ReadWorkbookSnapshot(string path)
        {
            byte[] ReadShared()
            {
                using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var output = new MemoryStream();
                input.CopyTo(output);
                return output.ToArray();
            }
            try
            {
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    byte[] first = ReadShared(), second = ReadShared();
                    if (first.Length == second.Length && Hash(first) == Hash(second)) return second;
                }
            }
            catch (IOException e)
            {
                throw new IOException("无法读取工作簿。请先在Excel/WPS中保存，等待保存完成后重新预览；若仍被独占锁定，请关闭该工作簿后重试。路径：" + path, e);
            }
            throw new IOException("工作簿正在变化，未读取到稳定的已保存内容；请等待Excel/WPS保存完成后重新预览。路径：" + path);
        }

        public static ConfigImportPlan Preview(string path, string scope, IConfigTableAdapter adapter = null)
        {
            adapter ??= Adapter;
            var messages = new List<ImportMessage>();
            var plan = new ConfigImportPlan { WorkbookPath = Path.GetFullPath(path), Scope = scope, Updates = Array.Empty<ConfigAssetUpdate>() };
            try
            {
                EnsureEditorReady();
                var index = new ConfigAssetIndex(scope, messages); plan.Index = index;
                plan.AssetHash = index.Fingerprint();
                byte[] bytes = ReadWorkbookSnapshot(path);
                plan.WorkbookHash = Hash(bytes);
                // Parse an immutable copy, so editing the workbook during preview cannot mix versions.
                Directory.CreateDirectory("Temp/BattleConfigImport");
                string copy = "Temp/BattleConfigImport/preview-" + Guid.NewGuid().ToString("N") + ".xlsx";
                ConfigTableData data;
                try { File.WriteAllBytes(copy, bytes); data = adapter.Parse(copy, messages); }
                finally { if (File.Exists(copy)) File.Delete(copy); }
                plan.Metadata = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(data.Metadata);
                if (data.Metadata.TryGetValue("projectId", out string project) && project != PlayerSettings.productGUID.ToString())
                    messages.Add(new(ImportMessageKind.Warning, "Meta", 0, "projectId", "", "工作簿来自不同项目；请核对ID，不会按名称猜测绑定。"));
                if (data.Metadata.TryGetValue("assetBaseline", out string baseline) && baseline != plan.AssetHash)
                    messages.Add(new(ImportMessageKind.Warning, "Meta", 0, "assetBaseline", "", "SO与导出时的基线不同；请逐项审阅差异，Excel管理字段将覆盖当前值。"));
                adapter.Validate(data, index, messages);
                if (!messages.Any(m => m.Kind == ImportMessageKind.Error)) plan.Updates = adapter.BuildUpdates(data, index, messages);
                foreach (var update in plan.Updates.Where(u => u.Existing != null && UnitBaseStatsAuthoring.HasDirtyObjects(u.Existing)))
                    messages.Add(new(ImportMessageKind.Error, "Asset", 0, "", update.Path, "受影响资产有未保存编辑；请先保存或撤销，再重新预览。"));
            }
            catch (Exception e) { messages.Add(new(ImportMessageKind.Error, "Workbook", 0, "", path, e.Message)); }
            plan.Messages = messages.AsReadOnly();
            return plan;
        }

        public static IReadOnlyList<ImportMessage> Export(string path, string scope, bool empty, IConfigTableAdapter adapter = null)
        {
            EnsureEditorReady(); adapter ??= Adapter;
            var messages = new List<ImportMessage>();
            // An empty template must remain available even while existing asset IDs need repair.
            ConfigAssetIndex index = empty ? null : new ConfigAssetIndex(scope, messages);
            ConfigTableData data = empty ? new() : adapter.Export(index, messages);
            if (messages.Any(m => m.Kind == ImportMessageKind.Error)) return messages.AsReadOnly();
            ExportScope = scope;
            if (index != null) data.Metadata["assetBaseline"] = index.Fingerprint();
            string expectedHash = ConfigWorkbookFiles.CurrentHash(path);
            // Validate the actual serialized cells too (ranges, enum values, empty fields).
            Directory.CreateDirectory("Temp/BattleConfigImport");
            string check = "Temp/BattleConfigImport/export-" + Guid.NewGuid().ToString("N") + ".xlsx";
            try
            {
                ConfigWorkbook.Write(check, data, adapter.Schemas);
                ConfigWorkbookReferences.Enrich(check, data, index);
                var parsed = adapter.Parse(check, messages); if (!empty) adapter.Validate(parsed, index, messages);
                if (!messages.Any(m => m.Kind == ImportMessageKind.Error))
                { ConfigWorkbookFiles.Publish(check, path, expectedHash); }
            }
            finally { if (File.Exists(check)) File.Delete(check); }
            return messages.AsReadOnly();
        }

        internal static void EnsureEditorReady()
            => ValidateEditorState(EditorApplication.isPlayingOrWillChangePlaymode, EditorApplication.isCompiling, EditorApplication.isUpdating);
        internal static void ValidateEditorState(bool playing, bool compiling, bool updating)
        {
            if (playing) throw new InvalidOperationException("运行/即将运行时不能导入；请退出Play Mode。");
            if (compiling || updating) throw new InvalidOperationException("Unity正在编译或导入；完成后重新预览。");
        }

        public static string Apply(ConfigImportPlan plan, bool confirmClear = false)
        {
            EnsureEditorReady();
            if (plan == null || !plan.IsValid) throw new InvalidOperationException("必须先完成无错误的校验与预览。");
            if (plan.RequiresClearConfirmation && !confirmClear) throw new InvalidOperationException("本次将清空现有子列表；请检查清空差异并明确确认。");
            if (FindPendingBackup() != null) throw new InvalidOperationException("存在未完成导入，请先恢复上次备份。");
            byte[] workbookBytes = ReadWorkbookSnapshot(plan.WorkbookPath);
            if (Hash(workbookBytes) != plan.WorkbookHash) throw new InvalidOperationException("工作簿已变化；请重新预览。");
            var diagnostics = new List<ImportMessage>();
            var current = new ConfigAssetIndex(plan.Scope, diagnostics);
            if (diagnostics.Any(m => m.Kind == ImportMessageKind.Error) || current.Fingerprint() != plan.AssetHash)
                throw new InvalidOperationException("配置或依赖资产已变化；请重新预览。");
            foreach (var u in plan.Updates)
            {
                if (u.Existing != null && UnitBaseStatsAuthoring.HasDirtyObjects(u.Existing)) throw new InvalidOperationException("存在未保存编辑：" + u.Path);
                if (u.IsNew && (File.Exists(u.Path) || File.Exists(u.Path + ".meta"))) throw new InvalidOperationException("新资源路径已被占用；请重新预览：" + u.Path);
            }
            if (plan.Updates.Count == 0) { WriteReport("无变化：未写入、未保存任何资产。", plan); return LastReport; }
            string folder = BackupRoot + "/" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            Directory.CreateDirectory(folder);
            var manifest = new BackupManifest { state = "Prepared", workbook = plan.WorkbookPath, workbookHash = plan.WorkbookHash };
            foreach (var u in plan.Updates)
            {
                manifest.assets.Add(new BackupAsset { path = u.Path, existed = !u.IsNew,
                    guid = u.IsNew ? "" : AssetDatabase.AssetPathToGUID(u.Path),
                    fileHash = FileHash(u.Path), metaHash = FileHash(u.Path + ".meta") });
                if (!u.IsNew)
                {
                    string dest = folder + "/files/" + u.Path;
                    Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    File.Copy(u.Path, dest); File.Copy(u.Path + ".meta", dest + ".meta");
                }
                string dir = Path.GetDirectoryName(u.Path).Replace('\\', '/');
                while (!Directory.Exists(dir))
                { if (!manifest.createdFolders.Contains(dir)) manifest.createdFolders.Add(dir); dir = Path.GetDirectoryName(dir).Replace('\\', '/'); }
            }
            // Back up exactly the validated version, not a later save or a separately opened locked file.
            File.WriteAllBytes(folder + "/Source.xlsx", workbookBytes);
            File.WriteAllText(folder + "/Plan.txt", string.Join("\n\n", plan.Messages));
            SaveManifest(folder, manifest); // durable before the first mutation
            try
            {
                var map = new Dictionary<string, Object>(StringComparer.Ordinal);
                foreach (var p in current.Buffs) map["Buffs/" + p.Key] = p.Value;
                foreach (var p in current.Skills) map["Skills/" + p.Key] = p.Value;
                foreach (var p in current.Growths) map["Growth/" + p.Key] = p.Value;
                foreach (var p in current.Units) map["Units/" + p.Key] = p.Value;
                foreach (var u in plan.Updates.Where(u => u.IsNew))
                {
                    EnsureAssetFolder(Path.GetDirectoryName(u.Path).Replace('\\', '/'));
                    var asset = ScriptableObject.CreateInstance(u.AssetType);
                    AssetDatabase.CreateAsset(asset, u.Path);
                    map[u.Key] = asset;
                    manifest.assets.Single(a => a.path == u.Path).guid = AssetDatabase.AssetPathToGUID(u.Path);
                    SaveManifest(folder, manifest);
                }
                int writes = 0;
                foreach (var u in plan.Updates)
                {
                    Object asset = u.IsNew ? map[u.Key] : u.Existing;
                    if (u.CustomApply != null) u.CustomApply(map);
                    else
                    {
                        if (asset is UnitConfig owner) UnitBaseStatsAuthoring.Ensure(owner);
                        BattleTableSchema.Assign(u.Row, asset);
                        if (asset is SkillConfig skill)
                        {
                            skill.effectConfigs = u.Children.Select(row =>
                            {
                                var e = new EffectConfig(); BattleTableSchema.Assign(row, e);
                                e.buff = e.buffId == 0 ? null : (BuffConfig)map["Buffs/" + e.buffId];
                                return e;
                            }).ToList();
                        }
                        if (asset is UnitConfig unit)
                        {
                            string growthId = u.Row.Get<string>("growthId");
                            unit.growth = growthId.Length == 0 ? null : (Game.Characters.CharacterGrowthConfig)map["Growth/" + growthId];
                            unit.skills = u.Children.Where(r => r.Get<UnitSkillRole>("role") == UnitSkillRole.Normal).Select(r => (SkillConfig)map["Skills/" + r.Get<int>("skillId")]).ToList();
                            var talent = u.Children.FirstOrDefault(r => r.Get<UnitSkillRole>("role") == UnitSkillRole.TalentAction);
                            unit.talentAction = talent == null ? null : (SkillConfig)map["Skills/" + talent.Get<int>("skillId")];
                        }
                    }
                    if (asset is UnitConfig statsOwner) EditorUtility.SetDirty(statsOwner.BaseStats);
                    EditorUtility.SetDirty(asset); AssetDatabase.SaveAssetIfDirty(asset);
                    AfterWriteForVerification?.Invoke(++writes);
                }
                manifest.state = "Completed"; SaveManifest(folder, manifest);
                WriteReport("导入成功：" + plan.Updates.Count + " 个资产。备份：" + folder, plan);
                return folder;
            }
            catch (Exception failure)
            {
                try { RestoreInternal(folder, true); WriteReport("导入失败，已恢复原文件：" + failure, plan); }
                catch (Exception recovery) { WriteReport("导入失败且恢复未完成；请保留备份并通过恢复入口重试。\n" + failure + "\n" + recovery, plan); throw new AggregateException(failure, recovery); }
                throw new InvalidOperationException("导入失败，原文件已恢复。" + failure.Message, failure);
            }
        }

        internal static void EnsureAssetFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
        private static void WriteReport(string result, ConfigImportPlan plan)
        {
            Directory.CreateDirectory(BackupRoot);
            File.WriteAllText(LastReport, DateTimeOffset.Now.ToString("O") + "\n" + result + "\n\n" + string.Join("\n\n", plan.Messages));
            string history = BackupRoot + "/History";
            Directory.CreateDirectory(history);
            string name = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            File.WriteAllText(history + "/" + name + ".json", Newtonsoft.Json.JsonConvert.SerializeObject(new
            { time = DateTimeOffset.Now, result, workbook = plan.WorkbookPath, hash = plan.WorkbookHash, scope = plan.Scope, metadata = plan.Metadata,
                assets = plan.Updates.Select(u => new { u.Path, u.IsNew, u.Description }) }, Newtonsoft.Json.Formatting.Indented));
        }
        [Serializable] private sealed class BackupManifest
        {
            public string state, workbook, workbookHash;
            public List<BackupAsset> assets = new();
            public List<string> createdFolders = new();
        }
        [Serializable] private sealed class BackupAsset { public string path, guid, fileHash, metaHash; public bool existed; }
        private static void SaveManifest(string folder, BackupManifest manifest)
        {
            string path = folder + "/manifest.json", temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(manifest, true));
            if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
        }
        public static string FindPendingBackup()
        {
            if (!Directory.Exists(BackupRoot)) return null;
            foreach (string file in Directory.GetFiles(BackupRoot, "manifest.json", SearchOption.AllDirectories).OrderByDescending(p => p, StringComparer.Ordinal))
                if (JsonUtility.FromJson<BackupManifest>(File.ReadAllText(file)).state == "Prepared") return Path.GetDirectoryName(file);
            return null;
        }
        public static string LatestBackup() => Directory.Exists(BackupRoot) ? Directory.GetFiles(BackupRoot, "manifest.json", SearchOption.AllDirectories)
            .OrderByDescending(p => p, StringComparer.Ordinal).Where(p => JsonUtility.FromJson<BackupManifest>(File.ReadAllText(p)).state == "Completed").Select(Path.GetDirectoryName).FirstOrDefault() : null;

        public static void Restore(string folder) => RestoreInternal(folder, false);
        private static void RestoreInternal(string folder, bool rollback)
        {
            EnsureEditorReady();
            string absolute = Path.GetFullPath(folder), root = Path.GetFullPath(BackupRoot) + Path.DirectorySeparatorChar;
            if (!absolute.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("只能恢复项目ConfigImportBackups内的清单。");
            var manifest = JsonUtility.FromJson<BackupManifest>(File.ReadAllText(Path.Combine(absolute, "manifest.json")));
            if (manifest.state == "Restored") throw new InvalidOperationException("该备份已经恢复，不能重复恢复。");
            string assetsRoot = Path.GetFullPath("Assets") + Path.DirectorySeparatorChar;
            foreach (string path in manifest.assets.Select(a => a.path).Concat(manifest.createdFolders))
                if (!Path.GetFullPath(path).StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("清单包含范围外路径。");
            // Check all backup files before touching anything.
            foreach (var a in manifest.assets.Where(a => a.existed))
            {
                if (!File.Exists(absolute + "/files/" + a.path) || !File.Exists(absolute + "/files/" + a.path + ".meta")) throw new IOException("备份文件不完整：" + a.path);
                if (!string.IsNullOrEmpty(a.fileHash) && (FileHash(absolute + "/files/" + a.path) != a.fileHash || FileHash(absolute + "/files/" + a.path + ".meta") != a.metaHash))
                    throw new IOException("备份完整性校验失败：" + a.path);
            }
            foreach (var a in manifest.assets)
            {
                string guid = AssetDatabase.AssetPathToGUID(a.path);
                if (!string.IsNullOrEmpty(a.guid) && !string.IsNullOrEmpty(guid) && guid != a.guid) throw new IOException("路径已被其他GUID的资产占用；拒绝覆盖：" + a.path);
                var obj = AssetDatabase.LoadMainAssetAtPath(a.path);
                if (!rollback && obj != null && UnitBaseStatsAuthoring.HasDirtyObjects(obj)) throw new IOException("恢复目标有未保存编辑；请先保存或撤销：" + a.path);
            }
            foreach (var a in manifest.assets)
            {
                if (a.existed)
                {
                    File.Copy(absolute + "/files/" + a.path, a.path, true);
                    File.Copy(absolute + "/files/" + a.path + ".meta", a.path + ".meta", true);
                    AssetDatabase.ImportAsset(a.path, ImportAssetOptions.ForceUpdate);
                }
                else if (File.Exists(a.path) || File.Exists(a.path + ".meta"))
                {
                    if (!AssetDatabase.DeleteAsset(a.path) || File.Exists(a.path) || File.Exists(a.path + ".meta"))
                        throw new IOException("无法清理本次新建资产；保留未完成清单供重试：" + a.path);
                }
            }
            foreach (string dir in manifest.createdFolders.OrderByDescending(p => p.Length))
                if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any()) AssetDatabase.DeleteAsset(dir);
            manifest.state = "Restored"; SaveManifest(absolute, manifest);
        }
    }
}
