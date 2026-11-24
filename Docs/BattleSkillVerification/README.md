# 本轮验证证据

2026-11-24，Unity 6000.3.23f1。时间为北京时间。

| 文件 | 实际运行结果 |
| --- | --- |
| skills.json | 技能模板验证，526 项通过，17:26:15 |
| results.json | 原表现链路回归，54 项通过，17:26:33 |
| camera-tuning.json | 相机配置回归，15 项通过，17:21:56 |
| rebuild.json | 阵型与相机重建回归，58 项通过，17:25 左右；原报告不包含完成时间字段 |
| action-submission.json | 逻辑行动校验，143 项通过，16:49:44 |
| playmode.txt | 真实 BattleScene，14 项检查通过；原保存配置和启动场景恢复，17:27:12 |
| scene-flow.json | SceneFlow 直接战斗启动，4 项通过，17:05:38，无记录到的运行时异常 |

四张模板截图为技能预览实际渲染的 960 × 540 画面：MeleeApproach.png 为本次行动约 0.85 秒，其他三张为约 0.50 秒。这个时间包含近战接近，不等于攻击 Timeline 的局部时间。截图不包含编辑器 GUI 覆盖的目标标签和攻击点辅助线。

normal-view.png、skill-view.png、stage-view.png 来自真实 BattleScene 的本轮烟测。烟测使用现有场景映射，主要验证 HUD、暂停、舞台和生命周期；它与四种新模板的专门验证分开记录。

这些基础胶囊、粒子和样例镜头用于确认接口与流程，不代表正式角色的美术完成度。完整说明见 [实施报告](../BattleSkillImplementationReport.md)。
