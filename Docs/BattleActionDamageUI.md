# Action累计伤害UI

## 使用效果

右上角只显示当前Action的一份累计伤害。Action开始时隐藏；第一条正伤害呈现时出现，后续命中逐条相加。群攻对多个目标的伤害累计到同一个数字。同一帧内的命中会在该帧显示合计，不人为延长战斗或编造命中间隔。

纯治疗、加盾、Buff、能量变化和零伤害均不显示。整次行动表现（含归位、受击等待、死亡退场）结束后立即清空隐藏。独立终结技/追加攻击Action重新计数；跳过、取消、退出战斗也会清空。

默认统计实际生命伤害和护盾吸收量，排除过量伤害。敌方行动也采用同一规则。数字直接更新，不用插值滚数拖延真实命中时机。

## 配置入口

- 面板：`Assets/Prefabs/UIPrefabs/HUD/Battle/BattleActionDamagePanel.prefab`
- UIConfig：`Assets/Config/UI/HUD/BattleActionDamageUIConfig.asset`
- UIId：`BattleActionDamage`，已加入UIRegistry，由BattleUIPresenter随战斗打开和关闭。
- 调整位置/尺寸：面板中的`DamageContent`，右上角锚点。默认位置(-90,-150)，大小600×170，以当前UIManager的3840×2160参考分辨率布局。
- 调整数值字号/颜色：`DamageContent/DamageNumber`的TMP组件。默认72，过长数字自动缩小到最低28。
- 修改标题：`DamageContent/Label`，默认`TOTAL DAMAGE`。可直接改成中文，并使用覆盖中文字形的TMP字体。

初始`DamageContent`为隐藏状态。编辑预制体时可以暂时开启查看排版；运行时由面板按当前Action有无伤害控制。

## 时机和职责

`BattlePresentationCoordinator.ActionSequence`开始Action → `ActionDamage.Begin`。

`ApplyEntry`在Timeline命中标记/弹道到达/默认命中点呈现结算记录 → `ActionDamage.Present`去重、累计并发出Changed → UI刷新。

`EndActionSession`正常结束、跳过或取消 → `ActionDamage.End`清空并通知UI隐藏。

该状态属于表现层，不参与伤害计算，不监听提前发生的逻辑扣血事件。面板仅订阅状态、格式化并展示数字。UIManager负责面板生命周期，关闭/禁用时解除订阅，重新启用或更换Context时重新绑定。

技能演出隐藏普通HUD时，此伤害面板继续显示；场景挂起仍遵循UIManager的统一挂起规则。所有文字和装饰关闭raycastTarget，不遮挡点击。

## 多段配置的前提

实际分段由已有SkillEffects效果与命中标记控制。一个效果只结算了一次伤害，即使动画挥了三刀，也不会由这个UI拆成三份；需要配置三条真实伤害结果，并让各命中点分别呈现。重复标记不会重复累计同一条结果。没有命中标记时沿用现有默认命中/末尾补齐行为。

## 验证入口

- `Tools > Battle > Verify Action Damage HUD`：面板、配置登记、去重、无伤害隐藏、多段累计、护盾、64位总量、Context与关闭清理。
- `Tools > Battle > Presentation > Verify Presentation`：实际两段Timeline的累计时机，以及完成、取消、跳过、辅助行动。

Edit Mode验证直接驱动UIPanel生命周期，并显式调用禁用/启用回调；不等价于已经完成真实玩家操作的Play Mode验收。

本次验证（2026-11-27）：Unity重新编译通过；伤害面板专项19项通过；表现流程62项通过（含新增Timeline累计、取消、跳过、辅助技能检查）。结果见`Docs/Verification/BattleActionDamageResults.json`和`BattleActionDamagePresentationResults.json`。未启动用户当前场景进行实际Play Mode视觉验收。
