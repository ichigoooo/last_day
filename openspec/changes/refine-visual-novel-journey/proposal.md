## Why

当前项目已经具备一个相当完整的“最后一天”高自由度框架：时间、电量、现金、地点、手机消息、现场对话都已成立，玩家中段的自由度并不弱。真正拖累完成度的，不是玩法深度，而是叙事封装方式。

现在的主要问题有四个：

- 开头的“入殓登记”仍以三块表单输入为主，用户在填写信息，而不是在经历一个被精心安排的开场。
- `Verdict` 过早要求玩家确认“遗愿”，会把终章才该成立的真话，提前降格为一个任务目标。
- 结尾被拆成“死亡 -> 葬礼 -> 冥想 -> 结束”的多个功能步骤，内容很多，但情绪不断被“下一步操作”打断。
- 现有 LLM 生成能力已经足够丰富，但真正需要精雕细琢的节点缺少固定的文案设计与节奏控制，因此整体更像一组系统，而不是一场视觉小说旅程。

这次变更要做的，不是简单“加文案”，而是对旅程进行重构：保留中段高自由度，把首尾做成可被 Dialogic 承载的、固定而高级的叙事框架；同时把“遗愿”“讣告”“墓志铭”重新放回它们最有重量的位置。

## What Changes

- 将整体体验重构为“固定序章 + 高自由中段 + 固定终章”的视觉小说式旅程。
- 使用 Dialogic 承载开头与结尾的关键时间线，保留 `LastDay` 作为自由行动沙盒，不将中段强行改造成传统分支树视觉小说。
- 将当前 `DeathRegistration` 从“三块表单”改造为“档案处对话”，把 `工作 / 关系 / 逃避` 的收集巧妙融入与死神的问答。
- 将 `FinalWish` 从 `Verdict` 前置任务改为终章弥留时刻才登记的内容，使它成为真正被逼出来的一句实话。
- 保留 `讣告`、`墓志铭` 与分享卡设计，但将其移动到“模拟死亡已完成、真相尚未揭示”的终章仪式中，先让玩家经历完整失去，再揭示其实没有死。
- 删除独立的“遗言一句”步骤，将其功能分别并入“临终遗愿”和“回到现实的一句”，减少概念重复。
- 重新定义各阶段的职责：
  - `DeathRegistration`：开机通知 + 档案处对话
  - `Verdict`：死因裁定 + 放行进入最后一天
  - `LastDay`：保留高自由玩法
  - `Death`：24:00 硬切黑屏与静音
  - `Funeral`：最终系统报告 + 讣告 + 墓志铭校对
  - `Meditation`：真相揭示 + 现实返还问题
  - `Ending`：静默收束与返回现实

## Capabilities

### New Capabilities

- `authored-bookend-dialogue-flow`: 允许用 Dialogic 驱动固定序章与固定终章，让开头和结尾具有可控的节奏、停顿和对白质量。
- `archive-intake-narrative`: 将用户信息采集改造成死神档案处对话，并在保留结构化数据的同时消除表单感。
- `closure-ritual-sequencing`: 将遗愿、系统报告、讣告、墓志铭与真相揭示编排为一条连续终章，而不是多个功能页面。

### Modified Capabilities

- `last-day-freeform-journey`: 保持中段高自由探索不变，但移除其对“前置遗愿”的依赖，改为使用序章提炼出的叙事锚点。
- `ending-summary-pipeline`: 保留悼词/墓志铭/分享卡的生成能力，但更换其出现顺序、交互语义与文案包装。

## Impact

- 主要影响文件/模块：
  - `scripts/ui/DeathRegistrationScreen.cs`
  - `scripts/ui/VerdictScreen.cs`
  - `scripts/ui/DeathScreen.cs`
  - `scripts/ui/FuneralScreen.cs`
  - `scripts/ui/MeditationScreen.cs`
  - `scripts/ui/EndingScreen.cs`
  - `scripts/ui/ShareCard.cs`
  - `scripts/systems/LastDayDirector.cs`
  - `scripts/systems/ClosurePromptVars.cs`
  - `scripts/models/GameSession.cs`
  - `scripts/models/SessionActivityLog.cs`
  - 新增 Dialogic 时间线、角色、变量桥接与可能的桥接脚本
- 主要影响资源：
  - `resources/prompts/*` 中与遗愿、悼词、墓志铭、终章总结相关的提示词
  - 新的 Dialogic 资产与叙事文稿
- 运行时行为变化：
  - 开头不再直接出现表单式问卷
  - 中段仍为高自由度沙盒
  - 终章先完成死亡仪式，再揭示“你没有死”
- 验证重点：
  - 开头是否像一段视觉小说，而不是配置表
  - 中段自由度是否不被破坏
  - 遗愿后置后，中段提示词和总结链路是否仍成立
  - 终章是否连贯、克制、有余味，而不是再次变成功能清单
