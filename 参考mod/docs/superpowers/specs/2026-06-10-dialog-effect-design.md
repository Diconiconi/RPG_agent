# 2026-06-10 - 对话效果机制 V1 设计

## 背景

当前项目方向已经调整为：不另建自有 mod 工程，而是直接基于 `参考mod` 改善现有 AI 对话 mod。用户希望 AI 对话不再只停留在文本层，而是能适度影响游戏进程。

本设计文档沉淀 2026-06-09 至 2026-06-10 已确认的头脑风暴结论，并吸收用户后续 review：情分变化不应每轮都发生，也不应只有 `-3..+3` 一个全局上限。V1 应建立“对话事件 -> 安全校验 -> 游戏效果”的框架：普通闲聊通常不改变情分；只有出现明确情绪变化、利益相关、送礼、辱骂、诋毁、攻击、威胁等有意义事件时，才按分档规则改变情分。

## 目标

- 玩家与 NPC 对话后，系统可以识别“有游戏意义的对话事件”，并在必要时修改游戏内 NPC 情分。
- 普通闲聊、问候、无明显立场变化的信息交换默认不改变情分。
- AI 只负责提出“效果建议”，不能直接执行游戏命令。
- Mod 负责解析、分类、校验、限幅、执行游戏侧情分变化，并给玩家显示简短提示。
- 情分变化按事件类型和强度分档，不再使用 `-3..+3` 作为唯一上下界。
- 关系变化原因要写入 NPC 记忆，影响后续对话语气和关系阶段。
- 为后续“通过对话触发具体游戏行为”预留统一事件框架，但 V1 只执行情分变化。

## 非目标

- V1 不触发战斗。
- V1 不直接生成任务分支、传送、发物品、杀死 NPC 或修改复杂剧情状态。
- V1 不让 AI 直接调用游戏命令、修改存档或绕过校验。
- V1 不追求所有 NPC 都有精细个性化关系规则；先用通用事件分档验证链路。

## 设计原则

- **事件化触发**：情分变化由明确事件触发，不由每轮对话自动触发。
- **Mod 裁决优先**：AI 输出只能视为建议，所有真实游戏状态变化必须经过本地代码校验。
- **分档限幅**：轻微情绪变化小幅处理，辱骂攻击、重大利益冲突、贵重送礼等允许更大幅度变化。
- **可配置**：分档阈值应集中配置，后续可以按 NPC 性格、势力、境界、剧情身份调整。
- **可观察**：玩家能看到简短提示，开发者能通过日志或记忆文件追踪变化原因。
- **失败静默降级**：解析失败、校验失败或游戏接口不可用时，不应影响正常对话回复。
- **后续可扩展**：为怒气、敌意、事件候选、战斗候选、任务候选预留字段，但 V1 不执行这些效果。

## 用户确认和修订范围

- 第一版仍然同步修改游戏内关系/情分。
- 游戏内仍显示简短提示，但只在真实情分变化成功后显示。
- 原先的 `-3..+3` 不再是全局唯一上限，改为“轻微/中等/重大/严重”分档。
- 普通对话默认 `favor_delta = 0`。
- 辱骂、诋毁、攻击、威胁、背叛等场景可以大幅降低情分。
- 送礼、让利、救助、完成承诺等场景可以较大幅增加情分。
- 本机制同时作为后续对话触发具体游戏行为的基础框架。

## 核心流程

1. 玩家向 NPC 发送一句话。
2. Mod 组装当前 NPC、近期对话、长期记忆和基础世界观 prompt。
3. AI 返回 NPC 回复正文，同时附带隐藏结构化效果建议。
4. Mod 提取回复正文并正常显示给玩家。
5. Mod 解析隐藏效果 JSON，得到建议事件类型、强度和情分变化。
6. Mod 判断本轮是否属于可触发事件。
7. Mod 根据事件类型、强度、NPC 上下文和配置表校验最终情分变化。
8. 校验通过后，Mod 调用游戏内情分接口修改 NPC 情分。
9. 修改成功后，Mod 显示简短提示并记录关系记忆。
10. 修改失败时，Mod 保留对话回复，不显示误导性成功提示。

## AI 输出协议

建议使用“正文 + 隐藏效果块”的稳定格式。示例：

```text
你这番话，未免欺人太甚。今日我暂且记下，往后再与你分说。

<dialog_effect>
{
  "effect_type": "insult_attack",
  "impact_level": "major",
  "favor_delta": -8,
  "reason": "玩家直接辱骂并贬损 NPC 名誉，造成明显敌意",
  "memory_summary": "玩家曾当面辱骂并诋毁该 NPC，使双方关系明显恶化",
  "anger_delta": 12,
  "trigger_candidates": ["future_hostile_dialog"],
  "confidence": 0.86
}
</dialog_effect>
```

字段含义：

- `effect_type`：本轮对话事件类型。
- `impact_level`：事件强度分档。
- `favor_delta`：AI 建议的情分变化，整数；最终执行值由 Mod 校验后决定。
- `reason`：给开发者和日志看的简短原因，不直接暴露给玩家。
- `memory_summary`：写入 NPC 长期记忆的关系摘要。
- `anger_delta`：预留字段，V1 可以记录，但不触发战斗。
- `trigger_candidates`：预留字段，V1 只记录或忽略，不执行具体游戏行为。
- `confidence`：AI 对事件判断的置信度，范围 `0..1`；置信度低时 Mod 应倾向归零或降档。

如果没有明确关系变化，AI 应输出：

```json
{
  "effect_type": "none",
  "impact_level": "none",
  "favor_delta": 0,
  "reason": "本轮是普通对话，没有明确情绪变化或利益影响",
  "memory_summary": "",
  "anger_delta": 0,
  "trigger_candidates": [],
  "confidence": 1.0
}
```

## 事件类型

V1 推荐先支持以下通用类型：

| `effect_type` | 含义 | V1 行为 |
| --- | --- | --- |
| `none` | 普通闲聊、问候、无明确立场变化 | 不改情分 |
| `emotional_shift` | 明确表达尊重、歉意、失望、冒犯等情绪变化 | 按强度小到中幅变化 |
| `interest_gain` | 玩家让利、帮忙、承诺有益事项、完成对 NPC 有利行为 | 增加情分 |
| `interest_loss` | 玩家损害 NPC 利益、索取过度、违背承诺、制造麻烦 | 降低情分 |
| `gift` | 送礼、赠药、赠法宝、提供资源 | 按礼物价值增加情分 |
| `insult_attack` | 辱骂、诋毁、威胁、羞辱、攻击名誉或立场 | 中到大幅降低情分 |
| `trust_break` | 欺骗、背叛、泄密、出卖利益 | 大幅降低情分 |
| `future_trigger_candidate` | 有潜力触发任务、战斗、拒绝对话等后续行为 | V1 只记录候选，不执行 |

## 强度分档和情分范围

Mod 应维护配置化分档表。以下是 V1 默认建议值：

| `impact_level` | 适用场景 | 默认范围 |
| --- | --- | --- |
| `none` | 普通对话，无明显情绪或利益变化 | `0` |
| `minor` | 礼貌、轻微冒犯、轻微好感或不快 | `-1..+1` |
| `moderate` | 明确道歉、明显尊重、明显冒犯、小额利益变化 | `-3..+3` |
| `major` | 重要送礼、明显让利、公开羞辱、严重诋毁、较大利益冲突 | `-10..+10` |
| `severe` | 背叛、重大欺骗、严重威胁、救命之恩、重大资源赠与 | `-25..+25` |

分档不是让 AI 自由决定最终值。AI 可以建议类型、强度和数值，但 Mod 必须按配置表、NPC 上下文和当前游戏状态裁决。

## 情分变化判定规则

Mod 校验时应优先使用以下规则：

- `effect_type = none` 时，最终 `favor_delta` 必须为 `0`。
- 普通闲聊、寒暄、询问常识、没有情绪与利益变化的回答，最终 `favor_delta` 必须为 `0`。
- `impact_level` 决定绝对上限，`favor_delta` 不得越过该档位范围。
- `confidence < 0.6` 时，建议直接归零；`0.6 <= confidence < 0.8` 时，建议降一档。
- 同一玩家消息只能处理一次效果，避免重试导致重复加减。
- NPC ID 不明确、当前对话对象不明确或游戏状态不可写：不执行情分变化。
- `trigger_candidates` 在 V1 中只允许记录，不能触发真实游戏行为。
- AI 请求战斗、传送、发物品、杀死 NPC 等非 V1 效果：全部忽略。

## 示例场景

### 普通对话

玩家只是问候、闲聊、询问地点或普通信息：

```json
{
  "effect_type": "none",
  "impact_level": "none",
  "favor_delta": 0
}
```

### 轻微情绪变化

玩家语气尊重、表达歉意，或略微冒犯：

```json
{
  "effect_type": "emotional_shift",
  "impact_level": "minor",
  "favor_delta": 1
}
```

### 送礼或让利

玩家赠送对 NPC 有实际价值的物品，或明显让利：

```json
{
  "effect_type": "gift",
  "impact_level": "major",
  "favor_delta": 8,
  "trigger_candidates": ["gift_memory"]
}
```

### 辱骂诋毁

玩家当面辱骂、诋毁名誉、羞辱宗门或威胁 NPC：

```json
{
  "effect_type": "insult_attack",
  "impact_level": "major",
  "favor_delta": -8,
  "anger_delta": 12,
  "trigger_candidates": ["future_hostile_dialog"]
}
```

### 严重背叛

玩家欺骗、背叛或破坏 NPC 核心利益：

```json
{
  "effect_type": "trust_break",
  "impact_level": "severe",
  "favor_delta": -20,
  "anger_delta": 25,
  "trigger_candidates": ["future_combat_candidate"]
}
```

V1 中 `future_combat_candidate` 只能进入记忆或日志，不能立即触发战斗。

## 安全校验规则

Mod 必须执行以下校验：

- 效果块缺失或 JSON 无法解析：视为 `effect_type = none` 且 `favor_delta = 0`。
- `favor_delta` 不是整数：视为 `0`。
- `effect_type` 不在白名单：视为 `none`。
- `impact_level` 不在白名单：视为 `none`。
- `favor_delta` 方向与事件类型明显冲突时归零，例如 `insult_attack` 却建议正向增加。
- `favor_delta` 超出当前档位范围时，按配置策略夹到范围内，或按更保守策略直接归零。
- `reason` 与 `memory_summary` 过长：截断到安全长度。
- 同一玩家消息只能处理一次效果，避免重复加减。
- NPC ID 不明确、当前对话对象不明确或游戏状态不可写：不执行情分变化。
- 非 V1 游戏行为全部不执行，只能记录候选。

推荐 V1 采用“中等保守”策略：

- 未识别事件直接归零。
- 低置信度事件直接归零。
- 超出档位范围时夹到该档位边界，并记录被裁剪。
- `severe` 档位只在明确背叛、严重威胁、重大救助或重大资源赠与时允许。

## 模块划分

### DialogEffect 数据模型

职责：

- 表示 AI 建议的对话效果。
- 提供默认空效果。
- 承载未来扩展字段，但 V1 只执行情分变化。

建议字段：

- `string EffectType`
- `string ImpactLevel`
- `int FavorDelta`
- `string Reason`
- `string MemorySummary`
- `int AngerDelta`
- `List<string> TriggerCandidates`
- `double Confidence`

### DialogEffectParser

职责：

- 从 AI 原始回复中提取 `<dialog_effect>...</dialog_effect>`。
- 将正文和效果分离。
- 解析 JSON 为 `DialogEffect`。
- 解析失败时返回正文和空效果。

### DialogEffectPolicy

职责：

- 保存事件类型白名单、强度分档、默认范围和保守策略。
- 后续可以扩展为配置文件，支持不同 NPC、势力或关系阶段的差异。

### DialogEffectValidator

职责：

- 检查事件类型、强度分档、数值范围、字段长度、NPC 上下文和执行次数。
- 把 AI 建议转换为可执行效果。
- 拦截 V1 不支持的触发器。

### NpcFavorEffectExecutor

职责：

- 接收已校验效果。
- 调用游戏侧 NPC 情分修改接口。
- 返回执行成功、失败或跳过状态。

潜在接入点需要后续反编译确认：

- `CmdAddNPCQingFen`
- `CmdGetNPCQingFen`
- `MCS_AIChatMod.OfficialNpcFavorChangedPatch`
- 与 `NPCInfo`、当前 NPC ID、当前交互 UI 相关的现有逻辑

### DialogEffectNotifier

职责：

- 情分变化成功后显示简短提示。
- 不显示 AI 原始 reason。
- 根据正负变化和分档使用克制文字。

提示文案示例：

- `对方态度略有缓和：情分 +1`
- `对方似乎更信任你了：情分 +3`
- `这份人情，对方记下了：情分 +8`
- `对方对你心生不快：情分 -1`
- `对方态度明显冷淡：情分 -5`
- `对方已对你怀有强烈敌意：情分 -15`

### RelationshipMemoryWriter

职责：

- 将已执行效果写入 NPC 长期记忆。
- 记忆类型使用 `relationship`。
- 摘要来自 `memory_summary`，必要时补充游戏时间、NPC ID、事件类型、强度分档和变化值。
- 对 `trigger_candidates` 只记录候选，不执行。

现有可参考结构：

- `参考mod/plugins/RPGSkillKit/memory/npc_memory.schema.json`
- `参考mod/plugins/RPGSkillKit/state/npc_state.schema.json`

## 数据流

```text
玩家输入
  -> NPCDialog / UI 发送入口
  -> AIChatManager 请求 AI
  -> AI 原始回复
  -> DialogEffectParser
      -> 可显示正文
      -> DialogEffect 建议
  -> DialogEffectPolicy
  -> DialogEffectValidator
  -> NpcFavorEffectExecutor
  -> DialogEffectNotifier
  -> RelationshipMemoryWriter
```

正常对话显示不依赖效果执行成功。即使效果链路失败，玩家仍应看到 NPC 的文本回复。

## 错误处理

- AI 没有输出效果块：显示正文，不改情分。
- 效果块格式错误：显示正文，不改情分，记录一次解析失败。
- AI 输出普通对话：显示正文，不改情分。
- 找不到当前 NPC ID：显示正文，不改情分。
- 游戏情分接口调用失败：显示正文，不显示成功提示，记录失败原因。
- 记忆写入失败：不回滚已成功的游戏情分变化，但记录失败。
- AI 输出危险触发器：忽略触发器，只处理合规的情分变化。

## 验证策略

### 离线验证

- 给 `DialogEffectParser` 准备样例：
  - 正常效果块。
  - 无效果块。
  - JSON 损坏。
  - 超大字段。
  - 多个效果块。
- 给 `DialogEffectValidator` 准备样例：
  - `none` 普通对话归零。
  - `minor` 限制在 `-1..+1`。
  - `moderate` 限制在 `-3..+3`。
  - `major` 限制在 `-10..+10`。
  - `severe` 限制在 `-25..+25`。
  - 非整数值归零。
  - 低置信度归零或降档。
  - `insult_attack` 正向加分被拦截。
  - `gift` 负向扣分被拦截。
  - 带战斗触发候选但不执行。

### 游戏内验证

- 选择普通 NPC 测试以下场景：
  - 普通寒暄：情分不变。
  - 尊重、道歉、轻微冒犯：小幅变化。
  - 送礼或让利：中到大幅增加。
  - 辱骂、诋毁、威胁：中到大幅降低。
  - 严重背叛候选：只记录情分和候选，不触发战斗。
- 检查提示是否只在真实修改成功后出现。
- 检查重复发送或 AI 重试不会重复加减同一轮情分。
- 检查 NPC 记忆是否记录事件类型、强度和关系变化。

## 风险

- 游戏侧情分接口签名、调用时机或副作用尚未确认。
- 当前只有参考 DLL 和 PDB，后续可能需要反编译确认安全接入点。
- AI 输出格式可能不稳定，因此 parser 和 validator 必须容错。
- 分档幅度过大可能影响原游戏事件条件，必须优先在测试存档验证。
- 如果提示太频繁，会破坏沉浸感；V1 可先保留提示以方便测试，后续再提供开关。
- 高强度事件需要谨慎识别，避免普通口角被误判成严重背叛。

## 第二阶段预留

V1 稳定后可以扩展：

- `anger_delta` 累积为 NPC 对玩家的敌意状态。
- `trigger_candidates` 从“只记录”升级为“满足条件后进入待确认事件队列”。
- 极端敌意触发拒绝对话、威胁、追杀任务或战斗候选。
- 根据 NPC 性格、势力、修为差距调整关系变化阈值。
- 增加玩家设置：关闭提示、只记录日志、开启强剧情化变化。
- 将关系变化与 `RPGSkillKit` 的 relationship skill 路由联动。
- 将送礼、交易、威胁、挑战等对话行为逐步接入真实游戏命令。

## 开放问题

- 游戏内最安全的情分修改接口是哪一个：`CmdAddNPCQingFen`、已有 favor patch，还是其他内部管理器。
- 当前 AI 回复链路中最适合插入 parser 的位置在哪里。
- 游戏提示应复用现有 UI 通知，还是先写入聊天面板系统提示。
- 礼物价值如何从游戏数据读取，是否能区分普通物品和贵重资源。
- `major` 与 `severe` 的默认范围是否需要根据游戏原始情分系统尺度再收紧。

这些问题不阻塞设计确认，但会进入后续实现计划和代码勘查。
