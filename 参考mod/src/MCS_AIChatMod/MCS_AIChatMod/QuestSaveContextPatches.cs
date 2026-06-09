using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace MCS_AIChatMod;

[HarmonyPatch]
internal static class QuestSaveContextPatches
{
	private static readonly string[] NewSaveTypeNames = new string[2] { "YSNewSaveSystem", "YSGame.YSNewSaveSystem" };

	private static readonly string[] LegacySaveTypeNames = new string[2] { "YSFileControl", "YSGame.YSFileControl" };

	private static readonly string[] NewSaveMethodNames = new string[18]
	{
		"AddAvatar", "AutoLoad", "AutoSave", "CreatAvatar", "LoadAvatar", "LoadOldSave", "LoadSave", "Save", "SaveAvatar", "SaveGame",
		"SetAvatar", "LoadArchive", "SaveArchive", "ReadNowArchiveData", "StartGameByLoad", "CreateNewArchive", "SwitchToArchive", "SelectArchive"
	};

	private static readonly string[] LegacyMethodNames = new string[5] { "ReadPlayerFile", "SavePlayerFile", "SetNowPlayerFile", "OnClickAvatar", "OnClickSave" };

	private static List<MethodBase> _cachedTargetMethods;

	private static bool _loggedTargetMethodsSummary;

	private static bool Prepare()
	{
		_cachedTargetMethods = BuildTargetMethods().ToList();
		if (_cachedTargetMethods.Count == 0)
		{
			ManualLogSource logger = AIChatManager.logger;
			if (logger != null)
			{
				logger.LogWarning((object)"[QuestSaveContextPatches] 未找到任何可用的存档上下文补丁目标，已跳过该补丁。");
			}
			return false;
		}
		if (!_loggedTargetMethodsSummary)
		{
			ManualLogSource logger2 = AIChatManager.logger;
			if (logger2 != null)
			{
				logger2.LogInfo((object)$"[QuestSaveContextPatches] 已找到 {_cachedTargetMethods.Count} 个存档上下文补丁目标。");
			}
			_loggedTargetMethodsSummary = true;
		}
		return true;
	}

	private static IEnumerable<MethodBase> TargetMethods()
	{
		return _cachedTargetMethods ?? (_cachedTargetMethods = BuildTargetMethods().ToList());
	}

	private static IEnumerable<MethodBase> BuildTargetMethods()
	{
		HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
		string[] newSaveTypeNames = NewSaveTypeNames;
		foreach (string typeName in newSaveTypeNames)
		{
			foreach (MethodBase method in CollectMethods(typeName, NewSaveMethodNames))
			{
				string key = $"{method.DeclaringType?.FullName}|{method}";
				if (seen.Add(key))
				{
					yield return method;
				}
			}
		}
		string[] legacySaveTypeNames = LegacySaveTypeNames;
		foreach (string typeName2 in legacySaveTypeNames)
		{
			foreach (MethodBase method2 in CollectMethods(typeName2, LegacyMethodNames))
			{
				string key2 = $"{method2.DeclaringType?.FullName}|{method2}";
				if (seen.Add(key2))
				{
					yield return method2;
				}
			}
		}
	}

	private static IEnumerable<MethodBase> CollectMethods(string typeName, string[] methodNames)
	{
		Type type = AccessTools.TypeByName(typeName);
		if (type == null)
		{
			yield break;
		}
		List<MethodInfo> declaredMethods = AccessTools.GetDeclaredMethods(type);
		HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
		foreach (string methodName in methodNames)
		{
			foreach (MethodInfo method in declaredMethods)
			{
				if (!(method.Name != methodName) && seen.Add(method.ToString()))
				{
					yield return method;
				}
			}
		}
	}

	private static void Prefix(MethodBase __originalMethod)
	{
		SyncSaveContext(__originalMethod, "prefix");
	}

	private static void Postfix(MethodBase __originalMethod)
	{
		SyncSaveContext(__originalMethod, "postfix");
	}

	private static void SyncSaveContext(MethodBase method, string phase)
	{
		try
		{
			int avatarIndex = -1;
			int slotIndex = -1;
			bool useNewSaveSystem = false;
			if (TryGetNewSaveContext(out avatarIndex, out slotIndex))
			{
				useNewSaveSystem = true;
			}
			else
			{
				avatarIndex = PlayerPrefs.GetInt("NowPlayerFileAvatar", -1);
				slotIndex = 0;
			}
			if (avatarIndex >= 0)
			{
				QuestSaveContextEventType eventType = ClassifyContextEvent(method?.Name);
				bool isPostfix = string.Equals(phase, "postfix", StringComparison.Ordinal);
				AIQuestManager.NotifyGameSaveContext(avatarIndex, slotIndex, useNewSaveSystem, eventType, isPostfix);
				ManualLogSource logger = AIChatManager.logger;
				if (logger != null)
				{
					logger.LogDebug((object)$"[QuestSaveContextPatches] {phase}:{method?.DeclaringType?.Name}.{method?.Name} -> avatar={avatarIndex}, slot={slotIndex}, isNew={useNewSaveSystem}");
				}
			}
		}
		catch (Exception ex)
		{
			ManualLogSource logger2 = AIChatManager.logger;
			if (logger2 != null)
			{
				logger2.LogWarning((object)("[QuestSaveContextPatches] context sync failed (" + phase + "): " + ex.Message));
			}
		}
	}

	private static QuestSaveContextEventType ClassifyContextEvent(string methodName)
	{
		switch (methodName)
		{
		case "AutoSave":
		case "Save":
		case "SaveAvatar":
		case "SaveGame":
		case "SaveArchive":
		case "SavePlayerFile":
			return QuestSaveContextEventType.Save;
		case "AutoLoad":
		case "LoadAvatar":
		case "LoadOldSave":
		case "LoadSave":
		case "LoadArchive":
		case "StartGameByLoad":
		case "ReadPlayerFile":
		case "ReadNowArchiveData":
			return QuestSaveContextEventType.Load;
		case "SwitchToArchive":
			return QuestSaveContextEventType.Switch;
		case "AddAvatar":
		case "CreatAvatar":
		case "CreateNewArchive":
		case "SelectArchive":
		case "SetAvatar":
		case "SetNowPlayerFile":
		case "OnClickAvatar":
		case "OnClickSave":
			return QuestSaveContextEventType.Select;
		default:
			return QuestSaveContextEventType.Unknown;
		}
	}

	internal static bool TryGetNewSaveContext(out int avatarIndex, out int slotIndex)
	{
		avatarIndex = -1;
		slotIndex = -1;
		try
		{
			if (string.IsNullOrEmpty(YSNewSaveSystem.NowAvatarPathPre))
			{
				return false;
			}
			avatarIndex = YSNewSaveSystem.NowUsingAvatarIndex;
			slotIndex = YSNewSaveSystem.NowUsingSlot;
			return avatarIndex >= 0;
		}
		catch
		{
			return false;
		}
	}
}
