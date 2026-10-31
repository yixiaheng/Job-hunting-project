# 项目开发规范

## UIConfig 目录分类

- 所有 `UIConfig` 资源必须按 UIManager 使用的 `UILayer` 层级分类，以配置自身的 `layer` 字段为准。
- 目录名称与 `UILayer` 枚举保持一致，统一使用 `Window`，不使用 `Windows`。

| UILayer | UIConfig 存放目录 |
| --- | --- |
| HUD | `Assets/Config/UI/HUD/` |
| Window | `Assets/Config/UI/Window/` |
| Popup | `Assets/Config/UI/Popup/` |
| Toast | `Assets/Config/UI/Toast/` |
| Overlay | `Assets/Config/UI/Overlay/` |

- `Assets/Config/UI/` 根目录保留 `UIRegistry.asset` 等登记资源，不散放具体面板的 `UIConfig`。
- 新增 UIConfig 或修改其 `layer` 时，必须同时确认并调整资源目录；不能只创建配置和注册引用而忽略分类。
- 移动 Unity 资源时必须保留原 `.meta` 和 GUID，保持 UIRegistry、prefab 及其他资源引用有效；不能为了整理目录重新创建配置资源。
- 整理后检查目录与 `layer` 一致，并验证登记引用仍然有效。

## UIManager 接入职责

- UIManager 负责界面的创建、层级、打开关闭、输入请求及生命周期；业务规则继续由原业务系统负责。
- 场景入口使用 UIId 和 Context 打开界面，不直接持有由框架创建的场景 UI 实例。
- `UILayer` 表示界面类别；`UISpace` 表示挂载空间。World Space UI 放在独立的 WorldUIRoot 下，保留 Canvas 模式、世界位置、尺寸与缩放，不挂入屏幕 Canvas。
- 模糊渲染由 UIManager 预制体上的 UIBlurManager 服务负责；UIBackgroundController 统计请求，UIBlurMask 负责显示，不能把渲染服务当作普通面板注册。
- 接入时保留面板内部的显示、动画及业务回调；关闭或卸载所属场景时解除订阅并释放资源。
