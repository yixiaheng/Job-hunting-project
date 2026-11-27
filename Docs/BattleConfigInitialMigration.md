# 首次配置身份修复记录

日期：2026-11-27。用户确认后执行；所有原有 `.meta`、GUID、目录和美术/成长引用保留。修改前已确认 Assets/Config 下没有未保存的资产编辑。

## 修复内容

| 资产 | 原值 | 新值 |
| --- | --- | --- |
| Battle/Units/Enemies/Enemy_A | 空 unitId | Enemy_A |
| Battle/Units/Enemies/Enemy_B | 空 unitId | Enemy_B |
| Battle/Units/Enemies/Enemy_C | 空 unitId | Enemy_C |
| Battle/Buffs/AtkUp | 201 | 9201 |
| Battle/Buffs/DefDown | 202 | 9202 |
| Battle/Buffs/SpdUp | 203 | 9203 |
| Battle/Buffs/SpdDown | 204 | 9204 |
| Battle/Buffs/Guard | 205 | 9205 |
| Battle/Buffs/QuantumDot | 206 | 9206 |
| Battle/Skills/Skill_Basic 第0个效果 | buffId=1，引用已不存在的GUID | buffId=9201，引用 Battle/Buffs/AtkUp |

以上路径均相对于 Assets/Config。Character/Buff_201—206 的 ID 和内容保持原样，两套 Buff 不合并，各自的数值和类型保持原样。修复前检索未发现 Battle/Buffs 六个资产在项目序列化资源中的引用；本次仅按用户选择为 Skill_Basic 接入 AtkUp。

旧技能的空 effectKey 不在这次修复中强行补写。导出工作簿提供 `legacy.1.0`、`legacy.1.1`、`legacy.2.0`、`legacy.3.0` 候选，首次应用时可从预览看见。

## 备份

原文件和 meta 保存在项目根目录 `ConfigImportBackups/InitialIdentityRepair/files/`，保持完整项目相对路径，`paths.txt` 列出全部10个资产。已逐一比较原 meta 与修复后 meta 的 SHA-256，完全一致。

这是一份经过确认的首次资产迁移备份，与导入器每次应用产生的事务备份分开。若要撤销这次身份迁移，先退出运行、处理未保存编辑，再按 paths.txt 将 files 下对应资产及 meta 恢复到原路径并刷新 Unity。恢复后会重新出现空ID、重复ID和失效Buff引用，工作台会再次阻止正式导出。
