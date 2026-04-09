## Why

当前“最后一天”已经具备时间、现金、电量、消息延迟等规则约束，但当场景中出现人物时，体验仍然停留在“人物作为旁白素材被描述”，而不是“人物作为可交互对象与你当面对话”。这会直接削弱游戏最重要的人际重量感，也让“关系”“告别”“道歉”“坦白”这类核心情绪只能被旁观式转述，无法真正被玩家经历。

现在需要把“最后一天”从单一的行动旁白流，升级为“行动模式 + 对话模式”并存的结构：当玩家在场景中遇到某个人时，系统能够进入面对面对话，由 LLM 生成对方说的话、玩家可回的话、自定义回应与结束对话入口，同时把这些对话过程记录进最终总结链路。

## What Changes

- 在“最后一天”中新增**场景内面对面对话模式**：当玩家遇到可交互人物时，回合呈现从环境旁白切换为直接对话。
- 为面对面对话引入新的**运行时会话状态**，用于维护当前说话对象、最近对话轮次、可用回应选项、是否允许继续或结束对话。
- 将 `LastDayDirector` 从单一路径的“行动 -> 遭遇帧”扩展为双路径：普通行动继续走场景遭遇流；若存在活动中的会话，则输入会继续推进当前对话。
- 调整 `LastDay` UI，使其在行动模式与对话模式之间切换：对象头像边框高亮、文本区显示当前人物发言、主选项变为玩家回应、输入框改为自定义回话入口。
- 在对话模式中禁用“前往场所”“手机”“死神花钱”等入口，避免系统模式冲突；玩家只能继续回应或主动结束对话。
- 将面对面对话轮次、玩家回应、对象发言与对话结束事件写入活动日志与叙事摘要输入，确保终局总结能够回收这些关键互动。
- 更新相关提示词契约，让 LLM 生成的人物、人物说话内容与回应选项完全由模型决定，但系统仍掌控模式切换、资源结算、异常兜底与内容安全。

## Capabilities

### New Capabilities

- `last-day-in-scene-dialogue`: 允许玩家在“最后一天”场景中与现场遇到的人进入面对面对话，由系统维护会话状态，并由 LLM 生成对象发言、玩家回应选项和继续/结束对话结果。
- `last-day-dialogue-mode-ui`: 让 `LastDay` 界面在行动模式与对话模式之间切换，在对话模式中高亮对象、显示当前人物说话内容、重映射输入区语义，并禁用与当前会话冲突的外部入口。
- `last-day-dialogue-history-log`: 将面对面对话过程作为结构化活动历史的一部分进行记录，并纳入故事压缩与终局总结输入。

### Modified Capabilities

- None.

## Impact

- Affected code:
  - `scripts/systems/LastDayDirector.cs`
  - `scripts/models/WorldState.cs`
  - `scripts/models/LastDayTurnResult.cs`
  - `scripts/models/EncounterFrame.cs`
  - new dialogue session / dialogue turn related models
  - `scripts/models/SessionActivityLog.cs`
  - `scripts/ui/LastDayScreen.cs`
  - `scenes/last_day.tscn`
- New prompts and protocol contracts will be required for:
  - scene encounter routing into dialogue vs ambient presentation
  - face-to-face dialogue turn generation
  - dialogue continuation / termination output
- Runtime behavior affected:
  - Last Day input flow
  - encounter-to-dialogue transitions
  - UI interaction lock rules during dialogue
  - story summary and ending context assembly
- Testing impact:
  - dialogue mode entry / continue / exit flows
  - dialogue mode UI state and button lock verification
  - activity log capture and summary-input coverage
