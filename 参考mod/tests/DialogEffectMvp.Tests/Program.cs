using System;
using System.IO;
using System.Reflection;
using System.Text;
using MCS_AIChatMod;

namespace MCS_AIChatMod.Tests
{
    internal static class Program
    {
        private static int Main()
        {
            int failed = 0;
            failed += Run("ParseReply strips hidden effect block", ParseReplyStripsHiddenEffectBlock);
            failed += Run("ParseReply strips repeated hidden effect blocks", ParseReplyStripsRepeatedHiddenEffectBlocks);
            failed += Run("ValidateAndClamp clamps severe insult", ValidateAndClampClampsSevereInsult);
            failed += Run("ValidateAndClamp rejects low-confidence event", ValidateAndClampRejectsLowConfidenceEvent);
            failed += Run("ValidateAndClamp accepts mixed-case impact level", ValidateAndClampAcceptsMixedCaseImpactLevel);
            failed += Run("BuildFallbackEffect escalates explicit severe insult and threat", BuildFallbackEffectEscalatesExplicitSevereInsultAndThreat);
            failed += Run("BuildFallbackEffect ignores ordinary conversation", BuildFallbackEffectIgnoresOrdinaryConversation);
            failed += Run("ApplyChatFavorGainCap caps positive gain at 60", ApplyChatFavorGainCapCapsPositiveGainAtSixty);
            failed += Run("ApplyChatFavorGainCap blocks positive gain at 60", ApplyChatFavorGainCapBlocksPositiveGainAtSixty);
            failed += Run("ApplyChatFavorGainCap keeps negative delta at 60", ApplyChatFavorGainCapKeepsNegativeDeltaAtSixty);
            failed += Run("Dialog battle trigger detects clear challenge reply", DialogBattleTriggerDetectsClearChallengeReply);
            failed += Run("Dialog battle trigger treats severe favor loss as battle event", DialogBattleTriggerTreatsSevereFavorLossAsBattleEvent);
            failed += Run("Dialog battle trigger requires severe insult attack", DialogBattleTriggerRequiresSevereInsultAttack);
            failed += Run("BuildFallbackEffect treats severe defamation as severe battle event", BuildFallbackEffectTreatsSevereDefamationAsSevereBattleEvent);
            failed += Run("PromptBuilder resolves bundled card by npc id", PromptBuilderResolvesBundledCardByNpcId);
            failed += Run("PromptBuilder resolves bundled special card by npc name", PromptBuilderResolvesBundledSpecialCardByNpcName);
            failed += Run("PromptBuilder returns null for unknown bundled card", PromptBuilderReturnsNullForUnknownBundledCard);
            failed += Run("PromptBuilder puts role relationship and realm before numeric attributes", PromptBuilderPutsRoleRelationshipAndRealmFirst);
            failed += Run("NativeDialogPresenter builds safe Next say line", NativeDialogPresenterBuildsSafeNextSayLine);
            failed += Run("NativeDialogPresenter keeps fallback speaker when npc name missing", NativeDialogPresenterKeepsFallbackSpeakerWhenNpcNameMissing);
            failed += Run("NPCDialog reopens immersive input after normal reply", NPCDialogReopensImmersiveInputAfterNormalReply);
            failed += Run("NPCDialog does not reopen immersive input before battle", NPCDialogDoesNotReopenImmersiveInputBeforeBattle);
            failed += Run("NPCDialog describes elapsed dialog time", NPCDialogDescribesElapsedDialogTime);
            failed += Run("NPCDialog builds prompt time context", NPCDialogBuildsPromptTimeContext);

            if (failed > 0)
            {
                Console.Error.WriteLine(failed + " DialogEffectMvp test(s) failed.");
                return 1;
            }

            Console.WriteLine("All DialogEffectMvp tests passed.");
            return 0;
        }

        private static int Run(string name, Action test)
        {
            try
            {
                test();
                Console.WriteLine("PASS " + name);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("FAIL " + name + ": " + ex.Message);
                return 1;
            }
        }

        private static void ParseReplyStripsHiddenEffectBlock()
        {
            object parsed = ParseReply("道友此言甚善。\n<dialog_effect>{\"effect_type\":\"none\",\"impact_level\":\"none\",\"favor_delta\":0,\"confidence\":1.0}</dialog_effect>");

            AssertEqual("道友此言甚善。", GetField<string>(parsed, "VisibleText"), "visible text");
            object effect = GetField<object>(parsed, "Effect");
            AssertEqual("none", GetField<string>(effect, "EffectType"), "effect type");
            AssertEqual(0, GetField<int>(effect, "FavorDelta"), "favor delta");
        }

        private static void ValidateAndClampClampsSevereInsult()
        {
            object parsed = ParseReply("你这等见识也配与我论道？\n<dialog_effect>{\"effect_type\":\"insult_attack\",\"impact_level\":\"severe\",\"favor_delta\":-80,\"confidence\":0.95}</dialog_effect>");
            int delta = ValidateAndClamp(GetField<object>(parsed, "Effect"));

            AssertEqual(-25, delta, "clamped severe insult delta");
        }

        private static void ParseReplyStripsRepeatedHiddenEffectBlocks()
        {
            object parsed = ParseReply("台词\n<dialog_effect>{\"effect_type\":\"none\",\"impact_level\":\"none\",\"favor_delta\":0}</dialog_effect>\n<dialog_effect>{\"effect_type\":\"none\",\"impact_level\":\"none\",\"favor_delta\":0}</dialog_effect>");

            AssertEqual("台词", GetField<string>(parsed, "VisibleText"), "visible text without repeated blocks");
        }

        private static void ValidateAndClampRejectsLowConfidenceEvent()
        {
            object parsed = ParseReply("此物赠予道友。\n<dialog_effect>{\"effect_type\":\"gift\",\"impact_level\":\"major\",\"favor_delta\":8,\"confidence\":0.2}</dialog_effect>");
            int delta = ValidateAndClamp(GetField<object>(parsed, "Effect"));

            AssertEqual(0, delta, "low-confidence delta");
        }

        private static void ValidateAndClampAcceptsMixedCaseImpactLevel()
        {
            object parsed = ParseReply("此物赠予道友。\n<dialog_effect>{\"effect_type\":\"gift\",\"impact_level\":\"Major\",\"favor_delta\":8,\"confidence\":0.9}</dialog_effect>");
            int delta = ValidateAndClamp(GetField<object>(parsed, "Effect"));

            AssertEqual(8, delta, "mixed-case impact level delta");
        }

        private static void BuildFallbackEffectEscalatesExplicitSevereInsultAndThreat()
        {
            object effect = BuildFallbackEffect("ni zhe zhong feiwu ye gan dang wo de lu? zai duo zui wo jiu rang ni shenbai minglie.", "dao you he bi ru ren?");
            int delta = ValidateAndClamp(effect);

            AssertEqual("insult_attack", GetField<string>(effect, "EffectType"), "fallback effect type");
            AssertEqual("severe", GetField<string>(effect, "ImpactLevel"), "fallback impact level");
            AssertEqual(-25, delta, "fallback threat delta");
        }

        private static void BuildFallbackEffectIgnoresOrdinaryConversation()
        {
            object effect = BuildFallbackEffect("ni hao, qing wen jinri tianqi ruhe?", "dao you, jinri feng he ri li.");
            int delta = ValidateAndClamp(effect);

            AssertEqual("none", GetField<string>(effect, "EffectType"), "ordinary effect type");
            AssertEqual(0, delta, "ordinary delta");
        }

        private static void ApplyChatFavorGainCapCapsPositiveGainAtSixty()
        {
            int delta = ApplyChatFavorGainCap(58, 8);

            AssertEqual(2, delta, "capped positive favor delta");
        }

        private static void ApplyChatFavorGainCapBlocksPositiveGainAtSixty()
        {
            int delta = ApplyChatFavorGainCap(60, 8);

            AssertEqual(0, delta, "blocked positive favor delta");
        }

        private static void ApplyChatFavorGainCapKeepsNegativeDeltaAtSixty()
        {
            int delta = ApplyChatFavorGainCap(60, -10);

            AssertEqual(-10, delta, "negative favor delta");
        }

        private static void DialogBattleTriggerDetectsClearChallengeReply()
        {
            object effect = CreateEffect("insult_attack", "severe", -25, 0.95);
            bool shouldTrigger = ShouldTriggerBattleAfterDialog("今日便与你一战，拔剑吧！", effect, -25);

            AssertEqual(true, shouldTrigger, "clear challenge should trigger battle");
        }

        private static void DialogBattleTriggerTreatsSevereFavorLossAsBattleEvent()
        {
            object effect = CreateEffect("insult_attack", "severe", -25, 0.95);
            bool shouldTrigger = ShouldTriggerBattleAfterDialog("古兄慎言。倪某与你尚有情分，莫让口舌伤了和气。", effect, -25);

            AssertEqual(true, shouldTrigger, "severe favor loss should trigger battle without challenge keywords");
        }

        private static void DialogBattleTriggerRequiresSevereInsultAttack()
        {
            object effect = CreateEffect("insult_attack", "major", -10, 0.95);
            bool shouldTrigger = ShouldTriggerBattleAfterDialog("既如此，战吧。", effect, -10);

            AssertEqual(false, shouldTrigger, "major insult should not trigger battle");
        }

        private static void BuildFallbackEffectTreatsSevereDefamationAsSevereBattleEvent()
        {
            object effect = BuildFallbackEffect("你勾结魔道，欺师灭祖，还要毁我全族名声。", "你此言太过。");
            int delta = ValidateAndClamp(effect);

            AssertEqual("insult_attack", GetField<string>(effect, "EffectType"), "severe defamation effect type");
            AssertEqual("severe", GetField<string>(effect, "ImpactLevel"), "severe defamation impact level");
            AssertEqual(-25, delta, "severe defamation delta");
        }

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

        private static void NativeDialogPresenterBuildsSafeNextSayLine()
        {
            Type type = GetNativeDialogPresenterType();
            MethodInfo method = type.GetMethod("BuildNextSayLineForTest", BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new InvalidOperationException("NativeDialogPresenter.BuildNextSayLineForTest was not found.");
            }

            string line = (string)method.Invoke(null, new object[] { "NPC#A\r\nB", "First#Second\r\nThird" });

            AssertEqual("NPC A B#First Second Third", line, "safe Next say line");
        }

        private static void NativeDialogPresenterKeepsFallbackSpeakerWhenNpcNameMissing()
        {
            Type type = GetNativeDialogPresenterType();
            MethodInfo method = type.GetMethod("BuildNextSayLineForTest", BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new InvalidOperationException("NativeDialogPresenter.BuildNextSayLineForTest was not found.");
            }

            string line = (string)method.Invoke(null, new object[] { "", "Silent." });

            AssertEqual("NPC#Silent.", line, "fallback speaker Next say line");
        }

        private static void NPCDialogReopensImmersiveInputAfterNormalReply()
        {
            bool shouldReopen = ShouldReopenImmersiveInputAfterReply(showImmersiveReply: true, shouldTriggerBattle: false);

            AssertEqual(true, shouldReopen, "normal immersive reply should reopen input");
        }

        private static void NPCDialogDoesNotReopenImmersiveInputBeforeBattle()
        {
            bool shouldReopen = ShouldReopenImmersiveInputAfterReply(showImmersiveReply: true, shouldTriggerBattle: true);

            AssertEqual(false, shouldReopen, "battle immersive reply should not reopen input");
        }

        private static void NPCDialogDescribesElapsedDialogTime()
        {
            string elapsed = DescribeDialogElapsedTime("0001年01月01日", "0001年02月03日");

            AssertEqual("1个月3天", elapsed, "elapsed dialog time");
        }

        private static void NPCDialogBuildsPromptTimeContext()
        {
            string context = BuildPromptTimeContext("0149年02月01日", "0149年02月15日");

            AssertContains(context, "当前游戏时间：0149年02月15日", "current game time");
            AssertContains(context, "上一次对话时间：0149年02月01日", "last dialog time");
            AssertContains(context, "距离上一次对话：14天", "elapsed dialog time context");
        }

        private static object ParseReply(string raw)
        {
            Type type = GetDialogEffectType();
            MethodInfo method = type.GetMethod("ParseReply", BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new InvalidOperationException("DialogEffectMvp.ParseReply was not found.");
            }

            return method.Invoke(null, new object[] { raw });
        }

        private static int ValidateAndClamp(object effect)
        {
            Type type = GetDialogEffectType();
            MethodInfo method = type.GetMethod("ValidateAndClamp", BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new InvalidOperationException("DialogEffectMvp.ValidateAndClamp was not found.");
            }

            return (int)method.Invoke(null, new[] { effect });
        }

        private static object BuildFallbackEffect(string playerInput, string npcReply)
        {
            Type type = GetDialogEffectType();
            MethodInfo method = type.GetMethod("BuildFallbackEffect", BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new InvalidOperationException("DialogEffectMvp.BuildFallbackEffect was not found.");
            }

            return method.Invoke(null, new object[] { playerInput, npcReply });
        }

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

        private static int ApplyChatFavorGainCap(int currentFavor, int requestedDelta)
        {
            Type type = GetDialogEffectType();
            MethodInfo method = type.GetMethod("ApplyChatFavorGainCap", BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new InvalidOperationException("DialogEffectMvp.ApplyChatFavorGainCap was not found.");
            }

            return (int)method.Invoke(null, new object[] { currentFavor, requestedDelta });
        }

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

        private static bool ShouldReopenImmersiveInputAfterReply(bool showImmersiveReply, bool shouldTriggerBattle)
        {
            MethodInfo method = typeof(NPCDialog).GetMethod("ShouldReopenImmersiveInputAfterReplyForTest", BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new InvalidOperationException("NPCDialog.ShouldReopenImmersiveInputAfterReplyForTest was not found.");
            }

            return (bool)method.Invoke(null, new object[] { showImmersiveReply, shouldTriggerBattle });
        }

        private static string DescribeDialogElapsedTime(string lastGameTimeText, string currentGameTimeText)
        {
            MethodInfo method = typeof(NPCDialog).GetMethod("DescribeDialogElapsedTimeForTest", BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new InvalidOperationException("NPCDialog.DescribeDialogElapsedTimeForTest was not found.");
            }

            return (string)method.Invoke(null, new object[] { lastGameTimeText, currentGameTimeText });
        }

        private static string BuildPromptTimeContext(string lastGameTimeText, string currentGameTimeText)
        {
            MethodInfo method = typeof(NPCDialog).GetMethod("BuildPromptTimeContextForTest", BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new InvalidOperationException("NPCDialog.BuildPromptTimeContextForTest was not found.");
            }

            return (string)method.Invoke(null, new object[] { lastGameTimeText, currentGameTimeText });
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

        private static Type GetDialogEffectType()
        {
            Type type = typeof(AIChatManager).Assembly.GetType("MCS_AIChatMod.DialogEffectMvp");
            if (type == null)
            {
                throw new InvalidOperationException("MCS_AIChatMod.DialogEffectMvp type was not found.");
            }

            return type;
        }

        private static Type GetNativeDialogPresenterType()
        {
            Type type = typeof(AIChatManager).Assembly.GetType("MCS_AIChatMod.NativeDialogPresenter");
            if (type == null)
            {
                throw new InvalidOperationException("MCS_AIChatMod.NativeDialogPresenter type was not found.");
            }

            return type;
        }

        private static T GetField<T>(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new InvalidOperationException("Field was not found: " + name);
            }

            return (T)field.GetValue(target);
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

        private static void AssertEqual<T>(T expected, T actual, string label)
        {
            if (!object.Equals(expected, actual))
            {
                throw new InvalidOperationException(label + " expected <" + expected + "> but got <" + actual + ">.");
            }
        }

        private static void AssertEndsWith(string actual, string expectedSuffix, string label)
        {
            if (string.IsNullOrEmpty(actual) || !actual.Replace('\\', '/').EndsWith(expectedSuffix.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(label + " expected suffix <" + expectedSuffix + "> but got <" + actual + ">.");
            }
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
    }
}
