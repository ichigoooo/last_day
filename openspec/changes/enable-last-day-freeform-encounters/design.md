## Context

当前 `LastDay` 阶段已经有相对完整的规则层：`TimeManager` 负责时间恒逝，`MoneySystem` 负责现金，`BatterySystem` 负责亮屏耗电和充电，`MessageSystem` 负责异步回复，`LastDayDirector` 已承担一部分“输入 -> 结算 -> 叙事”的导演职责。这些系统已经足以支撑“最后一天”的核心紧迫感。

问题在于内容层与视觉层仍停留在 Demo 架构：

- 输入自由度不足。`LocationManager` 仍以有限 canonical location 为中心，推荐地点实际上成了内容边界。
- 遭遇表达不足。当前回合模型只承载“旁白 + 三选项”，无法声明“这一帧到底看见什么、有没有人、人物是谁、地点是否真的到达”。
- 主视觉不可信。`LastDay` 目前是固定双卡布局，右侧对象位长期是占位图，无法满足“无对象则隐藏、单元素居中、同地点不同遭遇视觉变化”的产品要求。
- 资源缓存粒度错误。当前场景图缓存以 `locationId` 为键，无法表达“同一地点在不同遭遇下应显示不同主视觉”的要求。

这次变更是一个跨模块的架构升级：会改动导演流程、运行时模型、主视觉布局、提示词契约和 SVG 生成链路，因此需要正式 design 先把关键决策定下来。

约束如下：

- 项目是 Godot 4.6 + C# 移动向单局体验
- 规则层必须继续由引擎掌控，不能让 LLM 直接改资源与阶段
- 用户必须保持“任意地点、任意行动”的主观自由度
- 无 API、JSON 非法、SVG 失败时不能中断流程
- 需要兼容现有 `LastDay` 场景、状态栏、手机、花钱等既有系统

## Goals / Non-Goals

**Goals:**

- 让“最后一天”接受任意目的地与任意行动输入，推荐地点降级为快捷入口而非内容边界。
- 建立一套 `EncounterFrame` 协议，让 LLM 每回合都能自由声明地点呈现、遭遇、人物、旁白与选项。
- 把当前 `LastDay` 主视觉改造成协议驱动的四态布局：双显示、仅场景、仅人物、双隐藏。
- 将场景图和人物图统一接入 brief 驱动的 SVG 生成链路，并按内容哈希做运行时缓存。
- 让规则层继续保持权威：时间、现金、电量、消息、死亡推进、内容安全与兜底都由系统掌控。
- 为无 API、非法 JSON、字段缺失、异步加载串帧、SVG 生成失败定义稳定回退路径。

**Non-Goals:**

- 不实现“LLM 直接主控整个游戏世界状态”的 GM 架构。
- 不要求本次变更引入真正的地理模拟、真实路线或无限世界导航。
- 不要求一次性重做 `LastDay` 之外的其他阶段。
- 不要求本次变更解决所有 UI 动效与美术抛光问题。
- 不要求移除现有推荐地点、地图入口、手机和花钱系统。

## Decisions

### Decision 1：采用双协议、双调用的导演流程

新增两个明确分工的协议对象：

- `IntentParseResult`：用于理解用户输入，输出旅行意图、动作类型、耗时档位、屏幕使用、消费档位、摘要等机器语义
- `EncounterFrame`：用于声明当前回合最终该如何呈现给用户，包括地点名、到达模式、遭遇类型、人物信息、视觉 brief、旁白和选项

导演流程定为：

1. 用户输入
2. LLM 生成 `IntentParseResult`
3. 系统按规则结算时间 / 钱 / 电 / 消息
4. LLM 在已结算结果基础上生成 `EncounterFrame`
5. 系统校验、清洗、补默认值
6. UI 应用 `EncounterFrame`

选择该方案而不采用“单次大 prompt 直接生成一切”，原因是：

- 意图理解和叙事渲染是两类任务，混在一起会显著降低 JSON 稳定性
- 规则层需要在两次调用之间插入真实结算，否则 LLM 会自然倾向于越权决定后果
- 双协议便于日志、测试、兜底和后续演进

替代方案：

- 单次调用直接生成旁白、视觉和系统字段：实现快，但 JSON 更脆、调试困难、规则边界模糊
- 系统维护遭遇表再让 LLM 只写文案：更稳，但不符合本次“遭遇什么、遇到什么人都由 LLM 决定”的产品目标

### Decision 2：开放地点输入，保留推荐地点但取消白名单地位

`LocationManager` 继续保留，但角色改为：

- 推荐地点快捷入口
- 本地展示名、别名与最小兜底参考

它不再决定：

- 用户能不能去某个地点
- 某个地点只能对应哪个遭遇

所有地点输入都先以原文保留，并进入 `IntentParseResult.destination_text`。后续 `EncounterFrame.place_name` 与 `arrival_mode` 由 LLM 在系统结算后给出，用来表达：

- 真实到达
- 象征性到达
- 门口被拦下
- 进入记忆投射
- 今天无法真正抵达

选择该方案而不继续强化地点白名单，原因是：

- 产品目标明确要求“用户想去哪里就去哪里”
- 开放输入的关键不是无限地图，而是允许内容层自由解释用户想去的地方
- 到达模式可以承接不可达地点，而不需要系统先做硬拒绝

### Decision 3：EncounterFrame 是 UI 的唯一可信来源

当前 UI 依赖多个分散来源：

- 当前地点标题来自 `WorldState.CurrentLocationId`
- 左图来自地点卡
- 右图来自固定占位图
- 文本来自 `NarrativeTurn`

这会导致“文本说遇到保安，但 UI 还在显示心境占位图”之类的不一致。

新的约束是：

- 每回合必须生成一份 `EncounterFrame`
- UI 只能按 `EncounterFrame` 决定：
  - 地点标题
  - 是否显示场景图
  - 是否显示人物图
  - 人物名 / 人物角色
  - 旁白与选项

系统不再允许 UI 继承上一帧视觉状态。`show_scene_image` 和 `show_character_frame` 必须每回合显式返回，默认不继承。

替代方案：

- 继续保留 `NarrativeTurn`，只新增几个 bool 字段：实现量小，但会让模型、UI 和视觉管线长期耦合在一个贫弱对象上，不利于后续扩展

### Decision 4：视觉资源改为 brief 驱动，并以内容哈希缓存

当前场景图缓存键是 `locationId`，这不适合“同一地点不同遭遇不同视觉”的目标。

新的资源链为：

- `EncounterFrame.scene_visual_brief` -> `VisualSvgService.GetSceneTextureAsync(...)`
- `EncounterFrame.character_visual_brief` + `character_role` -> `VisualSvgService.GetCharacterTextureAsync(...)`

缓存键改为：

- `scene::<hash(scene_visual_brief)>`
- `character::<role>::<hash(character_visual_brief)>`

这样可以：

- 避免同一地点复用错误场景图
- 让人物图真正跟随这一回合内容变化
- 在无 API 或生成失败时精确回退到场景占位或人物占位

替代方案：

- 继续以地点为键缓存场景图：不满足同地点多遭遇
- 人物图全部用固定角色资源表：稳定，但限制了“人物由 LLM 决定”的范围

### Decision 5：主视觉布局按四态渲染，且应用新帧前必须清旧帧

主视觉只允许四种状态：

1. 场景 + 人物：双卡并排
2. 仅场景：场景卡居中
3. 仅人物：人物卡居中
4. 双隐藏：主视觉区收起或降到最小高度

应用新帧时，`LastDayScreen` 必须：

1. 清空旧人物纹理与旧人物文本
2. 清空旧场景副标题
3. 标记旧的异步视觉请求失效
4. 再根据新帧重新布局并请求资源

之所以把“清旧帧”提升为明确设计决策，是因为这类串帧问题在异步纹理加载下极易出现，且一旦出现会直接破坏内容可信度。

### Decision 6：规则层继续绝对权威

本次变更不改变现有规则边界：

- 时间推进仍由 `TimeManager`
- 现金仍由 `MoneySystem`
- 电量仍由 `BatterySystem`
- 消息延迟仍由 `MessageSystem`
- 阶段切换仍由 `GameManager` / `SceneSwitcher`

LLM 在任何协议中都不得声明：

- 实际扣除分钟数
- 实际花费金额
- 电量变化
- 是否死亡
- 消息何时送达

这意味着 `EncounterFrame` 是**声明式显示协议**，不是**状态写入协议**。

替代方案：

- 让 `EncounterFrame` 直接带系统结果：省 prompt 变量，但会模糊“显示层”和“规则层”的边界

### Decision 7：异常处理采用分层兜底，不中断回合

本方案将兜底拆为四层：

1. `Intent Parse` 失败 -> 使用默认 `IntentParseResult`
2. `EncounterFrame` JSON 非法 -> 使用默认 `EncounterFrame`
3. 字段部分缺失 -> 字段级补默认值
4. SVG 生成失败 -> 资源级占位图兜底

无论失败发生在哪一层，都不允许中断当前回合，也不允许把原始错误文本直接展示给用户。

## Risks / Trade-offs

- [Risk] LLM 输出结构化 JSON 的稳定性不足，尤其是同时包含自由地点名、人物信息与选项时更容易漏字段
  → Mitigation：双协议拆分、字段级默认值、严格校验器、默认帧回退、prompt 限制“只输出 JSON”

- [Risk] 开放地点输入会让同一回合内容更分散，难以保证叙事连续性
  → Mitigation：`IntentParseResult.summary`、`EncounterFrame.encounter_summary` 和现有 `NarrativeSummary` 共同形成最小连续性记忆

- [Risk] 异步场景图/人物图请求可能发生串帧，导致上一帧人物出现在下一帧
  → Mitigation：在 `LastDayScreen` 引入 frame token 或 request token，只有最新帧请求允许落地

- [Risk] 视觉资源由 brief 驱动后，缓存命中率下降、生成调用次数增加
  → Mitigation：按内容哈希缓存，同 brief 不重复生成；对极短回合可优先显示占位图再异步替换

- [Risk] 推荐地点被降级后，地图 UI 可能显得“不再重要”
  → Mitigation：保留“推荐去处 / 快速前往”的定位，把它设计成提升操作效率而非决定内容范围

- [Risk] 开放输入在无 API 时体验会明显退化
  → Mitigation：定义通用默认帧和泛化占位叙事，保证流程不断，但接受内容质量下降

- [Trade-off] 双调用比单调用多一次模型开销
  → 回报是规则边界更清晰、JSON 更稳、后续更好测

- [Trade-off] EncounterFrame 把 UI 所需字段集中后，模型负担会增加
  → 回报是 UI 状态来源单一，长期维护更简单

## Migration Plan

1. 引入新模型但不立即删除旧模型
   - 新增 `IntentParseResult`、`EncounterFrame`
   - 允许 `NarrativeTurn` 暂时保留，作为迁移期间 fallback

2. 重构导演流程
   - 先让 `LastDayDirector` 内部按双协议运行
   - 输出仍可临时映射回现有 UI 层对象，确保分步迁移

3. 重构主视觉 UI
   - 在 `last_day.tscn` 和 `LastDayScreen` 中加入四态布局与清帧机制
   - 人物图仍可先用占位图，确认协议成功后再接 SVG 生成功能

4. 接入通用视觉服务
   - 把现有地点 SVG 生成器扩展为通用 `VisualSvgService`
   - 先接场景 brief，再接人物 brief

5. 清理旧耦合路径
   - 当 `EncounterFrame` 成为唯一显示来源后，再逐步下线“固定右侧占位图”与按 `locationId` 缓存的旧逻辑

6. 手动回滚策略
   - 若新协议不稳定，可暂时把 `LastDayDirector` 切回旧叙事渲染路径
   - UI 仍保留旧 `NarrativeTurn` 兼容分支，直到协议稳定

## Open Questions

- `EncounterFrame` 是否要进入可恢复存档，还是只保留为运行时状态
- `arrival_mode` 是否需要在 UI 上直接展示给用户，还是只用于内部解释和日志
- 人物图第一版是否允许完全抽象的“轮廓角色图”，还是必须尽快接入 brief 驱动 SVG
- 推荐地点入口是否需要加“自定义地点”单独按钮，还是统一复用底部输入框
