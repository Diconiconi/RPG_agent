# 2026-06-10 自动加载 NPC 初始角色卡

## 任务摘要
- 在运行目录角色卡不存在时，从 `RPGSkillKit/character_cards_minimal` 自动初始化 NPC 角色卡。
- 保留原逻辑：运行目录已有角色卡时优先读取，不覆盖玩家手动编辑。
- 将 NPC prompt 中的角色卡、关系定位、双方境界和境界礼法提升到最高优先块。
- 顺手按用户要求简化 `AGENTS.md`，明确后续默认使用“中等变更模式”。

## 变更文件
- `AGENTS.md`
- `参考mod/docs/superpowers/plans/2026-06-10-auto-load-character-cards.md`
- `参考mod/src/MCS_AIChatMod/MCS_AIChatMod/PromptBuilder.cs`
- `参考mod/tests/DialogEffectMvp.Tests/Program.cs`
- `参考mod/工作目录.md`

## 验证过程
- `dotnet run --project "E:\RPG_agent\参考mod\tests\DialogEffectMvp.Tests\DialogEffectMvp.Tests.csproj"`
  - 14 项测试全部通过。
- `dotnet build "E:\RPG_agent\参考mod\src\MCS_AIChatMod\MCS_AIChatMod.csproj" -c Release`
  - 构建成功，0 个警告，0 个错误。
- `git diff --check`
  - 无空白错误，仅有 Git 对 LF/CRLF 的换行提示。
- 已将 Release DLL 复制到工作区 `参考mod/plugins/MCS_AIChatMod.dll`。
  - 源文件和目标文件 SHA256 一致：`C56405EF48FF4BB997DD47C6D025FC5ECBE0ADC1F79486B596D3FAB9D8D40FE8`。

## 风险 / 备注
- 只加载 `.character.txt` 草稿卡，不解析 JSON 角色卡，也不引入完整 RPGSkillKit 架构。
- 特殊 NPC 名称映射目前只覆盖：星铃儿/星凝、魏无极、吞云大圣、火麒麟、冲虚、白帝。
- 本次只复制工作区参考 mod DLL，没有修改 Steam 游戏目录或 LocalLow 存档目录。
- Release 构建输出仍提示可考虑用 app.config 处理 `System.Runtime.CompilerServices.Unsafe` 版本重映射；本次构建未报错。

## 下一步
- 游戏内找魏无极、倪旭欣等 NPC 验证运行目录角色卡自动生成。
- 测试低境界 NPC 面对高境界玩家或前辈时，语气是否更克制、敬重、谨慎。
- 继续观察好感度对话效果：普通对话不变，正向聊天最多到 60，负向仍可降低。
