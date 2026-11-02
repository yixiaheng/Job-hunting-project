# 角色养成系统

## 入口与资源

- 探索 HUD、配队主面板的“角色培养”按钮，以及背包的装备/培养入口，进入同一个养成场景。
- 场景：`Assets/Scenes/CharacterScene.unity`，身份 `SceneId.Character`。
- 主面板：`UIId.CharacterDetail`；配置位于 `Assets/Config/UI/Window/`。
- 材料、装备、确认窗口：`CharacterMaterials / CharacterEquipment / CharacterConfirm`；配置位于 `Assets/Config/UI/Popup/`。
- 所有登记引用在 `Assets/Config/UI/UIRegistry.asset`。
- 玩家运行时入口：`Assets/Resources/Bootstrap/PlayerRuntimeConfig.asset`。
- 成长、碎片、天赋、遗器和套装开发配置：`Assets/Config/Character/`。原角色、技能和光锥资源保留原 GUID。

## 职责与调用

`PlayerRuntime` 在任意场景加载前创建，初始化全局背包与角色服务。场景不再各自创建独立背包数据。

`UnitCatalog / UnitConfig / CharacterGrowthConfig / EquipmentConfig / RelicSetConfig` 是共享配置，不能当玩家进度修改。

`CharacterProgress` 保存角色等级、经验、突破、三项技能等级、阶数和七个装备实例 ID。天赋解锁从等级计算。

`InventoryService` 管材料和装备实例。角色服务实现 `IEquipmentUsageProvider`，查询装备拥有者，禁止删除穿戴中的装备。旧的直接装备成长写入接口在连接角色服务后拒绝调用。

`CharacterProgressionService` 是培养、穿脱、转移和合并的唯一业务入口：

1. 查询：`OwnedCharacters / GetCharacter / Stats / EquipmentOwner`，进度返回副本。
2. `Preview(CharacterRequest)` 在角色与背包副本上校验和计算，返回消耗、变化和溢出经验，不扣材料。
3. `TryCommit(CharacterPreview, out reason)` 校验数据版本，重新计算，先保存角色与背包整体，再交换内存状态，最后通知 UI。
4. 使用旧预览、保存失败、条件不足时不提交。通知回调期间禁止重入修改背包。

`CharacterChangedEvent` 只携带角色 ID、操作类别、版本及受影响角色 ID 列表。转移事件同时包含两名角色，UI 自行查询新快照。

## 确定的成长规则

- 角色等级上限：20 / 40 / 60 / 90 / 100；技能阶段上限：2 / 3 / 4 / 6 / 7。
- 60 级自动解锁固定天赋；0–5 阶消耗专属碎片，重复角色兑换 10 枚碎片。
- 升级材料按经验值换算；到突破上限自动减少材料用量，并显示最后一份材料的经验损失。
- 技能成长与升阶强化通过稳定的 `EffectConfig.key` 匹配 `SkillEffectGrowth.effectKey`，仅明确标记的伤害、治疗、护盾和 Buff 成长，不放大能量或行动提前。
- 属性计算顺序：角色等级/突破基础 + 光锥基础 → 百分比（升阶、光锥被动、遗器主属性、两件套）→ 遗器固定值；独立汇总伤害加成与开场护盾。
- 光锥按命途限制，等级 20 / 40 / 60 / 80，合并 1–5。目标保留等级、经验、突破，吸收材料的合并等级。材料必须未穿戴、未锁定且同款，超过 5 拒绝。
- 遗器六槽、固定主属性、强化 +15，无随机副词条。外圈四槽支持两件/四件套，球绳只计入位面两件套。
- 从其他角色转移装备必须通过确认页；当前槽位旧装备成为未穿戴，不交换双方装备。

全部成长数值放在配置。六个光锥命途均有对应资源；开发遗器包含防御、攻击与生命套装，首批属性均已接入战斗。

## 场景与 UI 生命周期

`CharacterSceneNavigator` 记录来源场景、时间和相机/监听器/输入状态，暂停游戏时间，暂时禁用来源的游戏输入与相机，叠加加载养成场景。

UIManager 的 `SuspendPresentation()` 返回可释放的租约。每个面板使用独立的显示门控 CanvasGroup，隐藏时不修改面板开关状态、Context、动画透明度，也不禁用业务根节点；暂停的面板不参与返回键、输入和背景请求。

`CharacterSceneController` 管胶囊展示模型、独立相机和灯光。展示使用专用 CharacterPreview 层，拖动按指针位移旋转。UIManager 创建并管理主面板和子弹窗。

退出卸载养成场景，再恢复来源场景与同一批 UI 实例。弹窗优先响应返回键。直接运行养成场景时，返回 SampleScene。战斗存在时拒绝进入培养；重复进入请求同样拒绝。

## 战斗快照

配队只允许已拥有角色。开始战斗时 `BattleLaunchData` 捕获 `BattleCharacterSnapshot`，包含最终属性、解锁天赋及解析后的技能数值；每次创建独立运行时技能对象。

直接重试复用原快照；重新编队生成新快照。战斗不会回写 UnitConfig 或局外进度。战斗结束/销毁时释放运行时技能与天赋 Buff 配置。

混合技能支持逐效果目标覆盖，例如岩壁守卫终结技伤害敌方、给我方护盾，雷鸣猎手战技提前自身行动。能量统一限制在合法区间。六个天赋依据实际行动结算触发，追加伤害不再次进入主动技能结算。

## 保存与迁移

正式文件：`Application.persistentDataPath/Saves/slot_0/player_profile.json`，角色与背包在同一 envelope、同一备份内恢复。保留原有 `player_inventory.json` 和其他模块文件。

首次没有新档时导入旧背包，保留装备实例 ID，补全缺少的合并等级和突破；发放配置中的初始角色，不重复发放原背包材料。全新进度才领取开发初始物品。

数据损坏不覆盖原文件。主文件损坏但备份可读取时，只读展示恢复的整组数据，禁止培养和保存；两份都损坏则初始化失败并保留文件。不要通过删除存档来掩盖数据错误。

槽位索引更新失败不会在主数据已经提交后返回“培养失败”，避免磁盘新状态与内存旧状态分裂。

## 开发验收入口

- `Tools/Characters/Build Progression Assets`：原生搭建配置、预制体、入口、场景与登记；保持已存在资源 GUID。重新搭建会覆盖养成页面布局和初版配置，请先提交自定义资源修改。
- 自动逻辑验证：`CharacterProgressionVerification.Run()`。
- 原生 Play Mode 链路验证：`CharacterProgressionPlayVerification.Begin()`。
- 验证报告写入 `Temp/CharacterProgressionChecks/`，测试失败会保留原因；测试代码存在不等于已经执行成功。

### 本轮实际验证（2026-11-02）

- Unity 原生逻辑验证通过 204 项断言，包括边界、保存失败回滚、旧档迁移、装备保护、六天赋及自身加速/行动提前。
- 原生 Play Mode 完整链路通过：从探索、配队、背包三次进入并返回；弹窗取消不消费；点击真实确认按钮培养并保存；养成数值与技能进入实际战斗；直接运行养成场景返回探索场景。
- Play Mode 验证临时切换独立 `slot_character_verification_*` 存档槽，退出运行模式恢复正式 `slot_0` 并删除测试槽，避免改动玩家进度。
- 中文字体与胶囊展示已通过实际截图检查；所有新增 UIConfig 按 Window/Popup 分类并登记。

后续新增角色须配置专属碎片、命途、稳定效果键与成长映射；新增装备须登记 ItemCatalog；新增 UIConfig 仍按 UILayer 分类。
