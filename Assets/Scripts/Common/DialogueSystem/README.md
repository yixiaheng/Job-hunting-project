# 对话系统使用说明

这是一套基于节点图的可复用对话系统。对话内容在 Unity 编辑器中配置，运行时由同一套逻辑推进节点、判断条件、显示选项并向其他游戏系统发送事件。

这里所说的“2D / 3D 通用”是指运行时通用：2D 游戏和 3D 游戏共用 Dialogue Graph、DialogueRunner、DialogueSession 和 UI。两者只在物理触发组件上不同，不需要制作两套节点编辑器。

## 1. 系统组成

| 组件 | 用途 |
| --- | --- |
| DialogueGraphSO | 保存一整张对话图，包括节点、连线和入口节点 |
| DialogueNode | 一条台词或一个自动分支节点 |
| DialogueRunner | 场景中的总控制器，接收输入并管理当前会话和附近 NPC |
| DialogueSession | 记录一次对话当前进行到哪里，并处理推进、选项和条件 |
| DialogueTrigger | 挂在 NPC 上，把 NPC 与一张对话图关联起来 |
| DialogueInteractor | 检测玩家进入或离开 NPC 的交互范围 |
| DialogueUIController | 显示角色名、正文和选项按钮 |
| SpeakerSO | 保存说话人的名称和名称颜色 |

运行流程：

    玩家进入 NPC 范围
        → DialogueInteractor 注册 NPC
        → 玩家按交互键
        → DialogueRunner 开始对话
        → DialogueSession 选择当前节点
        → DialogueUIController 显示台词和选项

## 2. 场景接入

### DialogueRunner

在场景中创建一个空物体并添加 DialogueRunner。一般一个场景只需要一个。

Persist Across Scenes 启用后，Runner 可以通过 DontDestroyOnLoad 跨场景存在。它保存的对话变量也会在本次游戏运行期间继续保留，但关闭游戏后仍会丢失。

### NPC

NPC 需要引用一张 DialogueGraphSO，并配置触发碰撞体。

3D NPC：

- 添加 DialogueTrigger3D
- 添加 Collider
- 勾选 Collider 的 Is Trigger

2D NPC：

- 添加 DialogueTrigger2D
- 添加 Collider2D
- 勾选 Collider2D 的 Is Trigger

触发检测双方至少有一侧需要对应的 Rigidbody 或 Rigidbody2D，否则 Unity 可能不会调用触发回调。

DialogueTrigger 中的 Graph 是 NPC 使用的对话图。Override Entry Id 通常留空；留空时从图中标记为 Entry 的节点开始。只有同一张图需要从不同节点开始时才使用覆盖入口。

### 玩家

玩家的交互范围对象添加 DialogueInteractor，并配置对应的 Collider 或 Collider2D。玩家进入 NPC 的触发范围后，NPC 会进入 Runner 的附近目标列表；离开范围后会被移除。

### UI

在 Canvas 或 UI 管理对象上添加 DialogueUIController，并绑定：

- 对话框 CanvasGroup
- 角色名文本（可选）
- 对话正文文本
- 交互列表父节点和 Prefab
- 选项列表父节点和 Prefab

选项 Prefab 需要包含 Button 和 TextMeshProUGUI。系统会根据当前节点的可用选项动态创建按钮。

玩家输入通常只需要调用：

    DialogueRunner.Instance.ShowDialogue();

未开始对话时，这个方法会和当前选中的 NPC 开始对话；对话进行中时，它会推进到下一句。当前节点有选项时，继续键不会跳过选项。

## 3. 创建对话图

在 Project 面板中选择：

    Create > Dialogue > Dialogue Graph

打开方式：

- 选中 DialogueGraphSO，在 Inspector 点击 Open Graph Editor
- 使用 Tools > Dialogue > Graph Editor，再把资源放入 Graph 字段

Graph View 空白处右键可以：

- Create Dialogue Node：创建普通 Line 节点
- Create Branch Node：创建自动 Branch 节点
- Set As Entry：把选中的节点设为对话入口

节点左侧的 In 是输入端口，右侧的 Next、Fallback 或 Choice 是输出端口。把输出端口连接到其他节点的 In，即可建立对话流程。

节点字段：

- Title：编辑器内的节点标题
- Type：Line 或 Branch
- Speaker：说话人资源
- Text：显示给玩家的台词
- On Enter：进入节点时执行的信号
- Add Choice：添加玩家选项或自动分支路径

修改后点击窗口顶部的 Save，或者保存 Unity 工程。

## 4. Line 与 Branch

### Line

Line 是显示给玩家的节点。

没有选项时，玩家按继续键后沿 Next 进入下一个节点。Next 没有连接时，再次推进会结束对话。

有选项时，系统显示满足条件的选项按钮。玩家点击选项后，沿对应的 Choice 连线进入目标节点。

示例：

    问候（Line）
        → 询问（Line）
            → 你叫什么名字 → 名字回答（Line）
            → 我是谁         → 身份回答（Line）

### Branch

Branch 是不显示给玩家的自动判断节点。进入 Branch 后，系统从上到下检查 Choice 的条件，并自动走第一条满足条件的分支。如果没有任何 Choice 可用，就走 Fallback。

示例：

    入口（Branch）
        → QuestState 等于 Completed → 完成对白
        → QuestState 等于 Accepted  → 进行中对白
        → Fallback                    → 初次对白

Branch 的选项顺序很重要。Always 会永远成立，因此不要把 Always 放在其他判断条件之前。

## 5. 选项条件

每个 Choice 都可以添加 Conditions。一个 Choice 有多个条件时，当前实现要求所有条件同时满足。

| Type | 含义 | 示例 |
| --- | --- | --- |
| Always | 永远满足 | 普通无条件选项 |
| FlagSet | 指定变量存在 | HasBadge |
| FlagNotSet | 指定变量不存在 | RewardReceived |
| Equals | 变量值等于指定值 | QuestState / Accepted |
| NotEquals | 变量值不等于指定值 | QuestState / Completed |

FlagSet 只检查变量是否存在，不检查具体值。即使 HasBadge 的值为 0，它仍然属于已设置。需要比较具体状态时，应使用 Equals。

NotEquals 在变量不存在时通常也会成立，因为不存在的变量会读取为空字符串。要判断变量是否尚未创建，应使用 FlagNotSet。

## 6. On Enter 信号

On Enter 会在进入节点时执行，当前支持三种类型。

### SetFlag

设置一个字符串变量，例如：

    Type: SetFlag
    Key: QuestState
    Value: Accepted

如果 Value 留空，系统会写入 1。

### ClearFlag

删除指定变量，例如：

    Type: ClearFlag
    Key: TemporaryDialogueState

清除后，FlagSet 不再满足，FlagNotSet 会满足。

### Emit

向其他游戏系统发送事件，例如：

    Type: Emit
    Key: QuestAccepted
    Value: FindBadge

Emit 不会自动创建任务、播放动画或发放奖励。任务、动画或物品系统需要订阅 DialogueRunner.DialogueSignalEmitted，再根据 Key 和 Value 执行自己的逻辑。

需要同时触发行为并记录状态时，可以在同一个节点中同时添加 Emit 和 SetFlag。

## 7. 变量与存档

对话变量当前保存在 DialogueRunner 内部的 DialogueVariableStore 中，本质上是字符串键值表，例如：

    SampleDialogueVisited = 1
    QuestState = Accepted

变量的保留范围：

| 情况 | 是否保留 |
| --- | --- |
| 当前对话推进 | 是 |
| 结束对话后再次交谈 | 是 |
| Runner 跨场景存在 | 是 |
| Runner 被销毁或重新创建 | 否 |
| 关闭游戏再启动 | 否 |

DialogueGraphSO 保存的是对话规则，不会把玩家运行时状态写回资源文件。永久状态需要接入存档系统：保存时导出变量，读取存档时重新写回变量仓库。

## 8. 推荐的任务对话结构

    入口 Branch
        → QuestState = Completed → 已完成对白
        → QuestState = Accepted  → 任务进行中对白
        → Fallback                → 初次询问

    初次询问（Line）
        → 接受任务 → 接受任务节点
        → 暂时拒绝 → 拒绝对白

    接受任务节点（Line）
        On Enter:
            SetFlag / QuestState / Accepted
            Emit / QuestAccepted / FindBadge

任务完成后，任务系统把 QuestState 更新为 Completed。下次与 NPC 对话时，入口 Branch 会自动进入完成对白。

## 9. 2D 与 3D 的区别

对话数据和运行逻辑完全相同，区别只在 Unity 的物理组件：

| 类型 | NPC Trigger | 碰撞组件 | 刚体 |
| --- | --- | --- | --- |
| 3D | DialogueTrigger3D | Collider | Rigidbody |
| 2D | DialogueTrigger2D | Collider2D | Rigidbody2D |

3D 物理回调不会响应 Collider2D，2D 物理回调也不会响应 Collider。除此之外，Graph、Session、Runner 和 UI 都可以直接共用。

## 10. 导出与复用

使用菜单：

    Tools > Dialogue > Export Package

可以把对话系统导出为 Unity Package，再导入其他 Unity 项目。导入后仍需在目标项目中配置：

- 场景中的 DialogueRunner
- NPC 的 DialogueTrigger 和 Graph 引用
- 玩家交互范围
- UI 和 Prefab 引用
- 项目自己的输入、任务和存档连接

一张 DialogueGraphSO 可以被多个 NPC 引用。需要从不同位置开始时，可以给不同 NPC 配置不同的 Override Entry Id；一般情况保持为空即可。

## 11. 常见问题

### 节点连线后仍显示旧台词

检查 NPC 引用的 Graph 是否就是当前编辑的资源，项目中是否存在同名 DialogueGraphSO，Override Entry Id 是否误填，以及场景里是否有重复或旧版的 DialogueUIController。

### Branch 没有按预期跳转

检查变量 Key 的拼写和大小写、条件排列顺序，并确认负责 SetFlag 的节点确实进入过。Branch 自身不显示文字，只显示最终跳转到的 Line 节点。

### 2D 或 3D 没有触发

检查 Trigger 类型、Is Trigger、Rigidbody 类型是否匹配。Collider 与 Collider2D 属于两套不同的物理系统。

### 对话时玩家仍能移动

移动或输入脚本应订阅 DialogueRunner.DialogueLockChanged。收到 true 时锁定移动或切换 Action Map，收到 false 时恢复。对话系统只发送状态，不直接控制具体角色控制器。

## 12. 当前功能边界

当前系统包含：

- 节点图编辑
- 线性对话和玩家选项
- 条件选项和自动 Branch
- 节点进入信号
- 2D / 3D 触发接入
- 对话变量
- Unity Package 导出

以下内容需要后续接入或扩展：

- 退出游戏后的变量存档
- 任务、背包和奖励系统
- 本地化、语音和打字机效果
- OR 条件、数值范围比较和复杂表达式
- 完整的 UI 主题和动画

建议先验证“入口 → Line → Choice → 结果”的最小闭环，再逐步加入 Branch、任务变量和外部事件。

