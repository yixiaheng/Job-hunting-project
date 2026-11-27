# 战斗属性扩展：第一步，配置与运行时数据

本轮完成时间：2026-11-27。

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

`ConfigTables/BattleConfig.WithOffensiveStats.xlsx`

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
UnitConfig
  └─ OffensiveStats：基础属性值
       ├─ BattleDataMaping → BattleUnit（敌方、直接创建的单位）
       └─ CharacterStatCalculator
            + CharacterProgress.level / ascension（真实存档进度）
            + UnitConfig.growth（成长规则）
            → CharacterStats.Level / OffensiveStats
            → BattleCharacterSnapshot（保存等级及计算后的值快照）
            → BattleUnit.Level / OffensiveStats（我方养成入场）
```

新增 `Battle.Logic.BattleOffensiveStats` 与 `ElementDamageBonuses` 两个可序列化值类型。SO、快照和每个战斗单位分别拥有数值副本；修改其中一个不会通过共享对象引用污染其他实例。

`UnitConfig` 的序列化字段是私有的，外部通过 `OffensiveStats` 只读属性读取。`BattleUnit` 使用 `SetOffensiveStats(...)` 明确替换本单位的运行时属性，并验证输入。不存在新增的公共可变字段。

当前养成对生命、攻击、防御的等级倍率不会自动乘到暴击率和其他百分比上。新属性通过 `UnitConfig.growth` 引用的 `CharacterGrowthConfig`，结合真实等级和突破阶段计算：

```text
新属性 = UnitConfig中的基础值
       + 每级增加 × (CharacterProgress.level - 1)
       + 每次突破增加 × CharacterProgress.ascension
```

在对应 Growth 资源的 Inspector 中，找到 **伤害属性成长 → 每级增加 / 每次突破增加**。两组包含同样的11个属性，各自默认0。这里填写的是每次增加的百分点，例如0.001代表增加0.1个百分点。零成长不是等级未读取，而是该项明确不随等级自然增长。

示例：基础暴击率0.05，每级增加0.001，每次突破增加0.02；实际11级、已突破2次时，暴击率为 `0.05 + 10×0.001 + 2×0.02 = 0.10`。这是可配置的本项目成长规则示例，不是《星穹铁道》角色通用的自然暴击成长规则。

真实等级的来源为 `PlayerRuntime.Characters.GetCharacter(unitId)`，取的是存档养成服务提供的进度副本。正式入场通过 `BattleLaunchData → BattleCharacterSnapshot` 使用它，而非把当前等级存回 UnitConfig。`BattleUnit.Level` 保存入场时的等级，后续防御与击破等级系数可以读取它；本轮没有加入具体等级系数公式。

正式 Play Mode 下，玩家养成入口不存在或尚未就绪时拒绝开始战斗，避免悄悄按裸配置创建角色。Edit Mode 的独立工具仍可按基础配置创建测试单位；敌人直接配置路径暂按1级，敌方遭遇等级与等级曲线尚需单独设计。

入场快照一旦建立，随后修改角色进度或成长配置不会改变该快照内的等级和新属性；重新创建入场快照会读取新的进度。新属性的装备、遗器、行迹贡献仍待后续扩展。成长配置仍在Unity中编辑，Excel当前仅管理基础属性，不包含成长表。

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
- 真实角色等级进入CharacterStats与BattleUnit，新属性按独立的等级和突破规则计算。
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

## 7. 本次实际验证结果

| 验证 | 结果 |
| --- | --- |
| Unity 脚本编译 | 通过，无编译错误 |
| `BattleOffensiveStatsVerification` | 53项通过（含真实等级、独立成长、快照隔离） |
| `CharacterProgressionVerification` | 337项通过（含升级提交后读取真实进度并建立战斗快照） |
| `BattleConfigImportVerification` | 99项通过 |
| 现有9个单位默认属性读取 | 全部取得5%暴击率、50%暴击伤害 |
| 新工作簿导出后原样预览 | 有效，0项更新 |
| 原 `BattleConfig.xlsx` 兼容预览 | 有效，0项更新；缺新列给出警告 |
| 完整 `BattleActionVerification` | 154项通过后，被既有能量UI字段检查中断，不能报告全部通过 |
| 被中断后的战斗逻辑检查补跑 | 服务、资源、敌方决策、结算、波次、持久化6组共29项通过 |

完整战斗验证中的既有问题：`BattleActionVerification.ActionTimelineResourceChecks` 查找 `energy_bar`，当前 `BattleUnitAllyUI` 已使用 `energy_show`；旧验证读取不存在的序列化字段时产生空引用。本次改动前的报告已记录同一错误。本轮没有为通过验证而修改用户的能量UI。

专项验证菜单：`Tools > Battle > Verify Offensive Attributes`。

结果文件：

- `Docs/Verification/BattleOffensiveStatsResults.json`
- `Docs/Verification/BattleOffensiveStatsRegression.json`
- `Docs/Verification/BattleOffensiveStatsCoreRegression.json`
- `Docs/Verification/BattleOffensiveStatsProjectCheck.json`

本次验证是在编辑器中使用独立临时资源进行，未声称已经进行完整的 Play Mode 伤害与视觉验证；新属性还没有接入实际伤害公式。
