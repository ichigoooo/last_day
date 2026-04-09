## 1. 协议与提示词

- [x] 1.1 新增 `IntentParseResult`、`EncounterFrame` 及相关子模型，并为字段默认值与枚举合法性建立统一校验入口
- [x] 1.2 重写 `last_day_intent_parse` 提示词契约，使其输出开放目的地输入所需的 `destination_text`、`is_travel_intent`、`summary` 等字段
- [x] 1.3 新增 `last_day_encounter_frame_render` 提示词，要求模型按 `EncounterFrame` 协议返回地点、遭遇、人物、视觉 brief、旁白和选项
- [x] 1.4 为 `EncounterFrame` 建立默认帧与字段级回退策略，覆盖 JSON 非法、字段缺失和非法枚举场景

## 2. 导演流程与状态模型

- [x] 2.1 重构 `LastDayDirector`，将回合流程拆分为“意图解析 -> 系统结算 -> 遭遇帧渲染 -> 协议校验”
- [x] 2.2 更新 `LastDayTurnResult`，使其返回 `EncounterFrame` 及必要的系统结算摘要，而不再只暴露旧 `NarrativeTurn`
- [x] 2.3 扩展 `WorldState`，增加当前遭遇帧、当前展示地点名、当前人物信息和基于 brief 的视觉缓存键支持
- [x] 2.4 保留旧 `NarrativeTurn` 兼容路径或迁移适配层，确保新导演流程可分步接入现有 UI

## 3. 开放地点输入与入口改造

- [x] 3.1 调整 `LocationManager` 角色为“推荐地点与本地兜底参考”，移除其作为内容边界的使用方式
- [x] 3.2 更新地图或快捷前往入口，使推荐地点与自由文本输入共享同一 `LastDayDirector` 流程
- [x] 3.3 确保无法映射到 canonical location 的地点仍可进入回合渲染，并把原始目的地文本保留到 `EncounterFrame` 阶段
- [x] 3.4 为“推荐地点入口缺失或不可用”的情况保留纯文本推进路径

## 4. 动态主视觉与视觉资源链

- [x] 4.1 重构 `last_day.tscn` 与 `LastDayScreen` 主视觉区，支持双显示、仅场景、仅人物、双隐藏四种布局状态
- [x] 4.2 在应用新 `EncounterFrame` 前实现旧帧清理逻辑，清空旧人物态、旧副标题和过期异步请求
- [x] 4.3 将 `LocationCardSvgService` 扩展或替换为通用 `VisualSvgService`，分别支持场景 brief 与人物 brief 的纹理生成
- [x] 4.4 将视觉缓存从 `locationId` 粒度迁移到基于 brief 哈希的场景键与人物键

## 5. 兜底、兼容与安全

- [x] 5.1 为无 API、意图解析失败、遭遇帧非法等情况接入默认回合与默认旁白，保证流程不中断
- [x] 5.2 为场景 SVG 与人物 SVG 生成失败分别提供占位图或降级显示策略
- [x] 5.3 将 `ContentSafetyFilter` 和现有危机词检测接入 `EncounterFrame` 的地点名、人物名、旁白和选项显示路径
- [x] 5.4 确认时间、现金、电量、消息延迟仍完全由系统结算，`EncounterFrame` 不可越权写入资源状态

## 6. 测试与收尾

- [x] 6.1 新增最小测试场景或手动验证入口，用于覆盖四态主视觉、跨帧清理和自由地点输入
- [x] 6.2 验证“同地点不同遭遇 brief 不复用旧图”“上一帧人物不会残留到下一帧”两类核心视觉风险
- [x] 6.3 验证推荐地点、自定义地点、无 API、非法 JSON、SVG 失败五类回退路径都能继续完成回合
- [x] 6.4 更新相关开发文档或注释，说明新协议、主要 prompt 与关键状态字段的职责边界
