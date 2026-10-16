# 游戏事件系统

## 结构

| 位置 | 职责 |
| --- | --- |
| Common/Events/EventBus.cs | 纯 C# 泛型基类，实现订阅、同步发布、异常隔离、退订与诊断 |
| Common/Events/SubscriptionBag.cs | 保存订阅凭证，统一清理；Clear / Dispose 后可以重新加入订阅 |
| GamePlay/Battle/Events/BattleEventBus.cs | 战斗子 Bus、IBattleEvent 和战斗消息定义 |
| GamePlay/Inventory/Events/InventoryEventBus.cs | 背包子 Bus、IInventoryEvent 和背包消息定义 |
| Game/GameEvents.cs | 游戏组装入口，持有跨场景系统 Bus 并连接 Unity 日志 |

EventBus 不继承 MonoBehaviour，不需要场景物体或 DontDestroyOnLoad。每个实例的监听表独立。游戏代码统一使用 `Game.GameEvents.Battle` / `Game.GameEvents.Inventory`；不要在发布者与订阅者分别 new 两套实例。测试可以创建独立实例。

## 订阅与发布

下面代码放在已有 UI 组件中，`unit` 是该 UI 当前绑定的 BattleUnit：

```csharp
private readonly Game.Events.SubscriptionBag subscriptions = new Game.Events.SubscriptionBag();

private void OnEnable()
{
    subscriptions.Add(Game.GameEvents.Battle.Subscribe<Battle.Events.HealthChangedEvent>(OnHealthChanged));
    RefreshFromUnit();
}

private void OnDisable()
{
    subscriptions.Clear();
}

private void OnHealthChanged(Battle.Events.HealthChangedEvent message)
{
    if (!ReferenceEquals(message.Unit, unit)) return;
    AnimateHealth(message.CurrentHealth, message.MaxHealth);
}
```

`RefreshFromUnit`、`AnimateHealth` 是接入方需要实现的 UI 方法，不是 Bus API。绑定晚于 OnEnable 时也要主动刷新。重新绑定前先处理旧订阅，停用时停止自己的 Tween，重新启用后重新同步当前值。

发布方式：`GameEvents.Battle.Publish(new HealthChangedEvent(unit, previous, current, max))`。实际血量通知已由 BattleUnit 自动发送，外部不要重复手工发布。

## 已接入的事件

| 消息 | 来源及数据 |
| --- | --- |
| HealthChangedEvent | BattleUnit 成功改变生命后；包含 Unit、BattleId、变化前后生命、最大生命 |
| CurrentUnitChangedEvent | BattleFlow.CurrentUnit 实际改变后；包含 BattleId 和新行动者，结束时可为 null |
| BattleEndedEvent | BattleFlow 首次确定结果后；包含 BattleId、Result、TurnCount |
| InventoryChangedEvent | InventoryService 成功修改、恢复后；包含产生通知的 InventoryService，可比较引用过滤来源 |

BattleFlow 每次创建生成新的 BattleId，单位加入流程时关联到该 ID。尚未加入流程的单位 BattleId 为 Guid.Empty。单位引用仅用于当前运行，不写入存档；同一 BattleUnit 不应同时加入两场战斗。

BattleUnit 使用 `InitializeHealth(max)` 初始化满血且不发事件；每个单位只允许初始化一次。`TakeDamage(amount)`、`Heal(amount)` 返回实际变化量，只在真正改变时发通知。非正数量不改变，生命限制在 0 到最大生命，普通 Heal 不复活。currentHealth / maxHealth 对外只读；将来调整上限或复活也需单独入口和通知。

GameOverCanvasUI 已改用 BattleEndedEvent 并按当前 BattleId 过滤。原 BattleFlow.GameOverEvent 和 InventoryService.Changed 保留兼容；同一 UI 请选择一种通知来源，避免刷新两次。新 Bus 的监听异常不会阻断其他新监听；旧委托仍遵循原 C# 委托语义。

本次不修改场景、不自动添加血条 Image 或 Tween。血量事件已可使用；现有 BattleUnitView.Refresh 的死亡立即隐藏行为，以及 Manager 同帧推进多个敌人行动的行为仍保留，需要在实现血条演出时明确处理。

## 类型限制与添加系统

`BattleEventBus : EventBus<IBattleEvent>`，消息必须实现 IBattleEvent，背包对应 IInventoryEvent。向战斗 Bus 发布背包消息会编译失败。按泛型事件类型精确匹配，不自动向父类、接口转发；发布时使用具体事件类型，不要先转成 IBattleEvent。

新增系统时，在该模块集中创建一个文件，包含事件标记接口、继承 EventBus 的子类、readonly struct 消息定义。再在 GameEvents 添加该系统实例，并加入统一清理和日志配置。没有实际需求时不预建角色、任务等空 Bus。

## 生命周期

- GameEvents 静态属性保存系统 Bus，切换场景不会清理它们。
- 普通 UI 在 OnEnable / OnDisable 订阅与退订，绑定型组件根据绑定生命周期管理；不要在 Refresh / Update 里订阅。
- 常驻服务在初始化时订阅，关闭时退订。战斗结束不清空整个战斗 Bus，否则会移除任务等长期监听者。
- Unity 进入运行时的 SubsystemRegistration 自动清理旧监听，兼容关闭 Domain Reload。Bus 实例不替换，旧凭证释放安全。
- 手动 ClearForNewSession 只供游戏会话所有者使用：先停止旧系统，再清理，之后重新初始化并订阅。它不是重试战斗、切场景或读档按钮。
- 若同时关闭 Scene Reload，需要游戏启动流程明确重建场景监听者；仅清空 Bus 不会让仍存活的组件自动重跑 OnEnable。

## 执行规则

- 仅 Unity 主线程使用，不是线程安全队列；回调立即同步执行，不等待 Tween、协程、Task。
- 按订阅顺序调用，但业务正确性不依赖监听先后。同方法两次订阅独立触发，两份凭证各自退订。
- Dispose 可重复执行。无订阅时发布不报错。Clear 清除全部监听，之后可重新订阅。
- 发布中新增监听不参与当前这轮；若又嵌套 Publish，新一轮可以看到它。尚未执行的已退订监听跳过。
- 回调中允许发布其他消息；总嵌套深度上限 64，超过记录异常，避免无界递归。
- 单个监听抛异常会报告消息类型、监听方法和异常，继续执行其他监听。诊断回调出错也不阻断分发。
- 稳定订阅的正常发布不创建监听快照数组；订阅和列表扩容会分配，日志和异常也会分配。
- Clear 只属于 Bus 所有者，模块使用者只释放自己的凭证。

状态数据仍由业务系统保存，新 UI 主动读取当前状态，Bus 不缓存历史事件。回调可以观察即时状态；一次血量变化不表示整次技能已结束。伤害计算、被动触发、奖励发放等必须成功的业务继续通过明确方法执行，不依赖通知回调完成。

## 诊断

```csharp
Game.GameEvents.Battle.EnableLogging = true;
int count = Game.GameEvents.Battle.GetSubscriberCount<Battle.Events.HealthChangedEvent>();
```

日志默认关闭，Unity 运行初始化会恢复关闭。订阅数量可用于发现重复开关面板后监听不断增加。监听者不得在回调里无限修改并再次发布同类消息；嵌套深度保护只用于定位问题，不是业务循环控制。
