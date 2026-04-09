## ADDED Requirements

### Requirement: Dialogue mode UI SHALL visually indicate the active speaker and conversation state
当“最后一天”处于面对面对话模式时，界面 MUST 明确表现出“你正在和某个人说话”，包括当前对象高亮、对象身份可见，以及当前文本属于该对象发言而非环境旁白。

#### Scenario: Portrait becomes the active dialogue focus
- **WHEN** 玩家进入面对面对话
- **THEN** 当前对象头像容器 MUST 进入高亮或激活状态
- **THEN** 界面 MUST 显示当前对象的名称、身份或关系提示
- **THEN** 主文本区 MUST 以该对象当前发言为核心内容

#### Scenario: UI leaves dialogue state cleanly
- **WHEN** 当前面对面对话结束
- **THEN** 当前对象高亮 MUST 被清除
- **THEN** 文本区和输入区 MUST 恢复普通行动模式语义

### Requirement: Dialogue mode UI SHALL remap options and input into reply semantics
面对面对话模式下，主选项和底部输入区 MUST 表示“你如何回应”，而不是“你接下来做什么行动”。

#### Scenario: Reply controls replace action controls semantically
- **WHEN** 玩家处于面对面对话模式
- **THEN** 主选项文案 MUST 表示玩家对当前人物的回应
- **THEN** 输入框提示文案 MUST 明确表示这是自定义回话入口
- **THEN** 提交按钮文案或语义 MUST 表示“回应”而不是“行动”

#### Scenario: Reply options return to action semantics after dialogue
- **WHEN** 当前面对面对话结束并回到普通行动模式
- **THEN** 主选项 MUST 重新表示场景行动选择
- **THEN** 输入框和提交按钮 MUST 恢复普通行动语义

### Requirement: Dialogue mode SHALL disable conflicting out-of-band actions
面对面对话模式下，系统 MUST 禁用会破坏当前会话语义的外部入口，包括“前往场所”“手机”“死神花钱”等。

#### Scenario: Conflicting shortcuts are disabled during dialogue
- **WHEN** 玩家处于面对面对话模式
- **THEN** “前往场所”入口 MUST 不可用
- **THEN** “手机”入口 MUST 不可用
- **THEN** “死神花钱”入口 MUST 不可用

#### Scenario: Conflicting shortcuts are restored after dialogue
- **WHEN** 当前面对面对话结束
- **THEN** 若系统本身没有其他锁定原因，这些入口 MUST 恢复可用

