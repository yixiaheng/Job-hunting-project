# 当前战斗系统说明

## 1. 系统目标与边界

当前战斗系统负责：

- 从已确认配队和角色养成数据生成本场快照；
- 创建逻辑单位和场景模型；
- 维护绝对时间行动轴；
- 校验并结算玩家／敌人行动；
- 管理共享战技点、能量、生命、护盾、Buff 和天赋；
- 生成结构化结算记录并交给表现层；
- 切换敌人波次；
- 打开战斗 UI 和结果页；
- 保存配队、奖励和关卡完成记录。

目前不负责弱点、削韧、击破、暴击、持续伤害和终结技插队。这些属于第 8 阶段。

## 2. 模块目录

```text
Assets/Scripts/GamePlay/Battle/
├─ BattleParty/       配队数据、配队管理和战斗进度
├─ Events/            战斗事件与结果请求
├─ Launch/            跨场景启动快照和战斗会话
├─ InBattle/
│  ├─ Data/           单位、技能、效果、Buff、encounter、记录结构
│  ├─ Services/       伤害、治疗、能量和 Buff 的原子服务
│  └─ Common/         Flow、行动轴、AI、驱动、actor、UI、波次
└─ Register/          旧战斗存档登记

Assets/Scripts/UI/Battle/   战斗状态栏、操作栏和结果面板
```

## 3. 核心对象职责

| 对象 | 生命周期 | 职责 |
| --- | --- | --- |
| `BattleManager` | 战斗场景 | 组装本场服务，保留场景／UI 兼容入口 |
| `BattleFlow` | 单场战斗 | 规则、行动校验、结算、胜负、历史记录 |
| `ActionQueue` | 单场战斗 | 绝对时间、下一行动者和行动提前 |
| `BattleTurnDriver` | 单场战斗 | 玩家边界、敌方自动回合、表现等待、换波 |
| `BattleUnitFactory` | 单场战斗 | 配置／养成快照生成逻辑单位，释放运行时资产 |
| `BattleActorRegistry` | 单场战斗 | 逻辑单位与场景模型映射和模型生命周期 |
| `BattleUIPresenter` | 单场战斗 | 三块战斗 UI 的打开、刷新和关闭 |
| `BattlePresentationQueue` | 单场战斗 | 按顺序播放结构化效果记录 |
| `BattleWaveController` | 单个 encounter | 波次配置游标 |
| `BattleSessionController` | 战斗场景 | 结果页、重试、重新配队和退出 |
| `PlayerRuntime` | 应用运行期 | 玩家完整档案、配队、奖励和关卡记录 |

## 4. 配置数据

### 4.1 `UnitConfig`

单位配置包含稳定 `id`、显示名、图标、属性／命途、基础生命、攻击、防御、速度、初始／最大能量、模型 prefab 和技能列表。

我方进入战斗时不会直接使用全部基础值：`BattleCharacterSnapshot` 会把角色等级、装备、行迹／技能等级和天赋结果固化成一份本场快照。敌人直接由 `UnitConfig` 映射。

### 4.2 `SkillConfig`

| 字段 | 含义 |
| --- | --- |
| `kind` | 普攻、战技或终结技 |
| `element` | 技能属性；目前不参与弱点计算 |
| `skillPointCost` | 我方施放时消耗的共享战技点 |
| `skillPointGain` | 我方施放时产生的共享战技点 |
| `energyCost` | 施放前需要且会扣除的能量 |
| `energyGain` | 主动效果结束后给施法者回复的能量 |
| `targetRule` | `Single`、`Spread`、`AoE` 或 `Self` |
| `targetSide` | 我方或对方 |
| `effectConfigs` | 按列表顺序执行的效果集合 |

普通攻击在 `skillPointCost == 0` 且 `skillPointGain == 0` 时兼容性回复 1 点。需要特殊产出时应显式设置 `skillPointGain`。

### 4.3 `EffectConfig`

一个技能由多个效果组成。效果可沿用技能目标，也可通过 `overrideTarget` 单独设置目标阵营和规则，因此同一技能可以伤害敌人并给队友加盾。

| `EffectType` | 当前行为 |
| --- | --- |
| `Damage` | 按施法者指定属性计算伤害，先扣护盾再扣生命 |
| `Heal` | 按施法者指定属性治疗存活目标 |
| `AddShield` | 以指定属性为基数叠加护盾 |
| `ApplyBuff` | 添加或叠层 Buff，刷新持续行动次数 |
| `GainEnergy` | 修改目标能量并限制在 `0..maxEnergy` |
| `AdvanceAction` | 缩短目标剩余行动时间；自身效果延后到重新排轴后应用 |
| `RemoveDeBuff` | 移除目标全部 Debuff |
| `RemoveBuff` | 移除目标全部状态 |
| `GainSkillPoint` | 为我方共享战技点回复 `value` 取整后的数值 |

`key` 用于标识结构化记录；`origin` 区分主动技能、天赋和装备；`growsWithSkill` 决定养成快照是否按技能等级放大数值。

### 4.4 `BuffConfig`

当前 Buff 只支持属性百分比修正。实际属性计算为：

```text
最终属性 = round(基础属性 × (1 + Σ(Buff.value × 层数 × valueMultiplier)))
```

`durationTimes` 表示持有者再行动多少次后过期。新施加到施法者自身的 Buff 会跳过本次行动末尾的第一次计时，避免刚加上就少一回合。

### 4.5 `BattleEncounterConfig`

encounter 统一配置稳定关卡 ID、初始／最大战技点、敌人波次和胜利奖励。将资产赋给场景 `BattleManager.encounter` 后生效；没有 encounter 时使用 Inspector `enemies` 作为单波回退。

## 5. 从配队到战斗启动

```text
配队 UI
  -> BattlePartyManager.ApplySortedFormation
  -> PlayerRuntime.TrySetFormation（完整档案原子保存）
  -> BattlePartyManager.TryCreateBattleLaunchData
  -> BattleLaunchData 固化四个站位与养成快照
  -> BattleLaunchContext 跨场景暂存
  -> 加载 BattleScene
  -> BattleManager.Awake 消费启动快照
  -> BattleManager.StartBattle 组装本场服务
```

`BattleLaunchData` 只保存配置和纯数据快照，不持有场景对象。重试使用同一启动快照，因此战斗过程中发生的局外数据变化不会改变本次重试数值。

直接从编辑器打开战斗场景时，如果没有 `BattleLaunchContext`，`BattleManager` 使用 Inspector `allies` 构造兼容启动快照。

## 6. 战斗初始化顺序

`BattleManager.StartBattle()` 的主要步骤是：

1. 校验 UIManager 和我方配置／站位／prefab；
2. 创建 `BattleUnitFactory`、`BattleActorRegistry` 和 `BattleWaveController`；
3. 创建我方逻辑单位和 actor；
4. 读取首波配置，创建敌方逻辑单位和 actor；
5. 创建 `BattleFlow`、共享战技点、正式 AI；
6. 创建表现队列、回合驱动和 UI presenter；
7. 打开我方状态、敌方状态和操作栏三个 HUD；
8. `Ready()` 选出首个行动者；
9. 启动回合驱动，敌方先手时自动推进到玩家边界。

销毁或重新开始本场时按相反顺序停止驱动、关闭 UI、解除事件、清理 actor，并释放只属于本场的运行时技能和 Buff 配置。

## 7. 行动轴

行动轴使用绝对时间：

```text
基础行动值 = 10000
首次／下次行动时刻 = 当前时间 + 10000 / 当前速度
```

`ActionQueue.PeekNext()` 选择绝对时刻最小的存活单位；同一时刻使用 `tieBreaker` 保证稳定顺序。开始行动时将 `CurrentTime` 推进到该单位的绝对时刻。

行动提前按剩余时间计算：

```text
剩余时间 = nextActionTime - CurrentTime
新时刻 = CurrentTime + 剩余时间 × (1 - 提前比例)
```

例如剩余 50、提前 50%，新时刻是当前时刻加 25，而不是把整个绝对时刻乘 50%。新波敌人从当前时刻开始计算首次行动，我方原有绝对行动时刻保持不变。

## 8. 行动请求与校验

```csharp
var action = new BattleAction
{
    caster = flow.CurrentUnit,
    skill = selectedSkill,
    mainTarget = selectedTarget
};

ActionValidation preview = flow.ValidateAction(action);
BattleActionResult result = flow.TryExecute(action);
```

`ValidateAction()` 是只读查询，不是可重复使用的许可；实际提交会重新校验。失败类型包括战斗结束、正在结算、无行动者、行动者非法、不是当前行动者、技能缺失／不归属／配置非法、能量或战技点不足、外场目标、无合法目标、AI 无合法行动和驱动停止。

### 8.1 目标规则

- `Self`：施法者自己；
- `AoE`：目标阵营全部存活单位；
- `Single`：一个主目标；
- `Spread`：主目标和相邻站位的存活单位。

单体／扩散在未选、已死亡或阵营不匹配时自动选择第一个合法目标。显式目标不属于本场时返回 `ForeignTarget`，不会自动替换。

## 9. 单次行动结算顺序

成功提交的固定顺序是：

1. 锁定本次请求并进入 `IsExecuting`；
2. 行动轴推进到施法者时刻；
3. 触发回合开始天赋；
4. 创建 `BattleActionRecord`；
5. 我方消费／获得技能自身战技点；
6. 扣除技能能量消耗；
7. 按 `effectConfigs` 顺序选目标并执行效果；
8. 给施法者结算 `energyGain`；
9. 持有者 Buff 计时和过期清理；
10. 按结算后的速度安排下次行动；
11. 应用自身行动提前；
12. 触发行动后天赋／追击；
13. 完成记录，增加行动次数；
14. 检查胜负或待切换波次；
15. 释放同步执行锁。

## 10. 数值规则

### 10.1 伤害

```text
原始伤害 = max(0, round(施法者指定属性 × effect.value × (1 + damageBonus)))
最终伤害 = max(1, 原始伤害 - max(0, 目标防御))
```

最终伤害先扣护盾，剩余部分扣生命。当前没有暴击、属性抗性、弱点或击破倍率。

### 10.2 治疗和护盾

```text
治疗量 = max(1, round(施法者指定属性 × effect.value))
护盾量 = max(0, round(施法者指定属性 × effect.value))
```

护盾的 `scaleStat == Health` 时使用施法者最大生命，其余使用当前计算属性。治疗不能复活死亡单位。

### 10.3 能量与战技点

- 能量始终限制在 `0..maxEnergy`；
- 我方技能施放前检查 `energyCost` 和共享战技点；
- 敌人不消费我方共享战技点；
- 战技点始终限制在 `0..Maximum`；
- 上限可通过 `SetMaximum()` 动态改变。

## 11. 敌人 AI

`EnemyActionDecider` 枚举所有能量可支付且配置完整的技能，选择合法主目标并评分。评分包含技能类型、伤害、预计击杀、治疗缺口、护盾、Buff／Debuff、净化、能量、战技点、行动提前和受影响目标数。

同分时保持技能列表和站位顺序，因此结果可复现。最终行动仍必须通过 `BattleFlow.ValidateAction()`；正式策略返回非法行动时，`TryStep()` 会按技能列表寻找第一个合法技能作为兼容兜底。

## 12. 结构化记录与表现层

`BattleActionRecord` 保存回合、开始／结束时间、施法者、技能、主目标、行动前后能量与战技点。其效果列表由 `BattleEffectRecord` 组成。

表现层只消费记录，不重新读取数值并计算结果：

```csharp
manager.PresentationQueue.EntryPresenter = (effect, completed) =>
{
    PlayEffect(effect, completed);
};
```

默认没有绑定播放器时立即完成。自定义播放器必须在每个效果结束时调用一次 `completed`，否则回合驱动会停在 `Presenting`。

## 13. 回合驱动状态机

```text
Start
  -> 敌方：AutoDeciding -> Resolving -> Presenting -> 继续判断
  -> 我方：WaitingForPlayer

玩家提交
  -> Resolving -> Presenting
  -> 敌方自动行动若干次
  -> WaitingForPlayer / TransitioningWave / Completed / Faulted
```

`Faulted` 代表无法合法继续，例如轮到敌人但没有任何合法技能，或波次配置声称还有下一波但无法生成有效敌人。业务层不应在 `Faulted` 后继续强行调用 `BattleFlow`。

## 14. 多波

每波保存 `UnitConfig[]`，逻辑单位只在该波进入时创建。清空非最终波后，驱动进入 `TransitioningWave`：

1. 从 actor 注册表移除旧敌人模型；
2. 创建下一波逻辑单位和模型；
3. 将新单位加入 `BattleFlow`、行动轴和目标选择器；
4. 替换敌方 UI 条目；
5. 继续自动推进或等待玩家。

我方对象始终是同一批 `BattleUnit`，所以生命、护盾、Buff、能量、战技点和已经排定的行动时刻自然保留。

## 15. 战斗 UI

`BattleUIPresenter` 打开：

- `BattleAlliesStatus`：我方生命／能量与当前行动者；
- `BattleEnemiesStatus`：通过 actor lookup 把敌方血条定位到世界模型；
- `BattleCommands`：普攻、战技、终结技和战技点。

操作栏通过 `IBattleCommandSource` 查询 `Flow`、创建行动和提交技能。按钮是否可用完全来自 `ValidateAction()`，所以能量、战技点、技能归属、当前行动者或目标变化都会反映在 UI 上。

`MouseRayCasting` 只负责本场目标选择；目标变化事件包含 `BattleId`，其他战斗或已关闭面板不会响应。

## 16. 战斗结束、奖励和会话

最终胜负由 `BattleFlow` 产生。`BattleManager` 在我方胜利时调用 `PlayerRuntime.TryGrantBattleRewards()`，然后发布只一次 `BattleEndedEvent`。

`BattleSessionController` 根据本场 `BattleId` 打开结果页并处理：

- 直接重试：复用原 `BattleLaunchData`；
- 重新配队：回到配队场景并保留战斗会话；
- 退出：回到记录的退出场景并清除启动／会话上下文。

场景加载失败会回滚 `BattleLaunchContext` 和 `BattleSessionContext`，结果按钮恢复可操作。

## 17. 玩家档案

`PlayerProfileData` v2 的结构为：

```text
roster       角色养成
inventory    背包与装备
battle
├─ formation
├─ completions[]
│  ├─ encounterId
│  ├─ clearCount
│  ├─ bestTurnCount
│  ├─ lastClearedAtUtc
│  └─ lastRewards
└─ revision
```

配队和胜利奖励都通过 `PlayerProfileCoordinator` 写入同一个带备份档案。奖励先在临时 `InventoryService` 中完整校验，档案写入成功后再提交到运行期背包，避免逐项发放产生半套状态。

v1 档案加载时自动补充空战斗进度并升级为 v2。新增档案字段时必须同步更新 `PlayerProfileData.Copy()`、迁移和 coordinator 的 revision 判断。

## 18. 新内容接入流程

### 18.1 新角色／敌人

1. 创建或更新 `UnitConfig`；
2. 配置稳定 ID、属性、能量、prefab 和技能；
3. prefab 最好包含 `BattleUnitActor` 和 Collider；缺少时注册表会补组件／碰撞体；
4. 我方角色登记到角色／养成目录，敌人加入 encounter 波次。

### 18.2 新技能

1. 创建 `SkillConfig`；
2. 明确技能类型、目标阵营和目标规则；
3. 配置能量、战技点消耗／产出；
4. 按执行顺序添加效果；
5. 混合目标效果使用 `overrideTarget`；
6. 需要养成缩放的效果设置 `growsWithSkill`；
7. 确认技能加入单位的 `skills` 列表。

### 18.3 新关卡

1. 创建 `BattleEncounterConfig`；
2. 设置唯一 `id`；
3. 设置初始／最大战技点；
4. 配置每波敌人和胜利奖励；
5. 赋给战斗场景的 `BattleManager.encounter`；
6. 确认敌人站位数量不超过场景 `enemyPos` 锚点数量；
7. 用隔离存档验证奖励物品 ID 存在于 `ItemCatalog`。

## 19. 扩展原则

- 新规则优先进入 `BattleFlow` 或独立服务，不放回 `BattleManager`。
- UI 不直接改战斗数据，只构造请求、查询校验和显示状态。
- 场景模型只能通过 actor 注册表关联，不给 `BattleUnit` 增加 Unity 对象引用。
- 表现层只能消费 `BattleEffectRecord`，不能重复结算。
- 新持久化模块必须加入完整档案的深拷贝、迁移和原子提交。
- 新波次机制必须保留绝对行动时刻语义。

## 20. 验证与排错

### 20.1 验证入口

- 菜单：`Tools/Battle/Verify Action Submission`；
- 请求文件：向 `Temp/BattleActionVerification/request.txt` 写入 `test`；
- 完整场景：`Tools/Scenes/Verify Unified Scene Flow`；
- 养成／存档：`CharacterProgressionVerification.Run()`。

### 20.2 常见问题定位

| 现象 | 首先检查 |
| --- | --- |
| 按钮不可点击 | `ValidateAction().Failure`、当前行动者、能量、战技点和技能归属 |
| 单体技能打错目标 | `BattleId`、目标阵营、目标存活状态和 `MouseRayCasting.selectedTarget` |
| AI 停止 | `BattleTurnDriver.LastFailure`、敌人技能配置和能量 |
| 血条找不到模型 | `BattleActorRegistry` 是否包含该单位、actor 是否激活 |
| 换波后直接胜利 | `SetPendingWave`、encounter 是否还有有效波次 |
| 换波后我方跑条重置 | 不应重建 `BattleFlow` 或我方 `BattleUnit` |
| 奖励未到账 | 结果页 `RewardError`、ItemCatalog ID、存档只读状态 |
| 配队重启丢失 | 档案是否为 v2、`battle.formation` 和保存错误日志 |
| 表现后不再推进 | 自定义 `EntryPresenter` 是否调用了 `completed` |

当前验证结果与尚未覆盖的桌面构建、真实 encounter 资产边界见 [BattleSystemChangeLog.md](BattleSystemChangeLog.md)。
