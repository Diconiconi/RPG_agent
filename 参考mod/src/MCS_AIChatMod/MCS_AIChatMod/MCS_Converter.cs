using System;
using System.Collections.Generic;

namespace MCS_AIChatMod;

internal class MCS_Converter
{
	private static string[] dajingjie = new string[5] { "炼气", "筑基", "金丹", "元婴", "化神" };

	private static string[] xiaojingjie = new string[3] { "初期", "中期", "后期" };

	private static readonly Dictionary<int, string> CharacterMap = new Dictionary<int, string>
	{
		{ 1, "善良" },
		{ 2, "稳重" },
		{ 3, "洒脱" },
		{ 4, "活泼" },
		{ 5, "傲慢" },
		{ 6, "温柔" },
		{ 7, "孤僻" },
		{ 8, "暴躁" },
		{ 11, "阴险" },
		{ 12, "稳重" },
		{ 13, "洒脱" },
		{ 14, "傲慢" },
		{ 15, "贪婪" },
		{ 16, "唯我" },
		{ 17, "孤僻" },
		{ 18, "暴躁" }
	};

	private static readonly Dictionary<int, string> statusDict = new Dictionary<int, string>
	{
		{ 1, "正常" },
		{ 2, "陷入瓶颈" },
		{ 4, "身负重伤" },
		{ 5, "神识受损" },
		{ 6, "寿元将尽" },
		{ 10, "天人感应" },
		{ 11, "灵光闪现" },
		{ 12, "灵思枯竭" },
		{ 20, "普通受邀" },
		{ 21, "道侣受邀" }
	};

	public static string GetCharacterText(int number)
	{
		if (CharacterMap.ContainsKey(number))
		{
			return CharacterMap[number];
		}
		return "未知";
	}

	public static string GetXiuWeiText(int number)
	{
		if (number < 0 || number >= dajingjie.Length * xiaojingjie.Length)
		{
			return "未知";
		}
		return dajingjie[number / 3] + xiaojingjie[number % 3];
	}

	public static string GetStatusText(int number)
	{
		if (statusDict.ContainsKey(number))
		{
			return statusDict[number];
		}
		return "未知";
	}

	public static string GetFavorText(int favorValue)
	{
		Dictionary<string, Func<int, bool>> favorLevels = new Dictionary<string, Func<int, bool>>
		{
			{
				"仇恨",
				(int value) => value < -50
			},
			{
				"不满",
				(int value) => value < 0
			},
			{
				"陌生",
				(int value) => value == 0
			},
			{
				"相识",
				(int value) => value > 0 && value <= 50
			},
			{
				"熟悉",
				(int value) => value > 50 && value <= 100
			},
			{
				"友善",
				(int value) => value > 100 && value <= 150
			},
			{
				"信赖",
				(int value) => value > 150 && value < 200
			},
			{
				"亲密",
				(int value) => value >= 200
			}
		};
		foreach (KeyValuePair<string, Func<int, bool>> level in favorLevels)
		{
			if (level.Value(favorValue))
			{
				return level.Key;
			}
		}
		return "未知";
	}
}
