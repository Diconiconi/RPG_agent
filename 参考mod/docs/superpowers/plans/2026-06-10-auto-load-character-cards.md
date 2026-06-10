# Auto-load Character Cards Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让现有 NPC 聊天 prompt 在运行目录角色卡不存在时，自动从 `RPGSkillKit/character_cards_minimal` 里加载对应 NPC 的初始设定卡，并把角色卡、关系定位、双方境界等级放到最高优先级，避免低境界 NPC 对高境界前辈失礼或猖狂。

**Status:** 已于 2026-06-10 执行完成。实现采用最小改动：保留原运行目录角色卡优先级，仅在缺失时从内置 txt 草稿卡初始化；prompt 只调整优先块顺序与措辞，不重做消息结构。

**Architecture:** 保持现有逻辑不重做：`PromptBuilder.ResolveInitialContext(...)` 仍然读取 `Application.persistentDataPath/{npcId}_{npcName}_character.txt`。只在该运行目录角色卡不存在时，增加一个最小兜底：从插件目录下的 `RPGSkillKit/character_cards_minimal` 查找默认角色卡，写入运行目录并用于本轮 prompt；若找不到，则继续创建原来的“空白角色卡”。同时只调整 `BuildRoleplayAttributePrompt(...)` 内部文本顺序和措辞，把角色身份、关系、境界差距提升到最高优先块，不改变消息结构和 AI 请求流程。

**Tech Stack:** C#、Unity/Mono、BepInEx、现有 `PromptBuilder`、现有轻量测试工程 `DialogEffectMvp.Tests`。

---

## Scope

本计划只做“自动加载已有 txt 草稿卡”的最小功能：

- 自动读取 `参考mod/plugins/RPGSkillKit/character_cards_minimal/**/*.character.txt`。
- 支持两种匹配方式：
  - 先按 NPC ID 匹配文件名前缀，例如 `20288_ni_xuxin.character.txt`。
  - 再按少量特殊 NPC 名称映射匹配，例如 `魏无极 -> special/wei_wuji.character.txt`。
- 不解析 `characters/important/*.character.json`。
- 不实现完整 RPGSkillKit prompt builder。
- 不改变 AI 消息结构、聊天历史、任务摘要、好感度效果机制。
- 调整 NPC 属性 prompt 内部权重：最高优先块必须包含角色卡、关系定位、玩家境界、NPC 境界、境界礼法约束。
- 境界礼法约束只影响 NPC 说话态度，不允许让 NPC 突然知道自己本不该知道的隐藏信息；若 NPC 无法确认玩家境界，应按“不可确定，谨慎试探”处理。

## File Structure

- Modify: `参考mod/src/MCS_AIChatMod/MCS_AIChatMod/PromptBuilder.cs`
  - 增加查找内置角色卡的最小 helper。
  - 修改 `LoadCharacterCard(...)`，在运行目录卡不存在时先尝试复制内置默认卡。
  - 调整 `BuildRoleplayAttributePrompt(...)`，把角色卡、关系定位和双方境界等级放到最高优先块。
- Modify: `参考mod/tests/DialogEffectMvp.Tests/Program.cs`
  - 复用现有轻量测试运行器，增加 `PromptBuilder` 角色卡匹配测试。
  - 增加 `BuildRoleplayAttributePrompt(...)` 顺序测试，确保最高优先块出现在基础属性杂项之前。
- Modify: `参考mod/工作目录.md`
  - 记录当前角色卡加载规则，方便后续接手。
- Add: `worklogs/YYYY-MM-DD-auto-load-character-cards.md`
  - 实现完成后记录任务摘要、变更文件、验证过程、风险/备注、下一步。

## Task 1: Add Failing Tests For Bundled Character Card Resolution

**Files:**
- Modify: `参考mod/tests/DialogEffectMvp.Tests/Program.cs`

- [ ] **Step 1: Add test registrations**

在 `Main()` 现有测试注册区追加：

```csharp
failed += Run("PromptBuilder resolves bundled card by npc id", PromptBuilderResolvesBundledCardByNpcId);
failed += Run("PromptBuilder resolves bundled special card by npc name", PromptBuilderResolvesBundledSpecialCardByNpcName);
failed += Run("PromptBuilder returns null for unknown bundled card", PromptBuilderReturnsNullForUnknownBundledCard);
```

- [ ] **Step 2: Add test methods**

在现有测试方法区追加：

```csharp
private static void PromptBuilderResolvesBundledCardByNpcId()
{
    string root = CreateBundledCardFixture();
    string path = ResolveBundledCharacterCardPath(root, 20288, "倪旭欣");

    AssertEndsWith(path, Path.Combine("families", "20288_ni_xuxin.character.txt"), "npc id card path");
}

private static void PromptBuilderResolvesBundledSpecialCardByNpcName()
{
    string root = CreateBundledCardFixture();
    string path = ResolveBundledCharacterCardPath(root, 0, "魏无极");

    AssertEndsWith(path, Path.Combine("special", "wei_wuji.character.txt"), "special card path");
}

private static void PromptBuilderReturnsNullForUnknownBundledCard()
{
    string root = CreateBundledCardFixture();
    string path = ResolveBundledCharacterCardPath(root, 999999, "不存在的NPC");

    AssertEqual<string>(null, path, "unknown card path");
}
```

- [ ] **Step 3: Add reflection and fixture helpers**

在 `Program.cs` helper 区追加：

```csharp
private static string ResolveBundledCharacterCardPath(string root, int npcId, string npcName)
{
    Type type = typeof(PromptBuilder);
    MethodInfo method = type.GetMethod("ResolveBundledCharacterCardPathForTest", BindingFlags.Static | BindingFlags.NonPublic);
    if (method == null)
    {
        throw new InvalidOperationException("PromptBuilder.ResolveBundledCharacterCardPathForTest was not found.");
    }

    return (string)method.Invoke(null, new object[] { root, npcId, npcName });
}

private static string CreateBundledCardFixture()
{
    string root = Path.Combine(Path.GetTempPath(), "mcs_card_fixture_" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(Path.Combine(root, "families"));
    Directory.CreateDirectory(Path.Combine(root, "special"));
    File.WriteAllText(Path.Combine(root, "families", "20288_ni_xuxin.character.txt"), "你是倪旭欣。", Encoding.UTF8);
    File.WriteAllText(Path.Combine(root, "special", "wei_wuji.character.txt"), "你是魏无极。", Encoding.UTF8);
    return root;
}

private static void AssertEndsWith(string actual, string expectedSuffix, string label)
{
    if (string.IsNullOrEmpty(actual) || !actual.Replace('\\', '/').EndsWith(expectedSuffix.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(label + " expected suffix <" + expectedSuffix + "> but got <" + actual + ">.");
    }
}
```

在文件顶部补充 using：

```csharp
using System.IO;
using System.Text;
```

- [ ] **Step 4: Run tests and verify red**

Run:

```powershell
dotnet run --project "E:\RPG_agent\参考mod\tests\DialogEffectMvp.Tests\DialogEffectMvp.Tests.csproj"
```

Expected:

```text
FAIL PromptBuilder resolves bundled card by npc id: PromptBuilder.ResolveBundledCharacterCardPathForTest was not found.
FAIL PromptBuilder resolves bundled special card by npc name: PromptBuilder.ResolveBundledCharacterCardPathForTest was not found.
FAIL PromptBuilder returns null for unknown bundled card: PromptBuilder.ResolveBundledCharacterCardPathForTest was not found.
```

## Task 2: Implement Minimal Bundled Card Lookup In PromptBuilder

**Files:**
- Modify: `参考mod/src/MCS_AIChatMod/MCS_AIChatMod/PromptBuilder.cs`

- [ ] **Step 1: Add special NPC alias map**

在 `PromptBuilder` 类开头加入：

```csharp
private static readonly Dictionary<string, string> BundledCharacterCardAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    { "星铃儿", Path.Combine("special", "xing_linger_xingning.character.txt") },
    { "星凝", Path.Combine("special", "xing_linger_xingning.character.txt") },
    { "魏无极", Path.Combine("special", "wei_wuji.character.txt") },
    { "吞云大圣", Path.Combine("special", "tunyun_dasheng.character.txt") },
    { "火麒麟", Path.Combine("special", "huoqilin.character.txt") },
    { "冲虚", Path.Combine("special", "chongxu.character.txt") },
    { "白帝", Path.Combine("special", "baidi.character.txt") }
};
```

说明：这只是特殊无 ID 文件名的最小映射。带 NPC ID 的卡继续通过文件名前缀自动匹配。

- [ ] **Step 2: Add root resolver**

在 `PromptBuilder` 中加入：

```csharp
private static string ResolveBundledCharacterCardRoot()
{
    string pluginDirectory = ConfigManager.pluginDirectory;
    if (string.IsNullOrWhiteSpace(pluginDirectory))
    {
        pluginDirectory = Path.GetDirectoryName(typeof(PromptBuilder).Assembly.Location);
    }
    if (string.IsNullOrWhiteSpace(pluginDirectory))
    {
        return string.Empty;
    }
    return Path.Combine(pluginDirectory, "RPGSkillKit", "character_cards_minimal");
}
```

- [ ] **Step 3: Add bundled card path resolver**

在 `PromptBuilder` 中加入：

```csharp
private static string ResolveBundledCharacterCardPath(string bundledRoot, int npcId, string npcName)
{
    if (string.IsNullOrWhiteSpace(bundledRoot) || !Directory.Exists(bundledRoot))
    {
        return null;
    }
    if (npcId > 0)
    {
        string idPrefix = npcId.ToString() + "_";
        string idMatch = Directory.GetFiles(bundledRoot, "*.character.txt", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(path => Path.GetFileName(path).StartsWith(idPrefix, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(idMatch))
        {
            return idMatch;
        }
    }
    if (!string.IsNullOrWhiteSpace(npcName) && BundledCharacterCardAliases.TryGetValue(npcName.Trim(), out string relativePath))
    {
        string aliasPath = Path.Combine(bundledRoot, relativePath);
        if (File.Exists(aliasPath))
        {
            return aliasPath;
        }
    }
    return null;
}

internal static string ResolveBundledCharacterCardPathForTest(string bundledRoot, int npcId, string npcName)
{
    return ResolveBundledCharacterCardPath(bundledRoot, npcId, npcName);
}
```

- [ ] **Step 4: Add bundled card loader**

在 `PromptBuilder` 中加入：

```csharp
private static bool TryLoadBundledCharacterCard(int npcId, string npcName, out string content)
{
    content = string.Empty;
    string bundledPath = ResolveBundledCharacterCardPath(ResolveBundledCharacterCardRoot(), npcId, npcName);
    if (string.IsNullOrEmpty(bundledPath))
    {
        return false;
    }
    try
    {
        content = TextEncodingUtil.ReadAllTextWithFallback(bundledPath);
        return !string.IsNullOrWhiteSpace(content);
    }
    catch (Exception ex)
    {
        AIChatManager.logger?.LogWarning((object)("[PromptBuilder] 读取内置角色卡失败: " + bundledPath + ", " + ex.Message));
        content = string.Empty;
        return false;
    }
}
```

- [ ] **Step 5: Preserve old LoadCharacterCard API and add NPC-aware overload**

保留原签名，减少对其他代码的影响：

```csharp
public static string LoadCharacterCard(string targetFilePath)
{
    return LoadCharacterCard(targetFilePath, null);
}
```

新增重载：

```csharp
private static string LoadCharacterCard(string targetFilePath, NPCInfo npcInfo)
{
    try
    {
        if (File.Exists(targetFilePath))
        {
            return File.ReadAllText(targetFilePath);
        }
        string characterContent;
        if (npcInfo != null && TryLoadBundledCharacterCard(npcInfo.NpcID, npcInfo.Name, out characterContent))
        {
            File.WriteAllText(targetFilePath, characterContent, Encoding.UTF8);
            AIChatManager.logger?.LogInfo((object)("[PromptBuilder] 已从内置角色卡初始化: " + targetFilePath));
            return characterContent;
        }
        File.WriteAllText(targetFilePath, "空白角色卡", Encoding.UTF8);
        AIChatManager.logger.LogInfo((object)"目标角色卡文件不存在，已创建空白卡！");
        return "";
    }
    catch (Exception ex)
    {
        AIChatManager.logger.LogError((object)("加载角色卡时出错: " + ex.Message));
        return null;
    }
}
```

如果原方法里已有代码，直接替换为上面两个方法。需要在文件顶部补充：

```csharp
using System.Text;
```

- [ ] **Step 6: Call the NPC-aware overload**

在 `ResolveInitialContext(NPCInfo npcInfo)` 中，把：

```csharp
string characterContent = LoadCharacterCard(AIChatManager.targetFilePath = Path.Combine(Application.persistentDataPath, saveFileName + ".txt"));
```

替换为：

```csharp
string characterContent = LoadCharacterCard(AIChatManager.targetFilePath = Path.Combine(Application.persistentDataPath, saveFileName + ".txt"), npcInfo);
```

- [ ] **Step 7: Run tests and verify green**

Run:

```powershell
dotnet run --project "E:\RPG_agent\参考mod\tests\DialogEffectMvp.Tests\DialogEffectMvp.Tests.csproj"
```

Expected:

```text
PASS PromptBuilder resolves bundled card by npc id
PASS PromptBuilder resolves bundled special card by npc name
PASS PromptBuilder returns null for unknown bundled card
All DialogEffectMvp tests passed.
```

## Task 3: Raise Character, Relationship, And Realm Priority In Prompt

**Files:**
- Modify: `参考mod/src/MCS_AIChatMod/MCS_AIChatMod/PromptBuilder.cs`
- Modify: `参考mod/tests/DialogEffectMvp.Tests/Program.cs`

- [ ] **Step 1: Add failing test registration**

在 `Main()` 测试注册区追加：

```csharp
failed += Run("PromptBuilder puts role relationship and realm before numeric attributes", PromptBuilderPutsRoleRelationshipAndRealmFirst);
```

- [ ] **Step 2: Add failing test method**

在测试方法区追加：

```csharp
private static void PromptBuilderPutsRoleRelationshipAndRealmFirst()
{
    object npcInfo = CreateNpcInfoForPromptOrderTest();
    string prompt = BuildRoleplayAttributePrompt(npcInfo, "你是测试前辈，处事稳重。");

    AssertContains(prompt, "=== 最高优先：角色、关系与境界礼法 ===", "highest priority heading");
    AssertContains(prompt, "NPC角色卡:", "character card label");
    AssertContains(prompt, "双方境界:", "realm relationship label");
    AssertOrder(prompt, "=== 最高优先：角色、关系与境界礼法 ===", "以下为玩家的基础信息:", "highest priority before player base info");
    AssertOrder(prompt, "双方境界:", "以下为你要扮演的npc的基础信息:", "realm before npc numeric attributes");
}
```

- [ ] **Step 3: Add reflection helper for private prompt builder**

在 helper 区追加：

```csharp
private static string BuildRoleplayAttributePrompt(object npcInfo, string characterContent)
{
    Type type = typeof(PromptBuilder);
    MethodInfo method = type.GetMethod("BuildRoleplayAttributePrompt", BindingFlags.Static | BindingFlags.NonPublic);
    if (method == null)
    {
        throw new InvalidOperationException("PromptBuilder.BuildRoleplayAttributePrompt was not found.");
    }

    return (string)method.Invoke(null, new object[] { npcInfo, characterContent });
}

private static object CreateNpcInfoForPromptOrderTest()
{
    NPCInfo npc = new NPCInfo();
    npc.NpcID = 9876501;
    npc.Name = "测试金丹前辈";
    npc.Gender = "男";
    npc.XingGe = "稳重";
    npc.ZhengXie = "正道";
    npc.XiuWei = "金丹初期";
    npc.HaoGanDu = "相熟";
    npc.QingFen = 0;
    npc.StatusStr = "正常";
    npc.ShouYuan = 500;
    npc.Age = 180;
    npc.ZiZhi = 70;
    npc.WuXing = 70;
    npc.DunSu = 30;
    npc.ShenShi = 20;
    npc.Title = "前辈";
    return npc;
}

private static void AssertContains(string text, string expected, string label)
{
    if (text == null || !text.Contains(expected))
    {
        throw new InvalidOperationException(label + " expected to contain <" + expected + ">.");
    }
}

private static void AssertOrder(string text, string earlier, string later, string label)
{
    int earlierIndex = text.IndexOf(earlier, StringComparison.Ordinal);
    int laterIndex = text.IndexOf(later, StringComparison.Ordinal);
    if (earlierIndex < 0 || laterIndex < 0 || earlierIndex >= laterIndex)
    {
        throw new InvalidOperationException(label + " expected <" + earlier + "> before <" + later + ">.");
    }
}
```

- [ ] **Step 4: Run tests and verify red**

Run:

```powershell
dotnet run --project "E:\RPG_agent\参考mod\tests\DialogEffectMvp.Tests\DialogEffectMvp.Tests.csproj"
```

Expected:

```text
FAIL PromptBuilder puts role relationship and realm before numeric attributes: highest priority heading expected to contain <=== 最高优先：角色、关系与境界礼法 ===>.
```

- [ ] **Step 5: Replace the top of BuildRoleplayAttributePrompt**

在 `BuildRoleplayAttributePrompt(NPCInfo npcInfo, string characterContent)` 中，将开头的 prompt 组装调整为下面的结构。保留后面的背包、功法、神通逻辑不变：

```csharp
StringBuilder sb = new StringBuilder();
sb.AppendLine("请你扮演觅长生世界中的npc,每次回复控制在50字以内");
sb.AppendLine();
sb.AppendLine("=== 最高优先：角色、关系与境界礼法 ===");
sb.AppendLine("以下内容优先级高于普通闲聊、最近对话和杂项数值。回答时必须先符合角色卡、双方关系和境界礼法。");
sb.AppendLine("NPC角色卡:");
sb.AppendLine(string.IsNullOrWhiteSpace(characterContent) ? "空白角色卡" : characterContent.Trim());
sb.AppendLine();
sb.AppendLine(BuildRelationshipPriorityBlock(playerProfile.Name, playerProfile.Gender, playerProfile.Cultivation, playerProfile.Faction, playerProfile.Identity, AIQuestPromptBuilder.ResolveNpcPlayerRelationshipForPrompt(npcInfo?.NpcID ?? 0), playerProfile.VisibilitySummary));
sb.AppendLine();
sb.AppendLine("双方境界:");
sb.AppendLine("玩家境界: " + playerProfile.Cultivation);
sb.AppendLine("NPC境界: " + npcInfo.XiuWei);
sb.AppendLine("境界礼法: 若对方境界明显高于你，称呼和语气必须更克制、敬重、谨慎，不要轻易挑衅或猖狂；若你无法确认玩家真实境界，也应保留修士间的基本分寸，先试探而不是冒犯。若你的角色卡明确设定为高位、狂傲或敌对，也要让冒犯建立在角色动机和实力判断上，而不是无意义发疯。");
sb.AppendLine();
sb.AppendLine("以下为玩家的基础信息:");
```

然后保留原有玩家基础信息行：

```csharp
sb.AppendLine("玩家的名字:" + playerProfile.Name);
sb.AppendLine("玩家的性别:" + playerProfile.Gender + ",");
sb.AppendLine("玩家的修为:" + playerProfile.Cultivation + ",");
sb.AppendLine("玩家的门派:" + playerProfile.Faction + ",");
sb.AppendLine("玩家的当前身份:" + playerProfile.Identity + ",");
sb.AppendLine();
sb.AppendLine("以下为你要扮演的npc的基础信息:");
```

在 NPC 基础信息中保留 `npc的id`、名字、性格、正魔立场、状态等杂项，但删除原来的这一行，避免角色卡重复出现且权重分散：

```csharp
sb.AppendLine("npc的角色卡:" + characterContent);
```

- [ ] **Step 6: Run tests and verify green**

Run:

```powershell
dotnet run --project "E:\RPG_agent\参考mod\tests\DialogEffectMvp.Tests\DialogEffectMvp.Tests.csproj"
```

Expected:

```text
PASS PromptBuilder puts role relationship and realm before numeric attributes
All DialogEffectMvp tests passed.
```

## Task 4: Update Current Handoff Document

**Files:**
- Modify: `参考mod/工作目录.md`

- [ ] **Step 1: Add current character card behavior**

在“现有参考结构”或“推荐下一步”附近加入：

```markdown
## 角色卡加载规则

- 当前 Mod 会优先读取运行目录角色卡：
  `C:\Users\22749\AppData\LocalLow\yusuiInc\觅长生\{npcId}_{npcName}_character.txt`
- 如果运行目录角色卡不存在，Mod 会尝试从插件目录的 `RPGSkillKit/character_cards_minimal` 复制一张初始卡。
- 带 NPC ID 的草稿卡按文件名前缀匹配，例如 `20288_ni_xuxin.character.txt`。
- 无 NPC ID 的特殊角色先用少量名称映射匹配，例如魏无极、星铃儿/星凝、吞云大圣、火麒麟、冲虚、白帝。
- 如果仍找不到初始卡，保持旧逻辑：创建 `空白角色卡`。
- 玩家已经手动编辑过的运行目录角色卡优先级最高，不会被内置草稿覆盖。
- NPC 聊天 prompt 的最高优先块应包含：角色卡、玩家与 NPC 关系定位、玩家境界、NPC 境界、境界礼法约束。
- 境界礼法约束用于避免低境界 NPC 对高境界前辈无意义猖狂；如果 NPC 无法确认玩家境界，应谨慎试探，不应默认冒犯。
```

## Task 5: Build, Deploy Workspace DLL, Worklog

**Files:**
- Add: `worklogs/YYYY-MM-DD-auto-load-character-cards.md`
- Runtime copy only: `参考mod/plugins/MCS_AIChatMod.dll` ignored by Git

- [ ] **Step 1: Run Release build**

Run:

```powershell
dotnet build "E:\RPG_agent\参考mod\src\MCS_AIChatMod\MCS_AIChatMod.csproj" -c Release
```

Expected:

```text
已成功生成。
0 个错误
```

- [ ] **Step 2: Copy workspace DLL only**

Run:

```powershell
$source = Resolve-Path -LiteralPath "E:\RPG_agent\参考mod\src\MCS_AIChatMod\bin\Release\net472\MCS_AIChatMod.dll"
$target = Resolve-Path -LiteralPath "E:\RPG_agent\参考mod\plugins\MCS_AIChatMod.dll"
Copy-Item -LiteralPath $source.Path -Destination $target.Path -Force
Get-FileHash -LiteralPath $source.Path -Algorithm SHA256
Get-FileHash -LiteralPath $target.Path -Algorithm SHA256
```

Expected: two hashes match. Do not copy to Steam or Workshop directories unless the user explicitly asks.

- [ ] **Step 3: Write worklog**

Create `worklogs/YYYY-MM-DD-auto-load-character-cards.md` with:

```markdown
# YYYY-MM-DD 自动加载特殊 NPC 初始角色卡

## 任务摘要
- 在运行目录角色卡不存在时，从 `RPGSkillKit/character_cards_minimal` 自动初始化 NPC 角色卡。
- 保留现有运行目录角色卡优先级，不覆盖玩家手动编辑。

## 变更文件
- `参考mod/src/MCS_AIChatMod/MCS_AIChatMod/PromptBuilder.cs`
- `参考mod/tests/DialogEffectMvp.Tests/Program.cs`
- `参考mod/工作目录.md`

## 验证过程
- `dotnet run --project "E:\RPG_agent\参考mod\tests\DialogEffectMvp.Tests\DialogEffectMvp.Tests.csproj"`
- `dotnet build "E:\RPG_agent\参考mod\src\MCS_AIChatMod\MCS_AIChatMod.csproj" -c Release`

## 风险 / 备注
- 只自动加载 txt 草稿卡，不解析 JSON 角色卡。
- 如果运行目录已有角色卡，不会覆盖。
- 角色卡、关系定位和双方境界被提升到 NPC 属性 prompt 的最高优先块。

## 下一步
- 游戏内找魏无极或倪旭欣测试角色卡是否自动生成并进入 prompt。
- 用低境界 NPC 面对高境界玩家/前辈的对话测试语气是否更克制。
```

- [ ] **Step 4: Review staged files before commit**

Run:

```powershell
git status --short
git diff --name-status
```

Expected staged/modified text files only; no `.dll` / `.pdb` / `.bin` should be added.

## Verification Checklist

- [ ] 运行目录已有 `{npcId}_{npcName}_character.txt` 时，继续读取现有文件，不覆盖。
- [ ] 运行目录无角色卡、NPC ID 有匹配草稿时，自动复制 ID 草稿卡。
- [ ] 运行目录无角色卡、特殊 NPC 名称有映射时，自动复制 special 草稿卡。
- [ ] 未匹配 NPC 仍创建“空白角色卡”，保持旧行为。
- [ ] `PromptBuilder.ResolveInitialContext(...)` 仍然输出原有 system prompt、角色属性 prompt、任务摘要、长期记忆、最近对话。
- [ ] 角色属性 prompt 中，角色卡、关系定位、双方境界和境界礼法位于玩家基础信息、NPC 杂项属性、历史对话之前。
- [ ] 低境界 NPC 面对明显高境界对象时，prompt 明确要求克制、敬重、谨慎，不无意义猖狂。
- [ ] 不修改好感度、对话效果、任务系统、真实游戏安装目录。

## Self-Review

- Spec coverage: 覆盖了“特殊 NPC 信息卡自动补全 NPC 聊天 prompt”，同时保留现有运行目录角色卡优先级；新增覆盖“角色卡 + 关系定位 + 境界等级最高优先”。
- Placeholder scan: 无 TBD/TODO/占位任务。
- Type consistency: 新增 helper 均在 `PromptBuilder` 内；测试通过反射调用 `ResolveBundledCharacterCardPathForTest(...)`。
