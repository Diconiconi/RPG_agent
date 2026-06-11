using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Fungus;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MCS_AIChatMod;

internal static class DialogEffectMvp
{
	private const int ChatFavorGainCap = 60;

	private static readonly Regex EffectBlockRegex = new Regex("<dialog_effect>\\s*(?<json>.*?)\\s*</dialog_effect>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

	private static readonly Dictionary<string, int[]> ImpactRanges = new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase)
	{
		{ "none", new[] { 0, 0 } },
		{ "minor", new[] { -1, 1 } },
		{ "moderate", new[] { -3, 3 } },
		{ "major", new[] { -10, 10 } },
		{ "severe", new[] { -25, 25 } }
	};

	private static readonly string[] FallbackInsultKeywords = new[]
	{
		"\u5e9f\u7269",
		"\u72d7\u4e1c\u897f",
		"\u8812\u8d27",
		"\u50bb\u5b50",
		"\u5783\u573e",
		"\u6df7\u8d26",
		"\u4e0d\u914d",
		"\u755c\u751f",
		"feiwu",
		"goudongxi",
		"gou dongxi",
		"chunhuo",
		"laji",
		"bupei"
	};

	private static readonly string[] FallbackThreatKeywords = new[]
	{
		"\u8eab\u8d25\u540d\u88c2",
		"\u518d\u591a\u5634",
		"\u4f60\u7b49\u7740",
		"\u8ba9\u4f60",
		"\u627e\u6b7b",
		"shenbaiminglie",
		"shenbai minglie",
		"zai duo zui",
		"rang ni"
	};

	private static readonly string[] FallbackSevereThreatKeywords = new[]
	{
		"\u6740\u4e86\u4f60",
		"\u5f04\u6b7b",
		"\u706d\u4e86\u4f60",
		"\u706d\u95e8",
		"\u53d6\u4f60\u6027\u547d",
		"sha le ni",
		"shale ni",
		"nongsi",
		"miemen"
	};

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
			return new ParsedReply
			{
				VisibleText = string.Empty,
				Effect = new Effect()
			};
		}
		Match match = EffectBlockRegex.Match(rawReply);
		if (!match.Success)
		{
			return new ParsedReply
			{
				VisibleText = rawReply.Trim(),
				Effect = new Effect()
			};
		}
		string visibleText = EffectBlockRegex.Replace(rawReply, string.Empty).Trim();
		Effect effect = ParseEffectJson(match.Groups["json"].Value);
		return new ParsedReply
		{
			VisibleText = visibleText,
			Effect = effect
		};
	}

	internal static bool TryApply(int npcId, string npcName, Effect effect, out int appliedDelta)
	{
		return TryApplyInternal(npcId, npcName, effect, showFeedback: true, out appliedDelta);
	}

	internal static bool TryApplyWithoutFeedback(int npcId, string npcName, Effect effect, out int appliedDelta)
	{
		return TryApplyInternal(npcId, npcName, effect, showFeedback: false, out appliedDelta);
	}

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

	internal static Effect ResolveEffectForApplication(string playerInput, string npcVisibleReply, Effect parsedEffect)
	{
		if (ValidateAndClamp(parsedEffect) != 0)
		{
			return parsedEffect;
		}
		return BuildFallbackEffect(playerInput, npcVisibleReply);
	}

	internal static bool ShouldTriggerBattleAfterDialogForDialog(int npcId, string npcVisibleReply, Effect effect)
	{
		return npcId > 0 && ShouldTriggerBattleAfterDialog(npcVisibleReply, effect, ValidateAndClamp(effect));
	}

	internal static bool TryTriggerBattleAfterDialog(int npcId, string npcName, string npcVisibleReply, Effect effect, int appliedDelta)
	{
		if (npcId <= 0 || !ShouldTriggerBattleAfterDialog(npcVisibleReply, effect, appliedDelta))
		{
			return false;
		}
		try
		{
			if (Tools.instance?.getPlayer() == null)
			{
				AIChatManager.logger?.LogWarning((object)("[DialogEffectMvp] 战斗触发失败：玩家实例不存在, npcId=" + npcId));
				return false;
			}
			ShowBattleTriggerTip(appliedDelta);
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

	private static Effect BuildFallbackEffect(string playerInput, string npcReply)
	{
		string rawText = ((playerInput ?? string.Empty) + "\n" + (npcReply ?? string.Empty)).ToLowerInvariant();
		string compactText = RemoveWhitespace(rawText);
		int insultHits = CountKeywordHits(rawText, compactText, FallbackInsultKeywords);
		int threatHits = CountKeywordHits(rawText, compactText, FallbackThreatKeywords);
		int severeThreatHits = CountKeywordHits(rawText, compactText, FallbackSevereThreatKeywords);
		if (insultHits <= 0 && threatHits <= 0 && severeThreatHits <= 0)
		{
			return new Effect();
		}
		if (severeThreatHits > 0)
		{
			return new Effect
			{
				EffectType = "insult_attack",
				ImpactLevel = "severe",
				FavorDelta = -25,
				Reason = "local fallback detected explicit lethal threat",
				AngerDelta = 25,
				Confidence = 0.9
			};
		}
		if (threatHits > 0)
		{
			return new Effect
			{
				EffectType = "insult_attack",
				ImpactLevel = "major",
				FavorDelta = -10,
				Reason = "local fallback detected explicit insult or threat",
				AngerDelta = 15,
				Confidence = 0.85
			};
		}
		return new Effect
		{
			EffectType = "insult_attack",
			ImpactLevel = "moderate",
			FavorDelta = -3,
			Reason = "local fallback detected explicit insult",
			AngerDelta = 8,
			Confidence = 0.8
		};
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
			AIChatManager.logger?.LogWarning((object)("[DialogEffectMvp] 隐藏效果 JSON 解析失败: " + ex.Message));
			return new Effect();
		}
	}

	private static int ValidateAndClamp(Effect effect)
	{
		if (effect == null || string.Equals(effect.EffectType, "none", StringComparison.OrdinalIgnoreCase))
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
		if (direction == 1 && effect.FavorDelta < 0)
		{
			return 0;
		}
		if (direction == -1 && effect.FavorDelta > 0)
		{
			return 0;
		}
		if (!ImpactRanges.TryGetValue(effect.ImpactLevel, out int[] range))
		{
			return 0;
		}
		if (effect.FavorDelta < range[0])
		{
			return range[0];
		}
		if (effect.FavorDelta > range[1])
		{
			return range[1];
		}
		return effect.FavorDelta;
	}

	private static int ExpectedDirection(string effectType)
	{
		if (string.Equals(effectType, "gift", StringComparison.OrdinalIgnoreCase) || string.Equals(effectType, "interest_gain", StringComparison.OrdinalIgnoreCase))
		{
			return 1;
		}
		if (string.Equals(effectType, "interest_loss", StringComparison.OrdinalIgnoreCase) || string.Equals(effectType, "insult_attack", StringComparison.OrdinalIgnoreCase) || string.Equals(effectType, "trust_break", StringComparison.OrdinalIgnoreCase))
		{
			return -1;
		}
		if (string.Equals(effectType, "emotional_shift", StringComparison.OrdinalIgnoreCase))
		{
			return 2;
		}
		return 0;
	}

	private static int ApplyChatFavorGainCap(int currentFavor, int requestedDelta)
	{
		if (requestedDelta <= 0)
		{
			return requestedDelta;
		}
		if (currentFavor >= ChatFavorGainCap)
		{
			return 0;
		}
		return Math.Min(requestedDelta, ChatFavorGainCap - currentFavor);
	}

	private static bool TryGetNpcFavor(int npcId, out int currentFavor)
	{
		currentFavor = 0;
		try
		{
			currentFavor = NPCEx.GetFavor(npcId);
			return true;
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[MCS_AIChatMod] DialogEffectMvp GetFavor failed: " + ex.Message);
			return false;
		}
	}

	private static bool TryAddNpcFavor(int npcId, int delta)
	{
		try
		{
			NPCEx.AddFavor(npcId, delta);
			return true;
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[MCS_AIChatMod] DialogEffectMvp AddFavor failed: " + ex.Message);
			return false;
		}
	}

	private static void ShowFeedbackTip(int delta)
	{
		string message = (delta > 0) ? ("\u5bf9\u65b9\u6001\u5ea6\u6709\u6240\u7f13\u548c\uff1a\u597d\u611f\u5ea6 +" + delta) : ("\u5bf9\u65b9\u6001\u5ea6\u8f6c\u51b7\uff1a\u597d\u611f\u5ea6 " + delta);
		UIManager.Instance?.AppendMessage("\u7cfb\u7edf", message, isPlayer: false, NPCDialog.GetCurrentGameTimeTextOrDefault(), string.Empty, null);
	}

	private static void ShowBattleTriggerTip(int delta)
	{
		string message = "对方怒意已决，好感度 " + delta + "，即将进入战斗。";
		UIManager.Instance?.AppendMessage("系统", message, isPlayer: false, NPCDialog.GetCurrentGameTimeTextOrDefault(), string.Empty, null);
	}

	private static int CountKeywordHits(string rawText, string compactText, IEnumerable<string> keywords)
	{
		int hits = 0;
		foreach (string keyword in keywords)
		{
			if (ContainsFallbackKeyword(rawText, compactText, keyword))
			{
				hits++;
			}
		}
		return hits;
	}

	private static bool ContainsFallbackKeyword(string rawText, string compactText, string keyword)
	{
		if (string.IsNullOrWhiteSpace(keyword))
		{
			return false;
		}
		string normalizedKeyword = keyword.ToLowerInvariant();
		if (rawText.Contains(normalizedKeyword))
		{
			return true;
		}
		string compactKeyword = RemoveWhitespace(normalizedKeyword);
		return compactKeyword.Length > 0 && compactText.Contains(compactKeyword);
	}

	private static string RemoveWhitespace(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return string.Empty;
		}
		char[] buffer = new char[text.Length];
		int length = 0;
		for (int i = 0; i < text.Length; i++)
		{
			char ch = text[i];
			if (!char.IsWhiteSpace(ch))
			{
				buffer[length++] = ch;
			}
		}
		return new string(buffer, 0, length);
	}

	private static string ReadString(JObject obj, string key, string fallback)
	{
		if (obj.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out JToken token) && token != null)
		{
			return token.ToString();
		}
		return fallback;
	}

	private static int ReadInt(JObject obj, string key, int fallback)
	{
		if (obj.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out JToken token) && token != null && int.TryParse(token.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
		{
			return value;
		}
		return fallback;
	}

	private static double ReadDouble(JObject obj, string key, double fallback)
	{
		if (obj.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out JToken token) && token != null && double.TryParse(token.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
		{
			return value;
		}
		return fallback;
	}
}
