# RPG_agent 项目说明

本文件适用于 `E:\RPG_agent` 项目。后续 Codex 或其他模型接手时，应优先阅读并遵守这里的规则。

## 文档语言规则

- 工作文档、设计文档、交接说明、worklog、阶段总结、风险记录等，默认使用中文书写。
- 变量名、类名、方法名、文件名、协议字段、命令、报错原文、Git 提交信息等可以保留英文。
- 如果需要引用英文日志或错误信息，应尽量补充中文解释，方便用户快速理解。
- 面向用户的完成报告也优先使用中文，除非用户明确要求英文。

## 当前项目方向

- 不另建独立自有 mod 工程，优先直接基于 `参考mod` 改善现有 AI 对话 mod。
- 工作区主体是 `参考mod/`；后续开发优先研究 `参考mod/plugins/MCS_AIChatMod.dll` 及其周边配置。
- 已确认的近期方向是“对话效果机制 V1”：AI 只建议效果，Mod 校验执行，小幅修改游戏内 NPC 情分，并显示简短提示。
- 战斗触发、任务分支、强剧情化状态变化属于后续阶段，不应在 V1 中贸然实现。

## Worklog 与 Git 上传规则

对于重要项目操作，例如完成有意义的功能、修改 mod 工作流、增加项目结构、更新 prompt/card 系统、写入设计文档，或任何应该长期保存的变更：

1. 在最终用户报告前，先写入或更新 worklog。
   - worklog 放在 `worklogs/`。
   - 日期使用 Asia/Shanghai。
   - 内容包括：任务摘要、变更文件、验证过程、风险/备注、下一步。
   - worklog 默认使用中文。
2. 提交前检查 Git 状态：
   - 运行 `git status --short`。
   - 审查变更文件，避免提交无关用户变更。
   - 不要提交 API key、认证文件、游戏存档、运行日志、本地游戏文件或复制来的游戏二进制。
3. 确认仓库远程地址是：
   - `https://github.com/Diconiconi/RPG_agent.git`
4. worklog 写完并检查无误后，提交项目版本。
5. 推送提交到远程仓库。
6. 如果因为认证、权限或网络问题导致 push 失败，报告准确阻塞原因，并把提交保留在本地。

纯聊天回答、只读研究、小型诊断且不改项目文件时，不需要执行 worklog/提交/推送流程。

## 安全边界

- 除非用户明确要求，不要修改真实游戏安装文件。
- 默认把 `F:\SteamLibrary\steamapps\common\觅长生` 和其他 Steam 游戏目录视为只读。
- 默认把 `C:\Users\22749\AppData\LocalLow\yusuiInc\觅长生` 视为运行时/存档数据；不要提交其中内容。
- 保持原始参考 mod 文件完整，除非用户明确批准修改。
- `参考mod` 中的 DLL、PDB、XML、BIN 等参考二进制可以本地保留，但默认不要提交到 GitHub。

## 推荐 Git 流程

```powershell
git status --short
git remote -v
git add <需要提交的文件>
git diff --cached --name-status
git commit -m "简短提交信息"
git push origin main
```

提交信息可以使用英文或中文；如果使用英文，最终报告中应附带中文说明。
