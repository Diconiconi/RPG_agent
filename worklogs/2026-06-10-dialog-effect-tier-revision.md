# 2026-06-10 - 对话效果分档设计修订

## 任务摘要
- 根据用户 review 修订对话效果机制设计文档。
- 将“每轮小幅情分变化”改为“普通对话不变，有明确事件才触发情分变化”。
- 将单一 `-3..+3` 上限改为按事件类型和强度分档处理，为后续对话触发具体游戏行为建立框架。

## 变更文件
- `参考mod/docs/superpowers/specs/2026-06-10-dialog-effect-design.md`
- `worklogs/2026-06-10-dialog-effect-tier-revision.md`

## 验证过程
- 读取 `AGENTS.md`，确认工作文档默认使用中文。
- 读取现有设计文档，定位原先“每轮变化”和 `-3..+3` 全局上限的问题。
- 检查 `git status --short --ignored` 和 `git remote -v`。
- 修订后执行格式和一致性检查。

## 风险 / 备注
- 本次只修改设计文档和 worklog，不修改 DLL、真实游戏目录或运行时存档。
- 工作区中 `AGENTS.md` 已存在一处未提交修改，本次不回退、不覆盖，提交时会单独避开。
- 分档范围仍需后续结合游戏原始情分尺度验证，尤其是 `major` 和 `severe` 档。

## 下一步
- 请用户 review 修订后的设计文档。
- 设计通过后，再进入 `superpowers:writing-plans` 实现计划阶段。
