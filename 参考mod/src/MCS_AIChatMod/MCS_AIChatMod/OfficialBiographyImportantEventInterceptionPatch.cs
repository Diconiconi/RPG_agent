using System;
using HarmonyLib;
using Newtonsoft.Json.Linq;

namespace MCS_AIChatMod;

[HarmonyPatch(typeof(NpcJieSuanManager), "CheckImportantEvent", new Type[] { typeof(string) })]
internal static class OfficialBiographyImportantEventInterceptionPatch
{
	private static bool Prefix(NpcJieSuanManager __instance, string nowTime)
	{
		if (__instance == null)
		{
			return true;
		}
		DateTime dateTime = DateTime.Parse(nowTime);
		Tools.instance.getPlayer();
		foreach (JSONObject jsonobject in jsonData.instance.NpcImprotantEventData.list)
		{
			if (!__instance.ImportantNpcBangDingDictionary.ContainsKey(jsonobject["ImportantNPC"].I))
			{
				continue;
			}
			int npcId = __instance.ImportantNpcBangDingDictionary[jsonobject["ImportantNPC"].I];
			JSONObject npcData = __instance.GetNpcData(npcId);
			DateTime targetTime = DateTime.Parse(jsonobject["Time"].str);
			if (dateTime < targetTime)
			{
				continue;
			}
			if (jsonobject["EventLv"].Count > 0)
			{
				bool passed = false;
				int currentValue = GlobalValue.Get(jsonobject["EventLv"][0].I, "NpcJieSuanManager.CheckImportantEvent(" + nowTime + ")");
				if (jsonobject["fuhao"].str == "=")
				{
					passed = currentValue == jsonobject["EventLv"][1].I;
				}
				else if (jsonobject["fuhao"].Str == ">")
				{
					passed = currentValue > jsonobject["EventLv"][1].I;
				}
				else if (jsonobject["fuhao"].Str == "<")
				{
					passed = currentValue < jsonobject["EventLv"][1].I;
				}
				if (!passed)
				{
					continue;
				}
			}
			bool canWrite = true;
			if (npcData["NoteBook"].HasField("101"))
			{
				foreach (JSONObject entry in npcData["NoteBook"]["101"].list)
				{
					if (entry["gudingshijian"].I == jsonobject["id"].I)
					{
						canWrite = false;
					}
				}
			}
			if (canWrite && !OfficialBiographyInterceptionHelper.TryCapture(BiographyEventKind.ImprotantEvent, npcId, "NpcJieSuanManager.CheckImportantEvent", new JObject
			{
				["npcId"] = npcId,
				["eventId"] = jsonobject["id"].I,
				["eventTime"] = jsonobject["Time"].Str
			}, jsonobject["Time"].Str))
			{
				__instance.npcNoteBook.NoteImprotantEvent(npcId, jsonobject["id"].I, jsonobject["Time"].Str);
			}
		}
		return false;
	}
}
