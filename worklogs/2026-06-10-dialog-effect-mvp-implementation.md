# 2026-06-10 对话情分 MVP 实现

## 任务摘要

- 基于 `参考mod/plugins/MCS_AIChatMod.dll` 反编译出可编译源码，并在原 mod 对话流程中加入“隐藏对话效果 JSON -> 校验 -> 修改当前 NPC 情分”的 MVP。
- 普通对话默认不改情分；只有送礼、利益变化、辱骂攻击、信任破裂、明显情绪变化等事件会按分档修改情分。
- 第一版只执行情分变化和提示，不触发战斗、任务、传送、发物品、杀死 NPC 等后续行为。

## 变更文件

- `参考mod/src/MCS_AIChatMod/`
  - 反编译出的 `MCS_AIChatMod` 可编辑源码。
  - 新增 `DialogEffectMvp.cs`，负责解析 `<dialog_effect>`、校验分档、调用 `NPCEx.AddQingFen(...)`。
  - 修改 `NPCDialog.cs`，在 AI 回复显示和写入历史前剥离隐藏 JSON，并在 NPC 台词显示后应用情分变化。
  - 修改 `MCS_AIChatMod.csproj`，补齐 Unity、BepInEx、Harmony、System.Text.Json 等编译引用。
  - 修改 `AIChatManager.cs` 中反编译导致的 `Logger` 受保护成员访问问题，使源码可编译。
- `参考mod/tests/DialogEffectMvp.Tests/`
  - 新增最小测试运行器，覆盖隐藏块剥离、重复隐藏块剥离、分档裁剪、低置信度拒绝、`impact_level` 大小写兼容。
- `参考mod/plugins/prompt.txt`
  - 追加隐藏对话效果协议，要求 AI 在台词后输出 `<dialog_effect>` JSON。
- `scripts/restore-bepinex-refs.ps1`
  - 新增本地编译依赖恢复脚本，下载 BepInEx 5.4.17，并校验 `BepInEx.dll` 与 `0Harmony.dll` 版本。

## 验证过程

- 安装并验证 `ilspycmd`：
  - 最新 `ilspycmd` 包在当前 .NET 8 工具链下安装失败。
  - 改用 `ilspycmd 9.1.0.7988` 后成功反编译。
- 确认游戏情分 API：
  - `CmdAddNPCQingFen.OnEnter()` 内部调用为 `NPCEx.AddQingFen(NPCID.Value, Count, showTip: true)`。
- TDD 验证：
  - 先写测试并确认失败：`MCS_AIChatMod.DialogEffectMvp type was not found`。
  - 实现后运行：`dotnet run --project "E:\RPG_agent\参考mod\tests\DialogEffectMvp.Tests\DialogEffectMvp.Tests.csproj"`，全部通过。
  - 新增 `Major` 混合大小写测试，先确认失败，再改为大小写不敏感分档表，随后通过。
  - 新增重复 `<dialog_effect>` 块测试，先确认第二个隐藏块会泄漏，再改为剥离全部隐藏块，随后通过。
- 编译验证：
  - `dotnet build "E:\RPG_agent\参考mod\src\MCS_AIChatMod\MCS_AIChatMod.csproj" -c Release`
  - 结果：生成成功，0 个错误；剩余 7 个警告来自反编译旧源码中的 nullable/未使用变量提示。
- 部署验证：
  - 已将 Release 输出 DLL 替换到 `参考mod/plugins/MCS_AIChatMod.dll`。
  - 原 DLL 已备份为 `参考mod/plugins/MCS_AIChatMod.dll.pre-dialog-effect.bak`，该备份不会提交。
  - 确认部署 DLL 中存在 `MCS_AIChatMod.DialogEffectMvp`。
  - 确认部署 DLL 引用版本保持与原 DLL 一致：`BepInEx 5.4.17.0`、`0Harmony 2.5.5.0`。

## 风险与备注

- 尚未做游戏内实测；需要用户进游戏用测试存档验证普通对话、送礼/利益正向事件、辱骂攻击负向事件。
- 当前提示会调用游戏原生 `NPCEx.AddQingFen(..., showTip: true)`，同时在聊天窗口显示一条系统提示；如果后续觉得提示重复，可以保留其中一种。
- `.tools/` 中的 BepInEx 编译依赖是本地缓存，不会提交。新机器或新模型接手时先运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\restore-bepinex-refs.ps1
```

- `参考mod/src/GameAssembly/` 只用于临时确认游戏 API，未提交完整游戏程序集反编译源码，避免仓库膨胀。

## 下一步

- 进游戏测试三类输入：
  - 普通寒暄：不显示情分变化。
  - 送礼/让利：情分增加。
  - 辱骂/攻击：情分降低，不触发战斗。
- 若游戏内验证通过，再考虑把 `anger_delta`、`trigger_candidates` 扩展成后续行为候选框架。
