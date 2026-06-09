# 2026-06-10 - 对话效果机制 V1 设计

## 背景

当前项目方向已经调整为：不另建自有 mod 工程，而是直接基于 `参考mod` 改善现有 AI 对话 mod。用户希望 AI 对话不再只停留在文本层，而是能适度影响游戏进程。第一版聚焦“对话改变 NPC 情分/关系”，战斗触发等高风险行为留到后续阶段。

本设计文档沉淀 2026-06-09 至 2026-06-10 已确认的头脑风暴结论，用于后续实现计划和代码改造衔接。

## 目标

- 玩家与 NPC 对话后，系统可以根据对话内容小幅修改游戏内 NPC 情分。
- AI 只负责提出“效果建议”，不能直接执行游戏命令。
- Mod 负责解析、校验、限幅、执行游戏侧情分变化，并给玩家显示简短提示。
- 每轮对话最多触发一次情分变化，变化范围限制为 `-3` 到 `+3`。
- 关系变化原因要写入 NPC 记忆，影响后续对话语气和关系阶段。

## 非目标

- V1 不触发战斗。
- V1 不生成复杂任务分支。
- V1 不让 AI 直接调用游戏命令、修改存档或绕过校验。
- V1 不追求所有 NPC 都有精细个性化关系规则；先以通用规则验证链路。

## 设计原则

- **Mod 裁决优先**：AI 输出只能视为建议，所有真实游戏状态变化必须经过本地代码校验。
- **小步可逆**：单次变化小，避免一句话破坏长期养成节奏。
- **可观察**：玩家能看到简短提示，开发者能通过日志或记忆文件追踪变化原因。
- **失败静默降级**：解析失败、校验失败或游戏接口不可用时，不应影响正常对话回复。
- **后续可扩展**：为怒气、敌意、事件候选、战斗候选预留字段，但 V1 不执行这些效果。

## 用户确认的范围

- 第一版选择方案 B：同步修改游戏内关系/情分。
- 玩家反馈选择方案 B：游戏内显示简短提示。
- 单轮变化选择方案 A：保守范围 `-3..+3`。
- 实现路线选择方案 A：AI 给建议效果，Mod 校验和执行。

## 核心流程

1. 玩家向 NPC 发送一句话。
2. Mod 组装当前 NPC、近期对话、长期记忆和基础世界观 prompt。
3. AI 返回 NPC 回复正文，同时附带隐藏结构化效果建议。
4. Mod 提取回复正文并正常显示给玩家。
5. Mod 解析隐藏效果 JSON。
6. Mod 校验建议是否符合 V1 规则。
7. 校验通过后，Mod 调用游戏内情分接口修改 NPC 情分。
8. 修改成功后，Mod 显示简短提示并记录关系记忆。
9. 修改失败时，Mod 保留对话回复，不显示误导性成功提示。

## AI 输出协议

建议使用“正文 + 隐藏效果块”的稳定格式。示例：

```text
你这番话倒还算诚恳。既如此，此事我会记下。

<dialog_effect>
{
  "favor_delta": 2,
  "reason": "玩家语气尊重，并主动表达善意",
  "memory_summary": "玩家曾以礼相待，使该 NPC 态度稍有缓和",
  "anger_delta": 0,
  "trigger_candidate": null
}
</dialog_effect>
```

字段含义：

- `favor_delta`：本轮建议情分变化，整数。V1 最终执行范围为 `-3..+3`。
- `reason`：给开发者和日志看的简短原因，不直接暴露给玩家。
- `memory_summary`：写入 NPC 长期记忆的关系摘要。
- `anger_delta`：预留字段，V1 只记录或忽略，不触发战斗。
- `trigger_candidate`：预留字段，V1 必须忽略或记录为候选，不执行。

如果没有明确关系变化，AI 应输出：

```json
{
  "favor_delta": 0,
  "reason": "本轮对话没有明显改变 NPC 态度",
  "memory_summary": "",
  "anger_delta": 0,
  "trigger_candidate": null
}
```

## 安全校验规则

Mod 必须执行以下校验：

- 效果块缺失或 JSON 无法解析：视为 `favor_delta = 0`。
- `favor_delta` 不是整数：视为 `0`。
- `favor_delta` 超出范围：夹到 `-3..+3`，或按更保守策略直接归零。
- `reason` 与 `memory_summary` 过长：截断到安全长度。
- 同一玩家消息只能处理一次效果，避免重试导致重复加减。
- NPC ID 不明确、当前对话对象不明确或游戏状态不可写：不执行情分变化。
- AI 请求战斗、传送、发物品、杀死 NPC 等非 V1 效果：全部忽略。

推荐第一版采用更保守策略：

- `favor_delta` 在 `-3..+3` 内才允许执行。
- 超出范围直接归零，并记录为被拦截。
- 只有 `favor_delta != 0` 且当前 NPC ID 可确认时才调用游戏接口。

## 模块划分

### DialogEffect 数据模型

职责：

- 表示 AI 建议的对话效果。
- 提供默认空效果。
- 承载未来扩展字段，但 V1 只执行 `favor_delta`。

建议字段：

- `int FavorDelta`
- `string Reason`
- `string MemorySummary`
- `int AngerDelta`
- `string TriggerCandidate`

### DialogEffectParser

职责：

- 从 AI 原始回复中提取 `<dialog_effect>...</dialog_effect>`。
- 将正文和效果分离。
- 解析 JSON 为 `DialogEffect`。
- 解析失败时返回正文和空效果。

### DialogEffectValidator

职责：

- 检查数值范围、字段长度、NPC 上下文和执行次数。
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
- 根据正负变化使用中性、克制的文字。

提示文案示例：

- `对方态度略有缓和：情分 +1`
- `对方似乎更信任你了：情分 +3`
- `对方对你心生不快：情分 -1`
- `对方态度明显冷淡：情分 -3`

### RelationshipMemoryWriter

职责：

- 将已执行效果写入 NPC 长期记忆。
- 记忆类型使用 `relationship`。
- 摘要来自 `memory_summary`，必要时补充游戏时间、NPC ID 和变化值。

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
  -> DialogEffectValidator
  -> NpcFavorEffectExecutor
  -> DialogEffectNotifier
  -> RelationshipMemoryWriter
```

正常对话显示不依赖效果执行成功。即使效果链路失败，玩家仍应看到 NPC 的文本回复。

## 错误处理

- AI 没有输出效果块：显示正文，不改情分。
- 效果块格式错误：显示正文，不改情分，记录一次解析失败。
- 找不到当前 NPC ID：显示正文，不改情分。
- 游戏情分接口调用失败：显示正文，不显示成功提示，记录失败原因。
- 记忆写入失败：不回滚已成功的游戏情分变化，但记录失败。
- AI 输出危险触发器：忽略触发器，只处理合规的 `favor_delta`。

## 验证策略

### 离线验证

- 给 `DialogEffectParser` 准备样例：
  - 正常效果块。
  - 无效果块。
  - JSON 损坏。
  - 超大字段。
  - 多个效果块。
- 给 `DialogEffectValidator` 准备样例：
  - `favor_delta = -3, 0, +3`。
  - `favor_delta = -99, +99`。
  - 非整数值。
  - 带战斗触发候选。

### 游戏内验证

- 选择一个普通 NPC 测试三类对话：
  - 尊重、示好：情分小幅增加。
  - 中性寒暄：情分不变。
  - 冒犯、威胁：情分小幅减少。
- 检查提示是否只在真实修改成功后出现。
- 检查重复发送或 AI 重试不会重复加减同一轮情分。
- 检查 NPC 记忆是否记录本轮关系变化。

## 风险

- 游戏侧情分接口签名、调用时机或副作用尚未确认。
- 当前只有参考 DLL 和 PDB，后续可能需要反编译确认安全接入点。
- AI 输出格式可能不稳定，因此 parser 和 validator 必须容错。
- 如果提示太频繁，会破坏沉浸感；V1 可先保留提示以方便测试，后续再提供开关。
- 情分修改可能影响原游戏事件条件，必须限制幅度并优先在测试存档验证。

## 第二阶段预留

V1 稳定后可以扩展：

- `anger_delta` 累积为 NPC 对玩家的敌意状态。
- 极端敌意触发拒绝对话、威胁、追杀任务或战斗候选。
- 根据 NPC 性格、势力、修为差距调整关系变化阈值。
- 增加玩家设置：关闭提示、只记录日志、开启强剧情化变化。
- 将关系变化与 `RPGSkillKit` 的 relationship skill 路由联动。

## 开放问题

- 游戏内最安全的情分修改接口是哪一个：`CmdAddNPCQingFen`、已有 favor patch，还是其他内部管理器。
- 当前 AI 回复链路中最适合插入 parser 的位置在哪里。
- 游戏提示应复用现有 UI 通知，还是先写入聊天面板系统提示。
- 情分变化是否需要按 NPC 类型、境界差距、剧情身份设置不同限幅。

这些问题不阻塞设计确认，但会进入后续实现计划和代码勘查。
