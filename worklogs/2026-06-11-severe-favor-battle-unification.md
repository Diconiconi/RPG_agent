# 2026-06-11 severe/-25 与战斗触发统一

## 任务摘要

用户游戏内实测发现：即使已经出现“好感度 -25”的严重冲突提示，NPC 回复“来吧”等较短表达时仍没有进入战斗。根因是上一版战斗触发还额外依赖 NPC 回复中的“开战、拔剑、一战”等关键词，覆盖范围过窄。

本次调整将 `insult_attack / severe / -25` 与战斗触发视为同一事件：只要好感度扣减成功，Mod 就尝试触发标准战斗，不再要求 NPC 回复正文命中特定宣战关键词。同时扩大本地 severe 兜底范围，把严重诋毁、师门/家族/正邪立场污蔑、身败名裂等严重冲突纳入 `severe/-25`。

## 变更文件

- `参考mod/src/MCS_AIChatMod/MCS_AIChatMod/DialogEffectMvp.cs`
  - `ShouldTriggerBattleAfterDialog(...)` 改为只校验最终效果是否为 `insult_attack / severe` 且实际扣减达到 `-25`。
  - 移除不再使用的战斗宣战/拒战关键词数组。
  - 新增 `FallbackSevereConflictKeywords`，覆盖严重诋毁和羞辱类场景。
  - 本地兜底检测到严重冲突时生成 `insult_attack / severe / -25`。
- `参考mod/tests/DialogEffectMvp.Tests/Program.cs`
  - 更新测试：`severe/-25` 不需要宣战关键词也应触发战斗。
  - 更新测试：身败名裂类威胁升级为 `severe/-25`。
  - 新增测试：勾结魔道、欺师灭祖、毁全族名声等严重诋毁触发 `severe/-25`。
- `参考mod/plugins/prompt.txt`
  - 将 prompt 口径改为：`severe/-25` 代表严重冲突事件，Mod 会本地校验并尝试触发标准战斗。
  - 补充 severe 适用范围：致命威胁、极严重侮辱、恶意诋毁、师门/家族/正邪立场羞辱或污蔑。
- `参考mod/工作目录.md`
  - 更新当前战斗触发规则，删除“必须命中特定宣战关键词”的旧描述。

## 验证过程

- 先运行失败测试：
  - `dotnet run --project "E:\RPG_agent\参考mod\tests\DialogEffectMvp.Tests\DialogEffectMvp.Tests.csproj"`
  - 预期失败：
    - `severe/-25` 未命中特定关键词时不触发战斗。
    - 严重诋毁没有生成 `insult_attack / severe / -25`。
- 实现后重新运行：
  - `dotnet run --project "E:\RPG_agent\参考mod\tests\DialogEffectMvp.Tests\DialogEffectMvp.Tests.csproj"`
  - 18 项测试全部通过。
- Release 构建：
  - `dotnet build "E:\RPG_agent\参考mod\src\MCS_AIChatMod\MCS_AIChatMod.csproj" -c Release`
  - 构建成功，0 错误，保留原反编译工程已有 7 个警告。
- 已将 Release DLL 覆盖到：
  - `E:\RPG_agent\参考mod\plugins\MCS_AIChatMod.dll`
  - `F:\SteamLibrary\steamapps\workshop\content\1189490\3681004564\plugins\MCS_AIChatMod.dll`
- 已校验三处 DLL SHA256 一致：
  - `D1083B0F07A1DD6C21F5F5C7F89C9AD7C104E7501F66915D6B758490FA1FD7D9`

## 风险 / 备注

- 现在 `severe/-25` 会直接进入战斗触发流程，因此 severe 档必须谨慎使用。
- 普通辱骂和一般威胁仍应停留在 `moderate/-3` 或 `major/-10`，只扣好感不开战。
- 战斗接口失败时仍会记录日志并保留好感度扣减。
- 当前仍只触发标准战斗，不改 NPC 死亡、任务状态或长期敌对状态。

## 下一步

- 游戏内重新测试刚才场景：出现“好感度 -25”后应直接进入战斗流程。
- 测试普通辱骂/一般威胁仍只扣好感，不开战。
- 继续观察 severe 词库是否过宽或过窄，再根据实测微调。
