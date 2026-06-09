# RPGSkillKit

这个目录是给 AI NPC 对话扩展用的本地 skill 库架构。它不替换现有 DLL，也不删除原 Mod 文件。

目标结构：

- `skills/global`: 总 skill，控制世界观、语言风格、角色扮演边界。
- `characters`: NPC 专属 skill，按 NPC ID 或名字绑定。
- `skills/relationship`: 关系阶段 skill，按好感、敌对、信任、亲密等状态调用。
- `skills/faction`: 宗门/势力 skill，补充门派价值观和知识边界。
- `memory`: 玩家和 NPC 的动态记忆。
- `state`: 从游戏存档或内存读取的当前状态结构。
- `templates`: 最终发送给模型的 prompt 模板。
- `config`: 调用顺序、替换说明、路由规则。
- `data_sources`: 游戏数据来源说明。
- `overrides`: 后续用于替换或覆盖原 `prompt.txt` 的候选文件。

推荐调用顺序：

1. 加载 `skills/global` 的总规则。
2. 根据当前 NPC ID 加载 `characters` 内的角色卡。
3. 根据 NPC 所属势力加载 `skills/faction`。
4. 根据好感/关系标签加载 `skills/relationship`。
5. 读取 `memory` 中该 NPC 与玩家的历史记忆。
6. 读取 `state` 中当前修为、地点、功法、任务等运行时状态。
7. 使用 `templates/final_prompt_template.txt` 组装最终 prompt。

当前目录只是架构和占位内容，原 Mod 的 `prompt.txt`、`MCS_AIChatMod.dll`、依赖 DLL 都没有改动。
