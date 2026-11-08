# Lua 热更新框架导入记录

本轮只把学习工程的 xLua、AssetBundle 更新/加载代码及编辑器构建工具导入当前项目；没有接入 `UIManager`、场景启动或任何现有业务面板。

## 目录

- `Assets/XLua/`：xLua C# 运行时、Editor 工具、代码生成器、模板和生成代码。
- `Assets/Plugins/`：xLua 各目标平台的原生运行库及原始导入设置。
- `Assets/Scripts/HotUpdate/`：`HotUpdateManager` 与 `ABManager`。
- `Assets/Scripts/Lua/`：学习工程的 `LuaManager`。
- `Assets/Editor/HotUpdate/`：AssetBundle 构建/上传工具和当前项目的 xLua AOT 绑定配置。
- `Assets/Lua/`：后续放置 Lua 模块的根目录，目前没有示例模块。

## IL2CPP / Android

当前 `ProjectSettings/ProjectSettings.asset` 已将 Android scripting backend 设为 `1`（IL2CPP），`AndroidTargetArchitectures` 为 `2`（ARM64）。已从学习工程导入 `Plugins/Android/libs/arm64-v8a/libxlua.so` 及其 Unity PluginImporter 元数据；其他平台原生库也按原目录导入。

`XLuaProjectConfig.cs` 是项目级 Lua/C# 边界配置。新增 Lua 可调用的 C# API 或新增 Lua 函数回调签名后，应执行 `XLua/Generate Code`，再提交 `Assets/XLua/Gen/` 生成代码。也可使用 `Tools/HotUpdate/Generate XLua IL2CPP Bindings` 菜单。

该项目当前安装的 Unity Editor 没有 Android Build Support 模块，所以尚未执行 Android IL2CPP 真机构建；Player Settings 已设为 IL2CPP 不等于已通过 Android 构建验证。安装对应 Unity Editor 版本的 Android Build Support/NDK/SDK 后再做构建验证。

## 尚未启用的部分

- `HotUpdateManager` 未放入场景、未启动下载；其 `serverRoot` 为空，须先配置项目自己的 HTTPS 内容服务器。
- 上传脚本中的服务器用户、地址和目录是 `CONFIGURE_ME`，未绑定学习工程的旧服务器。
- 没有改 `UIManager`、`UIConfig`、`UIRegistry`、面板预制体或 `UIPanel`。
- 没有创建 `LuaUIPanel` 或任何业务 Lua 面板。
- 没有把 xLua C# Hotfix 注入功能开启；当前导入的是 Lua 虚拟机与 Lua 脚本加载能力。
- 学习工程的 `LuaBehaviour` 对照样例刻意未导入，避免创建第二个独立 `LuaEnv`。
- 此更新器使用 AssetBundle，不等于已经完成整包更新、安全签名、断点续传或线上发布配置。

