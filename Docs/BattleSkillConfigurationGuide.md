# 技能表现配置与试用指南

更新：2026-11-24。本文对应本次实现，配置资源统一从 `Assets/Config/Battle/Presentation/Skills/` 开始试用。

## 1. 先看见完整效果

1. 示例已生成。需要补齐时，菜单打开 `Tools > Battle > Presentation > Create Skill Template Samples`。生成器不覆盖原角色／敌人资源；已有样例只做缺失材质等本轮已知问题的修复。正式制作建议先复制样例。
2. 在上述目录选择 `MeleeApproach.asset`，Inspector 点击“打开技能表现预览”。也可通过 `Tools > Battle > Presentation > Skill Studio` 打开。
3. 攻击者选择同目录的 `Caster.prefab`，目标选择 `Target.prefab`。
4. 点击“重播”。观察接近、攻击、受击、返回。调整双方参考距离、行动者槽位、目标槽位和“测试目标规则”，再重播。辅助样例默认全体我方，其他样例默认单体；测试规则只影响预览假数据，真实战斗仍由 SkillConfig 决定目标。
5. 换成 `Ranged.asset`、`Support.asset`、`Staged.asset`，依次试其他模板。

预览生成的是隔离测试模型和假战斗数据，不读取或写入玩家进度。关闭窗口清理预览。配置草稿保存在 `Library/BattlePresentationDrafts/Skill_*.json`，只有“保存到配置资产”才写回源配置；删 Library 会丢未保存草稿。

“重新求值到秒数”从初始状态重建并向前模拟，回拖不会累计移动。预览时间是整次行动时间；Timeline 显示的是攻击局部时间，两者在近战中不会相同。

左侧按钮默认显示“暂停”，尚未播放时不可点击；点击“重播”从头开始。播放中点击“暂停”后文字改为“继续”，再次点击从暂停处恢复。播放结束或跳过后回到默认状态；单帧和跳到指定时间会停在当前进度，可用“继续”接着播放。

## 2. 四种模板

| Template | 使用场景 | 需要重点配置 |
| --- | --- | --- |
| InPlace | 兼容原有序列，原地播放 | Timeline 或 Controller 攻击状态 |
| MeleeApproach | 走过去打 | 目标 MeleeStand、Stand Offset、Approach、Returning、Return Mode |
| Ranged | 原地施法、射击 | Cast / Hit、Projectile、事件 |
| Support | 治疗、护盾、增益一次性表现 | 生效事件、Impact Prefab、Impact Anchor |
| Staged | 固定位置的完整演出 | Stage Space、Stage Origin、参与者位置或 Stage Prefab |

模板按技能选择，不按整个角色选择。同一角色可以普攻近战、战技辅助、终结技固定演出。

旧资产默认 InPlace，原有技能不会因为更新框架就自动冲向敌人。指定 Stage Prefab 的旧序列自动使用原对象隔离演出，不再创建外观替身。

## 3. 给目标设置精确攻击点

打开目标 prefab，在 `BattleUnitActor` 同一个对象上添加 `BattleActorPresentation`，点击“补齐默认挂点”。已有引用不会覆盖。

| 挂点 | 含义 | 怎么摆 |
| --- | --- | --- |
| MeleeStand | 攻击者到哪里站着打 | 放在目标身体前方地面，蓝色前向指向攻击者应面向的方向 |
| Hit | 命中位置 | 身体中部；正式模型可以放在骨骼下 |
| Foot | 地面特效位置 | 脚底 |
| Cast | 发射位置 | 手、武器或施法位置 |

选择组件可直接用 Scene 手柄移动和旋转挂点。MeleeStand 不应跟着受击弯腰晃动，建议放在模型根的稳定子节点；Hit 可以跟骨骼。

默认 MeleeStand 在目标局部 `(0,0,1.7)`，默认朝向局部 Y 180 度，只是胶囊示例值。你的模型朝向不同，应直接在 Scene 调整。

近战最终位置 = MeleeStand 位置 + 挂点旋转 × 技能 Stand Offset。Stand Euler 在挂点朝向上叠加。偏移按米计算，不额外按模型缩放乘倍数。

没有 MeleeStand 时会提示警告并原地播放，不会把角色移到世界原点。

## 4. 接近 Animator 怎么配

示例 `Capsule_Skill.controller` 包含 Idle、Approach、Attack、Return。可以打开它作为参考。

### 4.1 由 Animator 控制全部身体状态

在技能配置的 Animation 中选择 `AnimatorController`：

- Approach State：例如 `Base Layer.Approach`，使用循环跑步动作。
- Attack State：例如 `Base Layer.Attack`，使用攻击动作。
- Return State：例如 `Base Layer.Return`，使用返回动作。
- Approach Rate：默认 `ApproachPlaybackRate`，Animator 中建立同名 Float。
- Return Rate：默认 `ReturnPlaybackRate`，建立单独 Float。
- 在 Approach 状态的 Speed 参数中启用 `ApproachPlaybackRate`；Return 同理。
- Blend Seconds：进入状态的 CrossFade 时间。
- Controller Attack Seconds / Impact Seconds：没有主 Timeline 时，决定攻击持续时间和生效时间。

框架进入接近状态、调整其专属倍率，到位后请求攻击状态。不能让 Approach 仅凭 Exit Time 自动出手，因为路程可能更长。其他姿态混合和过渡仍由你制作，但不要让无条件 Transition 把框架刚进入的技能状态立即带走。

Controller 模式下，主 Timeline 可以继续控制镜头和事件，Caster 身体动画轨会跳过，避免争抢同一 Animator。攻击有 Timeline 时，以 Timeline 时长和事件为准，Controller 的 Attack Seconds 只用于无 Timeline 回退。

### 4.2 保留你现在的 Timeline 攻击动画

选择 `Timeline`：接近／返回仍可用 Controller 状态；到位后将身体动画交给 Timeline 的 Caster 轨，结束后恢复驱动。胶囊缺少接近状态也能移动，只是没有跑步身体动作。

不要把跨越战场的距离录在 `Capsule_BasicAttack` 中。它只负责到攻击点后的局部动作。当前攻击 Marker 0.45 秒仍是攻击局部 0.45 秒，不用加接近时间。

距离适配不修改整台 Animator 的全局 speed。Approach 的 Reference Speed 表示动作正常播放对应的移动速度；实际速度除以它得到接近动作倍率，再按 Playback Limits 限制。Return 使用自己的设置。

Approach 的 Preferred Seconds 是希望多久到达；实际速度限制在 Minimum / Maximum Speed 中，超远允许接近时间延长。整体 Root Motion 在表现期间关闭。

## 5. 把技能绑定到真实战斗

两种配置方式：

1. 在角色 prefab 的 `BattleActorPresentation > Skills` 增加条目，填写实际 Skill Id 和 Sequence。这个角色的覆盖优先。
2. 选择 BattleScene 中的 `BattlePresentationCoordinator`，在 Skills 表中填写 Skill Id、Caster Faction 和 Sequence。

Skill Id 查看对应 `SkillConfig`。绑定使用 ID，支持养成产生的运行时技能副本。示例资产不自动替换所有现有技能映射。

你可以先只把岩壁守卫的一个普攻绑定为 MeleeApproach，再给真实敌人 prefab 补挂点；不用一次迁移全部角色。

配置 `Body Animator` 必须是实际播放身体动作的 Animator。Light Hit / Death 可放你制作的 Clip，路径仍相对于这个 Animator。原敌人可见子节点名为 enemy，你现有攻击 Clip 路径是 Capsule；需自行统一目标 Clip 路径或只调整这个 prefab，不能把不匹配的 Clip 当成已正常绑定。

## 6. 命中与生效标记

Timeline 添加 `BattleImpactMarker`，Kind 决定行为：

| Kind | 含义 |
| --- | --- |
| LegacyIndex | 保留旧 effectIndex / healthEffectsOnly 的方式 |
| ImpactGroup | 消费某个 EffectConfig 的全部目标记录，分发受击与生效效果 |
| CueOnly | 只播放目标效果，不重复应用数值 |
| LaunchGroup | 为指定组的目标发射弹体，到达时消费结果 |
| FlushRemaining | 应用未消费且未等待弹体的剩余结果 |

Effect Group 是 `SkillConfig.effectConfigs` 的从 0 开始序号。`-1` 选择主目标组及全部未消费效果。普通群攻优先使用 ImpactGroup，不用为每个敌人手工放一条 Target 动画轨。

LegacyIndex 的单个索引仍只应用一条记录；群攻如果希望同时掉血，改用组事件。资源、自回能等不会改变组序号。

普通受击使用目标组件 Light Hit。若主目标已经被 Timeline 的 Target 动画轨控制，通用受击不会再抢写这个 Animator。

数值已由 BattleFlow 结算，标记只决定何时显示和播放反馈。一个效果只应用一次；只有一条总伤害时，不要用三个 Impact 期望扣三遍。前两下可以 CueOnly，最后显示实际总伤害。

## 7. 远程与辅助

远程启用 Projectile 后配置 Prefab、FixedTime / FixedSpeed、Seconds / Speed、Follow Target。没有 Prefab 时显示一个小球用于调试。

在发射时放 LaunchGroup。主 Timeline 结束而弹体还没到时，框架保持表现状态直到到达；暂停和倍速同样作用于弹体。旧 Legacy Marker 在远程弹体模板中会转成组发射，避免提前显示命中。

Impact Prefab 是命中／生效的一次性特效；Impact Anchor 选择 Body、Foot、Cast，Impact Offset 是相对于目标挂点的偏移。Effect Lifetime 表示最大存活时间，本次行动退出也会清理临时效果。

Support 使用 ImpactGroup，在满血队友没有实际治疗记录时仍播放目标特效。全部我方模型显示，固定状态栏保留。

持续护盾罩／长期 Buff 特效不应直接作为本次 Impact Prefab 使用：当前实现交付的是一次性反馈，持续状态视觉需要后续按稳定状态标识接入独立生命周期。

## 8. 固定演出

选择 Staged：

- BattlefieldArea：Stage Origin / Euler 相对战场原点；Caster / Target Position 与 Euler 相对这个演出原点。
- 其他目标根据 Additional Target Spacing 排列。
- IsolatedSet：指定安全的 BattleStage prefab，Caster Anchor / Target Anchor 优先。原对象进入该布景并临时改层，原血条继续跟随。
- Isolated Set Offset 是隔离布景相对演出原点的额外偏移，默认 `(0,-1000,0)`；可以按关卡调整。Additional Target Anchors 可为额外目标逐个指定挂点，未指定则沿间距排列。
- Stage prefab 保持只含布景组件、灯光和挂点，不放战斗业务脚本、相机或 Collider。

需要制作整体轨迹时，在 Timeline 新增 `BattleStagePoseTrack`，创建 `BattleStagePoseClip`，选择 Caster / MainTarget，配置起终位置、旋转、Progress，以及 Height / Lateral / Forward 曲线。后三者是在舞台 Y / X / Z 方向的附加米数，可制作拱起或侧绕；Progress 决定起终位置和旋转间的进度。这些坐标相对舞台，曲线会驱动原 PresentationRoot。不要再用身体 AnimationTrack 同时写同一个整体根。

进入／退出需配合切镜，避免看到摆位瞬移。固定区域贴近墙壁或地形时自行检查穿模；隔离布景由框架在战场下方的专用位置实例化并控制渲染。默认恢复后再播放战场死亡段，不让死亡对象重现。

## 9. 运行时排错

选择 Coordinator 可查看当前 Session / Phase（调试 Inspector 提供阶段与事件信息）。暂停使用统一 Clock，不手工冻结某个 Animator。

| 现象 | 检查 |
| --- | --- |
| 原地攻击没有接近 | 模板是不是 MeleeApproach；实际目标有没有 MeleeStand；映射是否被角色覆盖 |
| 身体不跑 | Body Animator、Approach State 全路径、Controller 状态存在、Speed 参数启用 |
| 跑得太滑 | Reference Speed、Playback Limits、Minimum / Maximum Speed |
| 打空或贴得过近 | 目标 MeleeStand 和技能 Stand Offset，不要先缩放模型 |
| 攻击不动 | Driver 模式；Controller 模式跳过 Caster 动画轨是有意行为；检查状态名与 Controller |
| 受击不动 | Light Hit 的动画路径是否匹配；Target 轨是否已经占有 Animator |
| 群攻分批掉血 | 是否仍使用旧单条索引，改为 ImpactGroup |
| 治疗没有特效 | Impact Prefab 是否配置；用组事件而不是只找 Heal 数值记录 |
| 演出结束后一直等待 | 检查阻塞反应、弹体是否有到达；会话有活跃时间超时保护 |
| 血条丢失 | 挂点是否在模型下、相机裁剪范围、对象是否激活与允许世界显示 |

`SkipCurrent()` 跳过当前表现并继续回合；`Cancel()` 属于整场拆除，不能拿来做技能跳过按钮。

## 10. 验证入口

- `Tools > Battle > Presentation > Verify Skill Templates`：四模板、接近组合、暂停与恢复。
- `Tools > Battle > Presentation > Verify Presentation`：原表现链路回归。
- `Tools > Battle > Presentation > Verify Camera Tuning`：相机调参与选择显隐。
- `Tools > Battle > Presentation > Verify Real Battle Scene`：真实场景生命周期。
- `Tools > Battle > Verify Action Submission`：战斗规则与结算记录。

报告位于 `Temp/BattlePresentation/` 和 `Temp/BattleActionVerification/`。实际运行结果以本次报告时间与 passed 为准。后续请先从示例配置一次，再在实际角色上试；遇到问题可指出模板、技能 ID、当前阶段和表现现象。
