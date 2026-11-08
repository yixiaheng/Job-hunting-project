# Hot Update Bootstrap 使用说明

## 启动方式

游戏进入首个场景前，`HotUpdateBootstrap` 会自动查找并创建唯一的 `[HotUpdateRoot]`，将其设为 `DontDestroyOnLoad`，再依次准备本地 AssetBundle、尝试远程更新、初始化 `ABManager`、创建 xLua `LuaEnv` 并执行 Lua 入口。Lua 环境由根对象每帧调用 `Tick()`，根对象销毁时释放 Lua 环境和已加载 Bundle。

## Inspector 配置

配置资源位于 `Assets/Resources/Bootstrap/HotUpdateSettings.asset`。选中它即可调整：

- `Try Remote Update`：是否请求远端更新。
- `Server Root`：更新服务器目录 URL，例如 `https://example.com/game/windows`。留空时不发网络请求，直接使用本地 Bundle。
- `Manifest Name`：更新清单文件名，默认 `ABCompareInfo.txt`。
- `Request Timeout Seconds`：请求超时秒数。
- `Bundle Folder`：`persistentDataPath` 下的本地资源目录，默认 `AssetBundles`。
- `Copy Streaming Assets On First Launch`：首次启动且本地没有主 Bundle 时，从安装包复制首包。
- `Lua Entry`：启动执行的 Lua 模块名，不写扩展名；默认 `Main` 对应 `Assets/Lua/Main.lua.txt`。
- `Load Lua From Asset Bundles In Editor`：打开后，Editor 也从 AB 加载 Lua；关闭时先从 `Assets/Lua` 直接读取，适合日常调试。

目前网络更新失败会记录警告并继续使用本地资源。若本地首包不存在或 AB Manifest 无法初始化，启动会停止 Lua 初始化并在 Console 记录错误。服务端认证失败阻止进入游戏的流程尚未实现。

## 本地运行和 AB 验证

1. 在 Unity Build Profiles 选择 Windows Standalone 目标平台。
2. 执行 `Tools > Build AssetBundles`，生成到 `Assets/StreamingAssets/AssetBundles/`。首次运行前必须完成这一步。
3. `Assets/Lua` 文件夹已标记到名为 `lua` 的 AssetBundle；新增 Lua 脚本放入该文件夹即可随包构建。
4. 日常 Lua 调试时保持 `Load Lua From Asset Bundles In Editor` 关闭；进入 Play Mode 后 Console 应出现 `[Lua] Main.lua.txt loaded through LuaManager`。
5. 验证 AB 路径时打开该选项，再次执行 AB 构建并进入 Play Mode。构建输出复制到 `Application.persistentDataPath/AssetBundles`，可删除此本地目录后重进 Play Mode，检查首包复制流程。
6. 发布更新时把 `Assets/StreamingAssets/AssetBundles` 的 Bundle 文件上传到服务器，并在所有 Bundle 上传成功后最后发布 `ABCompareInfo.txt`。配置 `Server Root` 后启动会尝试下载更新。

## 当前范围和注意事项

- 当前 Windows 首包复制通过文件系统目录复制实现；Android 等将 `StreamingAssets` 暴露为 URL 的平台需要补充逐文件复制清单，本配置不是跨平台首包复制实现。
- 远端更新使用现有 MD5 清单校验，尚未加入清单签名或账号认证。
- `Lua Entry` 对应的脚本必须已打进 `lua` 包。Editor 直读模式可能掩盖漏打包，因此发布前应在 Editor 打开 AB 加载选项验证。
- Lua 运行时只能调用当前游戏程序已编译并生成 xLua 绑定的 C# API。新增 C# 桥接 API 仍需重新生成绑定并构建客户端。
- AssetBundle 内的 UI prefab 目前尚未接入 UIManager 的 AB 加载路径。
