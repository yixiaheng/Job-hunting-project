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

## UI 预制体目录分类

- 所有通过 UIManager 打开的面板预制体统一放入 `Assets/Prefabs/UIPrefabs/`，按照对应 UIConfig 的 `layer` 分类到 `HUD`、`Window`、`Popup`、`Toast`、`Overlay` 五个目录。
- 目录名称与 UILayer 枚举完全一致，使用单数 `Window`，不再新建 `Windows`。每层内部允许按业务建立子目录，如 `HUD/Battle`、`Window/Character`、`Popup/Character`。
- 通用条目、角色卡片、对话选项等未直接注册到 UIManager 的子预制体放入 `Widgets/`；`UIManager.prefab` 作为框架入口保留在 UIPrefabs 根目录，不作为普通面板分类。
- 以 `UILayer` 分类，不以 `UISpace` 分类；移动 World Space 面板不能修改 Canvas 模式、位置、尺寸和缩放。
- 新增面板、修改 layer 或迁移预制体时，同步检查 UIConfig 目录、面板目录和编辑器生成工具中的路径。
- 移动必须携带原 `.meta`，保留 GUID；核查 UIConfig、UIRegistry、场景和嵌套预制体引用，不通过重建资产来整理目录。

## UIManager 接入职责

- UIManager 负责界面的创建、层级、打开关闭、输入请求及生命周期；业务规则继续由原业务系统负责。
- 场景入口使用 UIId 和 Context 打开界面，不直接持有由框架创建的场景 UI 实例。
- `UILayer` 表示界面类别；`UISpace` 表示挂载空间。World Space UI 放在独立的 WorldUIRoot 下，保留 Canvas 模式、世界位置、尺寸与缩放，不挂入屏幕 Canvas。
- 模糊渲染由 UIManager 预制体上的 UIBlurManager 服务负责；UIBackgroundController 统计请求，UIBlurMask 负责显示，不能把渲染服务当作普通面板注册。
- 接入时保留面板内部的显示、动画及业务回调；关闭或卸载所属场景时解除订阅并释放资源。

## 代码开发规范

- 新代码遵循当前模块的既有结构和命名空间；UI 使用 `Game.UI`，背包使用 `Game.Inventory`，角色养成使用 `Game.Characters`，战斗逻辑使用 `Battle.Logic`。不要仅为统一命名而批量修改旧代码。
- Inspector 引用使用 `[SerializeField] private`；运行时状态通过只读属性或明确方法暴露，不新增公共可变字段。
- `ScriptableObject` 用于静态配置，运行时状态使用独立的数据对象、Context、Session 或 Snapshot；不得直接修改项目资产保存业务进度。
- `MonoBehaviour` 主要负责 Unity 生命周期和表现层连接；可独立测试的业务规则放在普通 C# 类或 Service 中。
- UI 只负责展示、收集输入和提交请求；战斗、背包、养成、存档等业务规则继续由各自系统负责。通过 UIManager 管理的界面使用 `UIId` 和 Context 打开。
- 战斗规则与结算放在 `Battle.Logic`，动画、镜头和等待流程放在表现层；`Validate`、`Preview` 等查询不得修改状态或触发业务事件。
- 事件订阅必须在面板关闭、组件禁用或场景卸载时解除；返回 `IDisposable` 的订阅应保存并释放。
- 预期内的业务失败使用 `Try...`、验证结果或结果对象表达；异常用于无效配置、非法参数和被破坏的内部状态。
- 运行时代码不得直接引用 `UnityEditor`；编辑器工具和验证代码放在 `Assets/Editor/` 或 Editor 专用程序集。
- 修改功能时扩展并运行对应验证：战斗使用 Battle 验证，UI 和场景使用 UI/SceneFlow 验证，角色养成与存档使用 CharacterProgression 验证。只报告本次实际运行的结果。
- 移动或重命名 Unity 资源必须保留 `.meta` 和 GUID；修改序列化字段名时考虑使用 `[FormerlySerializedAs]`。
- 旧代码只在当前任务涉及的范围内渐进整理，不进行无关的批量改名、格式化或架构重构。
