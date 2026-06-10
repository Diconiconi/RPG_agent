# 2026-06-10 对话效果改为好感度并增加聊天上限

## 任务摘要

- 将对话效果 MVP 的真实写入目标从 NPC 关系数值旧通道改为游戏内 NPC 好感度。
- 运行时执行改为 `NPCEx.GetFavor` + `NPCEx.AddFavor`，不再调用 `NPCEx.AddQingFen`。
- 增加“通过聊天获得的正向好感度最高到 60”规则：当前好感度达到或超过 60 后，聊天正向增益归零；接近 60 时只加到 60；负向对话效果仍可降低好感度。
- 同步更新 prompt、AGENTS、工作目录、设计文档和实现计划中的当前口径。

## 变更文件

- `参考mod/src/MCS_AIChatMod/MCS_AIChatMod/DialogEffectMvp.cs`
  - 新增 `ChatFavorGainCap = 60` 和 `ApplyChatFavorGainCap(...)`。
  - `TryApply(...)` 对正向 delta 先读取当前好感度并执行 60 封顶。
  - 执行层从 `NPCEx.AddQingFen(...)` 改为 `NPCEx.AddFavor(...)`。
  - 系统提示和日志改为“好感度”。
- `参考mod/tests/DialogEffectMvp.Tests/Program.cs`
  - 新增 3 个封顶测试：58 + 8 只加 2、60 + 8 不增加、60 - 10 仍降低。
- `参考mod/plugins/prompt.txt`
  - 将隐藏效果协议中文口径改为好感度。
  - 增加聊天正向好感度 60 封顶说明。
- `AGENTS.md`
  - 更新当前项目方向，记录好感度和 60 上限规则。
- `参考mod/工作目录.md`
  - 更新当前方向、接口、校验规则和后续接手说明。
- `参考mod/docs/superpowers/specs/2026-06-10-dialog-effect-design.md`
  - 将 V1 设计口径改为好感度，并补充 60 封顶规则。
- `参考mod/docs/superpowers/plans/2026-06-10-dialog-effect-implementation.md`
  - 将实现计划里的执行接口和完成标准更新为好感度。

## 验证过程

- 先新增失败测试并运行：
  - `dotnet run --project "E:\RPG_agent\参考mod\tests\DialogEffectMvp.Tests\DialogEffectMvp.Tests.csproj"`
  - 预期失败：`DialogEffectMvp.ApplyChatFavorGainCap was not found.`
- 实现后重新运行：
  - `dotnet run --project "E:\RPG_agent\参考mod\tests\DialogEffectMvp.Tests\DialogEffectMvp.Tests.csproj"`
  - 结果：全部 DialogEffectMvp 测试通过。
- Release 构建：
  - `dotnet build "E:\RPG_agent\参考mod\src\MCS_AIChatMod\MCS_AIChatMod.csproj" -c Release`
  - 结果：构建成功，0 错误，保留原反编译工程已有 7 个警告。
- 本地 workspace DLL：
  - 已将 Release 输出覆盖到 `E:\RPG_agent\参考mod\plugins\MCS_AIChatMod.dll`。
  - 已校验输出 DLL 与 workspace 插件 DLL 的 SHA256 一致。
- 文档口径检查：
  - 活跃文件中未再发现 `情分`、`QingFen`、`AddQingFen`、`CmdAddNPCQingFen`、`CmdGetNPCQingFen` 残留。

## 风险 / 备注

- 本次未覆盖 Steam 安装目录或 Workshop 目录，只更新了 workspace 内的运行 DLL。
- 尚未做游戏内实测；需要用户把新版 DLL 放入实际加载的 mod 目录后，用测试存档确认好感度读写和 60 封顶表现。
- 负向效果不受 60 上限限制，这是用户确认过的行为。
- `favor_delta` 字段名继续保留，因为现有代码和任务系统中 `favor` 对应游戏好感度。

## 下一步

- 游戏内验证三类场景：
  - 当前好感度低于 60 时，正向对话增加好感度。
  - 当前好感度接近或达到 60 时，正向对话最多加到 60，达到后不再增加。
  - 辱骂、威胁等负向对话仍可降低好感度。
