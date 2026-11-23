# 战斗相机系统文档入口

当前实现已经重建。请阅读 [战斗表现重建：实施计划与使用说明](BattlePresentationRebuild.md)。

新入口是 `Tools → Battle → Presentation → Presentation Studio`。旧 Camera Tuning 窗口与旧 BattleCameraDirector 不再作为配置入口；旧配置数值保留为迁移快照，新运行镜头使用 BattleShotDirector 和按状态划分的 Shot Settings。
