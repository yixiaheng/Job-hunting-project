using System;
using System.IO;

namespace Battle.Editor.ConfigImport
{
    internal static class ConfigWorkbookFiles
    {
        internal static string CurrentHash(string path) => File.Exists(path) ? ConfigImportService.Hash(ConfigImportService.ReadWorkbookSnapshot(path)) : "missing";

        // Publish on the destination volume. Replacement is atomic; the previous workbook is retained.
        internal static void Publish(string source, string destination, string expectedHash)
        {
            destination = Path.GetFullPath(destination);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            string temp = destination + "." + Guid.NewGuid().ToString("N") + ".writing";
            try
            {
                File.Copy(source, temp);
                if (CurrentHash(destination) != expectedHash) throw new InvalidOperationException("输出工作簿在生成期间已变化；未覆盖，请重新生成或另存为。");
                if (File.Exists(destination))
                {
                    // Refuse to overwrite a workbook open in Office, even if Office allows shared reads.
                    using (var probe = new FileStream(destination, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                    string backupDir = Path.Combine(Path.GetDirectoryName(destination), ".BattleWorkbookBackups");
                    Directory.CreateDirectory(backupDir);
                    string backup = Path.Combine(backupDir, Path.GetFileNameWithoutExtension(destination) + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".xlsx");
                    File.Replace(temp, destination, backup);
                }
                else File.Move(temp, destination);
            }
            catch (IOException e) { throw new IOException("无法写入工作簿；请关闭Excel/WPS中的该文件，或另存为新路径。原文件不会因写入失败被清空。路径：" + destination, e); }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}
