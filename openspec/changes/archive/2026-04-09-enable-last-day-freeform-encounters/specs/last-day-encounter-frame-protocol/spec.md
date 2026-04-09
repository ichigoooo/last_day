## ADDED Requirements

### Requirement: Last Day SHALL render from a structured encounter frame
系统 MUST 为“最后一天”每个回合生成并应用一份结构化 `EncounterFrame`，用来声明当前地点呈现、到达模式、遭遇类型、人物信息、旁白、选项和自定义提示。UI MUST 以该协议为唯一可信显示来源。

#### Scenario: Encounter frame is generated for a successful turn
- **WHEN** 玩家提交一次合法输入并完成系统结算
- **THEN** 系统 MUST 请求或构造一份 `EncounterFrame`
- **THEN** 该帧 MUST 至少包含地点名、到达模式、遭遇类型、旁白、选项和显示标记
- **THEN** UI MUST 基于该帧更新地点标题、主视觉、旁白与选项

#### Scenario: Encounter frame overrides prior visual assumptions
- **WHEN** 新回合返回的 `EncounterFrame` 与上一回合显示内容不同
- **THEN** 系统 MUST 以新帧内容为准
- **THEN** UI MUST NOT 继续依赖旧地点状态或旧对象状态来决定当前显示

### Requirement: Encounter frames SHALL declare visibility explicitly per turn
`EncounterFrame` MUST 在每一回合显式声明 `show_scene_image` 与 `show_character_frame`。系统 MUST 将其解释为当前回合的完整显示意图，而不是在上一帧基础上做继承或猜测。

#### Scenario: New frame hides the character
- **WHEN** 新回合 `EncounterFrame.show_character_frame=false`
- **THEN** 系统 MUST 隐藏人物显示区域
- **THEN** 系统 MUST 忽略该帧中附带的任何人物字段
- **THEN** 上一帧人物图和人物信息 MUST 被清除

#### Scenario: New frame shows only the character
- **WHEN** 新回合 `EncounterFrame.show_scene_image=false` 且 `show_character_frame=true`
- **THEN** 系统 MUST 只显示人物内容
- **THEN** 系统 MUST 不再保留上一帧场景图作为默认背景

### Requirement: Encounter frame validation SHALL preserve playability
系统 MUST 对 `EncounterFrame` 执行 JSON 解析、字段校验与字段级默认值修复。任何单次输出错误 MUST NOT 中断当前“最后一天”流程，系统 SHALL 回退到默认帧或字段级兜底行为。

#### Scenario: Encounter frame JSON is malformed
- **WHEN** 模型返回的 `EncounterFrame` JSON 无法解析
- **THEN** 系统 MUST 记录错误
- **THEN** 系统 MUST 回退到默认 `EncounterFrame`
- **THEN** 玩家 MUST 仍然得到可阅读的旁白和可操作的选项

#### Scenario: Encounter frame is missing optional or conditional fields
- **WHEN** `EncounterFrame` 缺失部分字段但仍可部分解析
- **THEN** 系统 MUST 对缺失字段应用默认值或降级逻辑
- **THEN** 系统 MUST 只在必要时关闭对应显示区域
- **THEN** 系统 MUST NOT 放弃整个回合

### Requirement: Encounter frame MUST NOT override system authority
`EncounterFrame` 只能声明当前回合应该如何呈现，MUST NOT 直接决定实际耗时、金钱、电量、消息送达或阶段推进。系统 SHALL 先完成规则结算，再把结果作为上下文传给遭遇帧渲染阶段。

#### Scenario: Encounter frame implies a successful dramatic action
- **WHEN** `EncounterFrame` 中的旁白或遭遇内容暗示一次重要事件成功发生
- **THEN** 系统 MUST 仍以已结算的规则结果为准
- **THEN** LLM 输出 MUST NOT 直接修改系统资源状态

#### Scenario: Encounter frame is generated after rule resolution
- **WHEN** 一次输入同时涉及移动、亮屏耗电或发消息
- **THEN** 系统 MUST 先完成真实规则结算
- **THEN** 遭遇帧渲染 MUST 以结算后的时间、金钱、电量和消息状态为上下文
