# Dialog Battle Trigger Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在现有对话效果分级基础上，新增“严重冲突 + NPC 明确宣战回复”后自动触发与该 NPC 战斗。

**Status:** 已于 2026-06-11 执行完成。实现保持原好感度分级不变，只增加本地战斗触发判定与标准战斗调用；实际 prompt 提示补充在 `参考mod/plugins/prompt.txt`。

**Architecture:** 保持现有 `DialogEffectMvp` 流程不重做：玩家输入和 NPC 可见回复仍先解析成对话效果，并照旧执行好感度扣减。只新增一个战斗触发判定层：当效果为 `insult_attack / severe` 且 NPC 回复包含明确开战意图时，在好感度应用后显示系统提示并调用游戏已有 `StartFight.Do(...)`。`NPCDialog.OnUISendMessage(...)` 只多接收 `TryApply(...)` 的结果，并在同一轮最多尝试一次战斗。

**Tech Stack:** C#、Unity/Mono、BepInEx、Fungus `StartFight.Do(...)`、现有 `DialogEffectMvp.Tests` 轻量测试工程。

---

## Scope

本计划只做战斗触发层，不重新设计好感度分级：

- 保留现有辱骂/威胁分级：
  - moderate: `-3`
  - major: `-10`
  - severe: `-25`
- 只有 `insult_attack / severe` 且 NPC 回复明确宣战时才触发战斗。
- 普通辱骂、普通威胁、NPC 只是警告/生气/让玩家离开时，不触发战斗。
- 战斗触发前先执行好感度骤减；只有好感度效果成功应用后才进入战斗尝试。
- 战斗前显示系统提示：
  `对方怒意已决，好感度 -25，即将进入战斗。`
- 不增加确认按钮，因为游戏已有开战前整备环节。
- 不修改任务系统、剧情系统、NPC 死亡/敌对状态、真实游戏安装目录。

## File Structure

- Modify: `参考mod/src/MCS_AIChatMod/MCS_AIChatMod/DialogEffectMvp.cs`
  - 增加战斗触发判定 helper。
  - 让 `TryApply(...)` 返回本轮实际应用的好感度 delta。
  - 增加 `TryTriggerBattleAfterDialog(...)`，负责系统提示和 `StartFight.Do(...)`。
- Modify: `参考mod/src/MCS_AIChatMod/MCS_AIChatMod/NPCDialog.cs`
  - 在 NPC 回复显示、好感度应用后，调用战斗触发 helper。
- Modify: `参考mod/tests/DialogEffectMvp.Tests/Program.cs`
  - 增加纯函数测试：宣战关键词命中、非宣战不命中、只有 severe insult 才允许战斗。
- Modify: `参考mod/工作目录.md`
  - 记录 V2 战斗触发规则。
- Add: `worklogs/YYYY-MM-DD-dialog-battle-trigger.md`
  - 实现后记录任务摘要、变更文件、验证和风险。

## Task 1: Add Failing Tests For Battle Trigger Eligibility

**Files:**
- Modify: `参考mod/tests/DialogEffectMvp.Tests/Program.cs`

- [ ] **Step 1: Add test registrations**

在 `Main()` 现有测试注册区，追加到好感度测试附近：

```csharp
failed += Run("Dialog battle trigger detects clear challenge reply", DialogBattleTriggerDetectsClearChallengeReply);
failed += Run("Dialog battle trigger ignores warning-only reply", DialogBattleTriggerIgnoresWarningOnlyReply);
failed += Run("Dialog battle trigger requires severe insult attack", DialogBattleTriggerRequiresSevereInsultAttack);
```

- [ ] **Step 2: Add failing test methods**

在现有测试方法区追加：

```csharp
private static void DialogBattleTriggerDetectsClearChallengeReply()
{
    object effect = CreateEffect("insult_attack", "severe", -25, 0.95);
    bool shouldTrigger = ShouldTriggerBattleAfterDialog("今日便与你一战，拔剑吧！", effect, -25);

    AssertEqual(true, shouldTrigger, "clear challenge should trigger battle");
}

private static void DialogBattleTriggerIgnoresWarningOnlyReply()
{
    object effect = CreateEffect("insult_attack", "severe", -25, 0.95);
    bool shouldTrigger = ShouldTriggerBattleAfterDialog("滚吧，我今日不与你计较。", effect, -25);

    AssertEqual(false, shouldTrigger, "warning-only reply should not trigger battle");
}

private static void DialogBattleTriggerRequiresSevereInsultAttack()
{
    object effect = CreateEffect("insult_attack", "major", -10, 0.95);
    bool shouldTrigger = ShouldTriggerBattleAfterDialog("既如此，战吧。", effect, -10);

    AssertEqual(false, shouldTrigger, "major insult should not trigger battle");
}
```

- [ ] **Step 3: Add reflection helpers**

在 helper 区追加：

```csharp
private static object CreateEffect(string effectType, string impactLevel, int favorDelta, double confidence)
{
    Type type = GetDialogEffectType();
    Type effectTypeObject = type.GetNestedType("Effect", BindingFlags.NonPublic);
    if (effectTypeObject == null)
    {
        throw new InvalidOperationException("DialogEffectMvp.Effect type was not found.");
    }

    object effect = Activator.CreateInstance(effectTypeObject);
    SetField(effect, "EffectType", effectType);
    SetField(effect, "ImpactLevel", impactLevel);
    SetField(effect, "FavorDelta", favorDelta);
    SetField(effect, "Confidence", confidence);
    return effect;
}

private static bool ShouldTriggerBattleAfterDialog(string npcReply, object effect, int appliedDelta)
{
    Type type = GetDialogEffectType();
    MethodInfo method = type.GetMethod("ShouldTriggerBattleAfterDialog", BindingFlags.Static | BindingFlags.NonPublic);
    if (method == null)
    {
        throw new InvalidOperationException("DialogEffectMvp.ShouldTriggerBattleAfterDialog was not found.");
    }

    return (bool)method.Invoke(null, new object[] { npcReply, effect, appliedDelta });
}

private static void SetField<T>(object target, string name, T value)
{
    FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    if (field == null)
    {
        throw new InvalidOperationException("Field was not found: " + name);
    }

    field.SetValue(target, value);
}
```

- [ ] **Step 4: Run tests and verify red**

Run:

```powershell
dotnet run --project "E:\RPG_agent\参考mod\tests\DialogEffectMvp.Tests\DialogEffectMvp.Tests.csproj"
```

Expected:

```text
FAIL Dialog battle trigger detects clear challenge reply: DialogEffectMvp.ShouldTriggerBattleAfterDialog was not found.
FAIL Dialog battle trigger ignores warning-only reply: DialogEffectMvp.ShouldTriggerBattleAfterDialog was not found.
FAIL Dialog battle trigger requires severe insult attack: DialogEffectMvp.ShouldTriggerBattleAfterDialog was not found.
```

## Task 2: Implement Battle Trigger Eligibility In DialogEffectMvp

**Files:**
- Modify: `参考mod/src/MCS_AIChatMod/MCS_AIChatMod/DialogEffectMvp.cs`

- [ ] **Step 1: Add battle reply keyword arrays**

在 `FallbackSevereThreatKeywords` 后追加：

```csharp
private static readonly string[] BattleChallengeKeywords = new[]
{
    "开战",
    "战吧",
    "一战",
    "与你一战",
    "与我一战",
    "拔剑",
    "亮剑",
    "动手",
    "出手",
    "领教",
    "讨教",
    "休怪我",
    "休怪在下",
    "今日便教训你",
    "今日便让你",
    "手底下见真章",
    "剑下见真章"
};

private static readonly string[] BattleRefusalKeywords = new[]
{
    "不与你计较",
    "懒得动手",
    "不屑动手",
    "不愿动手",
    "今日饶你",
    "滚吧",
    "退下",
    "莫要再说",
    "休要再言"
};
```

- [ ] **Step 2: Add pure trigger helper**

在 `ResolveEffectForApplication(...)` 后追加：

```csharp
private static bool ShouldTriggerBattleAfterDialog(string npcVisibleReply, Effect effect, int appliedDelta)
{
    if (effect == null || appliedDelta > -25)
    {
        return false;
    }
    if (!string.Equals(effect.EffectType, "insult_attack", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }
    if (!string.Equals(effect.ImpactLevel, "severe", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }
    string rawReply = (npcVisibleReply ?? string.Empty).ToLowerInvariant();
    string compactReply = RemoveWhitespace(rawReply);
    if (CountKeywordHits(rawReply, compactReply, BattleRefusalKeywords) > 0)
    {
        return false;
    }
    return CountKeywordHits(rawReply, compactReply, BattleChallengeKeywords) > 0;
}
```

- [ ] **Step 3: Run tests and verify green**

Run:

```powershell
dotnet run --project "E:\RPG_agent\参考mod\tests\DialogEffectMvp.Tests\DialogEffectMvp.Tests.csproj"
```

Expected:

```text
PASS Dialog battle trigger detects clear challenge reply
PASS Dialog battle trigger ignores warning-only reply
PASS Dialog battle trigger requires severe insult attack
All DialogEffectMvp tests passed.
```

## Task 3: Return Applied Favor Delta From TryApply

**Files:**
- Modify: `参考mod/src/MCS_AIChatMod/MCS_AIChatMod/DialogEffectMvp.cs`
- Modify: `参考mod/src/MCS_AIChatMod/MCS_AIChatMod/NPCDialog.cs`

- [ ] **Step 1: Change TryApply signature**

把：

```csharp
internal static bool TryApply(int npcId, string npcName, Effect effect)
```

替换为：

```csharp
internal static bool TryApply(int npcId, string npcName, Effect effect, out int appliedDelta)
```

并在方法开头加入：

```csharp
appliedDelta = 0;
```

- [ ] **Step 2: Set appliedDelta only after AddFavor succeeds**

在 `TryApply(...)` 中，找到：

```csharp
ShowFeedbackTip(delta);
AIChatManager.logger?.LogInfo((object)("[DialogEffectMvp] 已应用对话效果: npcId=" + npcId + ", npcName=" + npcName + ", delta=" + delta + ", type=" + effect.EffectType + ", reason=" + effect.Reason));
return true;
```

替换为：

```csharp
appliedDelta = delta;
ShowFeedbackTip(delta);
AIChatManager.logger?.LogInfo((object)("[DialogEffectMvp] 已应用对话效果: npcId=" + npcId + ", npcName=" + npcName + ", delta=" + delta + ", type=" + effect.EffectType + ", reason=" + effect.Reason));
return true;
```

- [ ] **Step 3: Update NPCDialog call site**

在 `NPCDialog.OnUISendMessage(...)` 中，把：

```csharp
DialogEffectMvp.TryApply(currentNpcId, speakerName, effect);
```

替换为：

```csharp
DialogEffectMvp.TryApply(currentNpcId, speakerName, effect, out int appliedDelta);
DialogEffectMvp.TryTriggerBattleAfterDialog(currentNpcId, speakerName, aiResponse, effect, appliedDelta);
```

- [ ] **Step 4: Run tests**

Run:

```powershell
dotnet run --project "E:\RPG_agent\参考mod\tests\DialogEffectMvp.Tests\DialogEffectMvp.Tests.csproj"
```

Expected:

```text
All DialogEffectMvp tests passed.
```

At this point `TryTriggerBattleAfterDialog(...)` is not implemented yet, so the project build should fail until Task 4. Do not stop here.

## Task 4: Implement Battle Trigger Side Effect

**Files:**
- Modify: `参考mod/src/MCS_AIChatMod/MCS_AIChatMod/DialogEffectMvp.cs`
- Modify: `参考mod/src/MCS_AIChatMod/MCS_AIChatMod/NPCDialog.cs`

- [ ] **Step 1: Add Fungus using**

在 `DialogEffectMvp.cs` 顶部 using 区追加：

```csharp
using Fungus;
```

- [ ] **Step 2: Add public internal trigger method**

在 `ShouldTriggerBattleAfterDialog(...)` 后追加：

```csharp
internal static bool TryTriggerBattleAfterDialog(int npcId, string npcName, string npcVisibleReply, Effect effect, int appliedDelta)
{
    if (npcId <= 0 || !ShouldTriggerBattleAfterDialog(npcVisibleReply, effect, appliedDelta))
    {
        return false;
    }
    ShowBattleTriggerTip(appliedDelta);
    try
    {
        if (Tools.instance?.getPlayer() == null)
        {
            AIChatManager.logger?.LogWarning((object)("[DialogEffectMvp] 战斗触发失败：玩家实例不存在, npcId=" + npcId));
            return false;
        }
        StartFight.Do(npcId, 1, StartFight.MonstarType.Normal, StartFight.FightEnumType.Normal, 0, 0, 0, 0, "战斗3", SeaRemoveNPCFlag: false, string.Empty, new List<StarttFightAddBuff>(), new List<StarttFightAddBuff>());
        AIChatManager.logger?.LogInfo((object)("[DialogEffectMvp] 已由对话触发战斗: npcId=" + npcId + ", npcName=" + npcName + ", delta=" + appliedDelta));
        return true;
    }
    catch (Exception ex)
    {
        AIChatManager.logger?.LogWarning((object)("[DialogEffectMvp] 战斗触发异常: npcId=" + npcId + ", " + ex.Message));
        return false;
    }
}
```

- [ ] **Step 3: Add battle tip helper**

在 `ShowFeedbackTip(...)` 后追加：

```csharp
private static void ShowBattleTriggerTip(int delta)
{
    string message = "对方怒意已决，好感度 " + delta + "，即将进入战斗。";
    UIManager.Instance?.AppendMessage("系统", message, isPlayer: false, NPCDialog.GetCurrentGameTimeTextOrDefault(), string.Empty, null);
}
```

- [ ] **Step 4: Add a no-feedback apply path for battle rounds**

在 `TryApply(...)` 后追加：

```csharp
internal static bool TryApplyWithoutFeedback(int npcId, string npcName, Effect effect, out int appliedDelta)
{
    return TryApplyInternal(npcId, npcName, effect, showFeedback: false, out appliedDelta);
}
```

然后把原 `TryApply(...)` 方法体改成：

```csharp
internal static bool TryApply(int npcId, string npcName, Effect effect, out int appliedDelta)
{
    return TryApplyInternal(npcId, npcName, effect, showFeedback: true, out appliedDelta);
}
```

把原 `TryApply(...)` 的旧方法体移动到新 helper 中：

```csharp
private static bool TryApplyInternal(int npcId, string npcName, Effect effect, bool showFeedback, out int appliedDelta)
{
    appliedDelta = 0;
    int requestedDelta = ValidateAndClamp(effect);
    if (npcId <= 0 || requestedDelta == 0)
    {
        return false;
    }
    int delta = requestedDelta;
    if (requestedDelta > 0)
    {
        if (!TryGetNpcFavor(npcId, out int currentFavor))
        {
            return false;
        }
        delta = ApplyChatFavorGainCap(currentFavor, requestedDelta);
        if (delta == 0)
        {
            AIChatManager.logger?.LogInfo((object)("[DialogEffectMvp] 对话好感收益已达上限: npcId=" + npcId + ", currentFavor=" + currentFavor + ", cap=" + ChatFavorGainCap));
            return false;
        }
    }
    if (!TryAddNpcFavor(npcId, delta))
    {
        AIChatManager.logger?.LogWarning((object)("[DialogEffectMvp] 好感度修改失败: npcId=" + npcId + ", delta=" + delta));
        return false;
    }
    appliedDelta = delta;
    if (showFeedback)
    {
        ShowFeedbackTip(delta);
    }
    AIChatManager.logger?.LogInfo((object)("[DialogEffectMvp] 已应用对话效果: npcId=" + npcId + ", npcName=" + npcName + ", delta=" + delta + ", type=" + effect.EffectType + ", reason=" + effect.Reason));
    return true;
}
```

这样战斗轮只显示：

```text
系统：对方怒意已决，好感度 -25，即将进入战斗。
```

普通扣好感轮仍显示原来的：

```text
系统：对方态度转冷：好感度 -10
```

- [ ] **Step 5: Update NPCDialog to choose feedback path**

在 `NPCDialog.OnUISendMessage(...)` 中，把 Task 3 的临时代码：

```csharp
DialogEffectMvp.TryApply(currentNpcId, speakerName, effect, out int appliedDelta);
DialogEffectMvp.TryTriggerBattleAfterDialog(currentNpcId, speakerName, aiResponse, effect, appliedDelta);
```

替换为：

```csharp
bool shouldTriggerBattle = DialogEffectMvp.ShouldTriggerBattleAfterDialogForDialog(currentNpcId, aiResponse, effect);
int appliedDelta;
bool applied = shouldTriggerBattle
    ? DialogEffectMvp.TryApplyWithoutFeedback(currentNpcId, speakerName, effect, out appliedDelta)
    : DialogEffectMvp.TryApply(currentNpcId, speakerName, effect, out appliedDelta);
if (applied && shouldTriggerBattle)
{
    DialogEffectMvp.TryTriggerBattleAfterDialog(currentNpcId, speakerName, aiResponse, effect, appliedDelta);
}
```

- [ ] **Step 6: Add dialog-facing trigger helper**

在 `TryTriggerBattleAfterDialog(...)` 前追加：

```csharp
internal static bool ShouldTriggerBattleAfterDialogForDialog(int npcId, string npcVisibleReply, Effect effect)
{
    return npcId > 0 && ShouldTriggerBattleAfterDialog(npcVisibleReply, effect, ValidateAndClamp(effect));
}
```

- [ ] **Step 7: Build**

Run:

```powershell
dotnet build "E:\RPG_agent\参考mod\src\MCS_AIChatMod\MCS_AIChatMod.csproj" -c Release
```

Expected:

```text
已成功生成。
0 个错误
```

## Task 5: Optional Prompt Nudge For Clear NPC Declaration

**Files:**
- Modify: `参考mod/src/MCS_AIChatMod/MCS_AIChatMod/PromptBuilder.cs`

Only do this task if initial game testing shows the NPC rarely says explicit battle words after severe insults. If tests already trigger naturally, skip this task.

- [ ] **Step 1: Add one sentence to the dialog effect protocol**

Find the section in `PromptBuilder` that describes hidden dialog effects. Add one sentence near the negative effect instructions:

```csharp
sb.AppendLine("若角色因严重侮辱或致命威胁决定动手，正文中必须明确说出开战/动手意图，例如“既如此，战吧”或“休怪我动手”。");
```

- [ ] **Step 2: Run tests and build**

Run:

```powershell
dotnet run --project "E:\RPG_agent\参考mod\tests\DialogEffectMvp.Tests\DialogEffectMvp.Tests.csproj"
dotnet build "E:\RPG_agent\参考mod\src\MCS_AIChatMod\MCS_AIChatMod.csproj" -c Release
```

Expected:

```text
All DialogEffectMvp tests passed.
已成功生成。
0 个错误
```

## Task 6: Update Handoff Document, Build DLL, Worklog

**Files:**
- Modify: `参考mod/工作目录.md`
- Add: `worklogs/YYYY-MM-DD-dialog-battle-trigger.md`
- Runtime copy only: `参考mod/plugins/MCS_AIChatMod.dll` ignored by Git

- [ ] **Step 1: Update 工作目录**

在 `参考mod/工作目录.md` 的战斗触发备注附近加入：

```markdown
## 对话触发战斗 V2
- 已有好感度分级不变：普通辱骂/威胁只扣好感，不直接开战。
- 当本轮效果为 `insult_attack / severe / -25`，且 NPC 回复明确表示开战、拔剑、动手、一战等意图时，Mod 会在好感度扣减后触发标准战斗。
- 战斗触发流程：NPC 回复 -> 系统提示“对方怒意已决，好感度 -25，即将进入战斗。” -> 调用 `StartFight.Do(...)`。
- NPC 只是警告、驱赶、表示不计较时，不触发战斗。
- 游戏已有战斗前整备环节，因此 V2 不增加二次确认按钮。
```

- [ ] **Step 2: Run full verification**

Run:

```powershell
dotnet run --project "E:\RPG_agent\参考mod\tests\DialogEffectMvp.Tests\DialogEffectMvp.Tests.csproj"
dotnet build "E:\RPG_agent\参考mod\src\MCS_AIChatMod\MCS_AIChatMod.csproj" -c Release
git diff --check
```

Expected:

```text
All DialogEffectMvp tests passed.
已成功生成。
0 个错误
```

`git diff --check` may print LF/CRLF warnings; it must not report whitespace errors.

- [ ] **Step 3: Copy workspace DLL only**

Run:

```powershell
$source = Resolve-Path -LiteralPath "E:\RPG_agent\参考mod\src\MCS_AIChatMod\bin\Release\net472\MCS_AIChatMod.dll"
$target = Resolve-Path -LiteralPath "E:\RPG_agent\参考mod\plugins\MCS_AIChatMod.dll"
Copy-Item -LiteralPath $source.Path -Destination $target.Path -Force
Get-FileHash -LiteralPath $source.Path -Algorithm SHA256
Get-FileHash -LiteralPath $target.Path -Algorithm SHA256
```

Expected: source and target hashes match. Do not copy to Steam or Workshop directories unless the user explicitly asks.

- [ ] **Step 4: Write worklog**

Create `worklogs/YYYY-MM-DD-dialog-battle-trigger.md`:

```markdown
# YYYY-MM-DD 对话触发战斗

## 任务摘要
- 在现有好感度分级基础上，新增严重冲突后的战斗触发。
- 触发条件为 `insult_attack / severe` 且 NPC 回复明确宣战。
- 触发流程为 NPC 回复、系统提示、调用标准战斗入口。

## 变更文件
- `参考mod/src/MCS_AIChatMod/MCS_AIChatMod/DialogEffectMvp.cs`
- `参考mod/src/MCS_AIChatMod/MCS_AIChatMod/NPCDialog.cs`
- `参考mod/tests/DialogEffectMvp.Tests/Program.cs`
- `参考mod/工作目录.md`

## 验证过程
- `dotnet run --project "E:\RPG_agent\参考mod\tests\DialogEffectMvp.Tests\DialogEffectMvp.Tests.csproj"`
- `dotnet build "E:\RPG_agent\参考mod\src\MCS_AIChatMod\MCS_AIChatMod.csproj" -c Release`
- `git diff --check`

## 风险 / 备注
- V2 只触发标准战斗，不改 NPC 死亡、任务状态或长期敌对状态。
- 只有好感度扣减成功后才尝试开战。
- 如果战斗接口失败，会记录日志并保留好感度变化。

## 下一步
- 游戏内用测试存档对普通 NPC 输入严重威胁，观察 NPC 宣战后是否进入整备/战斗流程。
- 测试 NPC 只是警告或驱赶时不会开战。
```

- [ ] **Step 5: Review staged files before commit**

Run:

```powershell
git status --short
git diff --name-status
```

Expected text/source files only. Do not stage DLL/PDB/bin/obj.

## Verification Checklist

- [ ] 普通辱骂仍只扣好感，不开战。
- [ ] major 威胁仍只扣好感，不开战。
- [ ] severe `insult_attack` 但 NPC 回复不宣战时，不开战。
- [ ] severe `insult_attack` 且 NPC 回复“战吧/拔剑/动手/一战”等宣战内容时，先扣好感，再显示战斗提示，再进入战斗。
- [ ] NPC 回复“不与你计较/懒得动手/滚吧”等拒绝战斗内容时，不开战。
- [ ] NPC ID 无效或玩家实例不存在时，不触发战斗，不抛出未捕获异常。
- [ ] 不修改真实 Steam 游戏目录或 LocalLow 存档目录。

## Self-Review

- Spec coverage: 覆盖了“NPC 回复开战后触发战斗”和“继续遵循既有好感度骤减分级”；没有重新设计分级。
- Placeholder scan: 无 TBD/TODO/占位任务。
- Type consistency: `TryApply(...)` 通过 `out int appliedDelta` 向 `NPCDialog` 暴露实际扣减值；`TryTriggerBattleAfterDialog(...)` 接收同一轮 NPC 回复和效果对象；战斗调用复用现有 `StartFight.Do(...)` 参数形式。
