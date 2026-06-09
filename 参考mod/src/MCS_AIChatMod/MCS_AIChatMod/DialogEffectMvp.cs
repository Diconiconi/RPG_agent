using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MCS_AIChatMod;

internal static class DialogEffectMvp
{
	private static readonly Regex EffectBlockRegex = new Regex("<dialog_effect>\\s*(?<json>.*?)\\s*</dialog_effect>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

	private static readonly Dictionary<string, int[]> ImpactRanges = new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase)
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

	internal static bool TryApply(int npcId, string npcName, Effect effect)
	{
		int delta = ValidateAndClamp(effect);
		if (npcId <= 0 || delta == 0)
		{
			return false;
		}
		if (!TryAddNpcQingFen(npcId, delta))
		{
			AIChatManager.logger?.LogWarning((object)("[DialogEffectMvp] 情分修改失败: npcId=" + npcId + ", delta=" + delta));
			return false;
		}
		ShowTip(delta);
		AIChatManager.logger?.LogInfo((object)("[DialogEffectMvp] 已应用对话效果: npcId=" + npcId + ", npcName=" + npcName + ", delta=" + delta + ", type=" + effect.EffectType + ", reason=" + effect.Reason));
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

	private static bool TryAddNpcQingFen(int npcId, int delta)
	{
		try
		{
			NPCEx.AddQingFen(npcId, delta, showTip: true);
			return true;
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[MCS_AIChatMod] DialogEffectMvp AddQingFen failed: " + ex.Message);
			return false;
		}
	}

	private static void ShowTip(int delta)
	{
		string message = (delta > 0) ? ("对方态度有所缓和：情分 +" + delta) : ("对方态度转冷：情分 " + delta);
		UIManager.Instance?.AppendMessage("系统", message, isPlayer: false, NPCDialog.GetCurrentGameTimeTextOrDefault(), string.Empty, null);
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
