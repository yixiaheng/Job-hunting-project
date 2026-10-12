# 背包系统开发与使用指南

本文面向本项目的开发、学习和求职演示，说明当前代码实际实现的能力。文中目录以项目根目录 `E:/unity/Job hunting project` 为基准。

先记住一句话：**SO 定义“物品是什么”，InventoryService 管理“玩家持有什么”，JSON 保存玩家进度，UI 负责展示与发起操作。**

## 目录

1. [快速运行与功能边界](#1-快速运行与功能边界)
2. [文件分层与职责](#2-文件分层与职责)
3. [配置和玩家数据](#3-配置和玩家数据)
4. [当前物品配置](#4-当前物品配置)
5. [初始化与操作流程](#5-初始化与操作流程)
6. [业务代码调用示例](#6-业务代码调用示例)
7. [界面绑定与刷新](#7-界面绑定与刷新)
8. [食物装备与养成接入](#8-食物装备与养成接入)
9. [存档与异常处理](#9-存档与异常处理)
10. [新增物品与扩展](#10-新增物品与扩展)
11. [验收与常见问题](#11-验收与常见问题)
12. [阅读顺序与展示重点](#12-阅读顺序与展示重点)

## 1. 快速运行与功能边界

### 怎么运行

1. 打开 `Assets/Scenes/SampleScene.unity`，进入 Play Mode。
2. 场景中的 `InventoryManager` 创建背包服务并读取存档。
3. 按 **B** 打开或关闭背包，按 **Esc** 或点击“关闭”退出。
4. 左侧筛选、搜索并选择物品，右侧查看详情。光锥可以锁定、解锁，点击“保存背包”可以立即保存。

对话进行时不允许打开背包。背包打开后关闭角色移动和 Cinemachine 相机输入，关闭时恢复原状态；它不暂停整个游戏，也不会修改 `Time.timeScale`。搜索框获得焦点时，输入 B 不会关闭背包，Esc 仍能关闭。

### 当前完成了什么

| 能力 | 当前状态 |
| --- | --- |
| 物品配置与目录查询 | 已实现，使用 SO |
| 材料和食物堆叠 | 已实现，同配置 ID 合并数量 |
| 光锥独立实例 | 已实现，每份有独立 ID、等级、经验、突破和锁定状态 |
| 奖励发放、批量扣材料 | 已实现，先检查全部条目再修改 |
| 分类、品质筛选、搜索、排序、详情 | 已实现 |
| 食物使用数量选择 | 已实现，范围为 1 到当前持有数量 |
| 锁定、解锁、移除保护 | 已实现；穿戴保护需要外部提供穿戴状态 |
| JSON 存档、变化通知 | 已实现，接入项目已有 SaveSystem |
| 格子复用与宽度自适应 | 已实现，不是大背包虚拟列表 |
| 背景模糊 | 复用原有 UIBlurManager / UIBlurMask |
| 治疗、复活、秘技点、战前加攻 | 已配置参数并预留接口，真实效果未接入 |
| 角色养成、光锥穿戴和被动 | 未接入；已提供请求事件和装备进度接口 |
| 仪器 | 已预留类型与分类，没有正式物品 |
| 圣遗物、随机词条、光锥叠影 | 未实现 |

对应系统未接入时，按钮显示提示，**不会假装使用成功，也不会因此扣物品**。当前图标是程序化占位图，不是最终美术资源。

## 2. 文件分层与职责

### 文件位置

```text
Assets/
├─ Scripts/GamePlay/Inventory/
│  ├─ ItemConfig.cs                  通用物品配置和枚举
│  ├─ EquipmentConfig.cs             装备配置、属性与等级成长
│  ├─ ItemCatalog.cs                 配置目录及 ID 校验
│  ├─ InventoryData.cs               存档数据和展示条目
│  ├─ InventoryInterfaces.cs         使用处理器与穿戴状态接口
│  ├─ InventoryService.cs            核心背包业务
│  ├─ InventoryManager.cs            初始化、保存、请求转发
│  └─ Register/InventorySaveDefinitions.cs
├─ Scripts/UI/Inventory/
│  ├─ Inventory UI Controller.cs     类名为 InventoryUIController
│  ├─ InventoryItemView.cs           单个物品格
│  └─ InventoryGridLayout.cs         按宽度计算列数
├─ Editor/Inventory/InventoryDemoSetup.cs
├─ Config/Inventory/
│  ├─ ItemCatalog.asset
│  ├─ Materials/                     4 份材料 SO
│  ├─ Foods/                         4 份食物 SO
│  └─ LightCones/                    4 份光锥 SO
└─ UI/
   ├─ Prefab/Inventory/InventoryItem.prefab
   ├─ Sprite/Inventory/              占位图标
   └─ Fonts/InventoryChinese SDF.asset
```

### 各层为什么分开

| 层 | 负责什么 | 不应该负责什么 |
| --- | --- | --- |
| 配置层 | 名称、图标、类别、效果参数、装备基础属性 | 玩家数量、某一份装备的等级 |
| 数据层 | 数量、实例 ID、等级、锁定等状态 | GameObject、按钮、相机引用 |
| Service | 查询、奖励、消耗、使用校验、装备操作 | 找场景物体、打开面板、直接写文件 |
| Manager | 创建 Service、存取、转发使用和养成请求 | 包办食物效果和角色成长计算 |
| UI | 显示数据、收集点击、调用业务 | 修改 SO 保存进度、直接改内部字典 |
| Editor 工具 | 创建演示资产、绑定场景 | 运行时初始化、每次启动发奖 |

`InventoryService` 是普通 C# 类，不能挂到 GameObject 上；Manager 和 UI 是 MonoBehaviour。Service 使用 SO 和其中的 Unity 类型，但它的操作不依赖场景物体。

```text
ItemCatalog → InventoryService ← InventoryManager → SaveSystem → JSON
                    ↑                  ↑
          InventoryUIController ────────┘
                    ↓
             InventoryItemView

使用 / 穿戴 / 养成请求 → 对应业务系统或目标选择界面
```

业务类型位于 `Game.Inventory` 命名空间，UI 和搭建工具目前在全局命名空间。

## 3. 配置和玩家数据

### 为什么 SO 和 JSON 同时存在

“旅途经验书”的 SO 定义 ID、名字、图标和经验参数；玩家数据记录“我有 20 本”。

两份“黎明之前”共用一个 EquipmentConfig，但一份可以是 1 级、未锁定，另一份是 5 级、已锁定。若把等级写进 SO，修改的是所有该配置装备的设计数据，不能表示某一份装备的状态。

SO 不是语言层面的只读对象；**运行时不修改 SO 来记录玩家进度，是本系统的使用约定**。

### 三种标识

| 标识 | 含义 |
| --- | --- |
| `configId` / `ItemConfig.id` | 一种物品的稳定 ID，例如 `cone_dawn`；同种物品共用 |
| `instanceId` | 某一份装备的 ID，发奖时生成 GUID；同名光锥也不同 |
| `InventoryEntry.Key` | UI 条目标识：堆叠物品用配置 ID，装备用实例 ID |

锁定、强化、移除某一份光锥必须传实例 ID，不能传 `cone_dawn` 这样的配置 ID。

### 数据类型

| 类型 | 字段及用途 |
| --- | --- |
| `ItemStack` | `configId`、`quantity`，一类堆叠物品 |
| `EquipmentInstance` | `instanceId`、`configId`、`level`、`experience`、`ascension`、`isLocked` |
| `InventoryData` | `stacks`、`equipment` 两个列表，供保存与恢复 |
| `ItemAmount` | 配置 ID 和数量，用于奖励、消耗清单，不代表装备实例 |
| `InventoryEntry` | 配置引用、数量、可选装备副本，供 UI 展示，不是存档格式 |
| `ItemUseRequest` | 配置、使用数量、目标 ID，传给使用系统 |

Service 内部字典便于按 ID 查询，存档列表便于序列化。不会保存按钮、GameObject 或 SO 引用。

`TryGetEquipment()`、`GetEntries()` 中的装备以及 `CreateSnapshot()` 返回副本；改副本不会修改背包，必须调用 Service 方法。展示条目的 `Config` 仍引用原 SO，不是玩家私有配置副本。

## 4. 当前物品配置

以下是演示参数；配置效果不代表真实效果已经接入。

| 类别 | 配置 ID | 名称 | 参数 / 用途 |
| --- | --- | --- | --- |
| 材料 | `character_exp` | 旅途经验书 | 角色经验参数 1000 |
| 材料 | `equipment_exp` | 光锥强化片 | 装备经验参数 500 |
| 材料 | `character_ascension` | 星核碎晶 | 角色突破材料，消耗配方待养成系统定义 |
| 材料 | `equipment_ascension` | 记忆棱镜 | 光锥突破材料 |
| 食物 | `food_revive` | 应急复苏剂 | 单体复苏并恢复最大生命 20% |
| 食物 | `food_technique` | 秘技补给罐 | 恢复队伍 2 点秘技点 |
| 食物 | `food_heal` | 便携营养餐 | 单体恢复最大生命 30% |
| 食物 | `food_attack` | 战前能量饮 | 下一场战斗攻击力加成 15% |
| 光锥 | `cone_dawn` | 黎明之前 | 生命 120、攻击 60、防御 30 |
| 光锥 | `cone_guard` | 守望之约 | 生命 150、攻击 30、防御 60 |
| 光锥 | `cone_wind` | 逐风旅人 | 生命 100、攻击 45、防御 25、速度 3 |
| 光锥 | `cone_memory` | 远星回响 | 生命 90、攻击 40、防御 30、增伤 3% |

首次新存档默认每种材料 20 个、食物 5 个、光锥 1 份，共 12 个条目。由场景中 `InventoryManager.startingItems` 决定，不是 Service 写死的规则。

### ItemConfig 字段

| 字段 | 说明 |
| --- | --- |
| `id` | 全目录唯一、大小写敏感的稳定 ID，发布后不要随意改名 |
| `displayName`、`icon`、`description` | 名称、Sprite、说明 |
| `category` | `Material`、`Food`、`Equipment` |
| `rarity` | `Common`、`Rare`、`Epic`、`Legendary`，用于品质颜色与筛选 |
| `useKind` | 使用行为，决定找哪个处理器 |
| `effectValue` | 固定数值，例如 2 点秘技点 |
| `effectRatio` | 比例，`0.3` 表示 30%，不是填 30 |
| `requiresTarget` | 使用时是否必须提供非空目标 ID |
| `IsEquipment` | 由是否为 EquipmentConfig 判断，不是 Inspector 可改的开关 |

回血比例基于谁、能否对满血角色使用等由处理器决定。Service 不解释效果参数。

### EquipmentConfig 字段

装备继承通用配置，额外包含 `equipmentKind`、`maxLevel`、`baseStats`、`statsPerLevel`、`passiveEffectId`。

```text
某项属性 = 基础值 + 每级成长 × (限制到有效范围的等级 - 1)
```

`GetStats(level)` 计算展示属性，不自动升级或扣经验。当前光锥上限 80，每级成长为生命 8、攻击 3、防御 2；突破字段暂不影响属性，`passiveEffectId` 暂无执行系统。

## 5. 初始化与操作流程

### 启动

```text
InventoryManager.Awake（执行顺序 -100）
  → 检查单例，按配置设置跨场景保留
  → new InventoryService(catalog)
  → ItemCatalog.BuildIndex 检查空项、重复 ID、装备类型
  → LoadInventory
      ├─ 成功：TryRestore 校验并恢复玩家数据
      ├─ NotFound：空背包 → 发初始物品 → 保存
      └─ 其他失败：记录原因，禁止覆盖原存档
  → 订阅 Service.Changed

InventoryUIController.Start
  → 获取 Service，绑定按钮、筛选和变化通知
  → 关闭背包显示根节点
```

新建 SO 不会自动给玩家发奖；修改初始物品也不改变已有存档。目录索引在创建 Service 时建立，运行时编辑目录不会自动重建，编辑后重新进入 Play Mode。

### 发奖与扣材料

```text
业务系统传清单
  → 合并相同配置 ID 的数量
  → 校验所有配置、数量和持有量
  → 全部通过后统一修改
  → Changed
      ├─ 背包打开时刷新 UI
      └─ Manager 标记 dirty，等待保存
```

“原子扣除”指正常业务校验失败时不会只扣一部分。它不是数据库事务或线程安全机制，也不自动保证“扣材料 + 更新角色状态”一起成功。

### 使用物品

```text
点击使用
  ├─ 无需目标且 CanUse 成功 → Manager.TryUse
  └─ 不能直接使用 → RequestUse → 外部界面选择目标 → Manager.TryUse

Service.TryUse
  → 检查配置、数量、处理器、目标
  → handler.CanUse 再校验
  → handler.TryUse 执行真实效果
      ├─ false：不扣物品
      └─ true：Service 扣物品 → Changed
```

请求事件成功只表示请求已交给订阅者，不表示使用、穿戴或养成完成。目标选择等待期间数量和角色状态可能变化，最终执行必须重新校验。

## 6. 业务代码调用示例

下面完整示例可放进 MonoBehaviour，通过按钮或奖励结算调用，不会在每次启动时自动发奖。

```csharp
using Game.Inventory;
using UnityEngine;

public class InventoryRewardExample : MonoBehaviour
{
    public void GiveReward()
    {
        InventoryManager manager = InventoryManager.Instance;
        if (manager == null || manager.Service == null)
        {
            Debug.LogWarning("背包尚未初始化。");
            return;
        }

        var rewards = new[]
        {
            new ItemAmount("character_exp", 10),
            new ItemAmount("food_heal", 2),
            new ItemAmount("cone_dawn", 1)
        };

        if (!manager.Service.TryAddRewards(rewards, out string reason))
        {
            Debug.LogWarning(reason);
            return;
        }

        Debug.Log("奖励已进入背包。");
    }
}
```

`Try...` 返回 bool：true 表示操作完成，false 表示被拒绝。`out string reason` 在失败时返回原因。不要忽略结果继续升级或显示成功。

### API 速查

| 方法 | 用途 / 注意事项 |
| --- | --- |
| `TryGetConfig(configId, out config)` | 查目录，不代表玩家持有 |
| `GetQuantity(configId)` | 堆叠数量；装备则为该配置的实例数量 |
| `TryAdd(configId, quantity, out reason)` | 发一种物品；装备会生成指定份数的独立实例 |
| `TryAddRewards(rewards, out reason)` | 一批奖励，全部校验后发放 |
| `TryRemove(configId, quantity, out reason)` | 扣一种堆叠物品，不执行食物效果 |
| `TryConsume(costs, out reason)` | 批量扣材料，不执行养成效果 |
| `TryGetEquipment(instanceId, out instance)` | 读独立装备副本 |
| `TrySetLocked(instanceId, locked, out reason)` | 锁定 / 解锁，不阻止强化或穿戴 |
| `TryRemoveEquipment(instanceId, out reason)` | 移除装备，拒绝锁定或提供器判定为已穿戴的装备 |
| `TrySetEquipmentProgress(instanceId, level, experience, ascension, out reason)` | 写进度，不扣材料、不自动升级 |
| `GetEntries(category, kind, sort)` | 取筛选、排序后的展示条目 |
| `CreateSnapshot()` / `TryRestore(data, out reason)` | 导出副本 / 校验并整体替换数据 |
| `CanUse(...)` / `TryUse(...)` | 查询可否使用 / 执行并在成功后扣物品 |

清单不能为 null 或空，数量必须大于 0。堆叠数量上限为 `int.MaxValue`，当前没有每格 99 个的拆堆规则，也没有背包容量限制。

### 批量扣材料

以下片段在已有方法中使用，`service` 指已初始化的 `InventoryManager.Instance.Service`，需导入 `Game.Inventory` 和 `UnityEngine`。

```csharp
var costs = new[]
{
    new ItemAmount("character_exp", 3),
    new ItemAmount("character_ascension", 1)
};

if (!service.TryConsume(costs, out string reason))
{
    Debug.LogWarning(reason);
    return;
}
```

经验书够但突破材料不足时，两种都不扣；同 ID 重复条目会合并，例如 2 本加 3 本等于一次扣 5 本。

**成功扣材料后角色不会自动升级。** 养成系统应先校验目标和成本，再协调材料扣除及状态更新，避免扣掉材料才发现不能升级。

### 操作独立装备

下面同样使用已初始化的 `service`。

```csharp
var entries = service.GetEntries(ItemCategory.Equipment, EquipmentKind.LightCone);
if (entries.Count == 0) return;

string instanceId = entries[0].Key;
if (!service.TrySetLocked(instanceId, true, out string reason))
    Debug.LogWarning(reason);

if (!service.TrySetEquipmentProgress(instanceId, 5, 10, 1, out reason))
    Debug.LogWarning(reason);
```

直接写 5 级、经验 10、突破 1 是接口示例，不是正式强化配方。锁定防移除，不阻止写进度。

### 监听变化

其他面板可以 `service.Changed += RefreshPanel`，不再使用时用同一个方法退订。事件无增量参数，监听者重新查询。

成功批量修改通常广播一次；锁定值未变化时不重复广播。监听方法不应发奖、扣材料或再次使用，以免循环调用。

## 7. 界面绑定与刷新

### 场景结构

```text
InventoryManager
UI / BackpackUICanvas                 控制器、Canvas，保持激活
└─ InventoryRoot                      默认隐藏；backPanel 指向这里
   ├─ Panel                           原 RawImage + UIBlurMask
   ├─ Dim                             遮罩及 UI 射线阻挡
   └─ Content                         标题、筛选、搜索、详情、按钮
      ├─ ItemScroll / Viewport / Items
      └─ Detail
```

`backPanel` 不能指向控制器自己的 GameObject，否则隐藏后也无法监听 B 打开。UI 射线阻挡不等于拦截所有 Physics 射线，世界点击代码仍应遵循项目的 UI 点击过滤规则。

### Inspector 引用

| 引用组 | 字段 |
| --- | --- |
| 业务、根节点 | `inventory`、`backPanel` |
| 格子生成 | `itemRoot`、`itemPrefab` |
| 筛选搜索 | `categoryDropdown`、`rarityDropdown`、`sortDropdown`、`searchInput` |
| 详情 | `detailIcon`、`detailTitle`、`detailDescription`、`detailStats` |
| 提示数量 | `countLabel`、`quantityLabel`、`statusLabel` |
| 业务按钮 | `useButton`、`lockButton`、`equipButton`、`progressionButton` |
| 常规按钮 | `decreaseButton`、`increaseButton`、`closeButton`、`saveButton` |

当前 SampleScene 已绑定。详情代码不是所有字段都做了空保护，新增界面需要完整绑定；还要配置 EventSystem、InputSystemUIInputModule 和 GraphicRaycaster。格子 Prefab 的按钮、图片、文本和选中节点也需完整绑定。

### 刷新规则

`Refresh()` 先按类别和装备类型取条目，再在 UI 按品质和名称过滤，最后复用格子、刷新详情。

- 类别下拉顺序是全部、材料、食物、光锥、仪器；改变顺序需同步改代码映射。
- 品质第一项为全部，排序顺序为品质、名称、数量。
- 名称采用包含匹配并忽略大小写，不是按配置 ID 搜索。
- 品质和数量降序；相同值按名称、再按 Key。名称采用 ordinal 比较，不是中文拼音排序。
- 选中 Key 仍在结果中就保留，否则选第一项；空结果清空选中与操作按钮。

格子不够才 Instantiate，多余的隐藏，够用时重新 Bind。它保留历史最大展示数量的格子，**不是只生成可见区域条目的虚拟列表**。

`InventoryGridLayout` 按可用宽度、间距和最小格宽计算列数。`InventoryItemView` 显示图标、名称、品质、数量或等级、锁定和选中状态。

### 字体和工具

背包专用 `Assets/UI/Fonts/InventoryChinese SDF.asset` 基于项目中文字体，启用多图集动态补字，避免小图集装满后出现方框。

- `Tools/Inventory/Setup Demo`：非运行状态、打开 SampleScene 后使用，创建缺失资产、首次界面并保存场景。
- 同路径已有物品 SO 和格子 Prefab 不覆盖；目录会补入示例物品。
- 已有 InventoryRoot 就跳过界面搭建，不是恢复默认按钮，也不自动修复所有缺失引用。
- `Tools/Inventory/Refresh Chinese Font`：创建缺失专用字体并重新绑定场景和格子；已有字体不会强制重建图集。

改界面直接编辑场景，改格子编辑 Prefab。工具会保存相关资产，先保存或备份自己的修改。

## 8. 食物装备与养成接入

### 使用处理器

接口位于 InventoryInterfaces.cs，下面仅展示已有签名，不要再次声明同名接口：

```csharp
public interface IItemUseHandler
{
    bool CanUse(ItemUseRequest request, out string reason);
    bool TryUse(ItemUseRequest request, out string reason);
}
```

一个处理器对应一种行为，不必为每个物品写类。两种回血食物可以共用 RestoreHealth 处理器，只在 SO 填不同回血比例。

实现时按以下约定：

1. CanUse 只查询，不改生命、配置或背包。
2. 根据 TargetId 找角色或装备；Service 只检查必需目标是否为空，不检查它是否真的存在、存活或属于队伍。
3. 校验使用数量、满血、死亡、秘技点上限等业务规则。
4. TryUse 再校验并一次应用全部效果；成功返回 true，拒绝返回 false 和原因。
5. 只做真实效果，不调用 TryRemove 扣物品；Service 在成功后扣除。

注册为 `service.RegisterUseHandler(ItemUseKind.RestoreHealth, handler)`，handler 是实际处理器对象；传 null 移除注册。

处理器负责保证失败时无部分效果，Service 不会回滚它已经修改的生命或 Buff。使用执行期间 Service 拒绝再次修改背包；这不是线程锁，业务仍按 Unity 主线程执行。

### 目标选择

订阅 `InventoryManager.UseRequested`，保存请求并打开角色 / 装备选择界面。选定后调用以下片段，变量来自请求和最终选择：

```csharp
bool used = manager.TryUse(configId, quantity, targetId, out string reason);
if (!used) Debug.LogWarning(reason);
```

目标 ID 建议采用角色数据 ID 或装备实例 ID，而不是名字或场景对象名称，具体约定由处理器和目标系统共同定义。接口本身不强制 ID 的格式。

取消选择不调用 TryUse，不扣物品；没处理器时选好了目标也不会使用成功。事件订阅者应在关闭或销毁时退订。

### 穿戴系统

`EquipRequested` 传装备实例 ID，订阅者选择角色并记录穿戴关系。当前 EquipmentInstance 不保存穿戴者，后续角色装备数据应保存角色 ID 与装备实例 ID 的对应关系。

已有接口：

```csharp
public interface IEquipmentUsageProvider
{
    bool IsEquipped(string instanceId);
    string GetOwnerName(string instanceId);
}
```

将实际实现赋给 `service.EquipmentUsage`，即可查询穿戴者并阻止移除已穿戴装备。**接口不负责真实穿戴、属性加成或保存关系**；不提供它时，Service 无法知道装备是否穿戴。

### 养成与战斗

`ProgressionRequested` 参数是材料配置 ID 或装备实例 ID，订阅者负责打开养成界面、选目标、检查上限和成本。

`TrySetEquipmentProgress` 只检查等级范围、经验非负、突破非负并写入数据，不计算经验曲线、突破配方或等级突破限制。

战斗奖励调用 TryAddRewards 即可。食物“下一场加攻”应进入战前增益状态，再由战斗映射流程读取；增益消耗时机、是否保存、失败是否保留，需要后续系统定义，不写在背包 UI 中。

## 9. 存档与异常处理

### 保存位置和内容

| 项目 | 当前值 |
| --- | --- |
| 框架 | `TapTap21.SaveSystem.dyh.SaveSystem` |
| 定义 | `InventorySaveDefinitions.Inventory` |
| 类型 / 版本 | `InventoryData` / 1 |
| key / 文件 | `player_inventory` / `player_inventory.json` |
| 默认槽位 | `slot_0`，可在 Manager Inspector 修改 |
| 目录 | `Application.persistentDataPath/Saves/<saveSlot>/` |

不替换旧示例 inventory.json，也不替换队伍站位存档。运行时输出 Application.persistentDataPath 可以找到实际目录。

以下仅演示背包数据部分，不是完整正式存档：

```json
{
  "stacks": [
    { "configId": "character_exp", "quantity": 20 }
  ],
  "equipment": [
    {
      "instanceId": "example_unique_equipment_id",
      "configId": "cone_dawn",
      "level": 1,
      "experience": 0,
      "ascension": 0,
      "isLocked": false
    }
  ]
}
```

真实文件还包含 SaveSystem 的 key、类型、版本、保存时间和 checksum 包装层，背包 JSON 位于 dataJson 字段。不要手改正文后忽略校验信息。

### 保存时机

- Service 变化：标记 dirty，在 LateUpdate 保存，同帧修改通常合并。
- 点击保存、关闭已打开背包：调用 SaveInventory。
- Manager.TryUse 成功：立即尝试保存。
- 应用暂停或退出：有待保存数据且允许保存时尝试保存。

当前是同步文件操作，不是后台异步保存。自动保存失败保留 dirty，间隔至少 5 秒重试；手动保存不受自动重试计时限制。

**TryUse 返回 true 是效果和扣除成功，不代表文件保存一定成功**；保存结果看 SaveInventory 返回值及 LastMessage。

### 读取保护

SaveSystem 读主文件失败会尝试备份。取到数据后，Service 校验两个列表、配置 ID、堆叠正数量及重复项、装备实例 ID、等级范围、经验和突破非负等。

空列表允许，null 列表拒绝。全部数据先写进临时字典，校验通过再替换内存背包；失败保留当前内存数据，Manager 禁止覆盖原文件。这个阶段的业务校验失败不会自动再选择另一份备份。

修复后用 LoadInventory 重新读取。**它替换当前数据，不是界面刷新方法**；未保存的修改可能丢失。只刷新显示用 UI.Refresh。

### 测试初始奖励

非运行状态改 Manager.saveSlot 为新的合法测试槽位，例如 inventory_demo_test，再运行；结束后恢复原槽位。不要删除整个 Saves 目录，否则会同时影响角色或队伍等存档。

改 startingItems 不影响已有存档；想追加物品用奖励接口。

## 10. 新增物品与扩展

### 新增另一种回血食物

1. 使用 `Create/Inventory/Item` 创建 SO。
2. 填唯一 ID、名称、图标、说明。
3. 设置 category 为 Food，useKind 为 RestoreHealth，requiresTarget 为 true，effectRatio 为 0.5。
4. 加入 ItemCatalog.asset。
5. 重新运行，通过 TryAdd 发放。

无需新增物品类或 UI 分支；真实治疗仍需已注册的 RestoreHealth 处理器，没有处理器时只能显示、持有。

### 新增仪器

用 `Create/Inventory/Equipment` 创建 SO，将 equipmentKind 设为 Instrument，填唯一 ID、属性并加入目录。发奖会生成独立实例，仪器分类可展示。

这复用持有、锁定、养成数据和显示结构；仪器装备槽位、专属功能、随机属性仍需业务系统实现。

### 兼容已有存档

改名字、图标通常不影响存档；改 ID、删除已持有配置、把食物改成装备、降低上限至低于玩家装备等级，都可能导致旧存档不通过校验。

结构变更要配合 SaveSystem 版本和迁移机制。当前没有自动改名 ID、忽略未知物品或迁移随机词条的规则。

## 11. 验收与常见问题

### 手工验收清单

| 操作 | 预期 |
| --- | --- |
| 新测试槽位首次启动 | 8 个堆叠条目、4 个光锥条目 |
| 同槽位重启 | 数量和实例 ID 保留，不重复发初始奖励 |
| B、Esc、搜索时输入 B | 开关正常，搜索不误关闭，Esc 可关闭 |
| 分类、品质、不存在的名称 | 筛选准确，空结果清空选中和按钮 |
| 多次刷新 | 已有格子复用，不反复销毁 |
| 同名光锥两份，改其中一份 | 实例 ID 不同，另一份等级与锁定不受影响 |
| 锁定、保存、读取、尝试移除 | 锁定状态保留，移除被拒绝 |
| 不足材料的批量扣除 | 全部数量不变 |
| 无效奖励、负数量、溢出 | 拒绝，不留下部分奖励 |
| 未接入处理器时使用 | 提示未接入，不扣食物 |
| 关闭背包 | 移动、相机恢复原状态，遮罩隐藏 |
| 保存与读取 | 只操作对应槽位背包，不覆盖队伍文件 |

实现阶段的本地 Pipeline 检查脚本在 `Temp/Inventory*Check.cs`。Temp 不受版本控制，不是正式自动测试套件；仅修改说明文档不需要重新跑游戏测试。

### 常见问题

| 现象 | 优先检查 |
| --- | --- |
| B 没反应 | 控制器激活、对话状态、Game View 焦点、backPanel 引用 |
| 背包为空 | 目录、Service、读取结果、存档持有量、筛选和搜索 |
| 初始数量改了没变化 | 槽位已有存档，用新测试槽位或奖励接口 |
| 同名光锥占多个条目 | 正常，用实例 ID 区分每份装备 |
| 改读出的装备没效果 | 读出的是副本，用 TrySetEquipmentProgress 修改 |
| 使用 / 装备只有提示 | 检查处理器和请求事件订阅者是否接入 |
| 中文方框 | 专用字体引用、源字体、容量、多图集设置 |
| 格子或详情 NullReferenceException | 完整检查控制器和格子 Prefab 引用 |
| 未成功读取，暂不覆盖存档 | 看 LastMessage、目录 ID 和校验原因，修复后重读 |
| 换场景没有 UI | Manager 可常驻，UI 不会自动常驻，目标场景需单独配置 |

## 12. 阅读顺序与展示重点

建议按 `ItemConfig → InventoryData → ItemCatalog → InventoryService → InventoryManager → InventoryUIController → InventoryItemView` 阅读，Editor 工具、字体生成和模糊渲染可以后看。

先掌握配置与玩家状态分开、配置 ID 与实例 ID 区别、Try 返回值、事件订阅与退订、请求和真实执行的区别。先做到能解释、能修改一条流程，不必一次独立写出整套系统。

求职演示可展示数据驱动新增物品、独立装备状态、扣除失败不改数据、重启恢复、业务与 UI 解耦。不要把预留接口描述成已经完成的治疗、养成、穿戴或随机词条系统。
