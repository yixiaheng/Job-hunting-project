# 战斗技能表现系统实施报告

日期：2026-11-24。项目：Unity 6000.3.23f1。

本轮已实现四种技能表现模板、胶囊体示例、配置预览工具和配套验证，可以开始逐个技能配置。原有战斗技能映射继续使用原配置；示例不会自动替换整场战斗的所有技能。

配置操作见 [BattleSkillConfigurationGuide.md](BattleSkillConfigurationGuide.md)。设计依据保留在 [BattleSkillPresentationPlan.md](BattleSkillPresentationPlan.md)。本报告描述当前已交付内容及其实际边界。

## 1. 实际交付

| 模板 | 已实现的行为 | 作者需要配置 |
| --- | --- | --- |
| MeleeApproach | 目标攻击站位快照 → 接近 → 主攻击 → 等待命中反馈 → 返回 → 死亡处理 → 释放控制权 | 目标 MeleeStand、技能偏移、接近／返回状态和速度、攻击 Timeline 或 Animator 状态 |
| Ranged | 原地身体动作 → 标记发射 → 弹体飞行 → 到达时显示结算与受击 → 等待完成 | Cast / Hit 挂点、弹体 prefab 或调试球、飞行时间／速度、命中标记 |
| Staged | 取得原对象控制权 → 固定区域或隔离布景摆位 → 演出 → 恢复原对象、相机与图层 | 布景或区域、参与者姿态、额外目标挂点／间距、专属镜头和动作 |
| Support | 原地施放 → 对已解析的我方目标分发生效效果 → 保留全队模型与固定 HUD | 生效标记、单体／全体规则、一次性特效、Foot / Hit 等挂点 |

旧序列的默认模板为 InPlace，继续按已有方式原地播放。模板属于技能，因此同一角色的不同技能可以使用不同模板。

### 1.1 接近属于技能，速度只作用于对应状态

近战接近、攻击、返回共同占据一次技能表现会话。战斗不会在角色仍在返回、弹体未到达或必须等待的受击未结束时交给下一次行动。

接近使用 Animator 中独立的 Approach 状态，返回使用 Return 状态，以 CrossFade 请求作者制作的混合。按距离计算实际移动速度，再由 Reference Speed 换算专属 Float 参数。不会把这个倍率应用到整次攻击 Timeline。

有两种身体驱动：

- Timeline：接近／返回用 Controller 状态，到位后主攻击身体由 Timeline 接管。
- AnimatorController：接近、攻击、返回都请求 Controller 状态；Timeline 可同时编排镜头和生效标记，跳过其 Caster 身体轨，避免双重控制。

框架在技能期间使用手动统一时钟推进 Animator，暂时关闭 Root Motion；结束后恢复之前的参数、状态、速度和剔除设置。这里的统一时钟处理与“调整整个 Animator 的攻击播速”是两件事。

### 1.2 逻辑身份、整体位移与局部动画分开

逻辑 BattleUnit 和 pos 不随技能位移改变。PresentationRoot 用于接近、返回和固定演出摆位；VisualRoot 及内部节点由身体动画控制。已有对象的 prefab 层级不被批量重建。

BattlefieldDirector 为每个参与者提供移动控制令牌。技能接管时停止该对象未完成的准备前移，技能期间拒绝重新应用阵型，释放后恢复常态控制。

目标的 MeleeStand 决定攻击者站立位置与朝向，技能在此基础上叠加 Stand Offset / Euler。站位点在接近开始时取快照，不追着受击动画移动。缺少挂点时警告并原地播放。

### 1.3 生效对象来自逻辑结算记录

BattleFlow 额外记录本次主目标集合、每个 EffectConfig 的实际目标集合及效果组编号。这样，即使满血治疗没有产生 Heal 数值记录，表现层仍知道哪些队友需要播放生效特效。

生效标记支持旧单条索引、整组生效、仅反馈、整组发射和剩余结果补齐。同一结算记录只消费一次；表现层不重新算伤害。显示生命支持追加攻击记录，护盾前后值保存在结算记录和显示缓存中，供显示层使用。

弹体默认对发射时目标点取快照，也可显式开启 Follow Target。追踪模式下，目标移动或转向后仍按 Hit 挂点的局部偏移求目标点。弹体可超出主 Timeline 时长，行动继续等待其到达。

### 1.4 固定演出使用原表现对象

不会克隆一份战斗单位代演。原 PresentationRoot 临时移动，原血条挂点继续随对象变化。

- BattlefieldArea：在战场参考系中的固定区域摆位。
- IsolatedSet：在同一 Unity Scene 的偏移位置实例化纯表现布景，临时分配表现图层和相机遮罩；默认偏移为 `(0,-1000,0)`，可配置。

BattleStage 的攻击者、主目标和额外目标挂点优先于配置坐标。其他参与目标按挂点或间距摆放；非参与者暂时隐藏。演出退出恢复图层、灯光遮罩、相机遮罩和原位置，并先把技能镜头移回战场，避免从地下布景向战场混合。

新增 BattleStagePoseTrack / Clip，可以在舞台局部空间为攻击者或主目标配置起终姿态，以及高度、侧向和前向曲线。这个轨道控制整体根，身体 AnimationTrack 控制模型内部动作。

### 1.5 完成与清理

正常结束等待主序列、阻塞弹体及受击、返回和死亡流程。剩余结算补齐后产生的受击也会等待完成。

SkipCurrent 跳过单次表现，补齐显示、清理临时对象并只完成一次回调；Cancel 用于整场退出，抑制旧回调。暂停、倍速和场景挂起通过统一表现时钟推进。活跃表现时间超过配置上限时走异常收尾，防止战斗永久等待。

攻击敌方期间，其他我方角色持续隐藏至整次技能结束；我方辅助技能保留全队。命令区可单独隐藏并关闭交互，固定队伍状态栏和世界血条不因这项设置一起消失。

## 2. 配置工具和资源

菜单入口：`Tools > Battle > Presentation > Skill Studio`。

预览在独立测试空间中创建胶囊或所选 prefab，使用临时逻辑数据执行真实表现流程，不读取玩家进度。支持：

- 选择攻击者／目标 prefab；
- 调整双方参考距离、行动者槽位、敌人数和目标槽位；
- 选择单体、扩散、群体等测试目标规则；
- 播放、暂停、单帧、重播、跳过；
- 重新构建并模拟到指定时间，避免反复拖动造成位移累计；
- 查看当前阶段、局部 Timeline 时间、生效日志、敌方显示生命和攻击点提示；
- 在配置草稿上修改数值，显式保存回源资产。

草稿存于 `Library/BattlePresentationDrafts/`，源配置的 GUID 在保存时保持不变。预览草稿只复制 Sequence 配置；其引用的 Timeline、Clip 和 prefab 仍是共享资产，编辑这些资源时应使用 Unity 的正常资产保存流程。需要做变体时先复制对应资源。

角色组件 Inspector 提供“补齐默认挂点”，Scene 手柄可移动、旋转挂点。运行时 Coordinator Inspector 显示会话、阶段和事件，并提供跳过当前技能按钮。

示例资源位于 `Assets/Config/Battle/Presentation/Skills/`：

| 资源 | 用途 |
| --- | --- |
| MeleeApproach.asset、MeleeAttack.playable | 接近后播放现有胶囊攻击的独立示例副本 |
| Ranged.asset / .playable | 施放、发射和弹体到达 |
| Support.asset / .playable | 我方群体生效反馈 |
| Staged.asset、StageAttack.playable | 原对象固定区域摆位与镜头 |
| Caster.prefab、Target.prefab | 附带挂点和受击的隔离胶囊测试模型 |
| Capsule_Skill.controller | Idle / Approach / Attack / Return，以及独立速度参数 |
| Capsule_Approach / Cast / Hit / Idle | 用于验证接口的简单胶囊动作 |
| ImpactGlow / SupportGlow、SkillParticles.mat | 用于确认命中、辅助事件的基础粒子效果 |

生成入口为 `Create Skill Template Samples`。生成器补齐缺失资源，并修复本轮早期样例的缺失材质、过小粒子范围等已知样例问题；不会重新覆盖原 Stone、原敌人 prefab 或原攻击动画。正式制作建议复制样例后配置自己的技能。

## 3. 关键代码位置

路径均相对项目根目录。

| 文件 | 责任 |
| --- | --- |
| Assets/Scripts/GamePlay/Battle/InBattle/Presentation/BattleSequenceDefinition.cs | 技能静态配置、模板、动画状态、接近参数、弹体与舞台设置 |
| 同目录 BattleActorPresentation.cs | 模型 Animator、挂点、受击／死亡 Clip、按技能 ID 的角色覆盖 |
| 同目录 BattleActionPresentationSession.cs | 单次表现快照、控制令牌、记录消费、弹体、特效与清理 |
| 同目录 BattleActorAnimationDriver.cs | Animator、Timeline 和反应动画的驱动边界与恢复 |
| 同目录 BattlePresentationCoordinator.cs | 模板流程、事件分发、相机绑定、舞台与完成条件 |
| 同目录 BattleStagePoseTrack.cs / BattleStagePoseClip.cs | 固定演出的局部整体运动 |
| 同目录 BattlefieldDirector.cs | 逻辑槽位对应的 HomePose 和单一位移控制权 |
| Assets/Scripts/GamePlay/Battle/InBattle/Data/BattleResolutionRecord.cs | 主目标、效果组目标、护盾前后值 |
| Assets/Editor/BattleSkillAuthoring.cs | 配置 Inspector、挂点手柄、Skill Studio |
| Assets/Editor/BattleSkillSamples.cs | 胶囊样例生成 |
| Assets/Editor/BattleSkillVerification.cs | 四模板、锚点、弹体、保存重载等验证 |

BattleManager / BattleUIPresenter 增加命令区独立显隐订阅；BattleEnemiesStatusPanel 根据原 actor 的世界表现可见性更新血条。运行时代码没有直接引用 UnityEditor。

## 4. 本次实际验证

验证在本机 Unity 编辑器执行，未生成桌面构建。时间为北京时间 2026-11-24；原始结果归档在 [BattleSkillVerification](BattleSkillVerification/)。

| 验证 | 实际结果 | 内容 |
| --- | --- | --- |
| 技能模板验证 | 526 / 526 通过，17:26:15 | 四模板正常／跳过、四个行动槽位 × 五个敌方目标、精确攻击点、Controller、暂停、原对象舞台、长弹体等待、满血辅助、资源保存重载、目标转向后的局部命中偏移 |
| 既有表现验证 | 54 / 54 通过，17:26:33 | 时钟、队列、输入驱动、Timeline、舞台、相机草稿及恢复 |
| 相机调参验证 | 15 / 15 通过，17:21:56 | 即时数值、生效与退出、显隐及草稿保存 |
| 阵型与相机重建验证 | 58 / 58 通过，17:25 | 阵型、人数、构图、相机所有权等回归 |
| 战斗行动校验 | 143 / 143 通过，16:49:44 | 提交、查询无副作用、资源和结算回归 |
| 真实 BattleScene | 14 项检查通过，17:27:12 完成并恢复 | 真实 HUD、选技能、暂停、命中前显示隔离、镜头返回、舞台遮罩、重试与旧回调失效 |
| SceneFlow 直接启动战斗 | 4 / 4 通过，17:05:38 | 场景初始化、真实结果面板、返回 Sample、时间恢复；无记录到的运行时异常 |

真实场景验证使用临时存档槽，并恢复原存档配置和编辑器启动场景。场景烟测用于验证接入与生命周期；四个新模板的细项覆盖来自专门的表现验证，不能把两者当成对所有正式技能逐个完成美术验收。

已逐张查看近战、远程、辅助和固定演出的预览关键帧。辅助粒子在修正发射范围后可见；固定演出样例已调整镜头，使攻击者与主目标都进入画面。样例用于看清流程和配置关系，美术仍为胶囊和基础粒子。

## 5. 当前边界与后续制作

1. **正式角色动画由作者继续制作。** 本轮没有新建完整人形角色、做动作重定向或制作正式技能特效；新增的是可替换的接口和胶囊闭环。
2. **接近为直线距离适配。** 不含导航、绕障或地形碰撞。需要侧绕、跳跃等完整轨迹时，可使用固定演出位移轨道；普通近战接近尚未提供任意路径编辑器。
3. **Root Motion 暂不作为整体位移来源。** 技能期间以程序位移和局部身体动画为主，避免多个系统争抢 Transform。
4. **一次性效果受会话生命周期限制。** 当前命中特效在达到 Lifetime 或行动退出时清理；持续护盾罩、长期 Buff 外观需要独立的状态视觉生命周期。
5. **群体逻辑与群体编舞分开。** 群体标记可对多个目标分发反馈；只有一个主目标身体轨和两种舞台角色移动轨。专属多人互动编舞仍需扩展轨道或制作专用序列。
6. **场景模型路径仍需匹配动画。** 示例 Target 将可见节点命名为 Capsule 以匹配胶囊 Clip；真实 enemy prefab 的节点名不同，不能直接假定同一个 Clip 已正确绑定。
7. **真实战斗逐个技能迁移。** 先配置目标挂点，再在角色 Skills 覆盖或 Coordinator Skills 表绑定 Sequence；不自动把现有普攻、战技和终结技全改成示例。
8. **镜头观感仍需要作者调。** 框架提供接管、轨道、完成等待和恢复，不会自动生成角色专属高质量镜头。

## 6. 建议的第一次配置

先在 Skill Studio 打开 MeleeApproach.asset，用示例 Caster / Target 重播一次；改变距离和目标槽位，确认接近、攻击、受击、返回。随后复制一份配置，用岩壁守卫的实际技能 ID 绑定它，并给真实敌方 prefab 添加 MeleeStand / Hit。

第一次只调攻击点位置和 Stand Offset，确认到位后，再换接近／返回动画和相机轨。遇到问题时记录技能 ID、模板、CurrentPhase 和画面现象，可以直接定位到移动、身体动画、生效事件或相机其中一条链路。
