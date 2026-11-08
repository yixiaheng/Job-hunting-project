# 统一场景流程验证记录

验证日期：2026-11-08（Asia/Shanghai）。项目版本：Unity 6000.3.23f1。使用方式与接口示例见 `Docs/SceneFlow.md`。

## 后续修复：切换期间相机交接

2026-11-08 21:02:41 +08:00，针对切换时出现 `No cameras rendering` 的反馈完成修复并重新运行主回归：**182 项通过，其中新增 37 项相机交接检查**；资源审计再次通过 60 项。运行时与编辑器程序集均重新编译通过。新结果保存为 `Docs/SceneCameraHandoffChecks.json`；下文 145 + 12 项为首次交付的历史记录。

本轮验证养成和配队的 Immediate / Fade 进入与返回。通过主动延迟目标初始化，确认等待期间来源相机仍开启、目标相机保持待显示；必要 UI 完成后才交接。操作每帧检查有效屏幕相机，并通过实际渲染回调确认来源和目标均产生画面。返回失败后检查目标相机重新接管，来源继续暂停。原有嵌套、数据保留、战斗与失败恢复检查也一并通过。

未在本轮重复执行三个独立启动菜单或构建 standalone；未改变 Single 战斗切换的卸载语义。测试结束后恢复原存档配置，编辑器回到 Sample 的非运行状态，保留原有 dirty 状态。

本次完成场景管理器、养成 / 配队导航、来源暂停恢复、战斗往返及必要资源适配。下列结果来自本项目的源码检查与 Unity Editor 实际 Play Mode 执行，未构建 Windows 成品。

## 验证结果

| 层次 | 结果 | 证据 |
| --- | --- | --- |
| 源码与资源审计 | 60 项通过，覆盖 16 个 UIConfig | `Docs/SceneFlowChecks.json` 中 `sourceAudit` |
| Unity 脚本编译 | 运行时和编辑器程序集编译成功，实际执行了新回归工具 | `Library/ScriptAssemblies/Assembly-CSharp.dll`、`Assembly-CSharp-Editor.dll`；汇总文件保存编译产物时间 |
| 主 Play Mode 回归 | 145 项通过；完成于 20:35:28 +08:00 | `playMode.main` |
| Character 独立启动 | 4 项通过；完成于 20:37:51 +08:00 | `playMode.directCharacter` |
| Party 独立启动 | 4 项通过；完成于 20:38:03 +08:00 | `playMode.directParty` |
| Battle 独立启动 | 4 项通过；完成于 20:38:14 +08:00 | `playMode.directBattle` |
| 画面检查 | 已查看养成、空配队及有展示角色的配队截图 | 临时截图 `Temp/SceneFlow/Character.png`、`Party.png`、`PartyWithCharacter.png` |

Play Mode 共 157 项断言，不包含前述 60 项静态检查。原始 JSON 已合并保存到 `Docs/SceneFlowChecks.json`，避免 Unity 清理 `Temp` 后丢失本次结果。截图没有作为大型二进制文件加入交付文档。

## 已实际覆盖的流程

### 探索保留、暂停与恢复

- Character、Party 分别连续进入 / 返回三次，覆盖 Idle、Move、Jump 状态与淡入淡出。
- 验证返回完成时仍为同一探索场景、同一玩家实例，位置、朝向、运动状态和速度与进入时一致；探索 Animator 在暂停期间不推进。
- 验证非默认 `timeScale = 0.7`、原本禁用的相机和 Zoom 动作恢复原值；相机遮罩、过渡输入限制和被检查的事件订阅恢复。
- 验证展示期间只有一个启用的 AudioListener。
- 验证探索 → 配队 → 养成逐级返回，配队的展示动画在被挂起时停止，返回后继续；探索在配队仍驻留时继续暂停。
- 配队子弹窗优先处理返回，子弹窗关闭后才返回来源场景。

### 数据与 UI

- 通过真实养成服务执行预览与提交，确认等级实际变化；执行装备穿戴并验证返回后保留。
- 配队调整在关闭并重新进入后保留。
- 从背包进入养成再返回，恢复同一背包实例及其原有相机输入限制。
- 控制器初始化等待必要 UI 真正到达 Open 状态；场景 UI 使用明确的所属场景句柄。

### 战斗链路

- 探索 → 配队 → Battle：探索和展示场景整场卸载，会话释放，战斗不继承暂停的全局时间。
- 通过真实战斗结算 UI 按钮执行重新配队、重试与退出。
- 重配保留原关卡与队伍上下文，再次挑战仍进入原关卡。
- 重配取消后返回探索并清理对应战斗上下文。
- 请求被拒绝时，开始挑战和结算按钮恢复可操作状态，原业务上下文不被错误覆盖。

### 异常与操作接口

- 无效场景、重复目标、并发请求、过期句柄和非栈顶返回被拒绝。
- 初始化失败或缺少预期控制器时，清理本次目标并恢复来源。
- 返回卸载失败时保留场景、会话和来源暂停，恢复目标交互，再次返回可成功。
- 初始化失败且目标清理失败时保留错误会话和恢复入口；实际点击恢复按钮后成功清理。
- 被接受的请求在 `Prepare` 抛异常时执行业务回滚，尚未加载目标时不卸载原配队。
- 低层 Additive 加载遵守 `SetActive = false`，不隐式建立会话；显式活动场景切换和等待卸载接口工作正常。

### 真正的独立启动

三次独立验证通过编辑器 Play Mode 启动场景设置，使目标成为唯一场景后再启动游戏，不仅是运行中切换过去。

- Character / Party 的 `Start` 接管无来源展示会话，返回操作加载 Sample 并恢复时间。
- Battle 从 Inspector 阵容初始化，通过真实结算 UI 退出到 Sample。

## 源码与资源审计范围

- `Assets/Scripts` 中检测到的 3 处 Unity 加载 / 卸载 / 活动场景修改全部位于 `GameSceneManager`。
- 16 个 UIConfig 的目录与其 `layer` 一致，对应面板 prefab 目录也与 UILayer 一致；UIRegistry 的引用可解析。
- 构建列表中四个场景 GUID 均与当前资产 `.meta` 匹配。Sample 的构建 GUID 已修正为 `07430a34e0c973749a84bb6620d5e61c`。
- Layer 9 已设置为 `PartyPreview`；配队场景与运行时展示模型使用该层。
- ExplorationHUD、BattlePartyPanel、PartyBackdrop 的原 prefab GUID 保留。
- World Space PartyBackdrop 与原版本在统一换行后逐字相同，保留其原空间、位置、缩放和布局。
- 新增脚本的 `.meta` 文件均存在。
- 对本次核心 C# 和项目设置修改的定向 `git diff --check` 未报告空白错误；未对项目已有的无关修改和 Unity YAML 进行全局格式化。

## 测试方法与边界

1. 移动 / 跳跃由回归脚本设置玩家状态和速度，并在往返完成的回调内检查快照；这验证状态保留机制，不等同于人工按键操作全过程。
2. 当前配队角色胶囊占位 prefab 没有 Animator。动画测试创建内存中的 Animator / AnimationClip 探针，验证非缩放时间及嵌套暂停恢复，不代表已验证未来真实角色美术动画。
3. 战斗结束由测试将敌方运行时 HP 归零，再调用正常战斗流程步骤触发结算；之后操作实际 UI 按钮。这验证场景与结算流程，不验证完整战斗体验或数值平衡。
4. 卸载失败通过仅在 Editor 编译的故障注入接口模拟，验证失败后的会话所有权和重试路径；未制造真实引擎 / 操作系统层卸载故障。
5. 事件订阅检查覆盖被测试的业务事件和连续往返流程，不声称已完成整个项目的内存泄漏分析。
6. 已适配旧 `CharacterNavigationRegression` 的配队入口，并纳入编译；未单独重跑旧验证菜单。本次实际流程由新回归工具覆盖。
7. 未构建或运行 Windows standalone / IL2CPP 成品，也未验证网络认证、AB 场景加载或跨进程探索恢复。
8. Battle 使用 Single 整场切换，退出时重新加载探索；不承诺恢复进入战斗前的临时位置与运动状态。

## 原有配置与编辑器现场

回归使用独立临时存档槽。结束后恢复 `PlayerRuntimeConfig.asset` 原始文件字节和编辑器 Play Mode 启动场景设置，删除测试存档槽。最终资源审计再次确认配置字节一致。

最终编辑器状态为退出 Play Mode、活动场景 SampleScene、场景仍为 dirty。保留进入任务前已有的未保存修改，没有为测试保存或重新打开 Sample。状态与恢复标记已保存到汇总 JSON。

## 已知的范围外问题

测试期间现有热更新启动器报告：

> 本地首包 AssetBundle 不存在；请先构建 Windows AssetBundles。

本次场景流程验证通过不代表热更新启动成功。该问题不属于本次明确限定的场景改造范围，未修改热更新、Lua 或 AB 构建流程。

## 重新执行

在 Unity 中使用：

- `Tools → Scenes → Verify Unified Scene Flow`
- `Tools → Scenes → Verify Direct Character Startup`
- `Tools → Scenes → Verify Direct Party Startup`
- `Tools → Scenes → Verify Direct Battle Startup`

需要重新生成相关 UI 时，运行 `Tools → Scenes → Configure Unified Scene Flow` 同步导航入口、展示层和构建场景配置。当前交付已完成该配置，无需在首次体验前重复运行。

回归的新结果输出到 `Temp/SceneFlow`，不会自动覆盖本次保存的 `Docs/SceneFlowChecks.json`。详细 API、调用关系与失败语义见 `Docs/SceneFlow.md`。
