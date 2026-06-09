# 2026-06-10 - 对话效果机制实现计划

## 任务摘要
- 根据已通过的对话效果机制 V1 设计文档，写入详细实现计划。
- 计划明确需要先安装 `.NET SDK 8` 和 `ilspycmd`，再从 `MCS_AIChatMod.dll` 导出可编辑源码。
- 计划将实现拆成工具链准备、核心离线测试、prompt 协议、mod 源码接入、游戏情分执行、游戏内验证几个阶段。

## 变更文件
- `参考mod/docs/superpowers/plans/2026-06-10-dialog-effect-implementation.md`
- `worklogs/2026-06-10-dialog-effect-implementation-plan.md`

## 验证过程
- 读取 `AGENTS.md`，确认项目文档默认中文、远程提交规则和安全边界。
- 读取 `参考mod/docs/superpowers/specs/2026-06-10-dialog-effect-design.md`，确认设计已包含事件化触发和分档情分规则。
- 检查当前环境，确认当前只有 .NET Runtime，没有 .NET SDK、`csc`、`mcs`、`ilspycmd`。
- 通过只读反射确认候选入口：`NPCDialog.OnUISendMessage`、`UIManager.AppendMessage`、`OfficialNpcFavorChangedPatch`、`NPCInfo`、`NPCChatTools`。
- 通过只读反射确认游戏侧候选命令：`CmdAddNPCQingFen` 有字段 `NPCID`、`Count` 和方法 `OnEnter()`。

## 风险 / 备注
- 本次只写计划，不修改 DLL、真实游戏目录或存档。
- 当前 `AGENTS.md` 已有一处未提交修改，本次不纳入提交。
- 由于源码需从 DLL 导出，真正执行计划时第一步必须安装工具并确认导出质量。
- 游戏侧情分修改仍需在执行 Task 8 时根据反编译结果确认，不能凭空假设命令调用方式。

## 下一步
- 用户选择执行方式：`Subagent-Driven` 或 `Inline Execution`。
- 如果开始执行，先安装 `.NET SDK 8` 和 `ilspycmd`，再导出参考 mod 源码。
