# Unity 热更新实施说明

> 实施状态（2026-11-22）：本说明中的 Unity 端功能已经完成。Editor 菜单统一位于
> `Tools/Hot Update/`，包含 `Build AssetBundles`、`Apply Project Setup`、
> `Publisher`、`Upload AssetBundles` 和 `Verify Project Setup`。正式 HTTPS 地址仍留空，
> `tryRemoteUpdate` 保持关闭；服务器 Caddy、Release 迁移和 `current` 切换仍需后续手动完成。

范围为 Windows PC 的 AssetBundle 与 xLua 热更新。开发者在 Unity Editor 中构建、通过 SSH 上传暂存版本并选择版本测试。正式发布由服务器管理员确认并执行。打包后的客户端通过 HTTPS 获取当前正式资源，启动界面显示下载和加载阶段。

## 已完成的代码与接入点

| 位置 | 现状与接入方式 |
| --- | --- |
| `Assets/Editor/HotUpdate/BuildAssetBundles.cs` | 保留构建入口。以 Unity 当前构建目标生成 `StreamingAssets/AssetBundles`，并输出 `ABCompareInfo.txt`。 |
| `Assets/Editor/HotUpdate/HotUpdatePublisherWindow.cs` | 提供版本号、SSH 参数、私钥、本地校验、Staging 上传和 Editor 来源选择。旧 `UpLoadAB.cs` 菜单已转到该窗口。 |
| `Assets/Editor/HotUpdate/HotUpdatePublisher.cs` | 上传到唯一临时目录，在服务器复核大小与 MD5，写入 `version.txt`，再以 `mv -T` 移到 `staging/<版本>`；不会修改 `releases` 或 `current`。 |
| `Assets/Resources/Bootstrap/HotUpdateSettings.asset` | Player 运行时配置入口。当前 `tryRemoteUpdate` 关闭、正式 HTTPS 地址为空，完成 Caddy 和域名配置前不会请求远端。 |
| `Assets/Scripts/HotUpdate/HotUpdateBootstrap.cs` | 自动创建 `[HotUpdateRoot]`，解析 Local、Current Release、Specific Release 或 Staging 来源，完成更新、回退、AB 初始化和 Lua 启动。 |
| `Assets/Scripts/HotUpdate/HotUpdateManager.cs` | 负责清单比较、按字节进度、暂存下载、大小与 MD5 校验、原子目录切换及激活失败回滚。主 Bundle 按文件存在性判断。 |
| `Assets/Scripts/HotUpdate/ABManager.cs`、`Assets/Scripts/Lua/LuaManager.cs` | 继续负责资源包与 xLua 加载。更新结束后先初始化 AB，再执行 Lua 入口。非本地 Editor 来源强制走 AssetBundle Lua。 |
| `Assets/Scenes/HotUpdateStartup.unity`、`Assets/Scripts/UI/Framework/HotUpdateProgressPanel.cs` | 独立启动场景先显示 Overlay 进度面板，再开始检查、下载、校验、加载或回退；失败时保留重试入口。 |

配置通过资源或 Editor 设置填写。启动时自动生成的组件只是运行实例。运行时配置继续使用原 Resources 路径和资产 GUID。`Local Only` 默认保留 Editor 的 Lua 源码直读；其余三种 Editor 来源均执行完整 AssetBundle 链路。

## 一、服务器管理员手动操作

1. **安装 Caddy。** 通过现有 SSH 登录 Ubuntu。在服务器终端运行 `sudo apt update`、`sudo apt install caddy`，然后用 `caddy version` 确认安装。配置完成前维持现有云防火墙规则。部署时先用 `sudo caddy validate --config /etc/caddy/Caddyfile --adapter caddyfile` 校验，再重载 Caddy。
2. **确认首批资源并发布。** 会话中已手动上传并核对 `staging/0.1.0` 的三个 Bundle 大小和 MD5。先检查服务器实际目录，再根据测试情况决定是否发布。首次发布时确认目标版本号未占用，把完整暂存目录移到 `releases/0.1.0`，再启用 `current -> releases/0.1.0`。已发布目录应保留供回滚。
3. **先开通 Editor 私有下载。** Caddy 在服务器回环地址（例如 `127.0.0.1:18081`）提供暂存目录。Windows 本机打开 SSH 转发后，在浏览器或 PowerShell GET 一次清单和 Bundle，再把本机 URL 填入 Editor 配置。以后用同一条隧道测试其他暂存版本。
4. **配置正式 HTTPS。** 准备实际域名，将 DNS A 记录指向 `49.233.205.48`，按腾讯云实例和域名要求完成适用的备案。配置 Caddy 正式站点，按所选证书验证方式放行端口，常规 HTTPS 站点使用 TCP 443，HTTP 验证或跳转需要 TCP 80。验证正式版本入口和 Bundle 能从外网通过有效证书下载，再填写 Unity 正式地址并启用远程更新。

Windows 的 SSH 转发示例。端口以实际 Caddy 配置为准，测试时保持终端连接。

```powershell
ssh -N -o ExitOnForwardFailure=yes -L 127.0.0.1:18081:127.0.0.1:18081 -i "$env:USERPROFILE\.ssh\tencent_game_server_ed25519" ubuntu@49.233.205.48
```

### 服务器与 Unity 的路径约定

当前只有 Windows 资源，沿用 `/srv/game-server/update` 根目录。下表的域名和端口都是配置示例。Caddy 正式站点显式映射 `/update/current/version.txt` 和 `/update/releases/*`。私有监听地址单独映射 `/staging/*`。关闭目录列表，确保 Caddy 运行用户对目标目录有遍历权限、对下载文件有读取权限。

| 用途 | 访问位置 | 服务器文件或目录 |
| --- | --- | --- |
| Editor 上传 | SSH，`ubuntu@49.233.205.48:22` | `/srv/game-server/update/staging/<版本>/` |
| Editor 下载暂存版 | `http://127.0.0.1:18081/staging/<版本>/ABCompareInfo.txt` | `staging/<版本>/ABCompareInfo.txt` |
| 读取当前正式版本号 | `https://update.example.com/update/current/version.txt` | `current/version.txt` |
| 下载指定正式版 | `https://update.example.com/update/releases/<版本>/ABCompareInfo.txt` | `releases/<版本>/ABCompareInfo.txt` |

实施建议采用一个独立的 `version.txt` 作为正式版本入口，内容只有版本号，例如 `0.1.0`。它由管理员在正式切换之前放进对应版本目录，独立于 `ABCompareInfo.txt`。Caddy 对 `current/version.txt` 设置 `Cache-Control: no-store`。正式客户端每次检查先读一次版本号，再固定从该版本的 `releases/<版本>/` 读取清单和所有 Bundle。这样即使下载期间切换 `current`，本次请求仍使用同一份资源。发布与回滚通过同目录创建临时符号链接，再原子替换 `current`。已发布版本保留到足以覆盖正在进行的下载。

## 二、Unity 开发任务

### 1. 暂存上传窗口

- 保留单独的构建菜单，在 `Tools → Hot Update` 下提供暂存上传入口。显示版本号、当前构建目标、产物目录和目标服务器。第一版支持 Windows，版本号限定为三段数字，例如 `0.1.1`。
- SSH 主机、端口、用户名和服务端根目录放入 Editor 专用配置。私钥路径使用本机文件选择器并保存在本机设置，或通过本机 SSH 配置指定身份。私钥文件留在本机 `.ssh` 目录。
- 读取现有清单并验证文件名、重复条目、大小与 MD5。上传清单所列文件和清单本身，排除 `.meta` 与 Unity 构建辅助 `.manifest`。上传使用临时目录，服务端校验完成后再移到 `staging/<版本>/`，避免测试读取半套资源。同名暂存版本提示占用，可使用新版本号继续。
- 异步运行 SSH/SCP，持续读取输出，显示成功或失败与远端路径。失败时保留正式版本和 `current` 的原有状态。正式迁移、启用和回滚保留为管理员手动操作。

### 2. 下载配置与版本选择

| 配置入口 | 需要填写的字段 |
| --- | --- |
| `Assets/Resources/Bootstrap/HotUpdateSettings.asset` | 远程更新开关、正式 HTTPS 基础地址（例如 `https://update.example.com/update/`）、清单名称、超时、本地目录、Lua 入口。现有 `serverRoot` 字段沿用或迁移序列化值并明确新语义。 |
| `Tools → Hot Update` 的 Editor 测试设置 | 来源、指定版本号、暂存 HTTP 基础地址。建议用项目范围的本机 Editor 设置。 |
| `Tools → Hot Update` 的 Editor 上传设置 | SSH 主机、端口、用户、远程根目录、本机密钥路径或 SSH 主机别名。 |

- Editor 支持四种来源。`Local Only` 保留日常 Lua 源码直读，也可手动选用本地 AB。`Current Release` 使用正式版本入口。`Specific Release` 输入版本号并直接读取对应 Release。`Staging Version` 输入版本号并通过本机回环 HTTP 地址，经 SSH 隧道下载。三种远程测试模式均执行完整 AB 加载链路。
- 打包后的 Player 在开启远程更新时只使用正式 HTTPS 配置和当前 Release 入口。Editor 配置与 SSH 信息仅存在于 Editor。实现 Editor 与运行时之间的设置桥接时，运行时程序集不得直接引用 UnityEditor 或 `Assets/Editor` 中的类型。
- Editor 测试缓存与正式 Player 缓存隔离，至少分别隔离暂存、指定 Release 与正式入口。切换来源或版本失败时，只允许使用同一来源和版本之前校验过的缓存，或使用安装包首包。界面和日志明确显示实际加载来源。
- 修正首包判断中对主 Bundle 文件 `AssetBundles` 使用 `Directory.Exists` 的问题。按文件存在性和可加载性判断，避免下次启动时用首包覆盖已更新的缓存。保留现有文件级差异下载。更新下载、校验或 AB/Lua 激活失败时，维持或恢复可用旧版资源。

### 3. 启动进度界面

- 在启动流程中先让进度界面能打开并渲染，再开始网络请求。采用独立启动场景时，调整现有自动启动逻辑，确保下载只有一个启动入口。普通场景的 Editor 调试入口也应继续可用。
- 进度面板由 UIManager 以 `UIId` 和 Context 打开，使用 `Overlay` 层。`UIConfig` 放在 `Assets/Config/UI/Overlay/`，预制体放在 `Assets/Prefabs/UIPrefabs/Overlay/`。面板使用安装包内置资源，启动时可先于远程 AB 加载。
- 下载显示当前文件、已下载字节、待下载总字节和百分比。下载总量按实际缺失和变化的文件计算。检查清单、比较、校验、切换、初始化 AB 和加载 Lua 显示阶段文字或不定进度。面板能读取当前状态，关闭时解除订阅。
- 下载失败且有可用本地资源时显示已回退，再继续启动。没有可用资源时显示实际错误并保留重试入口。等 AB 和 Lua 初始化成功后关闭进度面板并放行业务场景。

## 验收

1. Unity 构建后输入一个新版本号，上传暂存成功，文件大小和 MD5 均与清单一致，`current` 保持原目标。上传失败时无法通过目标版本目录拿到半套文件。
2. Editor 通过 SSH 隧道加载指定暂存版本，能在日志或 Lua 的可见变化中确认实际版本。测试指定 Release 和本地模式时，缓存不会串用。
3. 管理员启用测试通过的 Release，Windows 打包客户端通过真实 HTTPS 加载它。切回旧 Release 后重新启动，客户端加载旧版本。一次更新期间切换 `current` 时，正在下载的客户端继续读取原版本目录。
4. 验证首次启动、重复启动、无差异、部分下载、离线、文件校验失败和激活失败。UI 能区分下载与加载阶段。按项目规范运行对应 UI/SceneFlow 验证并记录实际结果。

当前范围覆盖 AssetBundle 与 Lua 的发布和下载。UI 预制体是否随版本实际变化取决于业务加载路径，现有 UIConfig 直接引用的面板需要另行接入 AB 加载。C#、xLua 绑定和原生插件变更仍使用新版客户端安装包。

### 2026-11-22 本地验收记录

- Unity 完成脚本编译，`Tools/Hot Update/Verify Project Setup` 通过 39 项检查，0 项错误。唯一警告是正式 HTTPS 地址尚未配置，因此 Player 远程更新保持关闭。报告位于 `Docs/HotUpdateVerification.json`。
- AssetBundle 构建成功；`AssetBundles`、`lua`、`uiprefabs` 的实际大小和 MD5 与 `ABCompareInfo.txt` 全部一致。
- `Local Only` Play 已从启动场景完成 Lua 初始化并进入 `SampleScene`。
- `Staging Version` 在 `127.0.0.1:18081` 不可达时，启动界面显示清单下载阶段；请求超时后回退到本地 Bundle，Lua 通过 AssetBundle 启动并进入 `SampleScene`。
- 尚未执行新的真实 SSH 上传，也未验证 Caddy、域名、真实 HTTPS 下载、Release 迁移和 `current` 切换。这些项目需要服务器准备完成后按本说明继续验证。
