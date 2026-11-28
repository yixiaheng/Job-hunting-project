# 角色成长 Excel 配置说明

更新日期：2026-11-27。三类配置现在全部接入同一个工作簿：基础属性 `UnitBaseStatsConfig`、全局成长 `CharacterGrowthTable`、角色养成 `CharacterGrowthConfig`。

## 1. 使用入口与中文文件名

- 正式完整工作簿：`ConfigTables/战斗配置.xlsx`。
- 空模板：`ConfigTables/战斗配置模板.xlsx`。
- 工作台：`Tools > Battle > 配置表工作台`。点击“默认中文表”可切到完整工作簿。
- 批量生成默认文件名：`战斗配置_批量生成.xlsx`。

编辑、保存后，回Unity点击“校验与预览”，检查差异，再“应用导入”。运行期间不可导入。Excel/WPS可以保持打开，但修改必须保存到磁盘。

文件名可以使用中文，也可以自己改名并在工作台重新选择。工作表名和第一行字段标识保持英文，以保证程序稳定解析；第二行是中文说明，枚举提供中文对照和下拉选项。原英文工作簿保留，不覆盖可能存在的手工编辑。

## 2. 哪张表负责什么

| 表 | 身份/排序 | 内容 |
| --- | --- | --- |
| Units | unitId | 1级基础属性、单位身份、growthId关联 |
| GrowthStages | stage 0—6 | 全局阶段起止等级、起止系数 |
| CharacterGrowth | growthId | 经验、材料、升阶加成、现有天赋等单值规则 |
| CharacterGrowthStages | growthId + stage | 每个突破阶段的技能等级上限和下一次突破消耗 |
| CharacterRanks | growthId + rank | 升到1—5阶的碎片消耗 |
| SkillEffectGrowth | growthId + effectKey；order排序 | 具体技能效果的每级成长、升阶门槛及额外倍率 |
| RefCharacterGrowth | 辅助查询，不导入 | 养成ID、对应角色名称、资产路径，给关联列提供下拉 |

已有Units/Skills/SkillEffects/Buffs/UnitSkills的导入语义继续保留。当前完整导出包含7个全局阶段、6份角色养成、42条角色阶段规则、30条升阶消耗、23条技能效果成长。

## 3. growthId怎样填写

`Units.growthId` 对应 `CharacterGrowth.growthId`，各成长子表也使用相同ID。

旧Growth资产此前没有业务ID，因此首次导出使用原资源GUID作为稳定身份，不按名称猜测、不迁移原文件。看到32位字符串是正常的：在 `RefCharacterGrowth` 根据中文角色名称找到对应ID，再用下拉选择或复制。不要把同一份配置在不同表中填成不同ID。

新建养成可以使用易读ID，例如 `growth.hero_ice`。导入后新SO的“养成配置ID”会保存此值。已有资产第一次真正更新时会固化原有效ID，GUID和资源路径保持不变；原样导入不因补写ID而产生改动。

- 修改ID视为新增配置，不重命名旧资产；同时更新Units关联才能让角色改用新配置。
- 多个单位可以共用一份养成配置，但其效果key必须能在各单位的技能中匹配。
- `Units.growthId` **整列缺失**：兼容旧表，保留现有引用；新单位默认无引用。
- 列存在但单元格为空：明确解除引用，已有引用会显示清空确认。我方无养成配置会有提示，正式养成入口仍要求配置完整。
- 新增我方单位可在同一工作簿新增成长主体和子表并绑定，不必先在Unity手动创建Growth。

## 4. 修改共享成长曲线

在 `GrowthStages` 配置：

```text
stage | startLevel | maxLevel | startMultiplier | endMultiplier
0     | 1          | 20       | 1               | 1.95
1     | 20         | 30       | 2.35            | 2.85
...
6     | 70         | 80       | 6.85            | 7.35
```

有数据时必须完整提供7行，阶段从0连续；首节点1级/1倍，相邻阶段等级衔接，最终等级80，倍率为正且不倒退。表存在但无数据表示本次不更新全局曲线。不能只写一个阶段作为局部补丁。

例如将末阶段 `endMultiplier` 从7.35改为7.50，会更新所有角色70—80级的插值结果。共享表固定写入 `Assets/Resources/Bootstrap/CharacterGrowthTable.asset`，不会为每个角色生成一份。修改前后系数在预览中同时显示，并提示全局影响。

等级边界和技能上限影响已有存档合法性。导入不会修改或降级玩家JSON；更换阶段结构、总等级上限与补偿旧进度属于单独迁移，本工具当前限定七阶段、80级。

## 5. 修改升级经验与突破费用

在 `CharacterGrowth` 找到角色growthId：

```text
下一级所需经验 = experienceBase + experiencePerLevel × 当前等级
```

例如填100和20，10级升级需求为300经验。`characterExperience`、`characterAscension`、`skillMaterial`、`shardItem` 填物品系统已有ID，导入校验引用；本次不提供物品表编辑。碎片ID允许为空但会提示，真正升阶前应补齐。

在 `CharacterGrowthStages` 中，每份主体需要七行：

- `stage`：当前突破阶段0—6。
- `skillCap`：这个阶段的技能等级上限，必须为正且不随阶段降低。
- `ascensionCost`：**从当前阶段突破到下一阶段**的材料数量。阶段6已经满突破，填0。

在 `CharacterRanks` 中，每份主体需要1—5阶五行，`shardCost` 是本次升到该阶的碎片消耗。材料类型由主体 `shardItem` 指定。行的位置不决定顺序，程序按显式阶段/阶数排序。

## 6. 修改技能效果成长

真正参与运行时技能倍率的是 `SkillEffectGrowth`，不是主体中的旧生成器默认参数。

例如 `Aegis.Basic.Damage`：

```text
effectKey    = Aegis.Basic.Damage
perSkillLevel= 0.1
requiredRank = 5
rankBonus    = 0.2
```

在对应SkillEffects行 `growsWithSkill=TRUE` 时：

```text
成长系数 = 1 + perSkillLevel × (该技能等级 - 1)
         + (角色升阶达到requiredRank时的rankBonus)
```

技能3级且升到5阶，系数为1.4；再乘技能效果本身的value。ApplyBuff沿用原运行时规则，将成长作用于Buff倍率。`requiredRank=0` 表示无升阶门槛，`rankBonus` 从初始即适用。

`order` 从0连续，`effectKey` 在同一growthId内唯一。删除、改名效果key时，需要同步更新技能效果和成长子表；现在允许同次导入完成两侧迁移。预览按导入后的完整关联检查，不会只看到旧引用就阻止合法迁移。

主体中的 `skillGrowth/rankBasic/rankSkill/rankUltimate` 仍保留，用于已有配置生成器的默认值。改它们不会自动重写已有 `SkillEffectGrowth` 条目。`rankPrimaryFirst/rankHealth/rankPrimaryLast/primaryStat` 则继续用于运行时升阶属性加成。现有天赋字段可配，但仍调用原天赋系统，本次不实现新天赋架构。

## 7. 导入、清空、兼容与恢复

- 出现在CharacterGrowth的主体会更新单值字段，并完整替换自己的阶段、升阶消耗、效果成长列表。
- 子表不能单独更新未出现的主体；缺少阶段或升阶行会报错。效果成长列表允许显式清空，预览会要求清空确认。
- 没出现在主体表的现有成长SO保持不变，不自动删除。
- 新版格式版本为2。五张成长表都是必需表，缺其中任何一张都报错。
- 旧v1若完全没有成长表，可继续导入原战斗配置并保留成长；若已出现部分成长表，就必须补全。
- 改变共享表、成长规则、被引用的技能或物品后，旧预览作废，需重新预览。
- 所有受影响的成长SO和共享表加入既有文件备份/恢复流程，原GUID保持不变；未保存的受影响资产会阻止应用。
- 模型、头像、演出资源继续由Unity维护。

批量复制单位：共用技能时共用原growthId；复制技能时会创建新的养成主体和子表，并把效果成长key同步映射到复制后的技能。模型和演出仍需另外配置。

## 8. 验证

新增菜单 `Tools > Battle > Verify Growth Excel Import`，使用临时共享表和临时角色资源，避免测试修改正式数值。

验证覆盖：五表往返、中文路径、新单位绑定新成长、共享曲线与主体同次更新、重复ID、缺表、断裂阶段、物品引用、效果key迁移、排序、批量复制、无变化导入、未保存保护、旧v1兼容，以及故障时恢复共享表和Growth文件。

报告：`Docs/Verification/GrowthExcelResults.json`、`GrowthExcelImportRetest.json`、`GrowthExcelProjectCheck.json`。既有完整Battle验证仍有能量UI字段 `energy_bar/energy_show` 不一致的阻断；其后战斗逻辑单独补跑，见 `GrowthExcelCoreRegression.json`。未声称进行了完整Play Mode视觉验收。

本次实际结果：Unity编译通过；成长Excel专项28项、既有Excel回归99项、共享成长104项、伤害属性54项、角色养成361项均通过。Battle完整验证在154项后遇到上述旧UI阻断，随后6组逻辑29项补跑通过。当前9单位和6角色真实存档读取正常，新中文表与原英文旧表预览均为0项更新，真实存档文件哈希未改变。最终汇总见 `GrowthExcelFinalResults.json`。
