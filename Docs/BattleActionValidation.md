# 战斗优化第一阶段：行动校验与执行

本阶段统一玩家、自动决策和编辑器验证的行动入口。本文保留第一阶段的接口细节；第二至第七阶段现已完成，整体架构、配置方法和扩展点见 [BattleSystemOptimization.md](BattleSystemOptimization.md)。

## 调用方式

```csharp
var action = new BattleAction
{
    caster = flow.CurrentUnit,
    skill = flow.CurrentUnit.skills[0],
    mainTarget = selectedTarget
};

ActionValidation preview = flow.ValidateAction(action);
// preview 适用于按钮显示；查询不改变任何战斗状态。
BattleActionResult result = flow.TryExecute(action);
// 提交会重新校验，不能将 preview 当作永久执行许可。
if (!result.Succeeded)
    Debug.LogWarning(result.Validation.Message);
```

- `BattleAction.caster` 必填，必须是本场当前存活行动者。
- 技能按实例归属检查，必须放入行动者的 `skills` 列表。养成生成的运行时技能副本可以直接使用。
- `Execute()` 和 `Step()` 保留布尔返回兼容入口，分别包装 `TryExecute()` 和 `TryStep()`。
- `BattleActionResult` 返回本次校验、成功标志、战斗结果及本次 `ActionResolution`。失败结果不借用上一行动的结算。
- `CurrentTime` 只读暴露行动轴时间，`IsExecuting` 表示当前正在同步结算。

## 校验和目标规则

统一检查终局、重入、行动者、技能归属、技能配置、能量与目标归属。

单体／扩散技能未选择目标，或者选择了本场已死亡、阵营不匹配的目标，继续自动选择第一个合法存活目标。来自另一场战斗，或者仅伪装相同 BattleId 但不在本场集合中的显式目标，一律拒绝。

全体和自身技能按配置选取目标。覆盖目标的效果独立选取目标，支持同一技能攻击敌人并保护队友。至少一个非空效果需要有合法目标；满血治疗、没有减益时净化仍可施放。

以下配置拒绝执行：负能量消耗、缺失／空／全空效果列表、未知目标或效果枚举、非有限效果数值，以及施加 Buff 时缺失配置。有效列表中的空效果项继续跳过。

## 状态与生命周期

非法行动不修改生命、护盾、能量、Buff、当前行动者、行动时间、行动次数或上一成功结算。查询发现终局时不发送结束事件；执行／自动推进发现终局时才完成一次结束通知。没有可调度单位时，执行入口保留原有平局行为。

正式顺序为：开始行动 → 回合开始天赋 → 扣能量和技能效果 → Buff 计时 → 安排行动间隔 → 自身拉条 → 行动后天赋 → 行动次数 → 胜负判定。

同步通知中的重复提交、自动推进和重新 Ready 被保护；执行完成或抛出异常时均释放重入锁。此保护不是完整事务回滚：业务监听者应遵守现有事件约定，不直接篡改单位／技能数据；旧 GameOverEvent 委托抛出异常时仍按原有语义向外传播。

## 玩家和 AI

`BattleManager.CreateAbilityAction()` 为按钮查询和实际提交构造相同请求。失败时显示原因并刷新，不继续敌方自动推进。能量不足的终结技不可点击，目标改变会触发查询刷新；面板关闭／禁用／销毁会解除订阅。

`BattleManager` 现使用第四阶段实现的 `EnemyActionDecider`，对全部合法技能和目标做确定性评分；`TryStep()` 仍保留按技能列表寻找合法技能的兼容兜底。完全没有合法技能时返回 `NoLegalAction` 并停止，战斗结果保持 None。

## 验证入口

- Unity 菜单 `Tools/Battle/Verify Action Submission`：专项编辑器验证。
- 或在 `Temp/BattleActionVerification/request.txt` 写入 `test`，编辑器会显式消费该请求；结果位于 `Temp/BattleActionVerification/results.json`。导入脚本本身不会自动运行验证。
- 养成回归使用现有 `CharacterProgressionVerification.Run()`；其测试角色已登记技能并显式指定行动者。
- `Tools/Scenes/Verify Unified Scene Flow`：现有隔离存档 Play Mode 回归，新增真实战斗按钮、拒绝提交和普攻后自动推进检查。该入口要求当前是 SampleScene，保留未保存编辑场景，并在结束时恢复存档配置。

验证数据使用临时 ScriptableObject 和现有隔离存档机制，不修改正式技能配置和玩家进度。

## 本次验证结果（2026-11-18）

Unity 6000.3.23f1 编译并执行以下验证：

| 验证 | 结果 | 本机报告 |
| --- | --- | --- |
| 行动提交与第 1—7 阶段专项 | 143 项通过 | `Temp/BattleActionVerification/results.json` |
| 养成与天赋回归 | 332 项通过 | `Temp/CharacterProgressionChecks/editmode.txt` |
| 实际场景与 UI 回归 | 260 项通过，无未处理运行异常 | `Temp/SceneFlow/results.json` |
| 直接启动战斗场景 | 4 项通过，无未处理运行异常 | `Temp/SceneFlow/direct-Battle.json` |

场景验证结束后已恢复原玩家配置、移除验证用隔离存档，未保存或覆盖用户的编辑场景。`git diff --check` 通过。本轮未重新生成桌面 EXE，以上运行证据来自 Unity 编辑器及 Play Mode。
