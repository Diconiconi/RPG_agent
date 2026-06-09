# Replacement Map

这里记录“以后真正接入时”要替换或拆分原 Mod prompt 的哪些部分。当前没有改动原文件。

## 原始部分

原文件：

`E:\RPG_agent\参考mod\plugins\prompt.txt`

原 prompt 大致包含：

- 觅长生世界观
- 修炼体系
- 主要势力
- 重要地点
- 重要人物
- 炼丹炼器、社交互动、终极目标

## 新架构拆分

原来的“世界观/修炼体系”应拆到：

- `skills/global/worldview.skill.txt`

原来的“语言风格和修仙语境”应拆到：

- `skills/global/language_style.skill.txt`

原来缺失的“扮演限制/拒绝越界”应新增到：

- `skills/global/roleplay_limits.skill.txt`

原来的“重要人物”不建议继续放在总 prompt 里，应替换为按 NPC 调用：

- `characters/index.json`
- `characters/important/*.character.json`

原来没有的“关系阶段变化”应新增：

- `skills/relationship/*.skill.txt`

原来没有的“宗门/势力立场”应新增：

- `skills/faction/*.skill.txt`

原来没有的“动态记忆”应新增：

- `memory/runtime/{npc_id}.memory.json`

原来由 Mod 临时读取的 NPC 信息，应规范成状态输入：

- `state/current_npc_state.json`

## 主要替换点

后续真正接入时，最核心替换的是“NPC 预输入相关部分”：

旧方式：

```text
重要人物:
魏无极：...
倪旭欣：...
百里奇：...
星铃儿/星凝：...
```

新方式：

```text
根据当前 NPC ID 只加载当前角色的 character skill。
再根据当前好感、关系、任务、位置、修为补充 relationship/memory/state。
```

这样可以避免所有角色设定混在一个大 prompt 里。
