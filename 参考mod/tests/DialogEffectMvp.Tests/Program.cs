using System;
using System.Reflection;
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
            failed += Run("BuildFallbackEffect detects explicit insult and threat", BuildFallbackEffectDetectsExplicitInsultAndThreat);
            failed += Run("BuildFallbackEffect ignores ordinary conversation", BuildFallbackEffectIgnoresOrdinaryConversation);

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

        private static void BuildFallbackEffectDetectsExplicitInsultAndThreat()
        {
            object effect = BuildFallbackEffect("ni zhe zhong feiwu ye gan dang wo de lu? zai duo zui wo jiu rang ni shenbai minglie.", "dao you he bi ru ren?");
            int delta = ValidateAndClamp(effect);

            AssertEqual("insult_attack", GetField<string>(effect, "EffectType"), "fallback effect type");
            AssertEqual("major", GetField<string>(effect, "ImpactLevel"), "fallback impact level");
            AssertEqual(-10, delta, "fallback threat delta");
        }

        private static void BuildFallbackEffectIgnoresOrdinaryConversation()
        {
            object effect = BuildFallbackEffect("ni hao, qing wen jinri tianqi ruhe?", "dao you, jinri feng he ri li.");
            int delta = ValidateAndClamp(effect);

            AssertEqual("none", GetField<string>(effect, "EffectType"), "ordinary effect type");
            AssertEqual(0, delta, "ordinary delta");
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

        private static Type GetDialogEffectType()
        {
            Type type = typeof(AIChatManager).Assembly.GetType("MCS_AIChatMod.DialogEffectMvp");
            if (type == null)
            {
                throw new InvalidOperationException("MCS_AIChatMod.DialogEffectMvp type was not found.");
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

        private static void AssertEqual<T>(T expected, T actual, string label)
        {
            if (!object.Equals(expected, actual))
            {
                throw new InvalidOperationException(label + " expected <" + expected + "> but got <" + actual + ">.");
            }
        }
    }
}
