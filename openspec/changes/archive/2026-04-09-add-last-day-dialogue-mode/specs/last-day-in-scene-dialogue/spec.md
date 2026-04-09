## ADDED Requirements

### Requirement: Last Day SHALL support model-generated face-to-face dialogue in scene
系统 MUST 在“最后一天”场景中支持由 LLM 生成的现场人物和面对面对话，而不要求人物必须来自预定义 NPC 表。只要当前遭遇判定为可交互人物在场，系统 MUST 能以该人物为会话对象进入面对面对话。

#### Scenario: Dialogue starts with a generated person at a freeform encounter
- **WHEN** 玩家完成一次合法行动，且本回合遭遇返回一个可交互人物与开场对白
- **THEN** 系统 MUST 用该回合返回的人物信息创建活动中的面对面对话
- **THEN** 该人物 MUST 可以是模型临时生成的身份，而不是预置名单中的固定角色
- **THEN** 玩家 MUST 直接看到该人物对自己说的话

#### Scenario: Ambient encounter remains non-dialogue
- **WHEN** 玩家完成一次合法行动，但本回合遭遇没有进入面对面对话
- **THEN** 系统 MUST 保持普通行动/环境呈现
- **THEN** 系统 MUST NOT 创建活动中的面对面对话

### Requirement: Face-to-face dialogue SHALL advance through reply turns
面对面对话 MUST 以轮次推进。每一轮 MUST 至少包含当前人物说的话、玩家可选的回应，以及自定义输入回应入口。

#### Scenario: Opening turn contains the other person’s line and reply options
- **WHEN** 玩家首次进入面对面对话
- **THEN** 系统 MUST 展示当前人物的开场发言
- **THEN** 系统 MUST 同时提供玩家可回的多个回应选项
- **THEN** 系统 MUST 允许玩家输入自定义回应文本

#### Scenario: Custom reply continues the active dialogue
- **WHEN** 玩家在活动中的面对面对话里输入一句自定义回应
- **THEN** 系统 MUST 将这句输入解释为对当前会话对象的回应
- **THEN** 系统 MUST 生成并展示后续对话轮次，而不是把它当作新的场景行动

### Requirement: Player SHALL be able to end an active face-to-face dialogue
面对面对话 MUST 提供明确的结束入口。结束后系统 MUST 退出会话并回到普通行动模式。

#### Scenario: Player ends the dialogue explicitly
- **WHEN** 玩家在活动中的面对面对话里选择“结束对话”
- **THEN** 系统 MUST 结束当前会话
- **THEN** 系统 MUST 让后续输入重新回到普通行动语义

#### Scenario: System concludes the dialogue after a closing turn
- **WHEN** 当前会话的下一轮结果声明本段对话已经结束
- **THEN** 系统 MUST 关闭活动中的面对面对话
- **THEN** 玩家 MUST 能继续进行其他普通行动
