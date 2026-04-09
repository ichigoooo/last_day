## Why

当前“最后一天”阶段已经具备时间、现金、电量、消息延迟等核心约束，但内容层仍停留在“有限地点 + 固定双卡主视觉 + 旁白三选项”的 Demo 结构。它无法兑现综合设计文档中“去任何地方、做任何事、在场景中自然遭遇”的目标，也无法满足新增设计变更中“主视觉随内容动态变化、同一地点可出现不同遭遇、无对象时不显示对象位”的产品要求。

现在需要把“最后一天”从“规则已成形、内容表达受限”的状态，升级为“输入自由、遭遇自由、视觉自由、协议受控”的正式架构。这样既能让 LLM 决定这一回合具体遇到什么、遇到谁、看见什么，也能保证 Godot 工程仍然能够稳定解析、渲染、结算并兜底。

## What Changes

- 允许玩家在“最后一天”中通过推荐入口或自然语言输入**任意目的地**与**任意行动**，而不是只能在有限地点目录中游走。
- 引入新的 **Encounter Frame 协议**，要求 LLM 每回合返回结构化的地点呈现、遭遇类型、人物信息、视觉 brief、旁白和选项，系统按协议校验并渲染。
- 将主视觉区从固定“左场景 + 右占位”重构为**协议驱动的四态布局**：双显示、仅场景、仅人物、双隐藏。
- 将场景图与人物图统一纳入**描述式视觉资源链**：LLM 输出视觉 brief，系统再请求 SVG 生成并做缓存，而不是直接依赖固定占位图。
- 重构“最后一天”导演流程，拆分为：意图解析、系统结算、遭遇帧渲染、UI 应用与异常兜底。
- 保留并强化现有规则层边界：时间、现金、电量、消息延迟、死亡推进、内容安全与异常回退继续由系统控制。
- 为 API 不可用、JSON 非法、字段缺失、SVG 失败等情况定义正式兜底行为，确保全自由输入不会破坏单局流程。

## Capabilities

### New Capabilities

- `last-day-open-destination-input`: 允许“最后一天”接受任意目的地和任意行动输入，推荐地点只作为快捷入口，不再构成内容边界。
- `last-day-encounter-frame-protocol`: 定义并落地一套由 LLM 输出、由系统校验的遭遇帧协议，用于声明地点呈现、遭遇类型、人物信息、旁白、选项和回退行为。
- `last-day-dynamic-visual-rendering`: 让主视觉区按遭遇帧协议动态渲染场景图与人物图，支持四态布局、brief 驱动资源生成与跨帧状态清理。

### Modified Capabilities

- None.

## Impact

- Affected code:
  - `scripts/systems/LastDayDirector.cs`
  - `scripts/models/NarrativeTurn.cs` or its replacement models
  - `scripts/models/WorldState.cs`
  - `scripts/models/LastDayTurnResult.cs`
  - `scripts/ui/LastDayScreen.cs`
  - `scenes/last_day.tscn`
  - `scripts/systems/LocationManager.cs`
  - `scripts/systems/LocationCardSvgService.cs` or a new generalized visual service
- New prompts and protocol contracts will be required for:
  - intent parsing
  - encounter frame rendering
  - scene SVG generation
  - character SVG generation
- Runtime behavior affected:
  - Last Day destination entry flow
  - encounter generation and rendering
  - main visual layout logic
  - fallback behavior for model and asset failures
- Testing impact:
  - new protocol parsing coverage
  - dynamic layout verification
  - no-API and malformed-output fallback validation
