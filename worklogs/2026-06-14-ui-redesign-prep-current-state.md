# UI 大改前当前状态记录

## 背景

本次记录用于在大窗口 UI 全盘改版前固定当前进度，方便后续继续改 `参考mod/` 内的现有 AI 对话 mod。

## 已完成的主要功能

- AI 对话效果现在写入 NPC 好感度，不再写入“情分”等其他关系数值。
- 通过聊天获得的正向好感度有上限：当前好感度达到或超过 `60` 后，不再通过聊天继续增加；负向效果仍可降低好感度。
- 严重负向对话效果 `-25` 已和战斗触发视为同一事件；严重侮辱、攻击、灭门威胁等场景可以由模型建议 `-25`，再由 Mod 本地校验后触发战斗。
- 普通点击 NPC “交谈”不再自动弹出大聊天窗口；大窗口仍通过 `B` 打开，用于查看历史、配置 API 和调试。
- 原游戏交谈流程中新增“自由输入”入口；原生菜单追加不稳定时，会显示一个备用的自由输入按钮。
- 自由输入提交后会调用现有 AI 回复、历史记录、好感度变化和战斗触发逻辑。
- NPC 回复已改为优先调用原生对话表现：先尝试 Next 临时剧情事件，再回退到 Fungus `SayDialog`，最后才回退到大窗口历史。
- 原生 NPC 回复结束后，如果没有触发战斗，会继续弹出自由输入框，形成连续对话。
- 自由输入框支持 `Enter` 发送、`Esc` 退出。
- NPC 对话记忆已加入游戏内时间上下文，prompt 中会包含当前游戏时间和距上次对话的时间差，避免长期记忆像连续即时对话一样衔接。

## 当前设计决定

下一项工作是大窗口 UI 美化和结构重做。已确认采用“案牍三栏式”方向：

- 左侧导航保留：对话、角色卡、生平、推演、设置。
- “好友”页删除。
- 原来的实验性主线推演功能保留，但从右侧配置区中移出，做成独立“推演”页。
- “对话”页更像聊天记录册，而不是即时聊天窗口；底部调试输入可以保留但弱化。
- 原右侧 API 配置栏不再常驻，相关配置移动到“设置”页。
- 打赏作者入口删除。
- 小自由输入框保持接近原生人物对话框风格，不放发送按钮，继续使用 `Enter` 发送、`Esc` 退出。

## 涉及文件概览

- `AGENTS.md`：补充项目默认中文、轻量小改、默认不推送等协作规则。
- `docs/superpowers/specs/2026-06-11-immersive-free-input-dialog-design.md`：记录沉浸式自由输入 UI 设计和用户补充意见。
- `参考mod/src/MCS_AIChatMod/MCS_AIChatMod/AIChatManager.cs`：调整 NPC 交谈入口行为，避免普通交谈自动弹出大窗口，并加入对话时间上下文注入。
- `参考mod/src/MCS_AIChatMod/MCS_AIChatMod/NPCDialog.cs`：拆分大窗口发送和沉浸式发送流程，加入原生回复展示、连续输入、时间上下文和错误回退。
- `参考mod/src/MCS_AIChatMod/MCS_AIChatMod/NativeDialogPresenter.cs`：封装 Next 临时剧情事件与 `SayDialog` 的原生对话展示。
- `参考mod/src/MCS_AIChatMod/MCS_AIChatMod/OnJiaoTanBtnClickPatch.cs`：在稳定的交谈按钮路径中触发自由输入入口注入。
- `参考mod/src/MCS_AIChatMod/MCS_AIChatMod/PromptBuilder.cs`：把对话时间上下文写入初始 prompt。
- `参考mod/src/MCS_AIChatMod/MCS_AIChatMod/UIManager.cs`：新增自由输入入口、沉浸式输入框、快捷键行为和部分临时 UI 风格调整。
- `参考mod/tests/DialogEffectMvp.Tests/Program.cs`：补充好感度、战斗触发、时间上下文、原生对话行格式等测试覆盖。

## 提交前验证

- `dotnet build "E:\RPG_agent\参考mod\src\MCS_AIChatMod\MCS_AIChatMod.csproj" -c Release` 通过，`0 warning / 0 error`。
- `dotnet run --project "E:\RPG_agent\参考mod\tests\DialogEffectMvp.Tests\DialogEffectMvp.Tests.csproj"` 通过，24 项测试全部 PASS。
