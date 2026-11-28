# 战斗属性扩展：第一步，配置与运行时数据

更新：2026-11-27。基础属性已拆到独立SO，成长采用全局突破阶段表；完整配置说明见 [角色共享成长架构](CharacterSharedGrowthGuide.md)。

本轮目标是先让伤害相关属性能配置、能导入、能经过养成与入场快照正确传到战斗单位。**新增属性尚未接入 AttackService 的伤害公式；修改暴击、元素增伤或击破特攻，目前不会改变战斗伤害数字。**

## 1. 新增哪些属性

所有比例都用小数存储，`0.2` 表示 `20%`，不要填 `20`。

| 属性 | 运行时读取 | 默认值 | 含义 |
| --- | --- | ---: | --- |
| 暴击率 | `unit.CritRate` | 0.05 | 5%；保存原始面板值，允许超过100% |
| 有效暴击率 | `unit.EffectiveCritRate` | 0.05 | 对原始暴击率限制到0—1，供之后的暴击判定使用 |
| 暴击伤害 | `unit.CritDamage` | 0.5 | 额外50%，对应暴击倍率1.5 |
| 击破特攻 | `unit.BreakEffect` | 0 | 200%填2，对应击破特攻因子为1+2 |
| 击破效率加成 | `unit.BreakEfficiencyBonus` | 0 | 增加50%效率填0.5；不是总效率 |
| 总击破效率倍率 | `unit.BreakEfficiencyMultiplier` | 1 | 由1+效率加成计算；供之后削韧结算使用 |
| 七种元素伤害加成 | `unit.GetElementDamageBonus(damageElement)` | 各0 | 物理、火、冰、雷、风、量子、虚数分别保存 |

实际可编辑的新增项共 **11 项**：暴击率、暴击伤害、击破特攻、击破效率加成、七种元素增伤。有效暴击率和总击破效率倍率是只读派生值，不重复保存。

暴击默认值参照《星穹铁道》常见玩家角色基础面板。当前 `UnitConfig` 是敌我共用类型，因此新旧单位都使用这组可覆盖的默认值；**这不是对《星穹铁道》所有敌人暴击参数的断言**。不希望某个敌人暴击时可以明确配置为0。

原来的 `BattleUnit.damageBonus` 仍表示已有通用增伤，本轮没有建立第二份同义字段。

## 2. 在哪里配置

### Unity Inspector

1. 在 Project 中选择对应的 `UnitConfig` 资源。
2. 展开 **伤害相关属性**。
3. 设置暴击率、暴击伤害、击破特攻、击破效率加成。
4. 展开 **元素伤害加成**，设置对应元素。

例如配置为：

```text
暴击率         0.75   → 75%
暴击伤害       1.50   → 额外150%，暴击总倍率2.5
击破特攻       2.00   → 200%
击破效率加成   0.50   → 基础100% + 加成50% = 总效率150%
火伤害加成     0.388  → 38.8%
```

敌我直接建模路径和我方养成路径都读取这里的基础配置。玩家已有入场快照后再修改 SO，不会覆盖该快照中已经保存的新属性；新配置在重新构建入场快照后使用。

### Excel

工作台仍为 `Tools > Battle > 配置表工作台`。新导出的 `Units` 表包含：

```text
critRate
critDamage
breakEffect
breakEfficiencyBonus
physicalDamageBonus
fireDamageBonus
iceDamageBonus
lightningDamageBonus
windDamageBonus
quantumDamageBonus
imaginaryDamageBonus
```

已导出一份当前9个单位的完整工作簿：

`ConfigTables/战斗配置.xlsx`

它带有全部原有业务表及新增属性列。导入前仍需要“校验与预览”，再“应用导入”。原 `BattleConfig.xlsx` 保留，避免覆盖用户可能正在编辑的工作簿。

旧表兼容语义：

- **整列不存在**：对已有单位保留当前 SO 值；新增单位使用默认值，并在预览中提示旧表缺列。
- **列存在但某个单元格为空**：仍是错误。清零请明确填0。
- **填负数、NaN或无穷大**：基础属性校验不通过。
- **暴击率超过1**：允许保存，不删除面板溢出；未来判定应使用有效暴击率。
- 新增列与原有数值字段一样，下次导入以表格为准。
- 批量新建和复制单位已经支持这些属性，含旧表来源的复制。

本轮不更改旧工作簿的格式版本；新增列是可兼容缺省的扩展。原有必需表、原有必需列的校验仍然保留。

## 3. 数据如何流动

```text
UnitConfig（身份、技能、资源引用）
  └─ BaseStats → UnitBaseStatsConfig（独立SO，保存为该单位的子资源）
       ├─ BattleDataMaping → BattleUnit（敌方直接读取基础值）
       └─ CharacterStatCalculator
            + CharacterProgress.level / ascension（真实JSON存档进度）
            + CharacterGrowthTable.Shared（三围统一成长系数）
            + 光锥白值、百分比与固定词条
            → CharacterStats → BattleCharacterSnapshot → BattleUnit
```

三围按1级基础值 × 共用系数计算，阶段内线性插值；突破的提升已经包含在阶段端点内。80级、突破阶段6时系数为7.35。基础数值保留小数，最终运行时整数面板才取整。

暴击率、暴击伤害、元素伤害、击破特攻、击破效率不随等级或突破自然增长。旧的每级增加/每次突破增加字段已移除，后续由装备、行迹、Buff等明确来源增加。

`UnitConfig.BaseStats.OffensiveStats` 是静态基础值；`BattleUnit.SetOffensiveStats(...)` 只改运行时值副本。`CharacterGrowthConfig` 继续负责经验消耗、技能成长、升阶和旧天赋规则，不再决定基础属性成长曲线。

正式入场通过 `PlayerRuntime.Characters.GetCharacter(unitId)` 取得真实存档进度，再建立快照。Play Mode中养成未就绪时拒绝正式入场；Edit Mode工具可以直接创建基础测试单位。敌方遭遇等级和敌方成长曲线仍需另行设计。

本次仍未将新属性接入装备/Buff加成或伤害结算。

## 4. 为什么不直接加入现有 GetStat

当前 `StatType` 与 `GetStat` 的调用约定是攻击、防御、生命、速度等整数属性，既有 Buff 还按原属性的百分比处理。

暴击率、暴击伤害和元素增伤是小数，不能经过整数取整；“增加10个百分点暴击率”也不同于“现有暴击率乘1.1”。本轮保持旧整数属性接口，通过独立的类型和只读接口访问新属性，避免旧伤害与 Buff 系统错误解释新值。

因此，本轮没有把这些字段加入旧 Buff 的 `StatType` 下拉选项，也没有让旧装备百分比数组越界。之后需要设计明确的属性修改语义，再把装备和战斗 Buff 接进来。

## 5. 元素编号与读取规则

原工程仅有火、冰、雷、风、量子、虚数，新增物理时没有插入枚举开头：

```text
Fire=0, Ice=1, Lightning=2, Wind=3,
Quantum=4, Imaginary=5, Physical=6
```

已有资产序列化编号保持原义。七种加成使用明确字段，不依赖枚举数组位置。

元素增伤应读取**本次伤害所属元素**。例如角色本身是火属性，但某个独立效果造成物理伤害，应查询物理增伤；不能只看 `unit.element`。

## 6. 已完成与下一步边界

本轮已完成：

- 新属性的 Inspector 配置和只读运行时接口。
- 直接创建、养成计算、我方入场快照的数据传递。
- 真实角色等级进入CharacterStats与BattleUnit；三围按共享突破表计算，双暴等属性保持固定。
- 七种元素的独立存储及物理元素编号兼容。
- Excel 导出、校验、差异、原位导入、旧表兼容和批量模板。
- 默认值、显式0、非有限数值、快照隔离与GUID保护的验证。

本轮尚未实现：

- 暴击随机判定，以及伤害记录中的暴击标记。
- 通用增伤与对应元素增伤进入直伤公式。
- 抗性、易伤、减防、无视防御等完整乘区。
- 韧性、弱点、削韧、击破状态、击破伤害、超击破和击破DOT。
- 新属性的装备成长、战斗Buff修改、角色面板UI展示。

**推荐下一步只做普通直伤链路：**明确伤害元素与伤害类型，将适用元素增伤和暴击接入单笔伤害计算，并记录是否暴击、采用的增伤与计算结果。击破还缺等级系数、目标韧性、弱点和结算时点等输入，不应只乘上 `1 + BreakEffect` 就宣称实现击破。

## 7. 验证

最新结果见 [角色共享成长架构](CharacterSharedGrowthGuide.md) 和 `Docs/Verification/SharedGrowthRegression.json`。

专项菜单：`Tools > Battle > Verify Offensive Attributes`、`Tools > Characters > Verify Shared Growth`。

完整战斗验证仍有既有阻断：`BattleActionVerification.ActionTimelineResourceChecks` 查找 `energy_bar`，当前UI使用 `energy_show`。未为通过旧验证修改用户UI；其余战斗逻辑检查单独补跑。验证在编辑器进行，不代表完整Play Mode演出验证。

共享成长与角色养成现也可通过Excel配置，详见 [角色成长Excel配置说明](CharacterGrowthExcelGuide.md)。
