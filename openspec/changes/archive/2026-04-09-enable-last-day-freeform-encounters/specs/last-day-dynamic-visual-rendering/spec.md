## ADDED Requirements

### Requirement: Main visual SHALL support four protocol-driven layout states
“最后一天”主视觉区 MUST 根据 `EncounterFrame.show_scene_image` 与 `EncounterFrame.show_character_frame` 渲染四种布局状态：双显示、仅场景、仅人物、双隐藏。系统 SHALL 不再固定保留对象占位。

#### Scenario: Both scene and character are present
- **WHEN** `EncounterFrame.show_scene_image=true` 且 `show_character_frame=true`
- **THEN** 主视觉区 MUST 以双卡并排方式显示场景与人物

#### Scenario: Only scene is present
- **WHEN** `EncounterFrame.show_scene_image=true` 且 `show_character_frame=false`
- **THEN** 主视觉区 MUST 只显示场景卡
- **THEN** 场景卡 MUST 在主视觉区内居中

#### Scenario: Only character is present
- **WHEN** `EncounterFrame.show_scene_image=false` 且 `show_character_frame=true`
- **THEN** 主视觉区 MUST 只显示人物卡
- **THEN** 人物卡 MUST 在主视觉区内居中

#### Scenario: Neither scene nor character is present
- **WHEN** `EncounterFrame.show_scene_image=false` 且 `show_character_frame=false`
- **THEN** 主视觉区 MUST 收起或降到最小高度
- **THEN** UI MUST 不显示空白占位框

### Requirement: Scene and character visuals SHALL be generated from briefs
系统 MUST 支持从 `EncounterFrame.scene_visual_brief` 与 `EncounterFrame.character_visual_brief` 分别生成场景图和人物图。两类资源 MUST 使用独立请求和缓存键，以避免同一地点不同遭遇之间的资源串用。

#### Scenario: Same place name with different encounter visuals
- **WHEN** 两个回合使用相同 `place_name` 但返回不同 `scene_visual_brief`
- **THEN** 系统 MUST 将其视为不同场景资源
- **THEN** 系统 MUST NOT 因为地点名相同而复用旧场景图

#### Scenario: Character role changes across turns
- **WHEN** 连续回合中的人物角色或人物视觉描述不同
- **THEN** 系统 MUST 分别请求或命中不同的人物资源缓存
- **THEN** 系统 MUST 用新人物图覆盖旧人物图

### Requirement: Visual updates SHALL clear stale state before applying a new frame
系统 MUST 在应用新 `EncounterFrame` 前清理上一帧的主视觉状态，并抑制过期异步请求回写，避免上一帧的场景图、人物图、人物名或副标题污染当前回合。

#### Scenario: A previous character request completes after a new frame is active
- **WHEN** 旧回合的人物图异步请求在新回合激活后才返回
- **THEN** 系统 MUST 丢弃该过期请求结果
- **THEN** 系统 MUST 保持当前回合的人物显示不被覆盖

#### Scenario: A new frame removes the prior character
- **WHEN** 上一回合有人物而新回合 `show_character_frame=false`
- **THEN** 系统 MUST 在渲染新回合前清空旧人物图、旧人物名和相关显示态

### Requirement: Visual rendering SHALL degrade gracefully on asset failures
无论是 API 不可用、SVG 生成失败、SVG 解析失败还是缓存缺失，系统 MUST 以占位图或极简渲染继续当前回合，而不是阻断玩家操作。

#### Scenario: Scene SVG generation fails
- **WHEN** 场景图生成或解析失败
- **THEN** 系统 MUST 使用场景占位图或极简场景渲染替代
- **THEN** 玩家 MUST 仍然看到旁白和选项

#### Scenario: Character SVG generation fails
- **WHEN** 人物图生成或解析失败
- **THEN** 系统 MUST 使用人物占位图或隐藏人物图降级
- **THEN** 当前回合 MUST 继续可玩
