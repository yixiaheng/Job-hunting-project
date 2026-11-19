# 战斗系统第 1—7 阶段优化说明

本文是本轮交付摘要。更细的资料分为：

- [BattleSystemChangeLog.md](BattleSystemChangeLog.md)：本轮所有新增与修改文件、行为变化和验证记录；
- [BattleSystemGuide.md](BattleSystemGuide.md)：当前整个战斗系统的架构、配置、运行流程和扩展方式；
- [BattleActionValidation.md](BattleActionValidation.md)：第一阶段行动校验接口的详细约定。

## 1. 完成范围

本轮把原先由 `BattleManager` 集中处理的战斗流程拆成可验证的逻辑、场景、表现和持久化模块，同时保留原场景与 UI 的兼容入口。

| 阶段 | 状态 | 交付内容 |
| --- | --- | --- |
| 1. 行动校验与统一执行 | 已完成 | `ValidateAction`、`TryExecute`、`TryStep`、结构化失败原因、跨战斗目标拒绝 |
| 2. `BattleManager` 职责拆分 | 已完成 | 单位工厂、actor 注册表、回合驱动、UI presenter、表现队列 |
| 3. 共享战技点 | 已完成 | 初始 4、默认上限 5、可变上限、技能消耗／产出、额外回复效果 |
| 4. 正式敌人 AI | 已完成 | 合法候选枚举、技能与目标评分、确定性决策、合法技能兜底 |
| 5. 结构化结算与表现播放 | 已完成 | 行动记录、效果记录、历史、串行表现入口、异步完成回调 |
| 6. 多波关卡 | 已完成 | encounter 配置、波次控制、当前时间入轴、跨波状态保留 |
| 7. 奖励、配队和完整存档 | 已完成 | 胜利奖励、完成记录、配队持久化、档案 v2、v1→v2 迁移 |
| 8. 进阶战斗规则 | 未实施 | 弱点／击破、暴击、持续伤害、终结技插队，保留为后续路线 |

## 2. 当前架构

```text
BattleManager（场景组装与兼容入口）
├─ BattleUnitFactory       配置／养成快照 -> 逻辑单位
├─ BattleActorRegistry     逻辑单位 <-> 场景模型，负责模型生命周期
├─ BattleWaveController    encounter 波次游标
├─ BattleFlow              行动轴、校验、结算、胜负和结构化记录
│  ├─ ActionQueue
│  ├─ TargetSelector
│  ├─ EffectService
│  └─ EnemyActionDecider
├─ BattleTurnDriver        玩家边界、敌方自动回合、波次切换状态机
├─ BattlePresentationQueue 结构化结算 -> 串行表现
└─ BattleUIPresenter       三个战斗面板的打开、刷新、换波与关闭

BattleSessionController    战斗结果页和重试／退出会话
PlayerRuntime              配队、奖励、完成记录和原子档案
```

正常调用链如下：

```text
按钮 -> BattleManager.CreateAbilityAction
     -> BattleFlow.ValidateAction（仅查询，不改状态）
     -> BattleTurnDriver.Submit
     -> BattleFlow.TryExecute
     -> ActionResolution / BattleActionRecord
     -> BattlePresentationQueue
     -> 自动敌方回合或重新等待玩家
```

`BattleManager` 仍保留场景和旧 UI 使用的公开入口：`Instance`、`battleFlow`、`StartBattle()`、`ChooseBasic/Skill/Ult()`、`ChooseAbility()` 和 `CreateAbilityAction()`。它不再直接拥有所有规则和模型查找逻辑。

## 3. 行动、目标和回合规则

每次行动必须显式携带 `caster`、`skill` 和可选 `mainTarget`。执行前统一检查：

- 战斗是否已经结束或正在同步结算；
- 行动者是否属于本场、存活且正好轮到行动；
- 技能实例是否属于行动者，配置与数值是否合法；
- 能量、战技点是否足够；
- 显式目标是否属于本场。

单体和扩散技能在未选目标、目标死亡或阵营不符时，自动补选本场第一个合法存活目标。显式传入其他战斗的单位时直接返回 `ForeignTarget`，不会降级为自动补选。

回合驱动公开以下状态：

- `WaitingForPlayer`：等待我方按钮提交；
- `AutoDeciding`：敌方 AI 决策；
- `Resolving`：逻辑结算；
- `Presenting`：播放结构化表现；
- `TransitioningWave`：清理旧敌人并加入下一波；
- `Completed`、`Faulted`、`Stopped`：终态或人工停止。

同步重入被拒绝；查询失败不会消耗行动时间、资源或次数。敌方没有任何合法行动时进入 `Faulted`，不会死循环或伪造胜利。

## 4. 共享战技点

共享资源位于 `BattleFlow.SkillPoints`，仅我方技能消费或获得：

- 默认初始值：4；
- 默认上限：5；
- encounter 可覆盖初始值和上限；
- `SetMaximum(maximum, fillAddedCapacity)` 可在战斗中改变上限；
- `SkillConfig.skillPointCost` 定义消耗；
- `SkillConfig.skillPointGain` 定义技能自身产出；
- `EffectType.GainSkillPoint` 可作为技能效果提供额外回复手段。

为了兼容现有技能资源，普通攻击在 `skillPointCost == 0` 且 `skillPointGain == 0` 时默认回复 1 点。新角色建议显式配置产出和消耗，避免依赖兼容默认值。

操作栏使用 `Characters` 占位节点在运行时创建战技点文本，显示为 `战技点 当前值/上限`；没有改写现有 prefab YAML。

## 5. 敌人 AI

`EnemyActionDecider` 对所有能量可支付、配置完整的技能评分，并为单体／扩散技能选择合法目标。评分考虑：

- 基础攻击、战技、终结技的技能优先级；
- 伤害、预计击杀和受影响目标数；
- 友方生命缺口与治疗价值；
- 护盾、Buff、Debuff、净化、能量、战技点和行动提前；
- 扩散目标的相邻单位数量。

同分时保持技能列表与站位顺序，决策是确定性的，便于复现和测试。`BattleFlow.TryStep()` 还保留按技能列表寻找首个合法技能的最终兜底。

## 6. 结构化结算与表现接入

每次成功行动生成 `BattleActionRecord`，记录：

- 回合数、行动开始和结束的行动轴时间；
- 施法者、技能、主目标；
- 行动前后能量和共享战技点；
- 本次产生的全部 `BattleEffectRecord`。

效果记录类型包括伤害、治疗、护盾、能量、战技点、Buff 添加／移除、行动提前和天赋追击。最近一次记录位于 `BattleFlow.LastResolution`，完整历史位于 `BattleFlow.ActionHistory`。

`BattlePresentationQueue` 默认立即完成，所以现有战斗不会因缺少动画播放器而停住。接入动画时只需设置 `EntryPresenter`，并在单条表现结束后调用回调：

```csharp
manager.PresentationQueue.EntryPresenter = (effect, completed) =>
{
    PlayEffect(effect, completed);
};
```

播放器必须保证每条记录最终调用一次 `completed`。逻辑层无需等待具体 Animator、特效或飘字实现，也不需要重新结算伤害。

## 7. 多波配置

创建 `BattleEncounterConfig` 后配置：

1. `id`：稳定且唯一的关卡标识，用于完成记录；
2. `initialSkillPoints` 和 `maximumSkillPoints`；
3. `waves.battleWaves`：每波的 `UnitConfig[] enemies`；
4. `victoryRewards`：最终胜利发放的物品 ID 与数量；
5. 将 encounter 赋给战斗场景中的 `BattleManager.encounter`。

如果场景没有 encounter，`BattleManager.enemies` 继续作为唯一首波，旧场景不需要立即迁移。

换波时：

- 我方生命、护盾、Buff、能量和战技点不重建；
- 我方已经排定的绝对行动时刻不变；
- 新敌人的首次行动时刻为“当前时刻 + 基础行动值 / 速度”；
- 旧敌人模型和 UI 条目被移除，新敌人重新登记；
- 仅最后一波清空后发布胜利。

当前代码已覆盖 Flow 级两波验证；项目现有 `BattleScene` 仍使用 Inspector 单波兼容路径，没有为了测试改写场景资源。

## 8. 配队、奖励与存档

玩家档案 `PlayerProfileData` 现为 v2：

```text
PlayerProfileData
├─ roster      角色养成
├─ inventory   背包与装备
└─ battle
   ├─ formation    已确认配队
   ├─ completions  关卡完成次数、最佳回合、最后完成时间和最后奖励
   └─ revision
```

`PlayerProfileStore` 注册了 v1→v2 迁移。旧档案加载时保留角色与背包，并补充空的战斗进度；`formatVersion` 更新为 2。`BattleFormation.MaxMembers` 改为静态只读属性，避免 LitJson 尝试反序列化公开常量。

配队每次增、删、替换或整体确认时，都先构造候选快照，再通过 `PlayerRuntime.TrySetFormation()` 原子写入完整档案；写入失败不会替换运行期配队。

最终胜利由 `PlayerRuntime.TryGrantBattleRewards()` 一次性提交：

- 校验并暂存奖励清单；
- 更新背包候选快照；
- 更新关卡完成次数、最佳回合、UTC 时间和最后奖励；
- 将角色、背包、配队／战斗进度写入同一个带备份的档案；
- 成功后更新运行期背包并生成 `BattleRewardReceipt`。

奖励物品 ID 必须存在于 `ItemCatalog`，数量必须大于 0。结算失败时结果页显示错误，避免静默丢奖励；同一个 `BattleManager` 只发布一次本场结果。

## 9. 主要扩展点

- 新角色技能：配置 `SkillConfig` 的能量、战技点、目标规则和效果列表。
- 新战技点回复方式：使用 `EffectType.GainSkillPoint`，或调用本场 `BattleSkillPoints.Gain()`。
- 新 AI 策略：实现 `IActionDecider`，仍由 `BattleFlow.ValidateAction()` 做最终合法性校验。
- 新结算动画：实现或绑定 `IBattleResolutionPresenter`，消费记录而不是重复读取／修改单位数值。
- 新关卡：创建 `BattleEncounterConfig`，不要把波次编排写进 `BattleManager`。
- 新档案模块：同步扩展 `PlayerProfileData.Copy()`、迁移和 `PlayerProfileCoordinator` 的原子提交参数。

## 10. 第八阶段路线

第八阶段本轮没有实施。建议按依赖顺序推进：

1. 在单位与技能配置中建立伤害属性、弱点集合和韧性；
2. 把伤害计算拆成可组合的命中、暴击、减伤和最终伤害管线；
3. 增加削韧、击破状态和击破伤害记录；
4. 将持续伤害定义为可结算的状态效果，并明确触发时点；
5. 在 `BattleTurnDriver` 之上增加终结技请求队列和插队窗口；
6. 为上述规则扩展 `BattleEffectRecordType`，使表现层继续只消费结构化记录。

终结技插队不应直接修改 `ActionQueue` 的普通剩余时间；它需要独立的请求队列、合法窗口和恢复原行动边界的规则。

## 11. 验证结果与边界

验证环境：Unity `6000.3.23f1`，2026-11-18。

| 验证 | 结果 | 报告 |
| --- | --- | --- |
| 第 1—7 阶段逻辑专项 | 143 项通过 | `Temp/BattleActionVerification/results.json` |
| 养成、存档迁移和六天赋编辑态回归 | 332 项通过 | `Temp/CharacterProgressionChecks/editmode.txt` |
| 完整场景、UI 与真实战斗命令回归 | 260 项通过，0 个未处理运行异常 | `Temp/SceneFlow/results.json` |
| 直接战斗场景启动与退出 | 4 项通过，0 个未处理运行异常 | `Temp/SceneFlow/direct-Battle.json` |

完整场景回归实际覆盖三块战斗 UI、回合驱动、actor lookup、非法终结技不推进、合法普攻与敌方自动推进，并使用随机隔离存档。测试结束后恢复了 `PlayerRuntimeConfig` 并移除测试存档，没有保存或覆盖带未保存修改的 `SampleScene`。

附加的 `CharacterProgressionPlayVerification` 本轮没有作为通过门禁：它在角色培养、真实保存、UI 恢复均通过后，于恢复场景 `0.8s` 后使用 `Vector3 ==` 比较玩家坐标而失败；完整场景回归已在返回完成回调中精确验证位置、旋转、速度和移动状态恢复。该附加测试未继续到后半段，因此不计入上述通过结果。

本轮没有重新构建桌面 EXE；以上结论是编辑器编译、编辑态验证和 Play Mode 回归结果。正式发布前仍应补一次目标平台构建和包体运行验证。
