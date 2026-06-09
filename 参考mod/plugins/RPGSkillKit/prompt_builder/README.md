# Prompt Builder

这个目录预留给后续的 Prompt 组装器代码。

当前只定义职责：

1. 接收当前 NPC ID。
2. 从 `characters/index.json` 找到角色卡。
3. 根据角色卡加载 faction skill。
4. 根据好感或关系标签加载 relationship skill。
5. 从 `memory/runtime` 读取该 NPC 的动态记忆。
6. 从游戏存档或内存读取当前 state。
7. 套用 `templates/final_prompt_template.txt`。
8. 输出最终 prompt 给原 Mod 的 AI 请求流程。

后续可选实现方式：

- 单独写一个 BepInEx 插件，Harmony patch 原 Mod 的 PromptBuilder。
- 或者先写离线工具，生成最终 prompt，再手动放进原 Mod。
