# Dialogic 变量表、时间线命名与桥接边界

本文档对应任务 **5.1**，并与 **5.4**（与 `SceneSwitcher` 衔接）共用，作为序章 / 宣判阶段 Dialogic 与 C# 的单一事实来源。

## 时间线命名与资源路径

| 逻辑名 | 常量（`DialogicRuntime`） | 资源路径 | 职责 |
|--------|---------------------------|----------|------|
| 序章开机通知 | `IntroBootTimeline` | `res://resources/dialogic/timelines/intro_boot.dtl` | 黑屏系统通知 + 情绪铺垫，无玩家输入 |
| 档案处三问 | `IntroArchiveTimeline` | `res://resources/dialogic/timelines/intro_archive.dtl` | 采集 `archive.work` / `archive.relation` / `archive.escape`，结束后由 C# 异步归档 |
| 死因裁定放行 | `VerdictReleaseTimeline` | `res://resources/dialogic/timelines/verdict_release.dtl` | 展示裁定感对白，结束后进入 `LastDay` |

**命名约定**：文件名 `snake_case.dtl`；常量前缀与阶段一致（`Intro*` / `Verdict*`）。新增时间线时：在 `DialogicRuntime` 增加 `const`、在本文档增一行、在对应屏幕的 `_Ready` / 流程注释中写明入口。

## Dialogic 变量表（VAR）

| 键 | 类型 | 写入方 | 读取方 | 说明 |
|----|------|--------|--------|------|
| `archive.work` | string | 时间线 `text_input` | `DeathRegistrationScreen.ProcessArchiveAsync` → `SoulProfile.WorkText` | 自我介绍 / 工作语境 |
| `archive.relation` | string | 时间线 `text_input` | 同上 → `RelationText` | 想到的人 / 关系 |
| `archive.escape` | string | 时间线 `text_input` | 同上 → `EscapeText` | 一直在躲的事 |
| `death_cause_text` | string | `VerdictScreen.BeginReleaseTimeline`（自 `GameSession`） | `verdict_release` 时间线文案插值 | 已裁定死因展示 |
| `reaper_opening` | string | 同上 | 同上 | 死神开场 / 批注语境 |

**注意**：嵌套路径必须使用 **点号**（`archive.work`），勿使用斜杠；Dialogic 的 `VAR` 子系统用 `.` 解析嵌套字典（见 `DialogicUtil._set_value_in_dictionary`）。上述键须在 **`项目设置 → Dialogic → Variables`**（`project.godot` 中 `[dialogic] variables=...`）中预置默认空字符串，否则 `set_variable` 会报 *non-existant variable*。`archive.*` 仅在 `intro_archive` 内由玩家填写；宣判阶段变量由 C# 在 `StartTimeline` 前通过 `DialogicRuntime.SetVariable` 注入，勿在 `.dtl` 内写死。

## 桥接调用边界

### 由 `DialogicRuntime`（C# 静态桥）承担

- `StartTimeline` / `EndTimeline`：与 `/root/Dialogic` 对话。
- `SetVariable` / `GetString`：经 `Dialogic` 子节点 `VAR`，与 GDScript API 一致。
- `ConnectTimelineEnded` / `DisconnectTimelineEnded`：`timeline_ended` 信号；同一 `Callable` 不会重复连接。

### 由场景脚本承担

- **DeathRegistrationScreen**：串联 `intro_boot` → `intro_archive`；在**开始异步归档**前断开 `timeline_ended` 并 `EndTimeline`，避免长时间 LLM / 切换期间残留布局或重复信号；归档完成后 `SceneSwitcher.SwitchToAsync(Verdict)`。
- **VerdictScreen**：注入变量 → `verdict_release` → 结束后 `SwitchToAsync(LastDay)`；`_ExitTree` 中断开并 `EndTimeline`。

### 不由 Dialogic 承担

- `GameSession`、`SoulProfile`、`SessionActivityLog` 的持久字段与 LLM 调用。
- `SceneSwitcher` 的淡入淡出与 `GameManager.SetPhase`（在切换成功后由 `SceneSwitcher` 触发）。

## 与 SceneSwitcher 的衔接（5.4）

1. **顺序**：先处理 `timeline_ended`（或链式启动下一段同场景时间线），再在 `await SceneSwitcher.SwitchToAsync(...)` 之前结束本地 Dialogic 会话（若已进入长异步则见上「归档前清理」）。
2. **防重入**：使用 `_timelineActive`（或等价标志）在回调入口短路，避免异步切换未完成时的二次触发。
3. **场景销毁**：`ChangeSceneToFile` 会卸载当前场景；`_ExitTree` 中必须 `DisconnectTimelineEnded` + `EndTimeline`，防止泄漏或对已释放节点发信号。

## 修订记录

- 初版：`refine-visual-novel-journey` 实施 5.1 / 5.4 时建立。
