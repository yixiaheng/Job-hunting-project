# 战斗表现重建：实施计划与使用说明

更新日期：2026-11-23。目标是提供可调、可扩展、能够逐步做出类似商业回合制战斗效果的表现系统；不宣称复刻《崩坏：星穹铁道》的内部实现。

## 1. 实施顺序与交付边界

|阶段|实施内容|对应代码 / 资产|
|---|---|---|
|1|逻辑槽位与表现位置分离；四人、1—5 敌人、Boss 阵型；准备前移与归位|BattlefieldLayout / BattlefieldDirector / BattleActorRegistry|
|2|统一技能选择状态；自动选择合法普攻与目标；切换技能只预览、再次点击释放|BattleSelectionState / BattleFlow.PreviewAction / BattleManager|
|3|重写常态镜头；按状态、角色、目标和安全框求解；有限过渡后停止写入|BattleShotDirector / BattleShotSettings / BattleCameraProfile|
|4|镜头演出关键帧、片段混合、冻结或跟随参照、群体取景、命中与死亡屏障|BattleCameraShotClip / BattleCameraShotTrack / BattlePresentationCoordinator|
|5|统一编辑态与运行态调整窗口；真实模型预览、Scene 手柄、草稿保存|BattlePresentationStudio|
|6|迁移 BattleScene；保留原资源 GUID 和旧数值备份；提供代表性样例|Assets/Config/Battle/Presentation/|
|7|编译、自动验证、实机场景与 HUD 回归、文档|BattleRebuildVerification / BattlePresentationVerification / BattlePresentationPlayVerification|

战斗伤害、行动轴、战技点、波次推进与存档仍由原战斗逻辑负责。没有增加终结技插队、弱点击破或追加攻击的战斗规则；表现队列可以被这些未来规则调用。

## 2. 开始使用

Unity 菜单：**Tools → Battle → Presentation → Presentation Studio**。

默认资产：

- `Assets/Config/Battle/Presentation/Camera/DefaultCamera.asset`：各镜头状态的默认构图。
- `Assets/Config/Battle/Presentation/Camera/DefaultLayout.asset`：双方阵型、准备前移与槽位机位补偿。
- `Assets/Config/Battle/Presentation/BeforeRebuildCamera.asset`：重建前的相机数值快照，不参与新场景运行。
- `Assets/Scenes/BattleScene.unity`：已接入 BattlefieldDirector 和 BattleShotDirector。

工作台编辑的是内存中的草稿，修改立即预览；按“保存相机与阵型草稿”才写入配置资产。停止运行不会自动把临时相机 Transform 写回场景。

2026-11-22 调参修复：草稿保留隐藏和不随场景保存的标记，但不再带 NotEditable，工作台数值可以直接编辑；镜头参数直接展开显示。从相机 Config 的 Inspector 打开工作台时，会选中该配置。工作台草稿没有未保存调整时，Inspector 对源 Config 的修改会自动同步到预览；双方都有修改时，保留草稿并显示冲突提示，可点击“采用 Config 数值（重新载入相机和阵型）”明确放弃草稿。预览敌人数仍只控制预览阵型，不是按人数隔离的相机配置。

### 不运行游戏调整

1. 打开工作台，选择相机配置和阵型配置。
2. 选行动者槽位、目标槽位、镜头状态。
3. 选敌人数或 Boss；可替换我方和敌方预览模型。
4. 在“镜头构图”调相对位置、FOV、朝向和选择偏移；在“阵型 / 前移”调槽位。
5. 切换 16:9、4:3、21:9，检查青色安全框。
6. Scene 视图可拖机位、旋转、槽位和准备点手柄；窗口中的画面由独立预览场景渲染。正式场景不需要生成真实战斗对象。
7. 保存；下次战斗使用保存后的资产。

预览复制模型的网格、材质、骨骼和 Animator，不复制游戏脚本、碰撞器或战斗逻辑。预览场景的地板和灯光是中性环境，最终材质光照请在实际战场复查。

### 运行中调整并保留

1. 运行 BattleScene，等到我方可选招状态。
2. 工作台点“暂停当前战斗并实时调整”。暂停的是表现时钟，并锁住战斗输入。
3. 可在窗口直接更换预览行动者、目标与镜头状态；不会改变真正的当前回合、技能、目标或生命。
4. 修改阵型、前移、相对机位、FOV、朝向与选中偏移，Game 画面立即响应。
5. 点“结束实时调整，恢复战斗”，恢复正式阵型、当前选择和之前的暂停状态。未保存的修改保留为草稿。
6. 停止 Play 后点保存；也可以在暂停调试期间明确保存。

草稿保存在 `Library/BattlePresentationDrafts/<GUID>-<localFileId>.json`。重启编辑器可以恢复，但删除 Library 会删除草稿，因此正式成果必须保存到 Assets。不同 Timeline 子片段使用不同 localFileId，不会互相覆盖。

保存检测源资产是否发生外部变化；冲突时保留草稿并提供覆盖或取消。重新载入会放弃当前草稿，另存为会生成新的相机和阵型组合。另存资产后需自行将其指定给场景默认配置或 Encounter；不会悄悄改变所有战斗。

## 3. 空间约定与阵型

战场局部 +Z 指向敌方，+X 沿阵列横向，+Y 向上。BattlefieldRoot 可以整体平移或旋转战场。

```text
                    +Z / 敌方
           E0      E1      E2
                  战场中心
           A0   A1   A2   A3
                     +X →
```

默认示意值：我方横向间距 1.8，阵列 Z=-4；敌方间距 2.2、Z=4；Boss Z=6；行动准备前移 0.6；前移过渡 0.2 秒。这些是本项目可编辑的起始值，不是原游戏测量值。

- `BattleUnit.pos` 是逻辑槽位，扩散选目标仍用它。
- Identity 根对象停在正式槽位；PresentationRoot 承担准备前移、归位；模型在其下播放动画。
- 动作完成并恢复动画姿态后，再归位，完成后才允许回合推进。
- 死亡不压缩剩余敌人的槽位，原位置留空。
- 下一波使用本波 Formation Preset；为空时用 Encounter 默认；仍为空时按 Count1—Count5 查找。
- 超出预设数量使用居中横排后备算法。Boss 预设需要在 Encounter 或波次中明确指定 `Boss`，不会靠名字猜测。
- 相机自动识别大于 4 单位高的目标并尝试 Boss 镜头；这与是否指定 Boss 阵型是两件事。

## 4. 常态镜头参数

|字段|作用|
|---|---|
|Position|相对行动者准备位置的右、上、前偏移；负 Z 在后方|
|Align To Formation|以行动者到敌方阵列中心的水平朝向建立机位基准；关闭则用战场固定朝向|
|Manual Rotation / Rotation|手工指定朝向；关闭时瞄准动态构图点|
|Target Weight|敌方阵列中心与当前目标焦点的插值权重|
|Aim Offset|构图点偏移|
|Fov|垂直视野角；越小看起来越近，不改变模型大小|
|Selection Position / Rotation / Fov|选中目标后的附加偏移；每次从基础值计算，不累加|
|锁定行动者屏幕位置 / 行动者屏幕位置|将行动者焦点约束在归一化屏幕坐标；左下区域可从 `(0.22, 0.28)` 起调，切换敌人数和目标时重新求解|
|Seconds / Easing|有限过渡时间和曲线；中途换目标从当前镜头姿态重新开始|
|Fit Targets / Safe Area|根据模型包围盒、宽高比和安全框后退取景|
|Hide Other Allies|常态选招时临时不渲染其他我方模型；不移动或删除单位；演出取得相机控制权时恢复|
|槽位 Camera Offset|同一镜头规则在不同角色槽位上的额外位置补偿|

常态状态包括 EnemySingle、EnemySpread、EnemyGroup、AllySupport、Self、EnemyAction、Overview、Boss。

配置覆盖顺序是：默认 → Encounter → 当前行动者模型上的 Camera Profile → 工作台草稿。每个状态整项覆盖；禁用某状态可回退到下一层。原有 shoulderOffset 等字段只为序列化迁移保留且在 Inspector 隐藏，运行镜头不再执行旧求解器。

视角切换由目标、技能、回合、事件、配置变化或宽高比变化触发。LateUpdate 只处理尚未完成的过渡和输出刷新；稳定机位不会每帧重新写 Transform。播放技能镜头时需要逐帧采样，这是有时间轨迹的必要部分。

## 5. 输入与目标预览

- 我方回合开始，选择合法普攻；没有合法普攻时尝试战技。
- 点击不同技能只切换预览；点击已选技能执行；空格也可执行。
- 输入框有焦点时空格不触发战斗；场景加载、场景暂停、表现队列占用、角色准备移动和工作台调试时均禁止提交。
- 鼠标选目标经过本场归属、存活和阵营校验。
- 未选、死亡或阵营不合法的目标沿用自动补选；其他战斗的单位明确拒绝。
- 预览和释放使用同一目标选择规则，并汇总每个效果的目标；选择不扣资源、不推进回合、不造成伤害。
- 主目标指示与受影响目标圈分别展示；扩散按逻辑相邻槽位判断。
- 若确认瞬间合法目标已变化，先更新预览，下一次确认才释放。
- 终结技仍遵循当前行动者的合法性；这里没有实现插队。

## 6. 技能演出

`BattleSequenceDefinition` 指向 Timeline，配置可选的独立 Stage、是否隐藏 HUD、施法者与目标动画轨名称。Coordinator 的 Skills 按技能 ID 和阵营映射；Events 按事件字符串映射。

镜头片段支持：

- Battlefield / Caster / Target / TargetGroup 参考空间。
- 起终点与轨迹曲线，或任意数量的按秒关键帧。
- 每帧位置、旋转、FOV；关键帧的 cut 和缓动。
- 自动瞄准目标与手动朝向。
- 锁定起始参照或跟随动画；可采用锚点朝向。
- 目标组中心和包围盒取景。
- 位置震动与旋转震动；频率按秒计算。
- 重叠片段按 Timeline 权重混合，不重叠处可以硬切。

工作台“演出关键帧”选择 Timeline 内镜头子资产，可拖动时间预览相机，捕获 Scene 机位为关键帧并明确保存。完整角色动画轨、片段排布、命中标记仍在 Unity Timeline 编辑；工作台不是完整 Timeline 的替代编辑器。相机片段预览不会执行伤害标记。

`BattleImpactMarker` 指定显示第几个伤害/治疗结果，-1 刷出剩余结果。业务结算先完成，显示层到标记时才通知血条；跨帧越过多个标记按顺序补齐，每个结果只展示一次。事件镜头中的标记不造成业务结算。

代表性样例：SampleBattlefield、SpreadAttack、GroupAttack、SupportAction、EnemyAction、DeathFocus、WaveEntrance、Victory、SampleSeparateStage。它们用于验证流程和作为制作起点，模型和动画仍是占位素材，不是最终角色演出。

## 7. 演出生命周期与扩展

```text
选择 → 验证/结算 → 演出取得镜头控制权
     → 动画/镜头采样 → 命中显示
     → 恢复模型姿态 → 归位 → 死亡聚焦/隐藏
     → 归还镜头 → 下一行动者
```

- BattleStart、WaveEnter、Death、Victory、Defeat 已接入公共流程。
- `QueuePresentationEvent` 可供未来 Spawn、Revive、追加攻击等表现调用；召唤/复活的逻辑状态应由业务系统先建立。
- 演出期间不能再次提交指令；连续事件经过串行队列，重复完成回调和旧会话回调无效。
- 死亡镜头完成才隐藏模型；换波入场完成才推进；胜负演出完成才发布结束结果。
- 独立 Stage 克隆视觉层，原角色不移动；切换输出相机的可见层，限制本战斗场景内灯光对 Stage 的影响，恢复时还原。
- Cancel、重开战斗、禁用组件时停止 Timeline、清绑定、还原姿态/渲染器/灯光/相机遮罩/HUD/镜头归属，丢弃旧完成回调。
- 不修改其他场景的灯光，不把 UI 生命周期转移给相机；HUD 仍由 UIManager 管理。

## 8. 后续制作范围

基础系统负责“谁在何时占用镜头，以及如何安全返回”。高质量人物动画、每个角色的专属剪辑、环境遮挡优化、技能 VFX、景深、运动模糊、曝光等后期仍需要按素材和视觉目标制作。

当前没有通用的穿墙避障、自动消除行动者与目标的所有轮廓重叠、完整攻击动画重定向或最终版角色终结技。应对具体模型使用工作台调相对机位、焦点锚点、前移和槽位补偿，不假设一个默认值适配全部体型。

## 9. 验证入口与证据

- `Tools/Battle/Presentation/Verify Rebuilt System`：阵型、选择无副作用、前移不改逻辑槽位、关键帧、安全框、工作台创建。
- `Tools/Battle/Presentation/Verify Presentation`：时钟、队列、显示血量、死亡、Stage、取消和配置保存。
- `Tools/Battle/Presentation/Verify Real Battle Scene`：隔离存档下运行正式场景、HUD、技能、工作台暂停恢复与演出中重开。
- `Tools/Battle/Verify Action Submission`：原战斗逻辑回归。
- `Tools/Scenes/Verify Direct Battle Startup`：UIManager / SceneFlow 场景生命周期回归。

最终运行的报告和截图归档在 `Docs/BattlePresentationVerification/`。以报告中的时间、passed 和错误记录为准。测试使用临时存档，退出后还原原始存档配置。

## 10. 当前视觉起点与调参顺序

先确定模型脚底与焦点锚点，再调阵型，再调镜头，最后做技能轨迹。不要用改变伤害槽位或缩放角色去补镜头。

推荐从 EnemySingle 开始：行动者放画面左侧，敌人留在中右区域；调整 Position.X 控制过肩侧偏，Position.Z 控制前景人物大小，Position.Y 控制肩位高度。然后检查四个行动者槽位，必要时调各槽位 Camera Offset。切换不同目标时先调 Target Weight，再调 Selection Position / Rotation / Fov。

当前默认单体机位相对偏移为 (2.1, 1.9, -2.4)，FOV 45 度，均为本项目示意值。扩散和群体使用更高的取景位置、FOV 55 和目标组安全框。角色模型可以在 BattleUnitActor 上指定 Camera Focus Anchor；关闭 Automatic Framing Bounds 后，可手动设置 Framing Center / Size，避免特效或武器夸大取景包围盒。

进攻镜头可启用“锁定行动者屏幕位置”。坐标采用归一化视口：左下角 `(0,0)`，右上角 `(1,1)`；黄色十字表示工作台预览中的锚点。求解时先确定朝向、FOV 和敌方取景，再移动相机使行动者的 Camera Focus Anchor 落在该坐标。因此敌人数、选中目标和宽高比改变时，行动者仍保持在指定屏幕区域。这个约束属于常态战场镜头，不覆盖 Timeline 技能演出镜头。

普通状态的 Hide Other Allies 是有意采用的可选镜头表现规则，演出前会恢复原渲染状态。若希望常态也能看到全队，关闭此选项，再结合槽位前后错位和机位调整；它不改变任何单位的逻辑位置、碰撞或行动顺序。
