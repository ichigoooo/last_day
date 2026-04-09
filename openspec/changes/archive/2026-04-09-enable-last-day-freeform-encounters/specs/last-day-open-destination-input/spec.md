## ADDED Requirements

### Requirement: Last Day SHALL accept arbitrary destinations and actions
“最后一天”阶段 MUST 允许玩家通过推荐入口或自然语言输入任意目的地与任意行动，而不是把推荐地点列表作为唯一合法内容边界。系统 SHALL 保留玩家原始目的地文本，并将其送入回合解释流程。

#### Scenario: Player enters a destination outside recommended places
- **WHEN** 玩家在“最后一天”输入一个不在推荐地点中的目的地，例如“南极”
- **THEN** 系统 MUST 接受该输入并进入回合解释流程
- **THEN** 系统 MUST 保留原始目的地文本供后续协议使用
- **THEN** 系统 MUST NOT 因为该地点不在推荐列表中而直接拒绝本回合

#### Scenario: Player uses a recommended place
- **WHEN** 玩家通过地图或推荐入口选择一个已有快捷地点
- **THEN** 系统 SHALL 将其视为一次合法目的地输入
- **THEN** 该输入 SHALL 与自由文本输入共享同一导演流程

### Requirement: Recommended places SHALL remain optional shortcuts
系统 SHALL 保留推荐地点或快速前往入口以降低操作成本，但这些入口 MUST 被解释为快捷方式，而不是完整地点集合。推荐入口不存在时，玩家仍 MUST 能完成“最后一天”回合推进。

#### Scenario: Recommended places are available
- **WHEN** “最后一天”场景展示推荐地点或地图快捷入口
- **THEN** 玩家 MUST 仍可通过自然语言输入其他目的地和行动
- **THEN** 推荐入口 MUST NOT 限制后续遭遇生成范围

#### Scenario: Shortcut UI is unavailable
- **WHEN** 推荐地点入口暂时不可用、隐藏或未加载
- **THEN** 玩家 MUST 仍能通过文本输入推进“最后一天”回合
- **THEN** 系统 MUST 不因缺少快捷入口而阻塞回合

### Requirement: Unsupported destinations SHALL be interpreted, not hard-rejected
对于当前世界模型无法直接映射到有限地点目录的目的地，系统 MUST 继续推进内容解释，并通过后续遭遇帧中的地点名与到达模式表达“真实到达、象征到达、边界受阻、记忆投射或今日不可达”等结果，而不是在输入阶段硬拒绝。

#### Scenario: Destination cannot map to a canonical place
- **WHEN** 玩家输入一个无法落入本地 canonical location 的目的地
- **THEN** 系统 MUST 继续执行意图解析与后续回合渲染
- **THEN** 系统 MUST 将该目的地原文保留给遭遇帧渲染阶段

#### Scenario: Destination is narratively unreachable today
- **WHEN** 回合渲染结果认为该目的地今天无法被真实抵达
- **THEN** 系统 MUST 允许遭遇帧以 `not_arrivable_today`、`blocked_arrival` 或其他合法到达模式表达这一结果
- **THEN** 玩家 MUST 仍然得到一段可继续交互的旁白与选项
