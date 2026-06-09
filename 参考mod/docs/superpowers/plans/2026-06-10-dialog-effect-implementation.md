# Dialog Effect V1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现“对话事件 -> Mod 校验 -> 游戏情分变化”的 V1 框架：普通对话不改情分，明确情绪/利益/送礼/辱骂等事件按分档影响 NPC 情分，并为后续对话触发具体游戏行为预留候选事件。

**Architecture:** 先从 `参考mod/plugins/MCS_AIChatMod.dll` 导出可编辑源码，建立可测试的 `DialogEffect` 核心模块，再把 parser、policy、validator 接入 AI 回复链路。V1 只执行情分变化；`anger_delta` 和 `trigger_candidates` 只记录，不触发战斗或任务。

**Tech Stack:** C#、Unity/Mono 游戏程序集、BepInEx/Harmony 风格参考 mod、Newtonsoft.Json、.NET SDK 8、ilspycmd、PowerShell、Git。

---

## 当前现实约束

- 当前工作区没有可直接编辑的 `MCS_AIChatMod` 源码工程，只有 `参考mod/plugins/MCS_AIChatMod.dll` 和本地 PDB。
- 当前机器只有 .NET Runtime，没有 .NET SDK，也没有 `ilspycmd`。
- 实现前需要安装 `.NET SDK 8`，再用 `ilspycmd` 导出参考 mod 源码。
- `AGENTS.md` 当前有一处未提交修改，执行本计划时不要误暂存，除非用户明确要求一起处理。
- `参考mod/plugins/*.dll`、`*.pdb`、`*.xml`、`Mod.bin` 是本地参考二进制，默认不提交。

## 文件结构

计划中的目标文件如下。路径以 `E:\RPG_agent` 为根目录。

- 创建：`参考mod/src/MCS_AIChatMod/`
  - 用 `ilspycmd` 从 `参考mod/plugins/MCS_AIChatMod.dll` 导出的参考 mod 源码。
  - 后续集成点预计在 `NPCDialog.cs`、`AIChatManager.cs`、`UIManager.cs`、`OfficialNpcFavorChangedPatch.cs` 附近。
- 创建：`参考mod/src/DialogEffectCore/DialogEffectCore.csproj`
  - 纯 C# 核心库，用于先离线测试 parser/policy/validator。
- 创建：`参考mod/src/DialogEffectCore/DialogEffects/*.cs`
  - `DialogEffect` 数据模型、解析器、策略表、校验器。
- 创建：`参考mod/src/DialogEffectCore.Tests/DialogEffectCore.Tests.csproj`
  - 不依赖 NuGet 测试框架的 console 测试工程，避免额外包管理复杂度。
- 创建：`参考mod/src/DialogEffectCore.Tests/Program.cs`
  - 离线自测入口。
- 修改：`参考mod/plugins/prompt.txt`
  - 加入隐藏 `<dialog_effect>` 输出协议。
- 修改：`参考mod/src/MCS_AIChatMod/NPCDialog.cs`
  - 在 AI 回复进入 UI/历史前分离可显示正文和隐藏效果块。
- 修改：`参考mod/src/MCS_AIChatMod/AIChatManager.cs`
  - 如 AI 回复最终在此处返回，则在这里或调用方保持原始回复可被 parser 处理。
- 创建：`参考mod/src/MCS_AIChatMod/DialogEffects/*.cs`
  - 将通过离线测试的核心文件复制或链接进参考 mod 源码工程。
- 创建：`参考mod/src/MCS_AIChatMod/DialogEffects/GameDialogEffectRuntime.cs`
  - 游戏运行态适配层，负责取当前 NPC、调用情分接口、提示、记忆写入。
- 创建：`参考mod/src/MCS_AIChatMod/DialogEffects/DialogEffectIntegration.md`
  - 记录实际接入点、反编译确认结果和游戏内验证步骤。

## Task 1: 安装工具并导出参考 mod 源码

**Files:**
- Create: `参考mod/src/MCS_AIChatMod/`
- Create: `参考mod/src/MCS_AIChatMod/DialogEffects/DialogEffectIntegration.md`

- [ ] **Step 1: 确认当前工具缺口**

Run:

```powershell
dotnet --info
Get-Command ilspycmd -ErrorAction SilentlyContinue
```

Expected:

```text
.NET SDKs installed:
  No SDKs were found.

Get-Command ilspycmd ... 无输出
```

- [ ] **Step 2: 安装 .NET SDK 8**

Run:

```powershell
winget install --id Microsoft.DotNet.SDK.8 --source winget --accept-package-agreements --accept-source-agreements
```

Expected:

```text
Successfully installed
```

Verify:

```powershell
dotnet --list-sdks
```

Expected:

```text
8.x.x [C:\Program Files\dotnet\sdk]
```

- [ ] **Step 3: 安装 ilspycmd**

Run:

```powershell
dotnet tool install --global ilspycmd
$env:PATH = "$env:USERPROFILE\.dotnet\tools;$env:PATH"
ilspycmd --version
```

Expected:

```text
ilspycmd: 8.x 或更高版本
```

- [ ] **Step 4: 导出 `MCS_AIChatMod.dll` 源码**

Run:

```powershell
New-Item -ItemType Directory -Force -Path "E:\RPG_agent\参考mod\src\MCS_AIChatMod" | Out-Null
ilspycmd -p -o "E:\RPG_agent\参考mod\src\MCS_AIChatMod" "E:\RPG_agent\参考mod\plugins\MCS_AIChatMod.dll"
```

Expected:

```text
命令成功结束，并生成 .csproj 与多个 .cs 文件
```

- [ ] **Step 5: 确认关键类存在**

Run:

```powershell
rg -n "OnUISendMessage|GetAIResponseAsync|AppendMessage|OfficialNpcFavorChangedPatch|NPCInfo|NPCChatTools" "E:\RPG_agent\参考mod\src\MCS_AIChatMod"
```

Expected:

```text
命中 NPCDialog、AIChatManager、UIManager、OfficialNpcFavorChangedPatch、NPCInfo 或 NPCChatTools 相关文件
```

- [ ] **Step 6: 写入接入点记录**

Create `参考mod/src/MCS_AIChatMod/DialogEffects/DialogEffectIntegration.md`:

```markdown
# DialogEffect 接入点记录

## 源码来源
- 由 `ilspycmd` 从 `参考mod/plugins/MCS_AIChatMod.dll` 导出。
- 导出日期：2026-06-10 Asia/Shanghai。

## 已确认候选入口
- `MCS_AIChatMod.NPCDialog.OnUISendMessage(string inputText)`：玩家从 Mod UI 发送消息入口。
- `MCS_AIChatMod.NPCDialog.AppendMessageToNpcHistory(...)`：写入 NPC 对话历史。
- `MCS_AIChatMod.UIManager.AppendMessage(...)`：聊天面板显示消息。
- `MCS_AIChatMod.OfficialNpcFavorChangedPatch.Prefix/Postfix(int npcid, int __state)`：现有官方情分变化监听。
- `MCS_AIChatMod.NPCInfo.NpcID/QingFen/Favor/Name`：NPC 状态信息。

## 游戏侧情分命令候选
- `CmdAddNPCQingFen`：字段 `NPCID`、`Count`，方法 `OnEnter()`。
- `CmdGetNPCQingFen`：字段 `NPCID`，方法 `OnEnter()`。

## V1 接入原则
- 先解析 AI 回复并剥离 `<dialog_effect>`，避免隐藏 JSON 显示给玩家。
- 只有通过 `DialogEffectValidator` 的效果才能进入游戏执行层。
- `trigger_candidates` 和 `anger_delta` 只记录，不触发战斗或任务。
```

- [ ] **Step 7: Commit**

```powershell
git status --short
git add -- "参考mod/src/MCS_AIChatMod"
git diff --cached --name-status
git commit -m "chore: recover reference mod source"
```

Expected:

```text
提交中包含 src/MCS_AIChatMod 源码和 DialogEffectIntegration.md，不包含 plugins/*.dll、*.pdb、*.xml、Mod.bin
```

## Task 2: 建立离线核心测试工程

**Files:**
- Create: `参考mod/src/DialogEffectCore/DialogEffectCore.csproj`
- Create: `参考mod/src/DialogEffectCore.Tests/DialogEffectCore.Tests.csproj`
- Create: `参考mod/src/DialogEffectCore.Tests/Program.cs`

- [ ] **Step 1: 写入测试工程，先让它失败**

Create `参考mod/src/DialogEffectCore.Tests/DialogEffectCore.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net6.0</TargetFramework>
    <ImplicitUsings>disable</ImplicitUsings>
    <Nullable>disable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\DialogEffectCore\DialogEffectCore.csproj" />
  </ItemGroup>
</Project>
```

Create `参考mod/src/DialogEffectCore.Tests/Program.cs`:

```csharp
using System;
using MCS_AIChatMod.DialogEffects;

internal static class Program
{
    private static int _failures;

    private static void Main()
    {
        TestPlainTextHasNoEffect();
        TestParsesMajorInsult();
        TestModerateClamp();
        TestLowConfidenceSkips();
        TestDirectionConflictSkips();

        if (_failures > 0)
        {
            Console.WriteLine("FAILED: " + _failures);
            Environment.Exit(1);
        }

        Console.WriteLine("PASS: DialogEffectCore");
    }

    private static void TestPlainTextHasNoEffect()
    {
        var result = DialogEffectParser.Parse("寻常寒暄，不应改变关系。");
        AssertEqual("寻常寒暄，不应改变关系。", result.VisibleText, "plain visible text");
        AssertEqual("none", result.Effect.EffectType, "plain effect type");
        AssertEqual(0, result.Effect.FavorDelta, "plain delta");
    }

    private static void TestParsesMajorInsult()
    {
        var raw = "你这话太过分了。\n<dialog_effect>{\"effect_type\":\"insult_attack\",\"impact_level\":\"major\",\"favor_delta\":-8,\"reason\":\"辱骂\",\"memory_summary\":\"玩家辱骂 NPC\",\"anger_delta\":12,\"trigger_candidates\":[\"future_hostile_dialog\"],\"confidence\":0.86}</dialog_effect>";
        var result = DialogEffectParser.Parse(raw);
        AssertEqual("你这话太过分了。", result.VisibleText, "insult visible text");
        AssertEqual("insult_attack", result.Effect.EffectType, "insult type");
        AssertEqual("major", result.Effect.ImpactLevel, "insult level");
        AssertEqual(-8, result.Effect.FavorDelta, "insult delta");
        AssertEqual("future_hostile_dialog", result.Effect.TriggerCandidates[0], "candidate");
    }

    private static void TestModerateClamp()
    {
        var effect = new DialogEffect { EffectType = "emotional_shift", ImpactLevel = "moderate", FavorDelta = 99, Confidence = 1.0 };
        var result = DialogEffectValidator.Validate(effect, DialogEffectValidationContext.Valid(1001, "npc-1001-msg-1"));
        AssertEqual(true, result.ShouldExecute, "moderate executes");
        AssertEqual(3, result.FinalFavorDelta, "moderate clamped to +3");
    }

    private static void TestLowConfidenceSkips()
    {
        var effect = new DialogEffect { EffectType = "gift", ImpactLevel = "major", FavorDelta = 8, Confidence = 0.5 };
        var result = DialogEffectValidator.Validate(effect, DialogEffectValidationContext.Valid(1001, "npc-1001-msg-2"));
        AssertEqual(false, result.ShouldExecute, "low confidence skip");
        AssertEqual("low_confidence", result.SkipReason, "low confidence reason");
    }

    private static void TestDirectionConflictSkips()
    {
        var effect = new DialogEffect { EffectType = "insult_attack", ImpactLevel = "major", FavorDelta = 5, Confidence = 1.0 };
        var result = DialogEffectValidator.Validate(effect, DialogEffectValidationContext.Valid(1001, "npc-1001-msg-3"));
        AssertEqual(false, result.ShouldExecute, "direction conflict skip");
        AssertEqual("direction_conflict", result.SkipReason, "direction conflict reason");
    }

    private static void AssertEqual<T>(T expected, T actual, string name)
    {
        if (!object.Equals(expected, actual))
        {
            _failures++;
            Console.WriteLine("FAIL " + name + ": expected=" + expected + " actual=" + actual);
        }
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Run:

```powershell
dotnet run --project "E:\RPG_agent\参考mod\src\DialogEffectCore.Tests\DialogEffectCore.Tests.csproj"
```

Expected:

```text
error MSB3202: The project file "...DialogEffectCore.csproj" was not found.
```

- [ ] **Step 3: 写入核心库工程**

Create `参考mod/src/DialogEffectCore/DialogEffectCore.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <ImplicitUsings>disable</ImplicitUsings>
    <Nullable>disable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="Newtonsoft.Json">
      <HintPath>..\..\plugins\Newtonsoft.Json.dll</HintPath>
    </Reference>
  </ItemGroup>
</Project>
```

- [ ] **Step 4: 运行测试确认仍失败，因为类型尚未实现**

Run:

```powershell
dotnet run --project "E:\RPG_agent\参考mod\src\DialogEffectCore.Tests\DialogEffectCore.Tests.csproj"
```

Expected:

```text
error CS0246: The type or namespace name 'MCS_AIChatMod' could not be found
```

- [ ] **Step 5: Commit**

```powershell
git add -- "参考mod/src/DialogEffectCore" "参考mod/src/DialogEffectCore.Tests"
git commit -m "test: add dialog effect core harness"
```

Expected:

```text
提交测试工程；测试当前因核心类型缺失而失败
```

## Task 3: 实现 DialogEffect 数据模型和 Parser

**Files:**
- Create: `参考mod/src/DialogEffectCore/DialogEffects/DialogEffect.cs`
- Create: `参考mod/src/DialogEffectCore/DialogEffects/DialogEffectParseResult.cs`
- Create: `参考mod/src/DialogEffectCore/DialogEffects/DialogEffectParser.cs`

- [ ] **Step 1: 写入 `DialogEffect`**

Create `参考mod/src/DialogEffectCore/DialogEffects/DialogEffect.cs`:

```csharp
using System.Collections.Generic;

namespace MCS_AIChatMod.DialogEffects
{
    public sealed class DialogEffect
    {
        public string EffectType { get; set; }
        public string ImpactLevel { get; set; }
        public int FavorDelta { get; set; }
        public string Reason { get; set; }
        public string MemorySummary { get; set; }
        public int AngerDelta { get; set; }
        public List<string> TriggerCandidates { get; set; }
        public double Confidence { get; set; }

        public static DialogEffect None()
        {
            return new DialogEffect
            {
                EffectType = "none",
                ImpactLevel = "none",
                FavorDelta = 0,
                Reason = string.Empty,
                MemorySummary = string.Empty,
                AngerDelta = 0,
                TriggerCandidates = new List<string>(),
                Confidence = 1.0
            };
        }
    }
}
```

- [ ] **Step 2: 写入 `DialogEffectParseResult`**

Create `参考mod/src/DialogEffectCore/DialogEffects/DialogEffectParseResult.cs`:

```csharp
namespace MCS_AIChatMod.DialogEffects
{
    public sealed class DialogEffectParseResult
    {
        public string VisibleText { get; private set; }
        public DialogEffect Effect { get; private set; }
        public bool HadEffectBlock { get; private set; }
        public string Error { get; private set; }

        public DialogEffectParseResult(string visibleText, DialogEffect effect, bool hadEffectBlock, string error)
        {
            VisibleText = visibleText ?? string.Empty;
            Effect = effect ?? DialogEffect.None();
            HadEffectBlock = hadEffectBlock;
            Error = error ?? string.Empty;
        }
    }
}
```

- [ ] **Step 3: 写入 `DialogEffectParser`**

Create `参考mod/src/DialogEffectCore/DialogEffects/DialogEffectParser.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace MCS_AIChatMod.DialogEffects
{
    public static class DialogEffectParser
    {
        private static readonly Regex EffectBlockRegex = new Regex(
            "<dialog_effect>\\s*(?<json>.*?)\\s*</dialog_effect>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        public static DialogEffectParseResult Parse(string rawResponse)
        {
            if (string.IsNullOrEmpty(rawResponse))
            {
                return new DialogEffectParseResult(string.Empty, DialogEffect.None(), false, string.Empty);
            }

            Match match = EffectBlockRegex.Match(rawResponse);
            if (!match.Success)
            {
                return new DialogEffectParseResult(rawResponse.Trim(), DialogEffect.None(), false, string.Empty);
            }

            string visibleText = EffectBlockRegex.Replace(rawResponse, string.Empty, 1).Trim();
            string json = match.Groups["json"].Value;

            try
            {
                JObject obj = JObject.Parse(json);
                DialogEffect effect = new DialogEffect
                {
                    EffectType = GetString(obj, "effect_type", "none"),
                    ImpactLevel = GetString(obj, "impact_level", "none"),
                    FavorDelta = GetInt(obj, "favor_delta", 0),
                    Reason = GetString(obj, "reason", string.Empty),
                    MemorySummary = GetString(obj, "memory_summary", string.Empty),
                    AngerDelta = GetInt(obj, "anger_delta", 0),
                    TriggerCandidates = GetStringList(obj, "trigger_candidates"),
                    Confidence = GetDouble(obj, "confidence", 1.0)
                };

                return new DialogEffectParseResult(visibleText, effect, true, string.Empty);
            }
            catch (Exception ex)
            {
                return new DialogEffectParseResult(visibleText, DialogEffect.None(), true, ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static string GetString(JObject obj, string key, string defaultValue)
        {
            JToken token;
            if (!obj.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out token) || token.Type == JTokenType.Null)
            {
                return defaultValue;
            }

            return token.ToString();
        }

        private static int GetInt(JObject obj, string key, int defaultValue)
        {
            JToken token;
            if (!obj.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out token) || token.Type == JTokenType.Null)
            {
                return defaultValue;
            }

            int value;
            return int.TryParse(token.ToString(), out value) ? value : defaultValue;
        }

        private static double GetDouble(JObject obj, string key, double defaultValue)
        {
            JToken token;
            if (!obj.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out token) || token.Type == JTokenType.Null)
            {
                return defaultValue;
            }

            double value;
            return double.TryParse(token.ToString(), out value) ? value : defaultValue;
        }

        private static List<string> GetStringList(JObject obj, string key)
        {
            var values = new List<string>();
            JToken token;
            if (!obj.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out token) || token.Type != JTokenType.Array)
            {
                return values;
            }

            foreach (JToken item in token)
            {
                if (item.Type != JTokenType.Null)
                {
                    values.Add(item.ToString());
                }
            }

            return values;
        }
    }
}
```

- [ ] **Step 4: 运行测试确认 validator 类型缺失**

Run:

```powershell
dotnet run --project "E:\RPG_agent\参考mod\src\DialogEffectCore.Tests\DialogEffectCore.Tests.csproj"
```

Expected:

```text
error CS0103: The name 'DialogEffectValidator' does not exist in the current context
```

- [ ] **Step 5: Commit**

```powershell
git add -- "参考mod/src/DialogEffectCore/DialogEffects"
git commit -m "feat: add dialog effect parser"
```

Expected:

```text
提交 parser 和数据模型；测试仍因 validator 缺失失败
```

## Task 4: 实现 Policy 和 Validator

**Files:**
- Create: `参考mod/src/DialogEffectCore/DialogEffects/DialogEffectPolicy.cs`
- Create: `参考mod/src/DialogEffectCore/DialogEffects/DialogEffectValidationContext.cs`
- Create: `参考mod/src/DialogEffectCore/DialogEffects/DialogEffectValidationResult.cs`
- Create: `参考mod/src/DialogEffectCore/DialogEffects/DialogEffectValidator.cs`

- [ ] **Step 1: 写入 `DialogEffectPolicy`**

Create `参考mod/src/DialogEffectCore/DialogEffects/DialogEffectPolicy.cs`:

```csharp
using System.Collections.Generic;

namespace MCS_AIChatMod.DialogEffects
{
    public static class DialogEffectPolicy
    {
        private static readonly HashSet<string> KnownEffectTypes = new HashSet<string>
        {
            "none",
            "emotional_shift",
            "interest_gain",
            "interest_loss",
            "gift",
            "insult_attack",
            "trust_break",
            "future_trigger_candidate"
        };

        private static readonly Dictionary<string, Range> Ranges = new Dictionary<string, Range>
        {
            { "none", new Range(0, 0) },
            { "minor", new Range(-1, 1) },
            { "moderate", new Range(-3, 3) },
            { "major", new Range(-10, 10) },
            { "severe", new Range(-25, 25) }
        };

        public static bool IsKnownEffectType(string effectType)
        {
            return !string.IsNullOrEmpty(effectType) && KnownEffectTypes.Contains(effectType);
        }

        public static bool TryGetRange(string impactLevel, out Range range)
        {
            if (string.IsNullOrEmpty(impactLevel))
            {
                range = new Range(0, 0);
                return false;
            }

            return Ranges.TryGetValue(impactLevel, out range);
        }

        public static string DowngradeImpactLevel(string impactLevel)
        {
            if (impactLevel == "severe") return "major";
            if (impactLevel == "major") return "moderate";
            if (impactLevel == "moderate") return "minor";
            return "none";
        }

        public static int ExpectedDirection(string effectType)
        {
            if (effectType == "gift" || effectType == "interest_gain") return 1;
            if (effectType == "interest_loss" || effectType == "insult_attack" || effectType == "trust_break") return -1;
            if (effectType == "none" || effectType == "future_trigger_candidate") return 0;
            return 2;
        }

        public struct Range
        {
            public int Min;
            public int Max;

            public Range(int min, int max)
            {
                Min = min;
                Max = max;
            }

            public int Clamp(int value)
            {
                if (value < Min) return Min;
                if (value > Max) return Max;
                return value;
            }
        }
    }
}
```

- [ ] **Step 2: 写入 validation context/result**

Create `参考mod/src/DialogEffectCore/DialogEffects/DialogEffectValidationContext.cs`:

```csharp
namespace MCS_AIChatMod.DialogEffects
{
    public sealed class DialogEffectValidationContext
    {
        public int NpcId { get; private set; }
        public string MessageKey { get; private set; }
        public bool AlreadyProcessed { get; private set; }

        private DialogEffectValidationContext(int npcId, string messageKey, bool alreadyProcessed)
        {
            NpcId = npcId;
            MessageKey = messageKey ?? string.Empty;
            AlreadyProcessed = alreadyProcessed;
        }

        public static DialogEffectValidationContext Valid(int npcId, string messageKey)
        {
            return new DialogEffectValidationContext(npcId, messageKey, false);
        }

        public static DialogEffectValidationContext AlreadyHandled(int npcId, string messageKey)
        {
            return new DialogEffectValidationContext(npcId, messageKey, true);
        }
    }
}
```

Create `参考mod/src/DialogEffectCore/DialogEffects/DialogEffectValidationResult.cs`:

```csharp
namespace MCS_AIChatMod.DialogEffects
{
    public sealed class DialogEffectValidationResult
    {
        public bool ShouldExecute { get; private set; }
        public int FinalFavorDelta { get; private set; }
        public DialogEffect Effect { get; private set; }
        public string SkipReason { get; private set; }
        public bool WasClamped { get; private set; }

        private DialogEffectValidationResult(bool shouldExecute, int finalFavorDelta, DialogEffect effect, string skipReason, bool wasClamped)
        {
            ShouldExecute = shouldExecute;
            FinalFavorDelta = finalFavorDelta;
            Effect = effect ?? DialogEffect.None();
            SkipReason = skipReason ?? string.Empty;
            WasClamped = wasClamped;
        }

        public static DialogEffectValidationResult Execute(DialogEffect effect, int finalFavorDelta, bool wasClamped)
        {
            return new DialogEffectValidationResult(true, finalFavorDelta, effect, string.Empty, wasClamped);
        }

        public static DialogEffectValidationResult Skip(DialogEffect effect, string reason)
        {
            return new DialogEffectValidationResult(false, 0, effect, reason, false);
        }
    }
}
```

- [ ] **Step 3: 写入 `DialogEffectValidator`**

Create `参考mod/src/DialogEffectCore/DialogEffects/DialogEffectValidator.cs`:

```csharp
namespace MCS_AIChatMod.DialogEffects
{
    public static class DialogEffectValidator
    {
        public static DialogEffectValidationResult Validate(DialogEffect effect, DialogEffectValidationContext context)
        {
            effect = effect ?? DialogEffect.None();

            if (context == null || context.NpcId <= 0)
            {
                return DialogEffectValidationResult.Skip(effect, "missing_npc");
            }

            if (context.AlreadyProcessed)
            {
                return DialogEffectValidationResult.Skip(effect, "already_processed");
            }

            Normalize(effect);

            if (!DialogEffectPolicy.IsKnownEffectType(effect.EffectType))
            {
                return DialogEffectValidationResult.Skip(effect, "unknown_effect_type");
            }

            if (effect.EffectType == "none")
            {
                return DialogEffectValidationResult.Skip(effect, "none");
            }

            if (effect.Confidence < 0.6)
            {
                return DialogEffectValidationResult.Skip(effect, "low_confidence");
            }

            if (effect.Confidence < 0.8)
            {
                effect.ImpactLevel = DialogEffectPolicy.DowngradeImpactLevel(effect.ImpactLevel);
            }

            DialogEffectPolicy.Range range;
            if (!DialogEffectPolicy.TryGetRange(effect.ImpactLevel, out range))
            {
                return DialogEffectValidationResult.Skip(effect, "unknown_impact_level");
            }

            int expectedDirection = DialogEffectPolicy.ExpectedDirection(effect.EffectType);
            if (expectedDirection == 0)
            {
                return DialogEffectValidationResult.Skip(effect, "non_executable_effect");
            }

            if (expectedDirection == 1 && effect.FavorDelta < 0)
            {
                return DialogEffectValidationResult.Skip(effect, "direction_conflict");
            }

            if (expectedDirection == -1 && effect.FavorDelta > 0)
            {
                return DialogEffectValidationResult.Skip(effect, "direction_conflict");
            }

            int finalDelta = range.Clamp(effect.FavorDelta);
            if (finalDelta == 0)
            {
                return DialogEffectValidationResult.Skip(effect, "zero_delta");
            }

            return DialogEffectValidationResult.Execute(effect, finalDelta, finalDelta != effect.FavorDelta);
        }

        private static void Normalize(DialogEffect effect)
        {
            effect.EffectType = NormalizeText(effect.EffectType, "none", 64);
            effect.ImpactLevel = NormalizeText(effect.ImpactLevel, "none", 32);
            effect.Reason = NormalizeText(effect.Reason, string.Empty, 160);
            effect.MemorySummary = NormalizeText(effect.MemorySummary, string.Empty, 240);

            if (effect.TriggerCandidates == null)
            {
                effect.TriggerCandidates = new System.Collections.Generic.List<string>();
            }

            if (effect.Confidence < 0) effect.Confidence = 0;
            if (effect.Confidence > 1) effect.Confidence = 1;
        }

        private static string NormalizeText(string value, string defaultValue, int maxLength)
        {
            value = string.IsNullOrEmpty(value) ? defaultValue : value.Trim();
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }
    }
}
```

- [ ] **Step 4: 运行离线测试**

Run:

```powershell
dotnet run --project "E:\RPG_agent\参考mod\src\DialogEffectCore.Tests\DialogEffectCore.Tests.csproj"
```

Expected:

```text
PASS: DialogEffectCore
```

- [ ] **Step 5: Commit**

```powershell
git add -- "参考mod/src/DialogEffectCore" "参考mod/src/DialogEffectCore.Tests"
git commit -m "feat: validate dialog effect policy"
```

Expected:

```text
提交 policy/validator；离线测试通过
```

## Task 5: 更新 Prompt 输出协议

**Files:**
- Modify: `参考mod/plugins/prompt.txt`
- Test: `参考mod/src/DialogEffectCore.Tests/Program.cs`

- [ ] **Step 1: 在 prompt 末尾加入协议说明**

Modify `参考mod/plugins/prompt.txt`，在末尾追加：

```text

【隐藏对话效果协议】
你需要在正常 NPC 回复之后，额外输出一个隐藏的 <dialog_effect> JSON 块。
正常回复必须保持角色口吻；<dialog_effect> 不属于 NPC 台词。

普通闲聊、问候、询问常识、无明显情绪或利益变化时，不改变情分：
<dialog_effect>
{"effect_type":"none","impact_level":"none","favor_delta":0,"reason":"普通对话，没有明确情绪变化或利益影响","memory_summary":"","anger_delta":0,"trigger_candidates":[],"confidence":1.0}
</dialog_effect>

只有出现明确情绪变化、利益相关、送礼、让利、辱骂、诋毁、攻击、威胁、欺骗、背叛等事件时，才建议情分变化。
effect_type 只能使用：none、emotional_shift、interest_gain、interest_loss、gift、insult_attack、trust_break、future_trigger_candidate。
impact_level 只能使用：none、minor、moderate、major、severe。
trigger_candidates 和 anger_delta 只是候选或记忆信息，不能直接触发战斗、任务、传送、发物品、杀死 NPC 或修改复杂剧情。
```

- [ ] **Step 2: 增加 prompt 样例解析测试**

Modify `参考mod/src/DialogEffectCore.Tests/Program.cs`，在 `Main()` 中 `TestDirectionConflictSkips();` 后加入：

```csharp
        TestPromptStyleNoneEffect();
```

并在 `AssertEqual` 方法前加入：

```csharp
    private static void TestPromptStyleNoneEffect()
    {
        var raw = "不过寻常寒暄罢了。\n<dialog_effect>{\"effect_type\":\"none\",\"impact_level\":\"none\",\"favor_delta\":0,\"reason\":\"普通对话，没有明确情绪变化或利益影响\",\"memory_summary\":\"\",\"anger_delta\":0,\"trigger_candidates\":[],\"confidence\":1.0}</dialog_effect>";
        var result = DialogEffectParser.Parse(raw);
        var validation = DialogEffectValidator.Validate(result.Effect, DialogEffectValidationContext.Valid(1001, "npc-1001-msg-4"));
        AssertEqual("不过寻常寒暄罢了。", result.VisibleText, "prompt none visible text");
        AssertEqual(false, validation.ShouldExecute, "prompt none skips");
        AssertEqual("none", validation.SkipReason, "prompt none reason");
    }
```

- [ ] **Step 3: 运行离线测试**

Run:

```powershell
dotnet run --project "E:\RPG_agent\参考mod\src\DialogEffectCore.Tests\DialogEffectCore.Tests.csproj"
```

Expected:

```text
PASS: DialogEffectCore
```

- [ ] **Step 4: Commit**

```powershell
git add -- "参考mod/plugins/prompt.txt" "参考mod/src/DialogEffectCore.Tests/Program.cs"
git commit -m "feat: document dialog effect prompt protocol"
```

Expected:

```text
提交 prompt 协议和对应解析测试
```

## Task 6: 将核心模块接入参考 mod 源码

**Files:**
- Create: `参考mod/src/MCS_AIChatMod/DialogEffects/DialogEffect.cs`
- Create: `参考mod/src/MCS_AIChatMod/DialogEffects/DialogEffectParseResult.cs`
- Create: `参考mod/src/MCS_AIChatMod/DialogEffects/DialogEffectParser.cs`
- Create: `参考mod/src/MCS_AIChatMod/DialogEffects/DialogEffectPolicy.cs`
- Create: `参考mod/src/MCS_AIChatMod/DialogEffects/DialogEffectValidationContext.cs`
- Create: `参考mod/src/MCS_AIChatMod/DialogEffects/DialogEffectValidationResult.cs`
- Create: `参考mod/src/MCS_AIChatMod/DialogEffects/DialogEffectValidator.cs`

- [ ] **Step 1: 复制通过测试的核心文件到 mod 源码**

Run:

```powershell
New-Item -ItemType Directory -Force -Path "E:\RPG_agent\参考mod\src\MCS_AIChatMod\DialogEffects" | Out-Null
Copy-Item -LiteralPath "E:\RPG_agent\参考mod\src\DialogEffectCore\DialogEffects\*.cs" -Destination "E:\RPG_agent\参考mod\src\MCS_AIChatMod\DialogEffects" -Force
```

Expected:

```text
DialogEffects 目录包含 7 个核心 .cs 文件
```

- [ ] **Step 2: 确认 mod 项目包含新文件**

Run:

```powershell
rg -n "Compile Include|DialogEffects" "E:\RPG_agent\参考mod\src\MCS_AIChatMod"
```

Expected:

```text
SDK-style 项目通常无需显式 Compile Include；旧式项目则需要在 .csproj 中看到 DialogEffects/*.cs
```

If the exported `.csproj` is old-style and requires explicit includes, add:

```xml
<Compile Include="DialogEffects\DialogEffect.cs" />
<Compile Include="DialogEffects\DialogEffectParseResult.cs" />
<Compile Include="DialogEffects\DialogEffectParser.cs" />
<Compile Include="DialogEffects\DialogEffectPolicy.cs" />
<Compile Include="DialogEffects\DialogEffectValidationContext.cs" />
<Compile Include="DialogEffects\DialogEffectValidationResult.cs" />
<Compile Include="DialogEffects\DialogEffectValidator.cs" />
```

- [ ] **Step 3: Commit**

```powershell
git add -- "参考mod/src/MCS_AIChatMod/DialogEffects"
git commit -m "feat: add dialog effect core to mod source"
```

Expected:

```text
提交 mod 源码中的 DialogEffects 核心文件
```

## Task 7: 接入 AI 回复解析，隐藏 JSON 不进 UI

**Files:**
- Modify: `参考mod/src/MCS_AIChatMod/NPCDialog.cs`
- Modify: `参考mod/src/MCS_AIChatMod/DialogEffects/DialogEffectIntegration.md`

- [ ] **Step 1: 找到 AI 回复写入 UI 的位置**

Run:

```powershell
rg -n "AppendMessage|AppendMessageToNpcHistory|AddDialogEntry|GetAIResponseAsync|OnUISendMessage" "E:\RPG_agent\参考mod\src\MCS_AIChatMod\NPCDialog.cs" "E:\RPG_agent\参考mod\src\MCS_AIChatMod\AIChatManager.cs"
```

Expected:

```text
命中 NPC 回复文本进入 UIManager.AppendMessage 或 NPCDialog.AppendMessageToNpcHistory 的位置
```

- [ ] **Step 2: 在最终显示前插入解析逻辑**

In `参考mod/src/MCS_AIChatMod/NPCDialog.cs`, add:

```csharp
using MCS_AIChatMod.DialogEffects;
```

At the line identified in Step 1 where the raw AI response string is assigned before UI/history writes, insert this code immediately after that assignment. If Step 1 finds more than one possible raw response assignment, stop this task and inspect the decompiled control flow before editing.

```csharp
DialogEffectParseResult effectParse = DialogEffectParser.Parse(aiResponseText);
aiResponseText = effectParse.VisibleText;
```

Required mapping:

- `aiResponseText` must be the string returned by AI before it is shown.
- `currentNpcId` must be the current NPC ID already used by this class.
- `npcName` must be the current NPC name already used by this class.

- [ ] **Step 3: 记录变量映射**

Append a short Chinese paragraph to `参考mod/src/MCS_AIChatMod/DialogEffects/DialogEffectIntegration.md` describing the exact raw response variable, NPC ID variable, NPC name variable, UI display call, and history write call found in Step 1. The paragraph must be complete prose, not empty bullet labels.

Example sentence style:

```markdown
## AI 回复解析接入
`NPCDialog.cs` 中，AI 原始回复在变量 `aiResponseText` 中进入 UI 与历史写入流程；当前 NPC ID 来自 `currentNpcId`，NPC 名称来自 `npcName`。解析后只把 `effectParse.VisibleText` 传给 UI 显示和历史写入，`<dialog_effect>` 不会显示给玩家。
```

- [ ] **Step 4: Manual build/check**

Run:

```powershell
dotnet build "E:\RPG_agent\参考mod\src\MCS_AIChatMod\MCS_AIChatMod.csproj"
```

Expected:

```text
若项目引用已完整：Build succeeded.
若缺 Unity/BepInEx 引用：把完整缺失引用报错写入 DialogEffectIntegration.md，不继续运行游戏测试。
```

- [ ] **Step 5: Commit**

```powershell
git add -- "参考mod/src/MCS_AIChatMod/NPCDialog.cs" "参考mod/src/MCS_AIChatMod/DialogEffects/DialogEffectIntegration.md"
git commit -m "feat: parse dialog effect from ai response"
```

Expected:

```text
提交 AI 回复解析接入；隐藏 JSON 不再进入可显示文本
```

## Task 8: 实现运行态桥接和情分执行器

**Files:**
- Create: `参考mod/src/MCS_AIChatMod/DialogEffects/DialogEffectRuntimeBridge.cs`
- Create: `参考mod/src/MCS_AIChatMod/DialogEffects/GameDialogEffectRuntime.cs`
- Modify: `参考mod/src/MCS_AIChatMod/NPCDialog.cs`
- Modify: `参考mod/src/MCS_AIChatMod/DialogEffects/DialogEffectIntegration.md`

- [ ] **Step 1: 写入 `DialogEffectRuntimeBridge`**

Create `参考mod/src/MCS_AIChatMod/DialogEffects/DialogEffectRuntimeBridge.cs`:

```csharp
using System.Collections.Generic;

namespace MCS_AIChatMod.DialogEffects
{
    public static class DialogEffectRuntimeBridge
    {
        private static readonly HashSet<string> ProcessedMessageKeys = new HashSet<string>();

        public static void ProcessPendingEffect(DialogEffect effect, int npcId, string npcName)
        {
            string messageKey = npcId + ":" + NPCDialog.GetCurrentGameTimeTextOrDefault() + ":" + (effect == null ? "none" : effect.MemorySummary);
            bool alreadyProcessed = ProcessedMessageKeys.Contains(messageKey);
            DialogEffectValidationContext context = alreadyProcessed
                ? DialogEffectValidationContext.AlreadyHandled(npcId, messageKey)
                : DialogEffectValidationContext.Valid(npcId, messageKey);

            DialogEffectValidationResult validation = DialogEffectValidator.Validate(effect, context);
            if (!validation.ShouldExecute)
            {
                GameDialogEffectRuntime.RecordSkippedEffect(npcId, npcName, validation);
                return;
            }

            if (GameDialogEffectRuntime.TryApplyFavorDelta(npcId, npcName, validation.FinalFavorDelta, validation.Effect))
            {
                ProcessedMessageKeys.Add(messageKey);
            }
        }
    }
}
```

- [ ] **Step 2: 写入 `GameDialogEffectRuntime` 初版**

Create `参考mod/src/MCS_AIChatMod/DialogEffects/GameDialogEffectRuntime.cs`:

```csharp
using System;
using UnityEngine;

namespace MCS_AIChatMod.DialogEffects
{
    public static class GameDialogEffectRuntime
    {
        public static bool TryApplyFavorDelta(int npcId, string npcName, int favorDelta, DialogEffect effect)
        {
            if (npcId <= 0 || favorDelta == 0)
            {
                return false;
            }

            bool applied = TryApplyQingFenByGameApi(npcId, favorDelta);
            if (!applied)
            {
                Debug.LogWarning("[MCS_AIChatMod] DialogEffect failed to apply qingfen delta. npcId=" + npcId + " delta=" + favorDelta);
                return false;
            }

            NotifyFavorChange(favorDelta);
            WriteRelationshipMemory(npcId, npcName, favorDelta, effect);
            return true;
        }

        public static void RecordSkippedEffect(int npcId, string npcName, DialogEffectValidationResult validation)
        {
            if (validation == null || validation.Effect == null || validation.Effect.EffectType == "none")
            {
                return;
            }

            Debug.Log("[MCS_AIChatMod] DialogEffect skipped. npcId=" + npcId + " reason=" + validation.SkipReason + " type=" + validation.Effect.EffectType);
        }

        private static bool TryApplyQingFenByGameApi(int npcId, int favorDelta)
        {
            // 这里必须在 Task 8 Step 3 用反编译出的 CmdAddNPCQingFen.OnEnter 内部逻辑替换。
            // 在替换前，禁止假装已经成功修改游戏情分。
            return false;
        }

        private static void NotifyFavorChange(int favorDelta)
        {
            string message = favorDelta > 0
                ? "对方态度有所缓和：情分 +" + favorDelta
                : "对方态度转冷：情分 " + favorDelta;

            UIManager instance = UIManager.Instance;
            if (instance != null)
            {
                instance.AppendMessage("系统", message, false, NPCDialog.GetCurrentGameTimeTextOrDefault(), string.Empty, null);
            }
        }

        private static void WriteRelationshipMemory(int npcId, string npcName, int favorDelta, DialogEffect effect)
        {
            string summary = effect == null ? string.Empty : effect.MemorySummary;
            Debug.Log("[MCS_AIChatMod] DialogEffect memory. npcId=" + npcId + " npcName=" + npcName + " delta=" + favorDelta + " summary=" + summary);
        }
    }
}
```

- [ ] **Step 3: 用反编译结果替换 `TryApplyQingFenByGameApi`**

Open the decompiled `CmdAddNPCQingFen.OnEnter()` source under `F:\SteamLibrary\steamapps\common\觅长生\觅长生_Data\Managed\Assembly-CSharp.dll` using `ilspycmd`:

```powershell
New-Item -ItemType Directory -Force -Path "E:\RPG_agent\参考mod\src\GameAssembly" | Out-Null
ilspycmd -p -o "E:\RPG_agent\参考mod\src\GameAssembly" "F:\SteamLibrary\steamapps\common\觅长生\觅长生_Data\Managed\Assembly-CSharp.dll"
rg -n "class CmdAddNPCQingFen|OnEnter\\(" "E:\RPG_agent\参考mod\src\GameAssembly"
```

Expected:

```text
找到 CmdAddNPCQingFen 的 OnEnter 实现
```

Replace `TryApplyQingFenByGameApi` with the smallest direct call that mirrors `CmdAddNPCQingFen.OnEnter()` logic. Do not instantiate a Fungus `Command` unless the decompiled code proves it is safe in runtime context.

- [ ] **Step 4: 记录情分 API**

Append a complete Chinese paragraph to `参考mod/src/MCS_AIChatMod/DialogEffects/DialogEffectIntegration.md` describing the confirmed game API, whether the implementation mirrors `CmdAddNPCQingFen.OnEnter()`, whether it instantiates a Fungus command, and whether `OfficialNpcFavorChangedPatch` observes the result. Do not commit empty headings or labels.

Example sentence style:

```markdown
## 情分执行接口
情分修改采用 `CmdAddNPCQingFen.OnEnter()` 中确认的内部调用路径，不直接实例化 Fungus Command。测试 NPC 为 `20315 焦飞`，测试存档为复制出的临时验证存档；执行后 `OfficialNpcFavorChangedPatch` 能观察到情分变化。
```

- [ ] **Step 5: 在 `NPCDialog.cs` 接入运行态桥接**

In `参考mod/src/MCS_AIChatMod/NPCDialog.cs`, after the parse code added in Task 7 and after `currentNpcId` / `npcName` have been resolved, add:

```csharp
DialogEffectRuntimeBridge.ProcessPendingEffect(effectParse.Effect, currentNpcId, npcName);
```

If `currentNpcId` or `npcName` is not available at the parse site, move the parse code to the nearest later point before UI/history writes where both values are available. Keep this invariant: `effectParse.VisibleText` is the only text displayed or saved as NPC reply.

- [ ] **Step 6: Build**

Run:

```powershell
dotnet build "E:\RPG_agent\参考mod\src\MCS_AIChatMod\MCS_AIChatMod.csproj"
```

Expected:

```text
Build succeeded.
```

- [ ] **Step 7: Commit**

```powershell
git add -- "参考mod/src/MCS_AIChatMod/DialogEffects" "参考mod/src/MCS_AIChatMod/NPCDialog.cs"
git commit -m "feat: apply validated dialog effect favor changes"
```

Expected:

```text
提交运行态桥接和情分执行器；不提交 GameAssembly 导出源码或游戏 DLL
```

## Task 9: 游戏内验证和收尾

**Files:**
- Modify: `参考mod/src/MCS_AIChatMod/DialogEffects/DialogEffectIntegration.md`
- Create: `worklogs/2026-06-10-dialog-effect-v1-implementation.md`

- [ ] **Step 1: 准备测试存档**

Run:

```powershell
git status --short
```

Expected:

```text
没有未提交源码改动，或只有当前任务的文档记录
```

Manual:

```text
复制一个测试存档，不在真实主存档上验证。
```

- [ ] **Step 2: 游戏内验证普通对话不改情分**

Input:

```text
道友近来可好？
```

Expected:

```text
NPC 正常回复；无情分提示；日志中 effect_type=none 或无执行记录。
```

- [ ] **Step 3: 游戏内验证送礼/让利增加情分**

Input:

```text
此物于我暂且无用，赠予道友，也算结个善缘。
```

Expected:

```text
NPC 正常回复；出现情分增加提示；实际 NPC 情分增加；记忆记录 effect_type=gift 或 interest_gain。
```

- [ ] **Step 4: 游戏内验证辱骂/威胁降低情分**

Input:

```text
你这等见识也配与我论道？莫要自取其辱。
```

Expected:

```text
NPC 正常愤怒回复；出现情分降低提示；实际 NPC 情分降低；trigger_candidates 只记录不触发战斗。
```

- [ ] **Step 5: 更新验证记录**

Append a complete Chinese verification paragraph to `参考mod/src/MCS_AIChatMod/DialogEffects/DialogEffectIntegration.md`. It must state whether ordinary dialogue changed no favor, whether gift/interest dialogue increased favor, whether insult/threat dialogue decreased favor, whether `<dialog_effect>` stayed hidden, whether `trigger_candidates` stayed non-executing, and whether any save or plot side effects appeared.

Example sentence style:

```markdown
## V1 游戏内验证记录
在复制出的测试存档中，普通寒暄没有改变情分；送礼/让利场景使测试 NPC 情分增加；辱骂/威胁场景使测试 NPC 情分降低。`<dialog_effect>` 没有进入聊天 UI，`trigger_candidates` 只进入日志或记忆记录，没有触发战斗、任务、传送或发物品。验证过程中未发现存档或剧情副作用。
```

- [ ] **Step 6: 写 worklog**

Create `worklogs/2026-06-10-dialog-effect-v1-implementation.md`:

```markdown
# 2026-06-10 - 对话效果机制 V1 实现

## 任务摘要
- 实现对话效果 V1：AI 输出隐藏效果建议，Mod 解析、校验并按事件分档修改 NPC 情分。
- 普通对话默认不改情分，送礼/利益/辱骂/背叛等事件按分档处理。
- `trigger_candidates` 和 `anger_delta` 只记录，不触发战斗或任务。

## 变更文件
- `参考mod/plugins/prompt.txt`
- `参考mod/src/DialogEffectCore/`
- `参考mod/src/DialogEffectCore.Tests/`
- `参考mod/src/MCS_AIChatMod/DialogEffects/`
- `参考mod/src/MCS_AIChatMod/NPCDialog.cs`
- `参考mod/src/MCS_AIChatMod/DialogEffects/DialogEffectIntegration.md`

## 验证过程
- 运行 `dotnet run --project 参考mod/src/DialogEffectCore.Tests/DialogEffectCore.Tests.csproj`。
- 运行 `dotnet build 参考mod/src/MCS_AIChatMod/MCS_AIChatMod.csproj`。
- 在测试存档中验证普通对话、送礼/让利、辱骂/威胁三类场景。

## 风险 / 备注
- 分档数值需要根据真实游戏情分尺度继续调整。
- 高强度事件需要观察误判率。
- 战斗触发仍属于后续阶段，V1 不执行。

## 下一步
- 根据测试结果调整分档范围。
- 设计 V2 的 `trigger_candidates` 待确认事件队列。
```

- [ ] **Step 7: 最终提交和推送**

```powershell
git status --short
git add -- "参考mod/src/MCS_AIChatMod/DialogEffects/DialogEffectIntegration.md" "worklogs/2026-06-10-dialog-effect-v1-implementation.md"
git diff --cached --name-status
git commit -m "docs: record dialog effect v1 verification"
git push origin main
```

Expected:

```text
推送成功；没有提交 plugins/*.dll、*.pdb、*.xml、Mod.bin、真实游戏目录或存档
```

## 自查结果

- Spec 覆盖：
  - 普通对话不改情分：Task 4、Task 5、Task 9 覆盖。
  - 事件类型和强度分档：Task 4 覆盖。
  - AI 只建议、Mod 校验执行：Task 3、Task 4、Task 7、Task 8 覆盖。
  - 隐藏 JSON 不进入 UI：Task 7、Task 9 覆盖。
  - 游戏情分执行：Task 8 覆盖。
  - 记忆和后续候选事件：Task 8、Task 9 覆盖。
  - 战斗不触发：Task 4、Task 8、Task 9 覆盖。
- 红旗词扫描：
  - 未发现表示未完成内容的标记词。
  - 集成记录步骤要求执行者写完整中文段落，不允许提交空标签。
- 类型一致性：
  - `DialogEffect`、`DialogEffectParser`、`DialogEffectPolicy`、`DialogEffectValidator`、`DialogEffectValidationContext`、`DialogEffectValidationResult` 在测试和实现步骤中名称一致。
  - `trigger_candidates` 在 JSON 中对应 C# 的 `TriggerCandidates`。
  - `impact_level` 在 JSON 中对应 C# 的 `ImpactLevel`。

## 执行选择

Plan complete and saved to `参考mod/docs/superpowers/plans/2026-06-10-dialog-effect-implementation.md`. Two execution options:

1. **Subagent-Driven (recommended)** - I dispatch a fresh subagent per task, review between tasks, fast iteration.
2. **Inline Execution** - Execute tasks in this session using executing-plans, batch execution with checkpoints.

Which approach?
