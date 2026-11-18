# 场景与玩家数据整理：本次验证

日期：2026-11-08，Asia/Shanghai。Unity：6000.3.23f1。

本次完成两条链路：场景操作统一到 GameSceneManager，完整档案组装统一到 PlayerProfileCoordinator。没有修改面板布局或重建场景、prefab、UIConfig；没有扩展签到、Lua、网络、战斗奖励或存储底层。

## 执行结果

| 验证层次 | 本次实际结果 | 证据 |
| --- | --- | --- |
| 源码与资源检查 | 75 项通过 | `ArchitectureVerification/source-audit.json` |
| Unity 脚本编译 | 运行时与编辑器程序集重新编译成功，新验证代码在 Editor 执行 | `ArchitectureVerification/summary.json` 记录程序集时间 |
| 档案、养成与原有战斗逻辑验证 | 332 项断言通过，23:07:58 完成 | `ArchitectureVerification/profile-editmode.txt` |
| 场景主 Play Mode 回归 | 199 项断言通过，23:03:03 完成 | `ArchitectureVerification/scene-flow.json` |
| Character / Party / Battle 独立启动 | 各 4 项，共 12 项通过 | `ArchitectureVerification/direct-*.json` |
| 真实养成 UI Play Mode 链路 | 60 项检查通过 | `ArchitectureVerification/cultivation-ui-playmode.txt` |
| 画面检查 | 已查看养成与含角色的配队截图，布局保留、没有探索内容穿入展示 | `Temp/SceneFlow/Character.png`、`PartyWithCharacter.png` |

## 覆盖内容

- 养成、配队各连续打开 / 返回三次；覆盖静止、移动、跳跃。返回时仍为原探索场景、原玩家实例，位置、朝向、速度和运动状态保持。
- 探索 → 配队 → 养成逐级返回；挂起展示场景的 Animator 停止推进，恢复后继续。背包上下文和原输入限制保留。
- Immediate / Fade 下主动延迟目标初始化：来源持续提供画面，来源相机与灯光提前排除目标展示层；目标必要 UI 已 Open 但仍隐藏，交接后才显示。逐帧检查可用屏幕相机，并观察实际渲染回调。
- 按场景 UI 挂起覆盖后来创建的面板；全局挂起仍只影响已有面板。初始化中的目标 UI 不提前参与背景请求。
- 非默认时间比例、原本关闭的相机、禁用的输入动作、相机 Mask 恢复；展示期间只有一个启用的 AudioListener，受检查的业务订阅退出后回到原数量。
- 配队 → 战斗 → 结算 → 重配 → 再战，以及取消重配、重试和退出；队伍和关卡上下文正确，战斗不继承展示暂停时间。
- 无效请求、重复目标、并发请求、过期 / 非栈顶句柄、缺少目标控制器、初始化失败、返回卸载失败。卸载失败保留会话并恢复目标交互，显式再次返回可以清理；未创建错误恢复专用 UI。
- 档案新建、已有完整档案读取、成组备份、损坏文件保护、深拷贝、普通背包保存、保存失败后重试、过期预览与装备保护。养成成功后修订号匹配，自动保存不重复写盘。
- 真实 UI 点击：材料取消和确认取消不修改数据；点击确认升级角色和技能，重新读取存档验证；养成后的属性和技能进入实际 BattleManager。

## 测试方法与范围

1. 场景及 UI 验证使用隔离存档槽，结束后删除测试槽并恢复 PlayerRuntimeConfig 原始字节。最终编辑器回到 SampleScene 的非运行状态；没有保存用户当前编辑场景。
2. 移动 / 跳跃由脚本设置状态与速度后验证冻结和恢复，不等同于人工键鼠手感测试。
3. 当前配队胶囊 prefab 没有真实角色 Animator，回归使用内存动画探针验证非缩放播放和嵌套暂停；不代表完成真实角色动画资源验收。
4. 保存失败使用测试存储模拟，卸载失败使用仅 Editor 存在的故障注入。没有故意破坏真实玩家存档或磁盘权限；5 秒自动保存重试调度保留并经源码检查。
5. 早期 `SceneFlowChecks.json`、`SceneCameraHandoffChecks.json`、`SceneFlowVerification.md` 是历史记录；本次结果归档在 `ArchitectureVerification/`。
6. 没有构建或执行 Windows 独立成品，本次编译与运行证据均来自 Unity Editor。
7. 背包搭建工具已改为从 UIRegistry 定位注册面板 prefab；已有布局不重建。最后一次编辑器程序集编译于 23:10:15 成功。工具本次仅做源码与编译检查，没有执行资源生成，以保留现有定制布局。

## 已知独立问题

启动日志仍报告：**“本地首包 AssetBundle 不存在；请先构建 Windows AssetBundles。”** 场景与数据验证已实际通过，但这不代表热更新启动链路已经跑通。本次按范围保留其配置，没有构建 AB 或修改热更新策略。

调用图与最小展示场景接入示例见 `SceneFlow.md`；完整档案数据流和职责说明见 `PlayerProfile.md`。
