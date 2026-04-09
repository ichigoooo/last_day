# LLM 请求提示词与输出契约

## 1. 全局原则

本项目的 LLM 调用统一遵守以下原则：

- **系统管状态，LLM 管理解与表达**。模型不能直接改时间、金钱、电量、消息延迟或死亡状态。
- **结构化优先**。凡是会进入系统分支、结算、缓存、UI 逻辑的输出，一律优先 JSON。
- **JSON 必须可直接解析**。禁止 Markdown 代码块、注释、尾逗号、单引号键名。
- **字段固定**。系统只依赖约定字段；模型不得擅自换键名。
- **信息不足时保守表达**。不要为了“完整”而编造重大事实。
- **安全边界统一**。不得输出血腥、暴力、自杀、自残、危险建议、违法建议、羞辱性内容。

DeepSeek 的使用约束也按这个方式组织：

- `system prompt`：定义角色、边界、JSON 输出格式、字段枚举
- `user prompt`：只承载本次业务数据
- `response_format={"type":"json_object"}`：只用于需要结构化输出的请求

也就是说，单个 `.user.txt` 文件本身不是完整提示词；完整请求始终是 `system + user + response_format` 三者同时成立。

## 2. 请求类型总表

| 请求 id | 阶段 | 调用模式 | 主要输出契约 |
|------|------|------|------|
| `soul_tag_extraction` | 阶段 1 | `ChatJsonAsync` | `{"tags":[]}` |
| `annotation` | 阶段 1 | `ChatTextAsync` | 纯文本，1-2 句 |
| `death_cause_generation` | 阶段 1 | `ChatJsonAsync` | `{"text":"..."}` |
| `verdict_opening` | 阶段 1 | `ChatTextAsync` | 纯文本，1-2 句 |
| `wish_suggestions` | 阶段 1 | `ChatJsonAsync` | `{"wishes":["","",""]}` |
| `last_day_location_resolve` | 阶段 2 | `ChatJsonAsync` | 地点归一化 JSON |
| `last_day_intent_parse` | 阶段 2 | `ChatJsonAsync` | 行动理解 JSON |
| `last_day_narrative_render` | 阶段 2 | `ChatJsonAsync` | 旁白 + 3 选项 JSON |
| `story_summary` | 阶段 2 | `ChatJsonAsync` | 叙事记忆压缩 JSON |
| `visual_svg` | 阶段 2 | `ChatTextAsync` | 原始 `<svg>` 文本 |
| `location_card_svg` | 阶段 2 | `ChatTextAsync` | 原始 `<svg>` 文本 |
| `message_reply` | 阶段 2 | `ChatJsonAsync` | `{"reply":"...","tone":"..."}` |
| `social_comments` | 阶段 2 | `ChatJsonAsync` | 3 条评论 JSON |
| `death_spend_consequence` | 阶段 2 | `ChatJsonAsync` | 花钱后果 JSON |
| `session_summary` | 阶段 3 | `ChatJsonAsync` | 终局总结 JSON |
| `eulogy` | 阶段 3 | `ChatJsonAsync` | `{"eulogy":"..."}` |
| `epitaph_suggestions` | 阶段 3 | `ChatJsonAsync` | `{"epitaphs":["","",""]}` |
| `last_words` | 阶段 3 | `ChatJsonAsync` | `{"last_words":"..."}` |
| `meditation_reflection` | 阶段 3 | `ChatJsonAsync` | 四段冥想 JSON |

## 3. 文件组织

当前采用两层文件：

- `*.system.txt`：系统提示词，定义角色、边界、输出格式
- `*.user.txt`：用户模板，只填充运行时变量，不单独承担 JSON 契约

已落地文件位于 [resources/prompts](/Users/wuyuheng/Documents/Projects/RedHackathon/red-hackathon/resources/prompts)。

## 4. 关键变量说明

### 4.1 `final_wish`

`final_wish` 指的是：

- 用户在宣判阶段确认的“本局核心遗愿”
- 它是这一天最想完成的那一件事
- 它不是当前回合正在输入的即时动作

例如：

- `final_wish`：`去见妈妈一面`
- 当前回合输入：`先在便利店买点她爱吃的东西`

前者是整局主线，后者是此刻动作。

因此在阶段 2、3 的不少 prompt 里，`final_wish` 用来提供“主线目标”，让模型判断：

- 眼前这个动作和整局主线有多相关
- 这段旁白是否应该往“靠近遗愿 / 逃离遗愿”去写
- 终局总结时，这一天究竟围绕哪件事打转

它不等于“当前遗愿”，更准确的中文应理解为“本局核心遗愿”。

## 5. 关键 JSON 契约

### 4.1 `soul_tag_extraction`

```json
{"tags":["工作","关系"]}
```

- `tags` 只能出现 `工作`、`关系`、`逃避`
- 可为空数组

### 4.2 `death_cause_generation`

```json
{"text":"死因正文"}
```

- `text` 为 1-3 句中文

### 4.3 `wish_suggestions`

```json
{"wishes":["建议1","建议2","建议3"]}
```

- 必须恰好 3 条

### 4.4 `last_day_location_resolve`

```json
{
  "location_id":"office",
  "location_name":"公司",
  "match_type":"alias",
  "confidence":0.86,
  "reason":"“办公室”与公司是同一类目的地。",
  "reject_text":""
}
```

### 4.5 `last_day_intent_parse`

```json
{
  "intent_type":"message",
  "location_fit":"current_ok",
  "suggested_location_id":"",
  "target_person":"relation",
  "action_tags":["goodbye","hesitant"],
  "cost_band":"short",
  "screen_usage":"active",
  "money_band":"none",
  "risk":"low",
  "wish_relevance":"high",
  "scene_frame":"unfinished_relation",
  "summary":"想给重要的人发一条迟来的告别消息。",
  "reject_reason":""
}
```

### 4.6 `last_day_narrative_render`

```json
{
  "narration":"海风把话吹得很轻，像替你提前练习了一次告别。",
  "options":[
    {"slot":"face","label":"直接把话说完"},
    {"slot":"deflect","label":"先绕到别的话题"},
    {"slot":"pause","label":"先什么也不说"}
  ],
  "custom_hint":"也可以自己输入真正想做的事。"
}
```

### 4.7 `story_summary`

```json
{
  "summary":"他今天一直绕着真正想联系的人打转，还没有把最重要的话发出去。",
  "arc_state":"avoiding",
  "open_threads":["迟来的告别","始终没发出的消息"]
}
```

### 4.8 `message_reply`

```json
{"reply":"刚看到，你现在还好吗？","tone":"warm"}
```

### 4.9 `social_comments`

```json
{
  "comments":[
    {"author":"朋友A","text":"怎么突然发这个","tone":"awkward"},
    {"author":"同事B","text":"今天先早点休息","tone":"plain"},
    {"author":"家人","text":"回家记得说一声","tone":"warm"}
  ]
}
```

### 4.10 `death_spend_consequence`

```json
{
  "narration":"那笔钱出去得很快，像终于承认了一次“我其实在乎”。",
  "effect_line":"这笔钱买到的不是东西，是迟来的表达。",
  "memory_tag":"gift"
}
```

### 4.11 `session_summary`

```json
{
  "title":"一份来不及写完的今日档案",
  "one_liner":"他没把一切做好，但终于没再把整天浪费给以后。",
  "highlights":["去过海边","发出了一条迟来的消息","留下了一句不太体面的实话"],
  "regret_line":"真正想见的人，还是没能当面见到。",
  "grace_line":"至少最后这一天，他有几次没有再躲。"
}
```

### 4.12 `eulogy`

```json
{"eulogy":"……"}
```

### 4.13 `epitaph_suggestions`

```json
{"epitaphs":["他终于没有再说改天","并非圆满，但算抵达","此处安放迟来的诚实"]}
```

### 4.14 `last_words`

```json
{"last_words":"如果还有明天，记得替我把那句话说完。"}
```

### 4.15 `meditation_reflection`

```json
{
  "opening":"……",
  "breath":"……",
  "echo":"……",
  "closing":"……"
}
```

## 6. 当前实现状态

- 已接入阶段 1 的请求：`soul_tag_extraction`、`annotation`、`death_cause_generation`、`verdict_opening`、`wish_suggestions`
- 已接入阶段 2 核心请求（由 `LastDayDirector` / `MessageSystem` 串起）：`last_day_location_resolve`（出行类输入）、`last_day_intent_parse`、`last_day_narrative_render`、`story_summary`（每 3 行动回合）、`message_reply`（异步到点回复）
- 已接入阶段 2 资源/UI 向：`location_card_svg`（`LocationCardSvgService` + `Image.LoadSvgFromString`）、`death_spend_consequence`（`DeathSpendService` + `DeathSpendUI`）
- 已定义、可选体验增强待接：`visual_svg`、`social_comments`（朋友圈评论未默认调用）
- 阶段 3 待接：`session_summary`、`eulogy`、`epitaph_suggestions`、`last_words`、`meditation_reflection`

## 7. 接入建议

- 纯文本输出只用于“直接展示给玩家且无需结构解析”的场景
- 其余请求一律走 JSON
- 阶段 2 上线前，优先实现：
  1. `last_day_location_resolve`
  2. `last_day_intent_parse`
  3. `last_day_narrative_render`
  4. `story_summary`
  5. `message_reply`
  6. `location_card_svg`
