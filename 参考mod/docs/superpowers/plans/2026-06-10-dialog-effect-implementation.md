# Dialog Effect MVP Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. This is the simplified MVP plan requested by the user: directly modify the existing reference mod instead of building a large separate framework.

**Goal:** 在现有 `MCS_AIChatMod` 上加一个最小可用的“对话影响好感度”功能：AI 回复中带隐藏效果 JSON，Mod 剥离 JSON、按事件分档校验，并调用游戏好感度逻辑修改当前 NPC 好感度。通过聊天获得的正向好感度最高到 `60`，负向对话效果仍可降低好感度。

**Architecture:** 直接从 `参考mod/plugins/MCS_AIChatMod.dll` 反编译出源码，在原 mod 源码中新增一个小型 `DialogEffectMvp` 处理器，并接入现有 NPC 对话回复流程。第一版只做好感度变化和简短提示；`anger_delta`、`trigger_candidates` 只保留为后续扩展信息，不触发战斗或任务。

**Tech Stack:** C#、Unity/Mono、BepInEx/Harmony 风格参考 mod、Newtonsoft.Json、.NET SDK 8、ilspycmd、PowerShell。

---

## 计划取舍

这个版本刻意不做完整框架：

- 不单独创建 `DialogEffectCore` 测试工程。
- 不拆出 parser、policy、validator、executor 多个文件。
- 不做复杂记忆系统。
- 不做任务、战斗、送礼真实物品流转。
- 不每个小步骤都提交。

MVP 只追求一件事：**在原 mod 对话流程里跑通“对话 -> 好感度变化”**。

## 需要安装的环境

先安装 .NET SDK 和反编译工具：

```powershell
winget install --id Microsoft.DotNet.SDK.8 --source winget --accept-package-agreements --accept-source-agreements
dotnet --list-sdks

dotnet tool install --global ilspycmd
$env:PATH = "$env:USERPROFILE\.dotnet\tools;$env:PATH"
ilspycmd --version
```

期望结果：

```text
dotnet --list-sdks 显示 8.x.x
ilspycmd --version 能正常输出版本号
```

## 文件改动范围

预计只改这些文件：

- 创建：`参考mod/src/MCS_AIChatMod/`
  - 从 `参考mod/plugins/MCS_AIChatMod.dll` 反编译出的可编辑源码。
- 创建：`参考mod/src/GameAssembly/`
  - 从游戏 `Assembly-CSharp.dll` 导出的只读参考源码，用来确认 `NPCEx.AddFavor` / `NPCEx.GetFavor` 调用路径。
- 创建：`参考mod/src/MCS_AIChatMod/DialogEffectMvp.cs`
  - MVP 单文件处理器：解析隐藏 JSON、校验分档、调用好感度修改、显示提示。
- 修改：`参考mod/src/MCS_AIChatMod/NPCDialog.cs`
  - 在 AI 回复进入 UI/历史记录前调用 `DialogEffectMvp`。
- 修改：`参考mod/plugins/prompt.txt`
  - 让 AI 回复附带 `<dialog_effect>` 隐藏 JSON。

不提交这些文件：

- `参考mod/plugins/*.dll`
- `参考mod/plugins/*.pdb`
- `参考mod/plugins/*.xml`
- `参考mod/Mod.bin`
- 真实游戏安装目录文件
- 游戏存档

## Task 1: 反编译参考 mod 和游戏程序集

**Files:**
- Create: `参考mod/src/MCS_AIChatMod/`
- Create: `参考mod/src/GameAssembly/`

- [ ] **Step 1: 导出参考 mod 源码**

Run:

```powershell
New-Item -ItemType Directory -Force -Path "E:\RPG_agent\参考mod\src\MCS_AIChatMod" | Out-Null
ilspycmd -p -o "E:\RPG_agent\参考mod\src\MCS_AIChatMod" "E:\RPG_agent\参考mod\plugins\MCS_AIChatMod.dll"
```

Verify:

```powershell
rg -n "OnUISendMessage|AppendMessage|AppendMessageToNpcHistory|GetAIResponseAsync|OfficialNpcFavorChangedPatch" "E:\RPG_agent\参考mod\src\MCS_AIChatMod"
```

期望看到 `NPCDialog`、`UIManager`、`AIChatManager` 或 `OfficialNpcFavorChangedPatch` 相关命中。

- [ ] **Step 2: 导出游戏程序集参考源码**

Run:

```powershell
New-Item -ItemType Directory -Force -Path "E:\RPG_agent\参考mod\src\GameAssembly" | Out-Null
ilspycmd -p -o "E:\RPG_agent\参考mod\src\GameAssembly" "F:\SteamLibrary\steamapps\common\觅长生\觅长生_Data\Managed\Assembly-CSharp.dll"
```

Verify:

```powershell
rg -n "NPCEx\\.AddFavor|NPCEx\\.GetFavor|class NPCEx" "E:\RPG_agent\参考mod\src\GameAssembly"
```

期望能找到 `NPCEx.AddFavor` 和 `NPCEx.GetFavor` 的声明或现有调用路径。

## Task 2: 更新 prompt，让 AI 输出隐藏效果 JSON

**Files:**
- Modify: `参考mod/plugins/prompt.txt`

- [ ] **Step 1: 在 prompt 末尾追加协议**

Append to `参考mod/plugins/prompt.txt`:

```text

【隐藏对话效果协议】
每次 NPC 正常回复后，额外输出一个 <dialog_effect> JSON 块。这个块不是 NPC 台词，不应被玩家看到。

普通寒暄、询问常识、无明显情绪变化、无利益关系变化时，必须输出：
<dialog_effect>
{"effect_type":"none","impact_level":"none","favor_delta":0,"reason":"普通对话，没有明确情绪或利益影响","memory_summary":"","anger_delta":0,"trigger_candidates":[],"confidence":1.0}
</dialog_effect>

只有出现明确情绪变化、利益相关、送礼、让利、辱骂、诋毁、攻击、威胁、欺骗、背叛等事件时，才建议好感度变化。
通过聊天获得的正向好感度最高到 60；当前好感度达到或超过 60 后，不再建议通过聊天增加好感度。负向事件仍可建议降低好感度。
effect_type 只能使用：none、emotional_shift、interest_gain、interest_loss、gift、insult_attack、trust_break。
impact_level 只能使用：none、minor、moderate、major、severe。
默认分档：minor=-1..+1，moderate=-3..+3，major=-10..+10，severe=-25..+25。
anger_delta 和 trigger_candidates 只是候选信息，不能直接触发战斗、任务、传送、发物品或杀死 NPC。
```

- [ ] **Step 2: 检查 prompt 里协议存在**

Run:

```powershell
rg -n "隐藏对话效果协议|dialog_effect|effect_type|impact_level" "E:\RPG_agent\参考mod\plugins\prompt.txt"
```

期望看到刚追加的协议内容。

## Task 3: 新增单文件 MVP 处理器

**Files:**
- Create: `参考mod/src/MCS_AIChatMod/DialogEffectMvp.cs`

- [ ] **Step 1: 创建 `DialogEffectMvp.cs`**

Create `参考mod/src/MCS_AIChatMod/DialogEffectMvp.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MCS_AIChatMod
{
    internal static class DialogEffectMvp
    {
        private static readonly Regex EffectBlockRegex = new Regex(
            "<dialog_effect>\\s*(?<json>.*?)\\s*</dialog_effect>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Dictionary<string, int[]> ImpactRanges = new Dictionary<string, int[]>
        {
            { "none", new[] { 0, 0 } },
            { "minor", new[] { -1, 1 } },
            { "moderate", new[] { -3, 3 } },
            { "major", new[] { -10, 10 } },
            { "severe", new[] { -25, 25 } }
        };

        internal sealed class ParsedReply
        {
            public string VisibleText;
            public Effect Effect;
        }

        internal sealed class Effect
        {
            public string EffectType = "none";
            public string ImpactLevel = "none";
            public int FavorDelta;
            public string Reason = string.Empty;
            public string MemorySummary = string.Empty;
            public int AngerDelta;
            public double Confidence = 1.0;
        }

        internal static ParsedReply ParseReply(string rawReply)
        {
            if (string.IsNullOrEmpty(rawReply))
            {
                return new ParsedReply { VisibleText = string.Empty, Effect = new Effect() };
            }

            Match match = EffectBlockRegex.Match(rawReply);
            if (!match.Success)
            {
                return new ParsedReply { VisibleText = rawReply.Trim(), Effect = new Effect() };
            }

            string visible = EffectBlockRegex.Replace(rawReply, string.Empty, 1).Trim();
            Effect effect = ParseEffectJson(match.Groups["json"].Value);
            return new ParsedReply { VisibleText = visible, Effect = effect };
        }

        internal static bool TryApply(int npcId, string npcName, Effect effect)
        {
            int delta = ValidateAndClamp(effect);
            if (npcId <= 0 || delta == 0)
            {
                return false;
            }

            bool applied = TryAddNpcFavor(npcId, delta);
            if (!applied)
            {
                Debug.LogWarning("[MCS_AIChatMod] DialogEffectMvp failed. npcId=" + npcId + " delta=" + delta);
                return false;
            }

            ShowTip(delta);
            Debug.Log("[MCS_AIChatMod] DialogEffectMvp applied. npcId=" + npcId + " npcName=" + npcName + " delta=" + delta + " type=" + effect.EffectType + " reason=" + effect.Reason);
            return true;
        }

        private static Effect ParseEffectJson(string json)
        {
            try
            {
                JObject obj = JObject.Parse(json);
                return new Effect
                {
                    EffectType = ReadString(obj, "effect_type", "none"),
                    ImpactLevel = ReadString(obj, "impact_level", "none"),
                    FavorDelta = ReadInt(obj, "favor_delta", 0),
                    Reason = ReadString(obj, "reason", string.Empty),
                    MemorySummary = ReadString(obj, "memory_summary", string.Empty),
                    AngerDelta = ReadInt(obj, "anger_delta", 0),
                    Confidence = ReadDouble(obj, "confidence", 1.0)
                };
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MCS_AIChatMod] DialogEffectMvp parse failed: " + ex.Message);
                return new Effect();
            }
        }

        private static int ValidateAndClamp(Effect effect)
        {
            if (effect == null || effect.EffectType == "none")
            {
                return 0;
            }

            if (effect.Confidence < 0.6)
            {
                return 0;
            }

            int direction = ExpectedDirection(effect.EffectType);
            if (direction == 0)
            {
                return 0;
            }

            if (direction > 0 && effect.FavorDelta < 0)
            {
                return 0;
            }

            if (direction < 0 && effect.FavorDelta > 0)
            {
                return 0;
            }

            int[] range;
            if (!ImpactRanges.TryGetValue(effect.ImpactLevel, out range))
            {
                return 0;
            }

            if (effect.FavorDelta < range[0]) return range[0];
            if (effect.FavorDelta > range[1]) return range[1];
            return effect.FavorDelta;
        }

        private static int ExpectedDirection(string effectType)
        {
            if (effectType == "gift" || effectType == "interest_gain") return 1;
            if (effectType == "interest_loss" || effectType == "insult_attack" || effectType == "trust_break") return -1;
            if (effectType == "emotional_shift") return 2;
            return 0;
        }

        private static bool TryAddNpcFavor(int npcId, int delta)
        {
            // 用 Task 4 从 NPCEx.AddFavor.OnEnter() 反编译出的最小调用替换这里。
            // 替换前保持 false，避免误以为已经改了真实好感度。
            return false;
        }

        private static void ShowTip(int delta)
        {
            string message = delta > 0
                ? "对方态度有所缓和：好感度 +" + delta
                : "对方态度转冷：好感度 " + delta;

            UIManager ui = UIManager.Instance;
            if (ui != null)
            {
                ui.AppendMessage("系统", message, false, NPCDialog.GetCurrentGameTimeTextOrDefault(), string.Empty, null);
            }
        }

        private static string ReadString(JObject obj, string key, string fallback)
        {
            JToken token;
            return obj.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out token) && token != null
                ? token.ToString()
                : fallback;
        }

        private static int ReadInt(JObject obj, string key, int fallback)
        {
            JToken token;
            int value;
            return obj.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out token) && int.TryParse(token.ToString(), out value)
                ? value
                : fallback;
        }

        private static double ReadDouble(JObject obj, string key, double fallback)
        {
            JToken token;
            double value;
            return obj.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out token) && double.TryParse(token.ToString(), out value)
                ? value
                : fallback;
        }
    }
}
```

- [ ] **Step 2: 如果导出的项目文件不自动包含新 `.cs`，手动加入**

Run:

```powershell
rg -n "DialogEffectMvp|Compile Include" "E:\RPG_agent\参考mod\src\MCS_AIChatMod"
```

如果 `MCS_AIChatMod.csproj` 是老式项目，并且需要显式列出源码文件，在 `.csproj` 的 compile item group 中加入：

```xml
<Compile Include="DialogEffectMvp.cs" />
```

## Task 4: 确认并接入真实好感度修改逻辑

**Files:**
- Modify: `参考mod/src/MCS_AIChatMod/DialogEffectMvp.cs`

- [ ] **Step 1: 查看 `NPCEx.AddFavor` / `NPCEx.GetFavor` 用法**

Run:

```powershell
rg -n "NPCEx\\.AddFavor|NPCEx\\.GetFavor|OfficialNpcFavorChangedPatch" "E:\RPG_agent\参考mod\src\MCS_AIChatMod" "E:\RPG_agent\参考mod\src\GameAssembly"
```

目标是确认现有任务系统和 Harmony patch 如何读写 NPC 好感度，并确认 `NPCEx.AddFavor` 会触发 `OfficialNpcFavorChangedPatch`。

- [ ] **Step 2: 替换 `TryAddNpcFavor` 并加入聊天收益上限**

在 `参考mod/src/MCS_AIChatMod/DialogEffectMvp.cs` 中，用 `NPCEx.GetFavor` 和 `NPCEx.AddFavor` 替换旧执行路径：

```csharp
private static bool TryAddNpcFavor(int npcId, int delta)
{
    NPCEx.AddFavor(npcId, delta);
    return true;
}
```

正向 `delta` 执行前必须读取当前好感度：当前好感度 `>= 60` 时跳过；当前好感度加本次增益会超过 `60` 时，只执行到 `60` 为止。负向 `delta` 不受该上限影响。

替换原则：

- 优先复用游戏已有的 NPC 好感度修改 API。
- 不直接改真实游戏安装目录。
- 不直接写存档文件。
- 如果只能通过 Fungus `Command` 触发，先暂停，确认这种调用在 mod 运行态是否安全。

## Task 5: 接入 NPC 对话回复流程

**Files:**
- Modify: `参考mod/src/MCS_AIChatMod/NPCDialog.cs`

- [ ] **Step 1: 找到 AI 回复进入 UI/历史的位置**

Run:

```powershell
rg -n "AppendMessage|AppendMessageToNpcHistory|AddDialogEntry|GetAIResponseAsync|OnUISendMessage" "E:\RPG_agent\参考mod\src\MCS_AIChatMod\NPCDialog.cs" "E:\RPG_agent\参考mod\src\MCS_AIChatMod\AIChatManager.cs"
```

需要找到这条链路：

```text
AI 原始回复字符串 -> UI 显示 -> NPC 对话历史保存
```

- [ ] **Step 2: 在显示前剥离隐藏 JSON**

在 AI 原始回复字符串进入 `UIManager.AppendMessage` 或 `AppendMessageToNpcHistory` 前，插入：

```csharp
DialogEffectMvp.ParsedReply parsedReply = DialogEffectMvp.ParseReply(aiResponseText);
aiResponseText = parsedReply.VisibleText;
DialogEffectMvp.TryApply(currentNpcId, npcName, parsedReply.Effect);
```

这里的 `aiResponseText`、`currentNpcId`、`npcName` 用当前文件里已有的真实变量替换。判断标准很简单：

- `aiResponseText` 是即将显示给玩家的 AI 回复。
- `currentNpcId` 是当前 NPC 的 ID。
- `npcName` 是当前 NPC 名称。
- 修改后，玩家不能看到 `<dialog_effect>` 块。

如果当前位置拿不到 NPC ID，就把 `TryApply(...)` 放到能拿到 NPC ID 的稍后位置，但 `ParseReply(...)` 必须在显示前执行。

## Task 6: 编译、部署和游戏内验证

**Files:**
- Modify: `参考mod/src/MCS_AIChatMod/`
- Copy output to game mod plugin directory only after build succeeds.

- [ ] **Step 1: 编译 mod**

Run:

```powershell
dotnet build "E:\RPG_agent\参考mod\src\MCS_AIChatMod\MCS_AIChatMod.csproj"
```

期望：

```text
Build succeeded.
```

如果缺 Unity、BepInEx、Harmony 或游戏 DLL 引用，给 `.csproj` 增加引用路径，引用来源优先使用：

```text
F:\SteamLibrary\steamapps\common\觅长生\觅长生_Data\Managed
E:\RPG_agent\参考mod\plugins
```

- [ ] **Step 2: 部署到测试环境**

只在测试用 mod 目录替换 DLL。执行时先用下面两条命令确认“编译输出 DLL”和“测试用 mod DLL”的真实位置：

```powershell
Get-ChildItem -LiteralPath "E:\RPG_agent\参考mod\src\MCS_AIChatMod\bin" -Recurse -Filter "MCS_AIChatMod.dll"
Get-ChildItem -LiteralPath "E:\RPG_agent\参考mod" -Recurse -Filter "MCS_AIChatMod.dll"
```

确认路径后，只备份并替换测试用 mod 目录里的 `MCS_AIChatMod.dll`。不要覆盖真实游戏安装目录里的原始文件，也不要把备份 DLL 提交到 git。

- [ ] **Step 3: 游戏内验证**

用测试存档验证三类对话：

```text
普通对话：道友近来可好？
期望：NPC 正常回复，不显示好感度变化提示。

正向事件：此物赠予道友，也算结个善缘。
期望：NPC 正常回复；当前好感度低于 60 时增加好感度并显示简短提示；接近 60 时只加到 60；达到 60 后不再通过聊天增加。

负向事件：你这等见识也配与我论道？莫要自取其辱。
期望：NPC 愤怒回复，好感度降低，显示简短提示，不触发战斗。
```

同时确认：

- `<dialog_effect>` 没有显示给玩家。
- 普通对话不会频繁改变好感度。
- `trigger_candidates` 不触发任何真实游戏行为。
- 存档没有异常。

## 完成标准

MVP 完成只需要满足：

- 原 mod 可以编译出 DLL。
- AI 回复里的 `<dialog_effect>` 不显示给玩家。
- 普通对话不改好感度。
- 正向事件能增加当前 NPC 好感度，但通过聊天获得的正向好感度最高到 `60`。
- 负向事件能降低当前 NPC 好感度。
- 游戏里出现简短提示。
- 不触发战斗、任务、发物品、传送等后续行为。

## 执行建议

这个 MVP 更适合 **Inline Execution**：

1. 先安装 `.NET SDK 8` 和 `ilspycmd`。
2. 反编译源码。
3. 我读 `NPCDialog.cs` 和 `NPCEx.AddFavor` 的真实代码。
4. 我按上面最小方案改原 mod。
5. 你进游戏用测试存档验证。

不建议现在走复杂的多子任务流程。先把真实好感度改动跑通，再决定要不要抽象成完整框架。
