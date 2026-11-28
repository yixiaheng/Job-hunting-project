# 战斗配置 Excel 导入使用说明

## 1. 入口与数据流

Unity 菜单：**Tools > Battle > 配置表工作台**。

数据流是 `Excel → 校验/预览 → 原位更新 ScriptableObject → 原有战斗和养成系统读取 SO`。Excel 仅为编辑器制作数据的入口，运行时没有 Excel 依赖，不需要安装 Excel、COM 或外部服务。使用 Excel/WPS 时请保存为普通 `.xlsx`，不要加密。

- 当前项目数据：`ConfigTables/BattleConfig.xlsx`。
- 空模板：`ConfigTables/BattleConfigTemplate.xlsx`。
- 默认索引：`Assets/Config`，包括 Battle 和 Character 目录的配置。
- 自动备份及最近报告：项目根目录 `ConfigImportBackups/`。
- 自动验证入口：**Tools > Battle > Verify Config Excel Import**。
- 结构优劣与后续建议：`Docs/BattleConfigExcelArchitectureReview.md`。

工作簿位于 Assets 外，不会随游戏资源打包。首次工作簿是交付时的快照；之后如果在 Inspector 修改了配置，编辑 Excel 前应再次“导出现有配置”，避免旧表覆盖新值。

## 2. 推荐的第一次操作

1. 退出 Play Mode，等待 Unity 编译结束。
2. 打开工作台，保留 `Assets/Config`，选择 `ConfigTables/BattleConfig.xlsx`。
3. 点击“校验与预览”。未编辑的初始表应显示无变化；如果旧效果没有 key，会显示候选 key 的变更。
4. 打开 Excel，按以下示例修改一个值，保存。
5. 再次“校验与预览”，选择“修改”筛选，确认前后值。
6. 点击“应用导入”。修改原 SO，路径和 GUID 保留。
7. 在 Inspector 查看结果。再次导入相同表格应当无变化。

工作台不会自动监听保存并写入。每次应用都必须来自当前文件和当前资产状态的有效预览。预览后修改 Excel 或配置，必须重新预览。

Excel/WPS可以保持工作簿打开。修改后先按Ctrl+S并等待保存完成，再进行预览；工具只能读取已保存到磁盘的内容，无法读取表格软件中尚未保存的编辑。读取使用共享只读快照，应用前再次检查快照哈希，备份也保存该验证版本。如果其他程序独占锁定文件，工作台会给出中文提示，此时关闭该工作簿再重试。

## 3. 填表规则

第一行是稳定英文字段名，第二行是中文说明，第三行起是数据。可移动列、排序行或隐藏行列；隐藏数据仍参与导入。不要改英文表头，不要合并单元格，不要把说明行当数据行。

所有声明字段必须有值。无值的数字显式填 `0`，布尔填 `TRUE/FALSE`。枚举用下拉中的代码，中文含义在 Enums；不接受枚举整数。字符串 ID 精确匹配，不接受首尾空格。数字必须有限，ID/顺序/层数等要求整数；字段自己的范围见第二行。

业务表禁止公式，即使 Excel 显示了正常结果也会报错。可以新增计算工作表，计算后复制并“粘贴为值”到业务表。百分比读底层数值：`20%` 与 `0.2` 等价，不要填 `20` 来表示 20%。

`Meta!B3` 是格式版本，不要手工修改。Instructions、Enums 是填写帮助；Meta 和五张业务表是导入结构的重要组成部分。额外计算表不会导入。

## 新增：差异预览、引用查询和安全操作

效果差异按effectKey显示 `[新增] / [删除] / [修改] / [移动]`，例如 `skill.10001.effect.0.value：1 → 1.2`。角色技能关联显示用途、技能ID和名称；移除关联不会删除SO。清空已有普通技能列表、天赋行动或技能效果列表会有单独确认框，取消即可返回检查；部分删除也会有警告。

工作台开启“ID / 名称 / 路径查询”，可搜索配置、复制ID、定位到Inspector。预览结果也有“定位资源”按钮；新资产导入后可以定位。预览中新配置会标为“待创建”。

工作簿新增三个工具拥有的辅助表：

| 表 | 内容 |
| --- | --- |
| RefUnits | 单位ID、名称、来源、资源路径 |
| RefSkills | 技能ID、名称、来源、资源路径 |
| RefBuffs | Buff ID、名称、来源、资源路径，包含0=无引用 |

UnitSkills和SkillEffects的ID引用列有下拉。下拉使用实际ID，名称从Ref*表查看；同一ID优先展示当前工作簿名称，新配置无需先导入即可加入辅助表。允许手填ID，不会因下拉尚未刷新而拒绝录入；最终有效性由导入校验判断。

**新增配置后刷新下拉：保存并关闭该Excel/WPS文件 → 工作台“刷新工作簿引用辅助表” → 重新打开。**此操作保留业务单元格、列顺序和额外计算表，只更新Ref*辅助内容、引用验证和辅助版本时间。不要把私人数据放进RefUnits/RefSkills/RefBuffs。普通预览和导入通常只需保存文件，不要求关闭。

“导出现有配置”会重建工作簿，不会合并Excel中尚未导入的修改，也不会携带额外计算表。覆盖已有文件前会明确提示，并把旧文件保存到同目录的 `.BattleWorkbookBackups/`。文件被Excel/WPS打开时，覆盖/刷新会拒绝并提示关闭或另存；导入读取不受这种普通共享锁影响。

新导出工作簿的Meta记录项目、项目标识、工作簿ID、索引范围、生成时间和资产基线。不同项目或SO已偏离导出基线会提示复核，不会擅自阻止合法迁移。旧工作簿刷新辅助表时保留原来源，不伪造旧文件的项目身份。每次应用（含无变化）在 `ConfigImportBackups/History/` 留下独立JSON记录，可通过“导入历史”查看。

语义警告只是提醒：例如能量消耗超过基础上限、Buff层数超过上限、growsWithSkill没有对应成长条目、overrideTarget关闭导致行内目标规则不生效。特殊玩法可以保留这些组合；重复身份、缺失引用等结构错误仍然必须修复。

## 新增：配置模板与批量复制

1. 在工作台选择已保存工作簿，打开“配置模板与批量复制”。源路径留空时从当前SO取数据。
2. 选择复制单位、复制技能、单体伤害模板、群体伤害模板、我方治疗模板或新单位＋普攻模板。
3. 复制操作通过“ID · 名称”选择来源；填写新名称、数量、单位ID前缀/起始序号和新技能起始ID。
4. 复制单位可选择“复制技能”，关闭后引用原技能。复制模式会同步生成UnitSkills和SkillEffects。单位在普通列表与天赋行动使用同一技能时，只生成一个技能副本。
5. 点击“生成预览”，检查新ID及语义警告。ID冲突直接报错，不会自行跳号。每批1—100项。
6. “另存为工作簿”生成独立xlsx，包含源业务数据与新增行；不会覆盖来源文件，额外计算表不复制。生成期间修改来源或SO后必须重新预览。
7. 回工作台选择新文件，正常校验和应用导入。

新单位的模型、头像、成长引用不会从复制来源自动带过去。复制技能会生成 `skill.<新ID>.effect.<order>` 的效果key，之后请配置对应的成长与演出。模板只负责规则数据；“单体伤害”不意味着已经制作近战走过去的动画。

示例：复制一个敌人，数量设为3，单位前缀 `Goblin_`，起始序号1，技能起始ID12000，开启“复制技能”。生成Goblin_1—Goblin_3及各自的技能/效果；仍需在Unity为三个单位绑定prefab，在技能表现系统中配置新技能ID对应的演出。

| 表 | 身份 | 用途 |
| --- | --- | --- |
| Units | unitId | 单位基础值、元素、命途、初始能量和类别 |
| Skills | skillId | 技能规则、战技点、能量和目标规则 |
| SkillEffects | skillId + effectKey | 技能内的嵌入效果，order 决定执行下标 |
| Buffs | buffId | Buff 属性、持续回合、层数和正负面标记 |
| UnitSkills | unitId + role + order | 单位普通技能列表及天赋行动关联 |

## 4. 四个常用操作

### 修改一个角色攻击力

在 Units 找到目标 `unitId`，修改 `attack`，例如将岩壁守卫的基础攻击从 `48` 调到 `52`。预览应只显示该单位的 attack 改变。

这是**基础攻击**。我方实际战斗属性仍会由已有成长、等级和装备计算，不保证战斗面板直接显示 52。敌人的基础配置和我方养成后的属性不是同一概念。

### 调整一个技能倍率

在 Skills 确认技能 ID，再到 SkillEffects 找该 ID 下的目标 effectKey。改 `value`，例如伤害倍率 `1.2 → 1.5`。不要修改 skillId、effectKey 或 order 来调整倍率。

`growsWithSkill` 决定是否参与已有成长。`scaleStat` 表示缩放基础属性。不同效果对 value 的语义不同，应与当前效果结算规则一致；导入不会改变规则。

### 新增 Buff 并绑定技能

1. Buffs 新增一个未使用的正整数 buffId，例如 `900001`，名称“测试虚弱”。
2. 填 `buffType=StatMod`、`durationTimes=2`、`maxStack=1`、`stat=Attack`、`value=-0.2`、`isDebuff=TRUE`。
3. 确保所属技能出现在 Skills。
4. 在 SkillEffects 给技能增加一行，填写新的唯一 effectKey，如 `Example.Skill.Weaken`，`order` 为该技能最后一个下标加 1。
5. 设置 `effectType=ApplyBuff`、`buffId=900001`、`stack=1`、`buffValueMultiplier=1`；其余列也必须明确填写。
6. 如果使用技能原有目标，`overrideTarget=FALSE`；targetSide、targetRule 仍填写有效值，但该覆盖不开启。

导入先创建本次需要的新资产，再解析关联，因此同一工作簿中可以同时新增 Buff、技能、单位。Buff 数字 ID 和真实 SO 引用同步。负面属性倍率可以为负。

### 新增敌人并配置技能

1. Units 新增稳定 ID，例如 `enemy_test_01`，类别 `Enemy`，设置名字和基础属性。
2. 在 UnitSkills 添加该单位与技能的关联：`role=Normal`，order 从 0 连续。
3. 复用已有技能时，不必在 Skills 重复列出该技能；但它必须位于配置索引范围内。
4. 新建技能时，在 Skills 和 SkillEffects 填写完整定义。
5. 导入后到 `Assets/Config/Battle/Units/Enemies/` 找新 SO，手动绑定 prefab、icon。工具不会复制另一单位的模型。
6. 将新敌人加入现有遭遇/波次配置。**创建 UnitConfig 不等于自动加入战场。**

新增 Ally 时会追加到范围内唯一的 UnitCatalog，保留原顺序；敌人不会加入。新的我方角色需要另行绑定成长、美术及已有业务需要的其他配置，导入器不代替完整角色制作流程。

## 5. 完整替换与保留边界

**出现的主体更新，主体对应的子列表完整替换。**

- 出现在 Units 的单位：其 UnitSkills 是本次完整技能定义。没有任何关联行，意味着 skills 清空且 talentAction 清空。
- 出现在 Skills 的技能：其 SkillEffects 是本次完整效果定义。没有效果行，意味着效果列表清空。
- 未出现在主体表的 SO：不改、不删。
- 子表不能独立更新未出现在主体表的单位或技能。
- 缺工作表直接报错，不会当成“空列表”。

Normal 的 order 按单位从 0 连续；TalentAction 最多一条且 order 为 0。效果 order 按技能从 0 连续。物理行顺序不影响执行顺序。

Unity 继续管理 `prefab`、`icon`、`growth` 和演出资源。Excel 管理的字段以 Excel 为准；Inspector 改这些字段后，下一次导入旧表会覆盖它们。不存在双向自动同步。

已有资产保持路径和 meta/GUID。新资产放入 Battle 下的 Allies、Enemies、Skills、Buffs；文件名由稳定 ID 和短哈希组成，显示名称仍是表中的 name。工具不整理旧目录、不自动删除 SO。修改 ID 是新增，旧配置保留；ID还关联存档、角色拥有列表和技能表现绑定，请保持稳定。

单位类型只影响编辑器分类和新角色登记，不改运行时阵营规则。已有 Ally/Enemy 类别转换会被阻止，需要另做迁移。

现有普攻规则保留：零消耗 Basic 在 skillPointGain=0 时仍可能默认恢复 1 点，这不是导入错误。

## 6. 效果和演出的保护

`effectKey` 用于成长匹配，`order` 是效果在 effectConfigs 中的下标，两者不能互相替代。

- 删除或改名导致现有成长配置引用悬空：阻止导入，并列出成长资产路径。
- 删除、替换或重排效果：警告 `BattleImpactMarker.effectGroup` 按下标匹配，需要复查技能 Timeline 的命中标记。
- 修改倍率：不改技能 ID、不改演出绑定。
- 旧效果没有 key：导出 `legacy.<skillId>.<index>` 稳定候选；第一次导入会明确显示。候选与已有 key 冲突时追加下划线。

校验不会自动修改成长配置，也不会自动重写 Timeline。

## 7. 预览、失败与恢复

预览显示新增、修改、无变化，以及每个错误的工作表/行/列/ID。修改项含字段前后值，技能关系和效果列表含完整前后内容。无变化资产不保存。

以下情况不可应用：存在错误、Play Mode、编译/资源刷新、受影响资产未保存、预览后文件或依赖资产已变化、上次导入未完成。

每次有修改的应用先备份受影响资产、meta、UnitCatalog（若改变）、来源工作簿和差异清单。写入异常自动恢复；若编辑器意外退出，工作台识别 `Prepared` 状态清单并提供恢复入口。恢复只涉及这次修改和新建的资产，不是整个项目回滚。

“恢复最近备份”会覆盖这些资产在该次导入之后的修改；先检查备份目录和提示。备份位于项目根目录，不随运行时打包。不要在尚有未完成导入时删除备份。此机制不是 AssetDatabase 原生事务；磁盘损坏、备份丢失等情况需人工处理。

## 8. 代码维护与扩展

代码均位于 `Assets/Editor/BattleConfigImport/`，命名空间 `Battle.Editor.ConfigImport`：

| 文件 | 职责 |
| --- | --- |
| ConfigTableSchema.cs | 字段定义、类型化行 DTO、诊断和 IConfigTableAdapter |
| ConfigWorkbook.cs | OpenXML 读写、表头定位、格式和基础值校验 |
| BattleConfigTableAdapter.cs | SO 索引、导出、跨表校验、成长保护和差异计划 |
| ConfigImportService.cs | 文件快照、预览失效检查、原位写入、备份及恢复 |
| BattleConfigImportWindow.cs | 中文编辑器界面 |
| BattleConfigImportVerification.cs | 临时资源验证及显式请求入口 |

`IConfigTableAdapter` 声明表结构并负责 Parse、Validate、Export、BuildUpdates。工作簿读写不依赖技能字段；后续模块可以提供适配器，再将它交给 ImportService。新模块的具体写入需要提供对应更新操作，并纳入同一备份/恢复计划。当前界面使用战斗适配器，暂未接入装备、养成或新天赋表。

依赖为 DocumentFormat.OpenXml 3.5.1、DocumentFormat.OpenXml.Framework 3.5.1、System.IO.Packaging 8.0.1；DLL仅允许 Editor。许可证、来源和哈希见 ThirdParty/README.md。System.Memory 使用 Unity 自带兼容程序集，避免复制造成类型冲突。

验证命令为编辑器菜单；自动化也可以写入 `Temp/BattleConfigImport/request.txt`，内容 `test`、`export`、`regression` 或 `open`，由编辑器在安全状态消费。不会因导入脚本而自动执行测试。

实际验证结果另见 `Docs/BattleConfigExcelVerification.md`。请区分 OpenXML格式/共享字符串兼容性验证与真实 Excel/WPS 应用保存验证。

## 9. 本次交付时的具体状态

初始工作簿含 9 个单位、21 个技能、34 条技能效果、12 个 Buff 和 21 条单位技能关联。首次校验会显示 3 个旧技能需要更新：为它们原来没有 key 的 4 个效果写入候选 key；不是修改技能数值。

正式配置已实际执行过“预览→导入→再次预览无变化→战斗工厂读取→恢复原文件”的往返验证，49 个配置及依赖文件和 meta 的哈希恢复一致。为了保留首次配置体验，交付时仍保留旧技能的空 key，等你查看差异后自行应用。

旧敌人空ID、两套Buff ID重复和失效Buff引用已按你确认的方案修复。详情和原文件备份见 `Docs/BattleConfigInitialMigration.md`。

## 伤害属性扩展说明（2026-11-27）

新导出的 `Units` 表包含暴击率、暴击伤害、击破特攻、击破效率加成及七种元素增伤，共11个新字段。完整填写方式见 `Docs/BattleOffensiveAttributes.md`；带当前数据的新工作簿为 `ConfigTables/BattleConfig.WithOffensiveStats.xlsx`。

旧表整列缺失时保留已有单位对应值，新单位使用默认值（暴击率0.05、暴击伤害0.5，其余0）；列存在但单元格为空仍然报错。新属性目前完成配置与运行时数据传递，尚未参与伤害结算。



## 2026-11-27：共享成长与独立基础属性

新导出工作簿为 `ConfigTables/战斗配置.xlsx`。原 `BattleConfig.xlsx` 不被覆盖，旧数值列继续兼容；最新空模板为 `ConfigTables/战斗配置模板.xlsx`。

`Units.maxHealth/attack/defence` 现在明确是1级基础值并支持小数，`speed` 为不自然成长的基础速度。Excel表头仍采用原字段名，写入目标改为单位独占的 `UnitBaseStatsConfig` 子资源；`UnitConfig` 本身不再存数值。暴击、爆伤、元素和击破属性也存入此子资源，不参与自然成长。

每个单位与自己的基础属性子资源保存在同一 `.asset` 文件中，仍原位更新、保留主体GUID。导入会检查子资源未保存修改，备份和回滚同时涵盖主体与基础属性。新建单位自动创建自己的基础属性SO；模型、头像、成长与演出仍由Unity配置。

共享成长表在 `Assets/Resources/Bootstrap/CharacterGrowthTable.asset`，现可通过GrowthStages导入；角色养成通过CharacterGrowth及关联子表导入。真实等级与突破阶段来自JSON；详见 [角色共享成长架构](CharacterSharedGrowthGuide.md)。敌人暂直接使用基础值。


## 2026-11-27：角色养成表与中文文件名

默认完整工作簿改为 `ConfigTables/战斗配置.xlsx`，空模板为 `ConfigTables/战斗配置模板.xlsx`，批量输出默认 `战斗配置_批量生成.xlsx`。工作台新增“默认中文表”按钮。旧英文文件保留；工作表与字段标识保持英文，填写说明继续中文。

新增 `GrowthStages`、`CharacterGrowth`、`CharacterGrowthStages`、`CharacterRanks`、`SkillEffectGrowth` 五张业务表和 `RefCharacterGrowth` 辅助表。`Units.growthId` 可绑定同工作簿新建或现有养成SO。物品ID、阶段连续性、效果key引用都会校验；共享曲线和养成资产均纳入备份与失败恢复。

新版格式为2；完整v1旧表仍可导入并保留未涉及的成长。新版缺表报错。具体字段含义、成长计算示例、清空语义及旧参数区别见 [角色成长Excel配置说明](CharacterGrowthExcelGuide.md)。此节更新优先于前文“成长在Unity手动绑定”的旧流程。
