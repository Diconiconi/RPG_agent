# 2026-06-10 - 提交 AGENTS 规则措辞修改

## 任务摘要
- 根据用户要求，将当前 `AGENTS.md` 的本地修改纳入 Git 提交并推送。
- 本次修改简化了 “Worklog 与 Git 上传规则” 中重要项目操作的描述。

## 变更文件
- `AGENTS.md`
- `worklogs/2026-06-10-agents-rule-wording.md`

## 验证过程
- 运行 `git status --short --ignored` 检查工作区状态。
- 运行 `git diff -- AGENTS.md` 确认 `AGENTS.md` 只有一处措辞变更。
- 运行 `git remote -v` 确认远程地址为 `https://github.com/Diconiconi/RPG_agent.git`。

## 风险 / 备注
- 本次只提交项目规则文档和对应 worklog。
- 不修改参考 mod、DLL、真实游戏目录或存档。
- 被忽略的本地参考二进制继续保留在工作区，不纳入提交。

## 下一步
- 后续继续按 `AGENTS.md` 中的中文文档和上传规则执行。
