# 战斗表现重建验证记录

Unity 6000.3.23f1，2026-11-20。

|验证|结果|报告|
|---|---|---|
|原战斗动作、资源、目标、波次与存档逻辑|143 项通过|battle-logic-final.json|
|表现时钟、队列、命中显示、死亡、独立舞台与镜头恢复|44 项通过|presentation-final.json|
|新阵型、选择预览、槽位隔离、三种宽高比安全框、关键帧与持久草稿|43 项通过|rebuild-final.json|
|真实 BattleScene、HUD、技能选择、工作台暂停与恢复、演出中重开|14 项通过，原存档配置恢复|playmode-final.txt|
|直接进入战斗、真实结算界面、返回主场景与时间恢复|4 项通过，无运行异常|scene-flow-final.json|

新系统在编辑器中编译成功。独立 Roslyn 编译也检查过运行时与编辑器程序集；第三方 KCC 示例的过时 Physics API 和原热更新工具的不可达代码警告不属于本次修改。

验证使用占位模型、测试技能和隔离存档；通过的是系统流程、资源归属、可调性和恢复行为，不代表已制作最终人物动画、美术或全部角色专属分镜。

截图：studio-preview.png 是不进入 Play Mode 的预览渲染；normal-view-final.png、skill-view-final.png、stage-view-final.png 来自真实 BattleScene 的最后一次实机验证。

SceneFlow 的旧结束战斗测试原本直接调用底层 Flow.Step 并只等待一帧。新流程有胜利演出屏障，因此已改为调用 TurnDriver.StepOnce 并等待真实结算面板，而非跳过演出来满足旧测试。
