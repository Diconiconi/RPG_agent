# 沉浸式自由输入对话 UI

## 完成内容

- 普通点击 NPC “交谈”不再自动弹出大聊天窗口；大窗口仍可用 `B` 打开做 API 配置和历史查看。
- 在原版交谈菜单出现后追加“自由输入”入口；原生按钮槽不足时显示一个近似原游戏风格的备用按钮。
- 点击“自由输入”后显示底部轻量输入框，玩家用鼠标左键点击“发送”提交。
- 自由输入复用现有 AI 回复、历史记录、好感度和战斗触发逻辑。
- NPC 回复优先使用原游戏 `UINPCLiaoTian.ShowTalk` 展示；不可用时回退到 `SayDialog`。
- 已同步 Release DLL 到 `参考mod/plugins/MCS_AIChatMod.dll`，未修改真实游戏安装目录。

## 验证

- `dotnet build "E:\RPG_agent\参考mod\src\MCS_AIChatMod\MCS_AIChatMod.csproj" -c Release` 通过，保留 7 个旧 warning。
- `dotnet run --project "E:\RPG_agent\参考mod\tests\DialogEffectMvp.Tests\DialogEffectMvp.Tests.csproj"` 通过，18 项测试全部 PASS。

## 注意

- 尚未进游戏实测 UI 布局和原版菜单追加是否稳定。
- 本次未提交、未推送。

## 追加排查

- 用户实测发现原版交谈菜单未出现“自由输入”。
- 确认真实 workshop mod 目录 DLL 已是新版，问题不是未覆盖 DLL。
- 日志确认 `OnJiaoTanBtnClickPatch` 能稳定触发，但原先依赖的 `OnLiaoTianClickPatch` 注入时机不稳定。
- 修复：在已确认稳定的“交谈按钮点击”路径中也启动自由输入入口注入；如果原生 `MenuDialog` 无法追加，则显示近似原游戏风格的备用按钮。
- 已重新编译，并同步到 `参考mod/plugins` 与 `F:\SteamLibrary\steamapps\workshop\content\1189490\3681004564\plugins`。
