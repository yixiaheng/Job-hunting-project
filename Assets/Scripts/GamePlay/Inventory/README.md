# 背包系统

完整的架构、字段、调用流程、接入示例和排查说明，请阅读 [背包系统开发与使用指南](Guide.md)。本文件保留快速使用说明。

## 运行

打开 `SampleScene`，进入运行模式，按 **B** 打开背包，按 **Esc** 或关闭按钮退出。打开背包时暂时关闭角色移动和 Cinemachine 相机输入，关闭时恢复原状态。搜索输入框内打字不会触发 B 快捷键。

界面支持全部 / 材料 / 食物 / 光锥 / 仪器分类，品质筛选、名称搜索、品质 / 名称 / 数量排序、物品详情、食物数量选择、独立装备锁定和保存。网格复用已创建的物品格，不会每次刷新全部销毁。

`Tools/Inventory/Setup Demo` 用于首次搭建 SampleScene。已有同路径 SO 和物品格 Prefab 不会覆盖；已有 `InventoryRoot` 不会重复搭建。修改界面请直接编辑场景。

## 分层

- **ItemConfig / EquipmentConfig**：SO 设计配置，不存玩家持有数量。配置位于 `Assets/Config/Inventory`，共 4 种材料、4 种食物、4 种光锥。当前图标是可替换的程序化占位图。
- **ItemCatalog**：物品 ID 到配置的查询目录；新增物品必须有唯一 ID，并添加到目录。发布后的 ID 不要随意改名，否则旧存档无法识别。
- **InventoryData**：JSON 存档数据。堆叠物品存配置 ID 和数量；装备每份有独立实例 ID、等级、经验、突破和锁定状态。
- **InventoryService**：持有、奖励、消耗、锁定、数据校验和变化通知，不依赖场景物体或 UI。
- **InventoryManager**：初始化、读取、存档以及与使用 / 穿戴 / 养成界面的请求桥接。
- **InventoryUIController / InventoryItemView**：展示与交互，不直接改存档数据。

SO 创建菜单为 `Create/Inventory/Item`、`Equipment` 和 `Catalog`。未来仪器使用 `EquipmentConfig`，将 `equipmentKind` 设为 `Instrument`；它和光锥共享独立装备实例机制。圣遗物随机词条暂未实现。

背包使用独立的 `Assets/UI/Fonts/InventoryChinese SDF.asset`，基于项目中文字体源文件生成，启用多图集动态补字，避免已有小字库装满后中文显示为方框。不会修改对话和战斗现有字体引用。

## 发放和消耗

```csharp
var service = InventoryManager.Instance.Service;
bool added = service.TryAdd("character_exp", 10, out string reason);
bool rewarded = service.TryAddRewards(new[]
{
    new ItemAmount("food_heal", 2),
    new ItemAmount("cone_dawn", 1)
}, out reason);
bool consumed = service.TryConsume(new[]
{
    new ItemAmount("character_exp", 3),
    new ItemAmount("character_ascension", 1)
}, out reason);
```

批量奖励和扣材料先校验全部条目，再统一修改；任意条目无效或数量不足则整批不变。同配置的多个消耗条目会合并。每次成功批量修改广播一次 `Changed`。光锥不能使用普通数量扣除，必须通过 `TryRemoveEquipment(instanceId)` 移除；锁定 / 已穿戴的装备会被拒绝。

## 后续接口

食物的真实效果、养成逻辑、装备穿戴还未接入。点击未接入的功能会显示提示，**不会伪造使用成功，也不会扣除物品**。

- 实现 `IItemUseHandler`，用 `RegisterUseHandler(ItemUseKind, handler)` 注册复苏、治疗、秘技点、战前攻击增益等处理器。`CanUse` 只做查询，`TryUse` 成功后才能返回 true。处理器不能回调修改背包；物品由 Service 在效果成功后统一扣除。多份使用的效果应由处理器按 `Quantity` 校验并一次应用，失败时不应留下部分效果。
- `InventoryManager.UseRequested`：需要选择角色 / 装备时打开目标界面；选定后调用 `TryUse(configId, quantity, targetId, out reason)`。
- `InventoryManager.EquipRequested`：参数是装备实例 ID，打开角色选择 / 穿戴界面。通过 `Service.EquipmentUsage` 提供 `IEquipmentUsageProvider`，即可显示穿戴者并禁止移除穿戴中的装备。
- `InventoryManager.ProgressionRequested`：堆叠材料传配置 ID，独立装备传实例 ID。养成界面选择目标、计算消耗，通过 Service 扣材料，并使用 `TrySetEquipmentProgress` 更新装备进度。
- `Changed`：用于刷新其他 UI。对外读取的装备和快照是副本，修改副本不会修改背包。

## 存档

使用项目已有的 SaveSystem，单独注册 `player_inventory`，文件为 `Application.persistentDataPath/Saves/slot_0/player_inventory.json`，不替换旧示例的 `inventory.json`，也不影响战斗队伍存档。

仅首次找不到存档时发放 `InventoryManager.startingItems`；再次启动直接读取，不重复发放。数据变化在 LateUpdate 保存，同帧修改合并；也可点击保存，关闭背包、应用暂停和退出时保存。保存失败保留待保存状态，自动重试间隔 5 秒。

读取失败或数据不合法时禁止覆盖原存档；修复后调用 `LoadInventory()` 重新读取。未知 ID、重复装备实例 ID、非法数量等都会拒绝整份数据。运行中不要直接改 SO 保存玩家进度。
