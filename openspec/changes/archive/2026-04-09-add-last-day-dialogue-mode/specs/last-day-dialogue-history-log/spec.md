## ADDED Requirements

### Requirement: System SHALL record face-to-face dialogue turns as structured activity history
系统 MUST 将面对面对话中的对象发言、玩家回应和对话结束事件记录为带有说话归属的结构化活动历史，而不是只把它们压扁成普通旁白文本。

#### Scenario: NPC line and player reply are both logged with attribution
- **WHEN** 玩家在面对面对话中经历一轮“对方说话 -> 玩家回应”
- **THEN** 系统 MUST 记录该轮对象发言
- **THEN** 系统 MUST 记录该轮玩家回应
- **THEN** 每条记录 MUST 保留说话者归属与对应文本

#### Scenario: Dialogue termination is logged
- **WHEN** 玩家主动结束当前面对面对话，或系统将其判定为自然结束
- **THEN** 系统 MUST 记录一次对话结束事件
- **THEN** 该事件 MUST 能与对应会话关联

### Requirement: Recorded face-to-face dialogue SHALL be included in story summarization and ending context
面对面对话历史 MUST 纳入故事压缩与终局总结输入，使最终总结能够回收这些关键互动。

#### Scenario: Story compression sees recent face-to-face dialogue
- **WHEN** 系统在“最后一天”阶段执行阶段性故事压缩
- **THEN** 用于压缩的近期上下文 MUST 包含最近记录的面对面对话内容或其结构化摘要

#### Scenario: Ending generation receives dialogue history
- **WHEN** 系统组装终局总结、悼词或类似收束文本所需的活动上下文
- **THEN** 这些上下文 MUST 包含本局记录过的面对面对话历史
- **THEN** 终局生成链路 MUST NOT 只能看到压扁后的普通行动旁白
