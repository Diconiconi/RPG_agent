# 2026-06-10 - 工作文档中文规则

## 任务摘要
- 根据用户要求，恢复并更新项目顶层 `AGENTS.md`。
- 明确后续工作文档、设计文档、交接说明、worklog 和阶段总结默认使用中文。
- 保留项目安全边界、worklog/提交/推送规则和当前参考 mod 改造方向。

## 变更文件
- `AGENTS.md`
- `worklogs/2026-06-10-document-language-rule.md`

## 验证过程
- 检查根目录，确认此前没有顶层 `.md` 文件。
- 检查 `git status --short --ignored`，确认只有被忽略的本地参考二进制。
- 检查 `git remote -v`，确认远程地址为 `https://github.com/Diconiconi/RPG_agent.git`。

## 风险 / 备注
- 这是项目规则和文档偏好变更，不涉及 DLL、游戏目录或运行时存档。
- 后续若必须引用英文错误日志或代码标识，应保留英文原文并补充中文解释。

## 下一步
- 后续新设计文档和 worklog 默认使用中文。
- 如果已有英文 worklog 需要统一风格，可以后续单独整理。
