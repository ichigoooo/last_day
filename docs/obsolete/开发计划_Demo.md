# 人生最后一天 Demo 开发计划

- 文档版本：V1.9
- 日期：2026-04-09
- 任务进度更新：2026-04-09（**阶段 0–4 已完成**：阶段 4 含 `CrisisKeywordGuard` + `crisis_help_overlay`（400-161-9995）、`last_day_dying_ui` 全屏 Shader、`AudioManager` + `assets/audio/sfx/*.wav` 占位、`ContentSafetyFilter`/`SoulPromptVars`、死因与 LastDay/Message prompt 画像变量；**抛光项**：讣告卡栅格截图、主视觉 Tween；**§5.8 真机走查**仍为发布前人工项）
- 定位：Hackathon Demo，纯前端，快速可体验
- 引擎：Godot 4.6（C#）
- 语言：C# (.NET)

---

## 0. 架构决策

| 决策 | 方案 |
|------|------|
| 编程语言 | **C#**（.NET，利用 async/await、LINQ、强类型加速开发） |
| 大模型调用 | `HttpRequest` 直接调 OpenAI/DeepSeek 兼容 API，用户自行填 Key |
| 数据存储 | JSON 文件持久化到 `user://` 目录（设置、预留存档根；**单次体验**，无多局轮回簿） |
| 对话系统 | **不用 Dialogic**（场景以 AI 动态生成为主，非预编时间线），自建轻量对话 UI |
| 画面风格 | 竖屏 1080×1920，深色基调，文字驱动，轻量 UI |
| 场所卡片图 | **SVG**（由 LLM 按模板生成或兜底），`merovi.svgtexture2d` 栅格化为 `Texture2D` 显示；见 §5.7 |
| 最后一天 AI 方案 | **系统控状态 + AI 做意图解析/叙事渲染**，不让 LLM 直接主控流程；Prompt 组织采用 `*.system.txt + *.user.txt`；详见 `docs/最后一天_AI自由行动技术方案.md` |
| 场景管理 | 每个「阶段」独立 `.tscn` 场景，全局单例（Autoload）管理状态与切换 |

---

## 1. 分期计划与任务清单

> **进度约定：** `- [x]` 已完成；`- [ ]` 未开始或进行中。各阶段下的条目为可勾选的 todo list。

### 总览

- [x] **阶段 0**：基础框架搭建（已完成，已跑通）
- [x] **阶段 1**：入殓登记 + 宣判（登记 → 死因/批注/开场 → 宣判遗愿 → 可进最后一天）
- [x] **阶段 2**：最后一天核心玩法（§5.9 布局 + 四机制 + SVG/手机/花钱/地图 + 时间终局）
- [x] **阶段 3**：死亡 + 葬礼 + 冥想 + 终局（单次体验收束，返回主菜单）
- [x] **阶段 4**：打磨与完善

---

### 阶段 0：基础框架搭建

**目标：** 能跑、能切场景、核心 Autoload 就位

- [x] **清理项目** — 删除无用的 Main.cs 空壳内容，重写为 C# 入口；保留 .sln/.csproj
- [x] **GameManager** — Autoload 单例，管理当前阶段（Phase 枚举）、灵魂画像、遗愿、全局状态
- [x] **SceneSwitcher** — 封装场景切换逻辑，统一管理 7 个阶段的跳转与过渡动画
- [x] **ApiBridge** — Autoload 单例，封装 LLM API 调用（`HttpRequest` + async/await），含超时；配置读自 `settings.json`
- [x] **SaveManager** — Autoload 单例，JSON 读写 `user://save_data.json`（预留扩展）与 `user://settings.json`（API 等）
- [x] **Settings 页** — 首次启动引导至设置页，输入 API Key + Base URL + 模型名并持久化
- [x] **全局 Theme** — 深色配色（深灰/黑底 + 冷灰蓝 + 克制金 + 低饱和红），代码生成 Theme，基础控件样式
- [x] **项目结构搭建** — 完整目录结构（见第 3 节），占位脚本与资源就位；各阶段 `.tscn` 占位可跳转

**产出：** 启动 → 设置页 → 主场景可跳转，API 调用可通。

---

### 阶段 1：入殓登记 + 宣判

**目标：** 完成前两个阶段，跑通「用户输入 → LLM 个性化 → 展示输出」全链路

- [x] **DeathRegistration 场景** — 旧表格风格 UI，三个自由文本输入框 + 提交按钮
- [x] **灵魂画像提取** — 从三问内容中提取标签（工作/关系/逃避），用关键词匹配 + LLM 辅助分类，存入 GameManager
- [x] **死神批注** — 调 LLM，prompt = 三问原文 + 批注要求（冷静、隐喻、1-2 句），打字机效果展示
- [x] **Verdict 场景** — 旧纸通知视觉风格，展示死因文本 + 死神开场白
- [x] **死因生成（LLM）** — 根据灵魂画像与三问由模型生成 1～3 句隐喻式死因（JSON 输出）；无 API / 失败时用固定兜底文案（非本地词库抽卡）
- [x] **遗愿确认** — 优先自由输入 → 用户卡住时 LLM 基于灵魂画像生成 3 条建议
- [x] **死神人设 Prompt** — LLM system prompt 定义死神人格：冷静、黑色幽默、不羞辱、清醒旁白者

**产出：** 从登记 → 死因宣判 → 遗愿确认，完整可体验。（已实现）

---

### 阶段 2：最后一天核心玩法（Demo 重中之重）

**目标：** 四大机制全部可用，形成真实紧迫感；**主界面布局**遵循 **§5.9**（顶栏 → 双主视觉 → 对白/选项区 → 用户底栏）。

- [x] **LastDay 主场景** — `last_day.tscn`：**状态栏**（`StatusBar`+`ClockArcControl`）→ **双主视觉**（左场所 SVG `SceneCard` / 右心境占位 `PortraitCard`）→ **滚动旁白** + **选项行**（含「自定义输入…」聚焦底栏）→ **底栏**（头像色块 + `LineEdit`）；**前往场所**为 `LocationMapOverlay` 全屏叠层（扣 30 游戏分）。（**抛光**：单/双主视觉 Tween 过渡可后续加）
- [x] **TimeManager** — 约 **1 真实秒 ≈ 1 游戏分钟**（≈1 真实分 ≈ 1 游戏小时）；`GetDayProgress01`、`GetDaylightPhaseIndex`、`GetAmbientTint`；`DaytimeDepleted` 信号；背景色随 `_bgTint` 变化
- [x] **LocationManager** — 8 场所 + 别名 + `TagsCsv`/`Mood` 供 SVG prompt
- [x] **场所卡片图（SVG）** — `LocationCardSvgService`：`location_card_svg` LLM → `Image.LoadSvgFromString` → `Texture2D`；`WorldState.LocationSvgCache` 内存缓存；失败用极简矢量兜底（§5.7）
- [x] **传送机制** — 自然语言（`LastDayDirector`）与 **地图 UI** 两路；地图固定 **30 游戏分钟** / 意图移动仍为导演内规则
- [x] **场所行动系统** — `LastDayDirector` 不变；选项第 4 项触发自定义输入
- [x] **MoneySystem** — 与 `StatusBar`、回合扣款、`DeathSpendService` 花钱一致
- [x] **BatterySystem** — 亮屏每 **3 游戏分钟** −1%；回合 `screen_usage` 另扣；**手机内充电宝**扣约 60 游戏分并加电量
- [x] **MessageSystem** — 聊天历史 → `message_reply`；`ClearRuntimeBuffers` 新局清屏
- [x] **PhoneUI** — CanvasLayer：聊天、发消息、充电宝、朋友圈发布与列表
- [x] **DeathSpendUI** — CanvasLayer：`resources/spend_options.json` + `death_spend_consequence`
- [x] **StatusBar** — `ClockArcControl` 弧 + 时间/阶段文 + 现金 + `ProgressBar` 电量
- [x] **时间耗尽处理** — `DaytimeDepleted` 或电量 ≤0 → 提示语 → `SceneSwitcher` → `Death`
- [x] **场景与 UI 检验（阶段内必做）** — 结构与叠层已按 §5.9 落地；**发布前**仍建议在目标分辨率真机走查 §5.8（触控热区、长文滚动、安全区）

**产出：** 最后一天在 Demo 约定内可完整游玩，四机制与 §5.9 主布局已对齐；抛光与真机检验见上。

---

### 阶段 3：死亡 + 葬礼 + 冥想 + 终局

**目标：** 完成单次体验闭环（无多局累积、无轮回簿）

- [x] **Death 场景** — `DeathScreen`：画面渐冷 + 叠黑、跳过、静候后入葬礼；`AudioManager` 死亡环境音钩子
- [x] **Funeral 场景** — `FuneralScreen`：悼词（`eulogy` JSON）+ 墓志铭候选（`epitaph_suggestions`）三选一/自填 + 遗言（可「生成参考」`last_words`）
- [x] **讣告卡生成** — `ShareCard`：死因、轨迹、消费与电量/消息摘要、墓志铭；**复制全文**（截图留作抛光）
- [x] **总结页** — 葬礼内只读区：现金结余、电量、消息条数、到访轨迹（`ClosurePromptVars` + `LocationManager`）
- [x] **Meditation 场景** — `MeditationScreen`：`meditation_reflection` 四段 JSON；跳过 / 进入终局
- [x] **Ending 场景** — `EndingScreen`：晨光浅色底、可选承诺输入（不落盘多局）、`ResetSession` 后回主菜单
- [x] **场景与 UI 检验（阶段内必做）** — 四场景已换绑脚本；发布前仍建议按 §5.8 真机走查窄屏与触控

**产出：** 完整流程一次走完，终局返回主菜单；再次「开始体验」视为新一局运行时数据（`GameManager.ResetSession`）；**各收束阶段场景在移动端观感与操作一致、无结构性布局问题**（见 §5.8）。

---

### 阶段 4：打磨与完善

- [x] **情绪安全检测** — 自杀/自伤关键词匹配 → 中止流程 → 展示求助页（热线 400-161-9995）
- [x] **「界面正在死去」Shader** — 饱和度/色温随游戏时间推进逐渐变化，死亡瞬间强对比
- [x] **音效** — 纸张声、盖章、心跳、环境音（少量关键点）
- [x] **死因多样性** — prompt 与输出后处理（敏感词、句式去套话）；本阶段补齐安全约束
- [x] **敏感内容过滤** — LLM 输出 prompt 层约束 + 关键词兜底
- [x] **重复可玩性** — 不同灵魂画像 → 不同死因/场所推荐/消息人物

---

## 2. 最小可体验版本（MVP）

时间极度紧张时，砍到以下范围：

- **阶段 0**：全做（已完成）
- **阶段 1**：死因与批注均依赖 LLM；死因不可用时代码兜底，死神批注可用固定模板兜底（LLM 超时或未配置 Key）
- **阶段 2**：4 个场所 + 时间系统 + 金钱系统（手机系统后补）；场所卡片图优先 SVG+LLM，可砍为仅兜底静态 SVG
- **阶段 3**：只做葬礼 + 终局（跳过冥想、简化总结页）
- **阶段 4**：跳过

MVP 目标：用户能跑完一轮完整流程（约 15 分钟），体验到核心紧迫感。

---

## 3. 项目文件结构

```
res://
├── scenes/                        # 场景文件
│   ├── main.tscn                  # 入口 / 启动页
│   ├── settings.tscn              # API Key 设置
│   ├── death_registration.tscn    # 入殓登记
│   ├── verdict.tscn               # 宣判
│   ├── last_day.tscn              # 最后一天（主玩法）
│   ├── death.tscn                 # 死亡过渡
│   ├── funeral.tscn               # 葬礼
│   ├── meditation.tscn            # 冥想
│   └── ending.tscn                # 终局（单次体验收束）
│
├── scripts/
│   ├── autoload/                  # 全局单例（Autoload）
│   │   ├── SaveManager.cs         # JSON 存档与设置
│   │   ├── GameManager.cs         # 游戏状态管理
│   │   ├── SceneSwitcher.cs       # 场景切换与过渡
│   │   ├── TimeManager.cs         # 游戏时间系统
│   │   ├── AudioManager.cs        # 音效管理
│   │   └── ApiBridge.cs           # LLM API 调用桥
│   │
│   ├── ui/                        # UI 控件脚本
│   │   ├── TypewriterLabel.cs     # 打字机效果文字
│   │   ├── LastDayScreen.cs       # 最后一天主界面（当前为基础竖排 HUD + 旁白/选项/输入）
│   │   ├── StatusBar.cs           # 顶部状态栏
│   │   ├── PhoneUI.cs             # 手机界面
│   │   ├── DeathSpendUI.cs        # 死神花钱 UI
│   │   ├── DialogueBox.cs         # 对话/旁白展示
│   │   └── ShareCard.cs           # 讣告卡/分享卡片
│   │
│   ├── systems/                   # 游戏系统逻辑
│   │   ├── LastDayDirector.cs     # 最后一天：地点/意图/结算/叙事/摘要编排
│   │   ├── PromptLoader.cs        # Prompt 模板加载
│   │   ├── LocationManager.cs     # 场所管理
│   │   ├── MoneySystem.cs         # 金钱系统
│   │   ├── BatterySystem.cs       # 电量系统
│   │   ├── MessageSystem.cs       # 消息/聊天系统
│   │   ├── SoulTagExtractor.cs    # 灵魂标签（阶段 1）
│   │   └── DeathCauseGenerator.cs # 死因 LLM 生成（含兜底）
│   │
│   └── models/                    # 数据模型
│       ├── SoulProfile.cs         # 灵魂画像
│       ├── GameSession.cs         # 单局游戏数据（含 WorldState）
│       ├── WorldState.cs          # 最后一天世界状态
│       ├── ResolvedAction.cs      # 单回合系统结算结果
│       ├── NarrativeTurn.cs       # 旁白 + 三选项
│       ├── LastDayTurnResult.cs   # 单回合对外结果
│       ├── LocationData.cs        # 场所数据
│       ├── DeathCause.cs          # 死因数据
│       └── MessageRecord.cs       # 消息记录
│
├── resources/
│   ├── prompts/                   # LLM Prompt 模板（JSON/文本）
│   │   ├── grim_reaper.system.txt          # 死神人设 system prompt
│   │   ├── annotation.system/.user.txt     # 批注 prompt
│   │   ├── death_cause_generation.system/.user.txt # 死因生成（JSON）
│   │   ├── last_day_narrative_render.system/.user.txt # 场所旁白与选项
│   │   ├── location_card_svg.system/.user.txt  # 场所卡片 SVG 生成（LLM，需符合 §5.7）
│   │   ├── eulogy.system/.user.txt         # 悼词 prompt
│   │   └── last_words.system/.user.txt     # 遗言 prompt
│   ├── locations/                 # 场所数据 JSON
│   │   ├── home.json
│   │   ├── office.json
│   │   ├── park.json
│   │   └── ...
│   ├── svg_tests/                 # SVG 管线试验 / 兜底样例（可选）
│   └── spend_options.json         # 花钱选项
│
├── assets/
│   ├── images/                    # 图片素材
│   ├── audio/                     # 音效/音乐
│   ├── fonts/                     # 字体
│   └── themes/                    # Godot Theme 资源
│
├── addons/
│   ├── dialogic/                  #（保留但 Demo 不使用）
│   ├── merovi.svgtexture2d/       # SVG 动态栅格化（SVGSprite2D / SVGTexture2D）
│   └── godot_mcp/                 # MCP 开发工具
│
└── project.godot
```

---

## 4. Autoload 注册清单

| 名称 | 脚本路径 | 职责 |
|------|----------|------|
| SaveManager | `res://scripts/autoload/SaveManager.cs` | `save_data.json` / `settings.json` |
| GameManager | `res://scripts/autoload/GameManager.cs` | 全局游戏状态、阶段管理、灵魂画像 |
| SceneSwitcher | `res://scripts/autoload/SceneSwitcher.cs` | 七阶段场景切换、淡入淡出过渡 |
| TimeManager | `res://scripts/autoload/TimeManager.cs` | 游戏时间流逝、`GameMinutesAdvanced` 信号 |
| MoneySystem | `res://scripts/systems/MoneySystem.cs` | 现金余额与扣款（最后一天） |
| BatterySystem | `res://scripts/systems/BatterySystem.cs` | 手机电量（最后一天） |
| LocationManager | `res://scripts/systems/LocationManager.cs` | 8 个 canonical 场所与别名 |
| MessageSystem | `res://scripts/systems/MessageSystem.cs` | 异步消息队列与 `message_reply` |
| AudioManager | `res://scripts/autoload/AudioManager.cs` | 音效播放、BGM 控制 |
| ApiBridge | `res://scripts/autoload/ApiBridge.cs` | LLM API 调用、响应解析（配置来自 SaveManager） |

---

## 5. 关键技术方案

### 5.1 LLM 调用

```
ApiBridge.CallLLM(string promptKey, Dictionary<string, string> variables)
```

- 从 `res://resources/prompts/` 加载 `*.system.txt` 与 `*.user.txt`，再替换变量
- `HttpRequest` 发 POST 到用户配置的 API Endpoint
- 支持流式（SSE）和非流式两种模式
- 超时 30s，超时时用预制模板兜底
- 所有 AI 生成内容在 UI 上以打字机效果展示

### 5.2 时间系统

- `TimeManager` 在 `_Process` 中累加 delta，按倍率转换为游戏时间
- 提供 Signal：`GameMinutePassed`、`GameHourPassed`、`DayEnded`
- 其他系统（电量、消息延迟、场所耗时）订阅时间信号
- 日光阶段：清晨(6-9) → 正午(9-14) → 下午(14-17) → 黄昏(17-20) → 深夜(20-24) → 午夜(0-6)

### 5.3 场所行动循环

```
用户选择场所 → 传送(-30分钟) → 输入"想做什么"
    → ApiBridge 生成旁白+选项+耗时
    → 展示 → 用户选择/自定义
    → 扣时间 → 判断剩余时间 → 继续或结束
```

### 5.4 手机系统

- PhoneUI 作为 LastDay 场景内的浮动控件
- 聊天：用户输入 → MessageSystem 创建待回复记录 → Timer 到时后调 LLM 生成回复
- 朋友圈：用户输入内容 → LLM 生成评论（可选）
- 电量：BatterySystem 订阅 GameMinutePassed 信号，亮屏时每 3 分钟扣 1%

### 5.5 数据持久化

- `user://settings.json`：API Key、API Base URL、模型名、`first_setup_completed` 等
- `user://save_data.json`：版本字段（预留扩展）；当前局运行时数据以 GameManager / 后续存档点为准，**不**做多局累积存档

### 5.6 死因生成（阶段 1，无本地词库）

- 使用 `resources/prompts/death_cause_generation.system.txt` + `resources/prompts/death_cause_generation.user.txt`：变量含三问、灵魂标签。
- `ApiBridge.ChatJsonAsync` 解析 `{"text":"..."}`；`DeathCauseGenerator` 为正文生成稳定 `id`（正文哈希）写入 `GameSession`。
- 未配置 API、超时或解析失败：`Phase1Copy.FallbackDeathCauseBody` 按标签返回兜底死因正文。

### 5.7 场所卡片 SVG（阶段 2）：集成方式与画风约束

**工程集成**

- 插件 **`addons/merovi.svgtexture2d`**：`SVGTexture2D.svg_data_frames` 存 SVG 字符串 → `SVGSprite2D` / 或先栅格化再赋 `TextureRect.texture`（与 UI 布局二选一）。
- **生成链路**：`LocationManager`（或专用组件）根据 `location_id` + 可选用户上下文调用 `ApiBridge`，Prompt 模板建议为 `resources/prompts/location_card_svg.system.txt` + `resources/prompts/location_card_svg.user.txt`；返回**整段 SVG 文本**，校验通过后写入 `SVGTexture2D` 并缓存（如 `user://cache/svg_loc_<id>_<hash>.svg` 或仅内存 `Texture2D`）。
- **兜底**：每个场所在 `resources/` 下预置 1 份极简 **ASCII、无 BOM** 的 `.svg`（与测试管线一致），避免解析失败；失败时勿阻塞传送。
- **编码**：文件与模型输出须为 **合法 UTF-8**；Prompt 中要求**注释与文本节点优先使用英文或通用符号**，避免乱码导致 `load_svg_from_string` 失败（阶段 1 测试已踩坑）。

**适合「场景配图」的 SVG 画风（供 Prompt 与验收）**

| 维度 | 建议 |
|------|------|
| 整体 | **符号化 / 氛围插画**，非写实；一眼能联想到场所（如路灯+长椅=公园），避免复杂场景叙事。 |
| 构图 | 固定 **`viewBox`**（如 `0 0 256 256` 或 `320 240`），主体居中，留边，适配竖屏卡片上的小图区。 |
| 配色 | 与全局 Theme 一致：**深底**（`#0c0d11`～`#12141a`）、**冷灰蓝**轮廓、**克制金**点缀、**低饱和红**少量；忌高饱和荧光、大块纯白。 |
| 技法 | 以 **路径 + 线性渐变（≤2～3 个）+ 描边** 为主；少用滤镜/模糊；**禁止** `<script>`、`foreignObject`、内嵌位图。 |
| 复杂度 | 控制路径数量与节点数，保证手机端栅格化可接受；必要时在 Prompt 中写死「路径不超过 N 条」。 |
| 输出格式 | 单一 `<svg xmlns=...>` 根元素；模型输出需可被 `ChatJsonAsync` 包在 `{"svg":"..."}` 或由后处理剥离 markdown 代码块。 |

**验收**：各场所卡片在 1x～2x 缩放下清晰、无解析错误；无 API 时使用兜底 SVG 仍可完整游玩。

### 5.8 场景与 UI 检验（阶段 2、3 共用）

本项为**阶段 2、阶段 3**交付前的固定自检，在 Godot 编辑器与真机/模拟器（竖屏）上完成；与 `project.godot` 中 **1080×1920** 基准视口、`window/size/window_*_override` 缩放下行为一致。

| 检验项 | 要求 |
|--------|------|
| **场景结构** | 根节点通常为全屏 `Control`；功能区用 `MarginContainer` / `VBox`+`HBox` 分层，避免过深无意义嵌套；弹层（手机、花钱、讣告卡）使用独立 `CanvasLayer` 或明确 `z_index`，避免与主界面抢输入。 |
| **布局与尺寸** | 主要信息区宽度随屏宽自适应（左右 `Margin` 一致，建议 ≥ 24～32 px 换算到设计分辨率）；列表与卡片在窄屏下不换行崩版；长文本用 `ScrollContainer` + `Label`/`RichTextLabel`，禁止裁切无滚动。 |
| **移动端规范** | 主操作按钮高度建议 **≥ 44～48 px**（设计分辨率下），相邻可点控件间距避免误触；正文字号与行距保证 **约 14～18 px 字号档**下仍可读（按 Theme 统一，忌过小灰字）。 |
| **锚点与对齐** | 顶栏/底栏用 **顶部/底部锚点** 贴边；居中块用 `anchors_preset` 或容器对齐，避免手写偏移导致不同长宽比下漂移；多分辨率下用 **容器子项最小尺寸** 而非固定像素撑满全屏。 |
| **安全区（可选加强）** | 刘海/底部横条机型：底栏按钮与重要文案不要紧贴物理边缘，可预留额外 `Margin` 或通过 `DisplayServer` 安全区（若后续接入）微调。 |

**自检方式建议**：编辑器 **「远程调试 / 设备预览」** 或导出 APK 在目标比例下走查；阶段 2 至少覆盖「主界面 + 打开手机 + 花钱弹层」；阶段 3 至少覆盖「死亡过渡 + 葬礼表单/悼词 + 冥想长文 + 终局按钮」。

### 5.9 LastDay 主界面布局（竖屏线框）

以下为 **「最后一天」主流程界面**的固定分区（自上而下），与产品线框一致；实现时以全屏竖屏 **1080×1920** 为基准，**§5.8** 为通用移动端验收。

```
┌─────────────────────────────┐
│  ① 状态栏                    │  ← 时间 · 余额 · 手机电量
├─────────────────────────────┤
│  ┌─────────┐  ┌─────────┐    │
│  │场景 SVG │  │对象 SVG │    │  ← ② 主视觉图像区（可选其一或并排）
│  │(左框)   │  │(右框)   │    │
│  └─────────┘  └─────────┘    │
├─────────────────────────────┤
│  ③ 对白区 / 选项区            │  ← 对象说话 ⇄ 需要选择时切换为选项
│     （长内容可滚动）           │
├─────────────────────────────┤
│ ④ [头像] 用户对白 / 输入      │  ← 玩家自己的一句区域
└─────────────────────────────┘
```

| 分区 | 内容与交互 |
|------|------------|
| **① 状态栏** | **游戏内时间**、**现金余额**、**手机电量**（与 `TimeManager` / `MoneySystem` / `BatterySystem` 绑定）；窄条置顶，信息一眼可读，具体控件形态见 `StatusBar`。 |
| **② 主视觉图像区** | **左**：当前**场景**配图（SVG 栅格化，与 §5.7 管线一致，分辨率按本区画框适配）。**右**：当前**对象/角色**配图（SVG），如死神、对话中出现的 NPC 等。二者均为 **可选**：仅场景或仅角色时，**单画框在区域内水平居中**；从「只显示一侧」变为「两侧都显示」时，用 **Tween / 动画**将画框从居中平滑过渡到**左右并排**（避免瞬切）。 |
| **③ 对白区 / 选项区** | **同一逻辑区域、两种展示形态**：有对象独白/旁白时，显示**文本框**（可用打字机）；需要玩家抉择时，切换为**选项列表**。选项列表的**最后一项**约定为 **「自定义 / 其他…」类入口**：**默认交互为点击后激活文本输入**（聚焦到底栏输入框或本区内联输入，由实现择一，需与 §5.8 触控热区一致）。 |
| **④ 用户对话框** | 底部固定条：**左侧小头像**（玩家/灵魂占位图）+ **右侧**显示用户已发送文案或当前输入占位；与 natural language 行动、选项末项触发的输入共用同一输入管线。 |

**与机制的关系**：手机浮层（`PhoneUI`）、死神花钱（`DeathSpendUI`）、场所地图/卡片等可作为 **CanvasLayer 全屏/半屏**叠在本四段之上，关闭后仍回到 §5.9 主布局。

---

## 6. 风险与兜底

| 风险 | 兜底方案 |
|------|----------|
| LLM API 不可用/超时 | 所有需要 AI 的地方都有预制模板兜底，降级为纯模板体验 |
| 用户无 API Key | 不进入主流程：主菜单与设置要求填写 Key；登记/宣判场景也会校验并引导至设置 |
| 时间不够 | 按 MVP 范围砍，优先阶段 0+2 |
| 情绪安全 | 关键词检测不依赖 LLM，本地正则匹配 |
| LLM 产出损坏的 SVG | 校验失败则用预置兜底图；Prompt 约束见 §5.7 |

---

**文档结束**
