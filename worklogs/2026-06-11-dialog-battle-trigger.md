# 2026-06-11 对话触发战斗

## 任务摘要
- 在现有好感度分级基础上，新增严重冲突后的战斗触发。
- 触发条件为 `insult_attack / severe`，并且 NPC 回复明确宣战、拔剑、动手或提出一战。
- 触发流程为 NPC 回复、系统提示、调用标准战斗入口。
- 保留原逻辑：普通辱骂、major 威胁和 NPC 只警告/驱赶时，只扣好感，不开战。

## 变更文件
- `参考mod/plugins/prompt.txt`
- `参考mod/src/MCS_AIChatMod/MCS_AIChatMod/DialogEffectMvp.cs`
- `参考mod/src/MCS_AIChatMod/MCS_AIChatMod/NPCDialog.cs`
- `参考mod/tests/DialogEffectMvp.Tests/Program.cs`
- `参考mod/工作目录.md`

## 验证过程
- 已先新增失败测试，确认缺口为 `DialogEffectMvp.ShouldTriggerBattleAfterDialog was not found`。
- `dotnet run --project "E:\RPG_agent\参考mod\tests\DialogEffectMvp.Tests\DialogEffectMvp.Tests.csproj"`
  - 17 项测试全部通过。
- `dotnet build "E:\RPG_agent\参考mod\src\MCS_AIChatMod\MCS_AIChatMod.csproj" -c Release`
  - 构建成功，0 个警告，0 个错误。
- `git diff --check`
  - 无空白错误，仅有 Git 对 LF/CRLF 的换行提示。
- 已将 Release DLL 复制到工作区 `参考mod/plugins/MCS_AIChatMod.dll`。
  - 源文件和目标文件 SHA256 一致：`392B41EACA3673202B56AAAA2326F3EFF1BA87121C7A412C86A8DC08908F2126`。

## 风险 / 备注
- V2 只触发标准战斗，不改 NPC 死亡、任务状态或长期敌对状态。
- 只有好感度扣减成功后才尝试开战。
- 如果战斗接口失败，会记录日志并保留好感度变化。
- `prompt.txt` 只提示 NPC 可以在正文中明确表达开战意图；实际战斗仍由 Mod 本地校验，AI 不能直接执行游戏命令。

## 下一步
- 游戏内用测试存档对普通 NPC 输入严重威胁，观察 NPC 宣战后是否进入整备/战斗流程。
- 测试 NPC 只是警告或驱赶时不会开战。
