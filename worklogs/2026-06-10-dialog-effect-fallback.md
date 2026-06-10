# 2026-06-10 对话效果本地兜底修复

## 任务摘要

用户在游戏内实测发现：NPC 对话已经正常生成，但辱骂、威胁类对话没有出现系统提示，也没有实际改变情分/好感。排查后确认当前 MVP 过度依赖模型输出隐藏的 `<dialog_effect>` JSON；当模型只输出短台词、不输出隐藏块时，Mod 不会执行任何情分变化。

本次修复增加本地保守兜底：当隐藏效果没有产生有效 delta 时，Mod 会根据玩家输入和 NPC 回复中的明显辱骂/威胁关键词生成负向 `insult_attack` 效果。普通寒暄和普通询问仍保持 0，不触发情分变化。

## 变更文件

- `参考mod/src/MCS_AIChatMod/MCS_AIChatMod/DialogEffectMvp.cs`
  - 新增辱骂、威胁、严重威胁关键词兜底识别。
  - 新增 `BuildFallbackEffect` 和 `ResolveEffectForApplication`。
  - 已有隐藏 JSON 产生有效 delta 时仍优先使用隐藏 JSON。
  - 新增明确 Unicode 系统反馈消息，避免反编译乱码字符串影响显示。
- `参考mod/src/MCS_AIChatMod/MCS_AIChatMod/NPCDialog.cs`
  - NPC 回复显示后，改为通过 `ResolveEffectForApplication` 解析最终效果，再执行情分修改。
- `参考mod/tests/DialogEffectMvp.Tests/Program.cs`
  - 新增测试：明显辱骂+威胁会触发 `insult_attack / major / -10`。
  - 新增测试：普通对话不会触发情分变化。

## 验证过程

- 已先运行失败测试，确认缺口为 `DialogEffectMvp.BuildFallbackEffect was not found`。
- 已运行：
  - `dotnet run --project "E:\RPG_agent\参考mod\tests\DialogEffectMvp.Tests\DialogEffectMvp.Tests.csproj"`
  - 结果：全部 DialogEffectMvp 测试通过。
- 已运行：
  - `dotnet build "E:\RPG_agent\参考mod\src\MCS_AIChatMod\MCS_AIChatMod.csproj" -c Release`
  - 结果：构建成功，0 错误，保留原反编译工程已有 7 个警告。
- 已覆盖运行时 DLL：
  - `E:\RPG_agent\参考mod\plugins\MCS_AIChatMod.dll`
  - `F:\SteamLibrary\steamapps\workshop\content\1189490\3681004564\plugins\MCS_AIChatMod.dll`
- 已校验两处 DLL SHA256 一致。

## 风险与备注

- 兜底逻辑目前只处理明显负向场景，重点覆盖辱骂/威胁；送礼、道歉、让利等正向兜底暂未实现。
- 关键词检测是保守 MVP，后续可以改成更完整的规则表或配置文件。
- 如果游戏已在运行，必须完全退出游戏并从 Steam 重新启动，新 DLL 才会被加载。
- 本次只提交源代码、测试和 worklog；运行时 DLL 本地已覆盖，但不默认提交二进制。

## 下一步

- 游戏内重新测试：从 NPC 交谈入口打开 AI 对话，输入明显辱骂/威胁语句，观察是否出现“系统：对方态度转冷：情分 -10/-3”等反馈，并检查 NPC 情分是否变化。
- 如果负向兜底稳定，再考虑正向场景：送礼、道歉、利益让渡、赞赏等。
