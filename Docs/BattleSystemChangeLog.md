# 战斗系统修改总览

## 1. 文档范围

本文记录 2026-11-18 战斗系统第 1—7 阶段优化产生的全部代码和文档变化。它描述“改了什么”；当前系统如何运行见 [BattleSystemGuide.md](BattleSystemGuide.md)。

本轮没有修改或保存战斗场景，没有移动 UIConfig 或 UI prefab，也没有重新生成桌面 EXE。现有 `BattleScene` 继续走 Inspector 单波兼容配置。

## 2. 行为变化摘要

### 2.1 行动提交

- `BattleAction` 增加显式 `caster`。
- 玩家按钮、敌人 AI、编辑器测试统一通过 `BattleFlow.ValidateAction()`、`TryExecute()` 和 `TryStep()`。
- 校验覆盖战斗终局、同步重入、行动者归属、当前行动者、技能归属、技能配置、能量、战技点、目标归属和合法目标。
- 单体／扩散技能保留自动补选；其他战斗的显式目标直接返回 `ForeignTarget`。
- 失败结果不修改单位状态、资源、行动轴或行动次数，也不会复用上一次成功结算。

### 2.2 架构职责

- `BattleManager` 从“规则、模型、UI、回合全部负责”改为场景组装协调器。
- 逻辑单位创建、场景 actor、回合驱动、UI 生命周期、波次和表现播放分别由独立对象负责。
- 删除 `BattleUnit.gameObject`，逻辑层不再直接持有场景对象。
- UI 通过 `IBattleCommandSource` 和 `IBattleActorLookup` 使用窄接口。

### 2.3 战技点

- 新增我方共享 `BattleSkillPoints`，默认初始 4、上限 5。
- `SkillConfig` 增加 `skillPointGain`，已有 `skillPointCost` 进入正式校验和结算。
- 普攻在产出、消耗均未配置时兼容性回复 1 点。
- 新增 `EffectType.GainSkillPoint`，允许技能效果提供额外回复。
- 养成战斗快照同步复制 `skillPointGain`，避免角色进入战斗后丢失显式产出。
- 操作栏显示当前值和上限，并根据真实校验结果决定按钮是否可用。

### 2.4 AI 与回合驱动

- 新增确定性的 `EnemyActionDecider`，评分全部合法技能与目标。
- 新增 `BattleTurnDriver`，明确等待玩家、自动决策、结算、表现、换波、完成和故障状态。
- AI 无合法行动时停止并暴露 `NoLegalAction`，不再无限推进。
- 原 `FirstSkillDecider` 保留为简单策略／测试策略，并补齐空值、能量和存活目标检查。

### 2.5 结构化结算

- 每次成功行动生成 `BattleActionRecord` 和若干 `BattleEffectRecord`。
- 记录伤害、治疗、护盾、能量、战技点、Buff、拉条和天赋追击。
- `BattleFlow.ActionHistory` 保存本场历史，`LastResolution` 保存最近一次结算。
- 新增串行 `BattlePresentationQueue`，默认即时完成，允许动画层异步接管单条效果表现。

### 2.6 多波

- `BattleWave.enemies` 从运行时 `BattleUnit[]` 修正为配置态 `UnitConfig[]`。
- 新增 `BattleEncounterConfig` 和 `BattleWaveController`。
- 新敌人按当前绝对时间加入行动轴；我方剩余行动时间、生命、护盾、Buff、能量和战技点不重建。
- 非最终波清空时不发布胜利，最后一波清空后才结束战斗。
- 未配置 encounter 时继续把 `BattleManager.enemies` 当作首波。

### 2.7 奖励、配队和存档

- `PlayerProfileData` 从 v1 升到 v2，增加 `battle` 模块。
- 注册 v1→v2 迁移，旧角色和背包数据保留，战斗进度补空值。
- 配队变更通过完整档案原子保存，失败时不替换运行期配队。
- 胜利时一次性结算背包奖励、完成次数、最佳回合、最后完成时间和最后奖励。
- 战斗结束事件、结果 UI context 和结果面板传递并显示奖励或结算错误。
- `BattleFormation.MaxMembers` 从公开常量改为静态只读属性，避免 LitJson 反序列化常量字段。

## 3. 新增代码文件

| 文件 | 职责 |
| --- | --- |
| `Assets/Scripts/GamePlay/Battle/InBattle/Data/ActionValidation.cs` | `ActionFailure`、只读校验结果和行动执行结果 |
| `Assets/Scripts/GamePlay/Battle/InBattle/Data/BattleSkillPoints.cs` | 共享战技点的消费、回复和可变上限 |
| `Assets/Scripts/GamePlay/Battle/InBattle/Data/BattleResolutionRecord.cs` | 行动记录与效果记录的数据结构 |
| `Assets/Scripts/GamePlay/Battle/InBattle/Data/BattleEncounterConfig.cs` | 波次、战技点和奖励的关卡配置资产 |
| `Assets/Scripts/GamePlay/Battle/InBattle/Common/BattleUnitFactory.cs` | 创建本场逻辑单位并释放运行时技能／Buff 资产 |
| `Assets/Scripts/GamePlay/Battle/InBattle/Common/BattleActorRegistry.cs` | 逻辑单位与场景模型映射、生成、查询和销毁 |
| `Assets/Scripts/GamePlay/Battle/InBattle/Common/BattleTurnDriver.cs` | 玩家／AI 回合边界、表现等待和波次切换状态机 |
| `Assets/Scripts/GamePlay/Battle/InBattle/Common/BattleUIPresenter.cs` | 三块战斗面板的打开、刷新、换波和关闭 |
| `Assets/Scripts/GamePlay/Battle/InBattle/Common/BattlePresentationQueue.cs` | 结构化结算的串行表现队列 |
| `Assets/Scripts/GamePlay/Battle/InBattle/Common/BattleWaveController.cs` | encounter 波次游标和旧单波回退 |
| `Assets/Scripts/GamePlay/Battle/InBattle/Common/EnemyActionDecider.cs` | 正式敌人技能／目标评分策略 |
| `Assets/Scripts/GamePlay/Battle/BattleParty/Data/BattleProgressData.cs` | 配队、关卡完成记录和奖励收据 |
| `Assets/Editor/BattleActionVerification.cs` | 第 1—7 阶段专项验证和真实按钮验证入口 |

每个新增 Unity C# 文件都包含对应 `.meta`，GUID 不依赖重新创建资源。

## 4. 修改代码文件

### 4.1 战斗核心

| 文件 | 修改内容 |
| --- | --- |
| `BattleAction.cs` | 增加 `caster`，行动请求不再隐式依赖当前单位 |
| `BattleUnit.cs` | 删除场景 `gameObject` 引用，保留纯运行时战斗数据 |
| `BattleFlow.cs` | 统一校验／执行、共享战技点、结构化记录、重入保护、AI 兜底、多波与胜负逻辑 |
| `ActionQueue.cs` | 防止重复加入；新单位基于当前时间计算首次行动时刻 |
| `TargetSelector.cs` | 支持动态加入单位；拒绝外场 caster／target |
| `EffectService.cs` | 执行并记录全部效果，增加战技点回复、Buff 移除和行动提前记录 |
| `BattleTalentResolver.cs` | `ActionResolution` 绑定结构化记录；天赋追击写入效果记录 |
| `BattleManager.cs` | 改为服务组装器；接入 encounter、回合驱动、AI、UI、换波、奖励和兼容 API |
| `BattleUnitActor.cs` | `Bind(null)` 安全返回，actor 生命周期由注册表负责 |
| `FirstSkillDecider.cs` | 补齐 caster、空技能、能量和死亡目标处理 |

### 4.2 配置与波次

| 文件 | 修改内容 |
| --- | --- |
| `SkillConfig.cs` | 增加 `skillPointGain` |
| `Enums.cs` | 增加 `EffectType.GainSkillPoint` |
| `BattleWave.cs` | 敌人数据改为 `UnitConfig[]` |
| `EnemyWaves.cs` | 波次列表默认初始化，避免空列表 |
| `UIContextData.cs` | 敌人 UI context 增加 `IBattleActorLookup` |
| `BattleCharacterSnapshot.cs` | 养成快照复制并还原 `skillPointGain` |

### 4.3 配队、档案与奖励

| 文件 | 修改内容 |
| --- | --- |
| `BattleFormation.cs` | `MaxMembers` 改为静态只读属性，避免序列化公开常量 |
| `BattlePartyManager.cs` | 优先读取档案配队；所有变更先保存候选快照再发布事件 |
| `PlayerProfileStore.cs` | 档案 v2、`battle` 模块、深拷贝和 v1→v2 迁移 |
| `PlayerProfileCoordinator.cs` | 完整档案保存同时跟踪战斗进度 revision |
| `PlayerRuntime.cs` | 暴露战斗进度；增加配队保存和胜利奖励原子提交 |
| `InventoryService.cs` | 增加奖励批量校验／添加和已保存快照提交入口 |
| `BattleResultEvents.cs` | 结束事件携带奖励收据和奖励错误 |
| `BattleSessionController.cs` | 将奖励信息传给结果 UI context |

### 4.4 战斗 UI

| 文件 | 修改内容 |
| --- | --- |
| `BattleCommandsUIContext.cs` | 从直接持有 `BattleManager` 改为 `IBattleCommandSource` |
| `BattleUIController.cs` | 使用只读校验刷新按钮；订阅目标变化；显示共享战技点；完整解除订阅 |
| `BattleEnemiesStatusPanel.cs` | 通过本场 actor lookup 定位模型，不再读取 `BattleUnit.gameObject` |
| `BattleResultUIContext.cs` | 携带奖励收据和结算错误 |
| `GameOverCanvasUI.cs` | 运行时增加奖励文本并显示奖励或失败原因 |

### 4.5 验证代码

| 文件 | 修改内容 |
| --- | --- |
| `CharacterProgressionVerification.cs` | 测试行动显式 caster／技能归属、档案 v2、战技点产出快照 |
| `SceneFlowVerification.cs` | 在真实战斗场景执行按钮、actor lookup、能量拒绝和敌方自动推进检查 |

## 5. 文档文件

| 文件 | 用途 |
| --- | --- |
| `Docs/BattleActionValidation.md` | 第一阶段行动 API、目标规则和失败语义 |
| `Docs/BattleSystemOptimization.md` | 第 1—7 阶段交付摘要和第 8 阶段路线 |
| `Docs/BattleSystemChangeLog.md` | 本文，记录全部改动 |
| `Docs/BattleSystemGuide.md` | 当前完整战斗系统说明 |

## 6. 兼容性处理

- 保留 `BattleManager.Instance`、`battleFlow`、`StartBattle()`、`ChooseBasic/Skill/Ult()`、`ChooseAbility()` 和 `CreateAbilityAction()`。
- 保留 `BattlePartyManager.AddorReplace()` 旧拼写入口。
- 没有 encounter 时仍使用 Inspector `enemies`。
- 没有显式配置普攻战技点产出时默认回复 1。
- `PartyFormationState` 继续作为当前运行会话缓存；正式来源优先使用玩家档案。
- 战技点文本复用现有 `Characters` 占位节点，没有修改 prefab YAML。

## 7. 验证记录

环境：Unity `6000.3.23f1`。

| 验证 | 已完成结果 |
| --- | --- |
| 战斗逻辑专项 | 143 项通过 |
| 养成、存档和六天赋编辑态回归 | 332 项通过；新增战技点快照断言需要在下次 Unity 刷新后重跑 |
| 完整场景、UI 和真实战斗命令 | 260 项通过，0 个未处理运行异常 |
| 直接战斗场景启动 | 4 项通过，0 个未处理运行异常 |
| 运行时 C# 工程静态编译 | `Assembly-CSharp.csproj` 通过，0 个错误 |
| 补丁格式 | `git diff --check` 通过 |

场景回归使用随机隔离存档，结束后恢复 `PlayerRuntimeConfig` 并删除测试存档，没有保存带未提交修改的 `SampleScene`。附加 `CharacterProgressionPlayVerification` 的后置坐标精确比较问题和桌面构建边界见 [BattleSystemOptimization.md](BattleSystemOptimization.md)。

直接构建 `Assembly-CSharp-Editor.csproj` 仍会在 Unity 自带的 ProBuilder 包 `EditorHandleDrawing.cs` 中遇到既有 `ObjectPool<>` 类型歧义；这与本轮战斗源码无关。Unity 自身此前已成功完成脚本域重载和上述编辑态／Play Mode 回归。新增的养成快照战技点断言因为 Unity 窗口当前最小化，尚未触发新一轮编辑器导入，因此没有修改既有的 332 项报告数字。

## 8. 未实施范围

第 8 阶段仍未实施：弱点／削韧／击破、暴击、持续伤害和终结技插队。当前 `ElementType` 只是配置数据，尚未参与弱点或伤害倍率计算。
