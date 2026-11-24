# 导入资源冲突修复

日期：2026-11-24。

后续清理：用户明确本次只需要 Hierarchy 的 GameObject 分类工具，因此已将附带音游的 23 个脚本、对应目录和 SlimUI 移出 Assets，存入 `UserSettings/ImportConflictBackups/UnusedImport_*` 本地备份，退出 Unity 导入与编译。保留 `Assets/M Studio/Colourful Hierarchy Category GameObject/` 的两个编辑器脚本、Color Palette 配置及原说明文档。下文为清理之前的冲突修复记录；其中“保留音游／SlimUI”已被此次清理取代。移出前已检查其 GUID 没有被现有场景、prefab、asset 引用。项目原有 Odin、DOTween 和 TMP 依赖继续保留。

## 原因

1. 导入的音游示例包含全局 UIManager。项目原有 UIManager 位于 Game.UI，但旧业务代码通过 using 引用它；同名全局类型优先解析，导致大量 Instance / Open / TryGetPanel / Close 不存在的编译错误。
2. 导入包覆盖了项目原有的 TextMesh Pro 示例与部分公共资源。旧示例把当前版本的 UV0 数据当成 Vector2 数组，并使用旧的 Mesh.uv 赋值方式，产生类型错误。
3. 导入包覆盖了 Odin 和 DOTween DLL，但配套符号文件仍来自原项目版本。Unity API Updater 报 SymbolsNotMatchingException，13 个程序集的更新失败。

## 处理

- 将此次导入的 Manager、Controller、Enums、SO，以及 DontDestroy.cs / test.cs 共 23 个脚本放进 Imported.RhythmGame 命名空间。示例内部类型相互引用不变，原战斗和 UI 框架不用批量改名。
- 移除 SongPanelController 和 MenuUIManager 未使用的 UnityEditor 引用，避免导入的运行时脚本直接依赖编辑器程序集。
- 在备份后，从项目已有版本恢复本次被覆盖的 30 个第三方文件，包括 DOTween、Odin、TMP 示例、TMP shader 和默认 LiberationSans 资源。原自定义中文字体资产未纳入回退。
- 保留所有脚本和资源的原路径、meta 与 GUID；M Studio 和 SlimUI 导入资源继续保留。

被恢复前的导入文件及 SHA-256 清单保存在本机：

`UserSettings/ImportConflictBackups/20261124_180600/`

这是被 Git 忽略的本地备份目录。需要长期保管时应另行归档。

## 验证

- Unity 实际脚本编译成功，Assembly-CSharp 与 Assembly-CSharp-Editor 已重新生成。
- 独立运行 Unity 自带 Roslyn 对运行时和编辑器程序集编译均成功。未修改 PackageCache 或通过删除示例脚本回避冲突。
- 修复后运行真实 BattleScene 烟测，结果见同目录 ImportedResourceConflictVerification/playmode.txt。

本轮修复处理导入兼容性，没有把音游示例接入当前战斗流程，也没有对其单独玩法做功能验收。后续再导入完整项目资源时，优先排除项目已有的 Plugins 和 TextMesh Pro 公共依赖，避免旧版本再次覆盖。
