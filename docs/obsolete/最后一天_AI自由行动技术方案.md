# 最后一天 AI 自由行动技术方案（定稿）

## 1. 要解决的真实问题

“一天内任意选择”的难点，不是让大模型多写一点文案，而是同时满足四件事：

1. 用户真的可以说“想去哪里、想做什么”，而不是只能点死菜单。
2. 时间、金钱、电量、消息延迟这些硬规则不能被 AI 写崩。
3. 叙事要前后连贯，不能每回合像重开一局。
4. Demo 必须能在 Godot + C# 当前架构里做出来，不能依赖一个不可控的全自动 GM。

因此，本项目不采用“让 LLM 直接主控流程”的方案，而采用：

**系统掌控状态与规则，AI 负责理解用户意图、生成局部叙事与选项。**

这意味着“开放输入”是真的，但“世界演化”仍由引擎裁决。

---

## 2. 核心架构结论

### 2.1 一句话方案

把“最后一天”做成一个**有限世界模型 + 开放语言接口**的系统：

- **有限**：地点、资源、规则、事件类型、叙事骨架有限。
- **开放**：用户输入地点和行动可以是自然语言，AI 把它翻译到有限系统里。

### 2.2 必须坚持的边界

- **AI 不直接改 `WorldState`**。
- **AI 不直接决定是否扣钱、扣电、扣时**。
- **AI 不直接决定是否传送成功、消息何时到达、是否死亡**。
- **AI 只能输出结构化建议和文案；最终状态变化由系统结算。**

这是整个方案能稳定落地的前提。

---

## 3. 运行时分层

建议把阶段 2 拆成四层：

### 3.1 Rule Layer（硬规则层）

由 C# 系统维护，绝不交给模型：

- 当前游戏时间、昼夜阶段
- 现金余额
- 手机电量与亮屏状态
- 当前地点
- 已访问地点
- 待接收消息队列
- 是否死亡 / 是否已进入收束阶段

对应现有系统：

- `TimeManager`
- `MoneySystem`
- `BatterySystem`
- `MessageSystem`
- `GameManager.Session`

### 3.2 World Layer（世界模型层）

这层也由系统维护，但内容是数据驱动：

- **地点目录**：`home / office / park / hospital / bar / school / seaside / cemetery`
- **地点标签**：私密、公共、消费、纪念、关系、逃避、告别等
- **地点 affordance**：在这里通常能做什么，如 `talk / wait / buy / recall / rest / observe`
- **联系人目录**：那个人、家人、同事、朋友、自己
- **行动原型**：道歉、告白、告别、吃饭、散步、购买、等待、发消息、发圈、充电

注意：用户看见的是“任意输入”，系统内部跑的是“有限原型”。

### 3.3 Director Layer（编排层）

这层是本方案的关键。它不写文案，只负责组织一次行动回合：

1. 解析用户意图
2. 把意图映射到合法地点 / 合法行动
3. 计算资源变化
4. 决定本回合进入哪个叙事骨架
5. 再交给 AI 生成旁白和选项文案

建议新增一个专门的协调器，例如：

- `scripts/systems/LastDayDirector.cs`

它应成为 `last_day.tscn` 的核心流程入口。

### 3.4 Narrative Layer（叙事渲染层）

这一层才调用 LLM，负责：

- 把系统已确定的行动写成旁白
- 把选项原型改写成当前语境下的三条选项
- 生成自定义行动的后续文字
- 生成消息回复、朋友圈文案、死神花钱后的反馈

结论是：

**AI 负责“怎么说”，系统负责“发生了什么”。**

---

## 4. “任意去哪里”如何实现

这里不能真的做无限地点，而要做“输入自由、落点有限”。

### 4.1 地点归一化策略

用户输入地点后，系统做三层解析：

1. **直接命中 canonical location**
   例如“公司”“办公室”都归到 `office`
2. **语义映射到地点类别**
   例如“楼下便利店”映到 `store_like`，Demo 中再落到最近可用场所模板
3. **不可达或越界请求**
   例如“去纽约”“回到小学时代”“复活某人”
   系统不给模型自由发挥，而是返回一个带叙事包装的拒绝或替代方案

### 4.2 Demo 期建议

Hackathon Demo 不要做真无限地点，建议：

- 8 个 canonical location
- 4~6 个 category fallback
- 1 个 “无法抵达/不属于今天”的统一拒绝骨架

这样用户会感觉“我可以自由说”，但你实际只维护少量稳定场景。

---

## 5. “任意做什么”如何实现

同样不能把自然语言直接当逻辑执行，而要做行动原型化。

### 5.1 行动原型集合

建议第一版只做 10~14 个原型：

- `talk`
- `apologize`
- `confess`
- `goodbye`
- `buy`
- `eat`
- `wait`
- `walk`
- `look`
- `remember`
- `rest`
- `post`
- `message`
- `charge`

### 5.2 用户输入到行动原型的映射

例如：

- “我想在海边坐着发呆” -> `seaside + wait/look`
- “我想去找前任说清楚” -> `target_person + talk/apologize`
- “我想狠狠干最后一票” -> 高风险/越界，进入拒绝或降级骨架

### 5.3 为什么这样不会显得假

因为玩家真正感知的是：

- 我输入的话被理解了
- 系统回应和我的输入有关
- 世界对我的选择有代价反馈

玩家并不需要一个无限状态空间；玩家需要的是**被理解感 + 后果感**。

---

## 6. 单回合流程：必须采用三段式

建议把每次“用户行动”统一成下面这条流水线：

### 6.1 Step A：Intent Parse

用户在场所输入一句自然语言，例如“我想在这里给她发最后一条消息”。

先调一次 **JSON-only** LLM，用来做意图解析，输出例如：

```json
{
  "intent_type": "message",
  "target_person": "relation",
  "location_fit": "current_ok",
  "action_tags": ["goodbye", "hesitant"],
  "tone": "restrained",
  "cost_band": "short",
  "risk": "low",
  "suggested_scene_frame": "unfinished_relation"
}
```

这一阶段只做“理解”，不做最终文案。

### 6.2 Step B：System Resolve

`LastDayDirector` 根据解析结果做硬结算：

- 这个地点能不能做这件事
- 是否需要传送
- 本回合实际耗时是多少分钟
- 是否需要花钱
- 是否亮屏扣电
- 是否生成一个待回复消息事件
- 是否推进“遗愿完成度”或“未竟之事张力”

这一步生成一个系统内部对象，例如 `ResolvedAction`。

### 6.3 Step C：Narrative Render

再调一次 LLM，根据 `ResolvedAction + 当前世界状态 + 最近几条上下文` 生成：

- 本段旁白
- 三个选项
- 自定义输入提示
- 若有需要，生成对象 SVG 提示词或场景摘要

输出示例：

```json
{
  "narration": "海风把话吹得很轻，像替你提前练习了一次告别。",
  "options": [
    { "id": "say_plainly", "label": "直接把想说的话发出去" },
    { "id": "ask_about_today", "label": "先像平常一样问候" },
    { "id": "put_phone_down", "label": "先不发，继续看海" }
  ],
  "custom_hint": "也可以自己输入你真正想做的事。"
}
```

### 6.4 为什么一定要拆两次

因为“理解意图”和“写文案”是两种任务。混成一次长提示，输出会越来越散，后期很难 debug。

Hackathon 版可以允许在简单场景下合并为一次调用，但**系统设计上必须按两段式建模**。

---

## 7. 叙事一致性：靠记忆分层，不靠把全日志塞给模型

如果每回合把完整历史都塞给模型，token 很快失控，而且文风会漂。

建议分三层记忆：

### 7.1 Hard Facts

完全结构化，永不丢失：

- 三问原文
- 灵魂画像标签
- 本局核心遗愿（宣判阶段确认）
- 已去过的地点
- 已发送/已收到的重要消息
- 关键消费
- 当前资源

### 7.2 Short Context

只给最近相关的 3~5 条事件：

- 最近一次地点切换
- 最近一次用户输入
- 最近一次 AI 旁白摘要
- 最近一次重要回复

### 7.3 Narrative Summary

每 3~4 回合压缩一次，形成一句短摘要，例如：

- “他今天一直在拖延联系那个人，但已经三次绕开真正要说的话。”

这句摘要由系统保存，后面给 LLM 当长期叙事线索。

结论：

**完整日志留给终局悼词；日常行动只喂局部上下文 + 压缩摘要。**

---

## 8. 三选项怎么做才不会像假开放

选项不能完全随机，建议固定成“立场槽位”，再由 AI 改写。

### 8.1 推荐的三槽结构

大多数场景都从这三种立场里选：

1. **直面**
2. **绕开**
3. **停顿 / 留白**

例如同一场景下可变成：

- 直接说真话
- 先说些不痛不痒的
- 什么也不说，只待一会儿

这样能保证：

- 每回合都有推进、退缩、悬置三种心理姿态
- 与产品气质一致
- 自定义输入仍然有存在价值

### 8.2 自定义输入的地位

自定义输入不是“备用输入框”，而是第四条真实路径。  
但它进入系统后，仍要先走 `Intent Parse -> System Resolve -> Narrative Render`。

---

## 9. 手机、消息、朋友圈必须做成异步事件，不要混进主回合里

这是自由度的另一个核心。

### 9.1 正确做法

消息系统单独维护一个 `PendingEventQueue`：

- 发送时立刻写入 `sent`
- 系统采样一个回复延迟，例如 47 游戏分钟
- 到点时再触发回复生成

回复文本再单独调 LLM，输入只需要：

- 对象身份
- 这段对话最近几句
- 用户今天的总体状态摘要

### 9.2 好处

- 主场景行动和手机系统解耦
- “等回复”会自然嵌入时间压力
- 未来可扩展未读提示、息屏错过、死亡前最后一条回复

---

## 10. 遗愿主线如何嵌进去

自由行动不能变成纯沙盒，否则叙事会散。

建议给每局维护一个 `WishThreadState`，包括：

- `wish_type`
- `wish_targets`
- `progress_level`
- `hesitation_level`
- `closure_possible`

它不强行规定玩家去哪，但会影响：

- AI 旁白的重心
- 哪些地点更容易触发强相关内容
- 选项语气
- 终局总结和悼词

这就是“自由，但不脱题”。

---

## 11. 明确不采用的方案

### 11.1 不采用“LLM 直接当 GM”

原因：

- 无法保证资源规则
- 前后状态容易矛盾
- 很难复现 bug
- fallback 几乎不可做

### 11.2 不采用“无限地点 + 无限行动的真开放世界”

原因：

- Hackathon 周期做不完
- 视觉资源、场景 affordance、消息关系都无法兜住
- 用户并不真的需要无限，只需要高质量响应

### 11.3 不采用“纯模板分支树”

原因：

- 会直接失去本项目最重要的自由输入体验
- 和前文设计目标冲突

---

## 12. 对当前代码结构的落地建议

建议新增或补全以下对象：

### 12.1 Models

- `scripts/models/WorldState.cs`
- `scripts/models/ResolvedAction.cs`
- `scripts/models/NarrativeTurn.cs`
- `scripts/models/PendingEvent.cs`
- `scripts/models/LocationArchetype.cs`

### 12.2 Systems

- `scripts/systems/LastDayDirector.cs`
  负责一次行动回合的总编排
- `scripts/systems/LocationManager.cs`
  补全地点目录、语义映射、affordance 查询
- `scripts/systems/MessageSystem.cs`
  改成真正的异步事件队列
- `scripts/systems/MoneySystem.cs`
  只做余额与扣款合法性
- `scripts/systems/BatterySystem.cs`
  只做亮屏耗电与充电结算

### 12.3 Prompt Files

当前已新增的关键 Prompt Files：

- `resources/prompts/last_day_intent_parse.system.txt`
- `resources/prompts/last_day_intent_parse.user.txt`
- `resources/prompts/last_day_narrative_render.system.txt`
- `resources/prompts/last_day_narrative_render.user.txt`
- `resources/prompts/message_reply.system.txt`
- `resources/prompts/message_reply.user.txt`
- `resources/prompts/story_summary.system.txt`
- `resources/prompts/story_summary.user.txt`

---

## 13. Demo 范围建议

为了让方案真的能做出来，阶段 2 建议收敛为：

- 8 个 canonical location
- 12 个行动原型
- 每回合最多 2 次模型调用
- 每 3 回合做一次摘要压缩
- 手机只做消息，不急着把朋友圈做得很重
- 场所 SVG 只在首次进入时生成并缓存

这个范围内，你已经能得到非常强的“任意选择”感，而且是可控的。

---

## 14. 最终结论

本项目的正确方向不是“让 AI 生成整个最后一天”，而是：

**用系统做一台受约束的生命模拟器，再让 AI 给每一步赋予语言、情绪和局部戏剧性。**

也就是：

- **世界是有限的**
- **输入是开放的**
- **状态由系统裁决**
- **叙事由 AI 润色**

这套方案既符合你文档里“自由行动 + 四大机制 + 预制骨架 + 个性化插入”的原始方向，也能直接落进当前 Godot/C# 项目，而不是再起一套不可控的大而空架构。

---

## 15. 当前进展与交接说明

本节用于后续开发接手，优先回答三个问题：现在做到哪了、提示词怎么写、下一步先做什么。

### 15.1 当前已完成

- **架构方向已定**：最后一天阶段不采用 “LLM 直接主控流程”，而采用 **系统控状态 + AI 做理解与叙事渲染**。
- **提示词资产已落地**：`resources/prompts/` 已统一整理为 `*.system.txt` 和 `*.user.txt` 两层文件。
- **阶段 1 已接入新规范**：以下调用已改为从 prompt 文件加载，而不是把 JSON 约束散写在代码里：
  - `soul_tag_extraction`
  - `annotation`
  - `death_cause_generation`
  - `verdict_opening`
  - `wish_suggestions`
- **PromptLoader 已支持新组织方式**：见 `scripts/systems/PromptLoader.cs` 中的 `LoadSystem(promptId)`、`LoadUser(promptId)`、`Combine(...)`。
- **LLM 输出契约文档已补齐**：详见 `docs/LLM请求提示词与输出契约.md`。
- **当前编译状态正常**：本轮整理后执行 `dotnet build RedHackathon.sln` 成功，`0` warning、`0` error。

### 15.2 当前提示词撰写规范

这是当前仓库的统一规范，后续新增请求必须继续遵守。

#### 15.2.1 文件组织

- `*.system.txt`
  - 放角色、人设、安全边界、输出格式、JSON 字段和枚举约束
  - 这部分相对稳定，不能混入本次业务数据
- `*.user.txt`
  - 只放本次请求的运行时上下文
  - 用 `{{key}}` 占位
  - 不承担 JSON 契约定义

不要再使用“一个 `.txt` 文件就是完整提示词”的写法。

#### 15.2.2 DeepSeek / JSON 输出规范

结构化请求必须同时满足以下三项：

1. `system prompt` 明确写出 JSON 输出格式、字段名、允许值、禁止事项
2. `user prompt` 只提供业务上下文
3. API 调用启用 `response_format = {"type":"json_object"}`

也就是说，真正的完整请求是：

- `system = PromptLoader.Combine(公共人设, 任务 system prompt)`
- `user = PromptLoader.LoadUser(promptId)` 替换变量后得到的文本
- `response_format = json_object`

单个 `.user.txt` 文件本身不是完整提示词。

#### 15.2.3 内容边界

- 系统状态由代码裁决，模型不得改时间、扣款、扣电、消息延迟、是否死亡。
- 凡是进入逻辑分支、资源结算、缓存、UI 渲染的数据，一律优先 JSON。
- `user` 文件中不要引用“另一个 prompt 文件”，模型运行时看不到仓库路径。
- 信息不足时保守表达，不硬编重大事实。
- 不输出血腥、暴力、自杀、自残、危险建议、违法建议、羞辱性内容。

#### 15.2.4 `final_wish` 的统一语义

`final_wish` 现在统一理解为：

- **本局核心遗愿**
- 在宣判阶段确认
- 表示“这一天最想完成的一件事”
- 不是当前回合的即时动作

示例：

- `final_wish = 去见妈妈一面`
- 当前回合输入 = `先去便利店买点她爱吃的`

前者是整局主线，后者是此刻动作。

因此：

- 在 `intent_parse / narrative_render / story_summary / eulogy / meditation` 等请求里，应保留 `final_wish`
- 在纯视觉、纯局部、不依赖主线目标的请求里，可以删掉 `final_wish`
- 已经从 `location_card_svg` 这类纯视觉请求中移除该字段

### 15.3 当前 Prompt 规划

目前 prompt 体系已经覆盖阶段 1 到阶段 3 的主要请求类型，包括：

- 阶段 1：标签提取、批注、死因、宣判开场、遗愿建议
- 阶段 2：地点归一化、行动意图解析、场景旁白与三选项、叙事摘要、SVG、消息回复、朋友圈评论、花钱后果
- 阶段 3：终局总结、悼词、墓志铭、遗言、冥想

后续如果新增请求，命名建议继续遵守：

- `prompt_id.system.txt`
- `prompt_id.user.txt`

并同步补到 `docs/LLM请求提示词与输出契约.md`。

### 15.4 接手开发的优先顺序

建议后续开发按下面顺序推进，不要并行乱接：

1. 先补阶段 2 的运行时数据模型
   - `WorldState`
   - `ResolvedAction`
   - `PendingEvent`
   - `NarrativeTurn`
2. 再实现 `LastDayDirector`
   - 串起 `地点归一化 -> 意图解析 -> 系统结算 -> 叙事渲染`
3. 接入最关键的 5 个请求
   - `last_day_location_resolve`
   - `last_day_intent_parse`
   - `last_day_narrative_render`
   - `story_summary`
   - `message_reply`
4. 再补次级体验
   - `death_spend_consequence`
   - `location_card_svg`
   - `social_comments`
5. 最后接阶段 3 的收束请求
   - `session_summary`
   - `eulogy`
   - `epitaph_suggestions`
   - `last_words`
   - `meditation_reflection`

### 15.5 接手时不要改动的原则

- 不要把“自由输入”重新做回纯菜单树。
- 不要把 LLM 提升为流程裁判。
- 不要把完整日志在每回合都塞给模型。
- 不要把 JSON 结构约束写回 `user prompt`。
- 不要在代码里散写 prompt 文本；统一走 `resources/prompts/`。

### 15.6 接手时优先查看的文件

- `docs/最后一天_AI自由行动技术方案.md`
- `docs/LLM请求提示词与输出契约.md`
- `scripts/systems/PromptLoader.cs`
- `scripts/systems/SoulTagExtractor.cs`
- `scripts/systems/DeathCauseGenerator.cs`
- `scripts/ui/DeathRegistrationScreen.cs`
- `scripts/ui/VerdictScreen.cs`
- `resources/prompts/`
