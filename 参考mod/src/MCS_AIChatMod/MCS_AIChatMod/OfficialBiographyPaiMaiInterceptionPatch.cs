using System;
using HarmonyLib;
using Newtonsoft.Json.Linq;

namespace MCS_AIChatMod;

[HarmonyPatch(typeof(NPCTeShu), "NextNpcPaiMai", new Type[] { })]
internal static class OfficialBiographyPaiMaiInterceptionPatch
{
	private static bool Prefix()
	{
		foreach (int paiMaiId in NpcJieSuanManager.inst.PaiMaiNpcDictionary.Keys)
		{
			int maxItems = jsonData.instance.NpcPaiMaiData[paiMaiId.ToString()]["ItemNum"].I;
			for (int index = 0; index < NpcJieSuanManager.inst.PaiMaiNpcDictionary[paiMaiId].Count && index < maxItems; index++)
			{
				int npcId = NpcJieSuanManager.inst.PaiMaiNpcDictionary[paiMaiId][index];
				if (jsonData.instance.AvatarJsonData.HasField(npcId.ToString()))
				{
					int randomTypeIndex = NpcJieSuanManager.inst.getRandomInt(0, jsonData.instance.NpcPaiMaiData[paiMaiId.ToString()]["Type"].Count - 1);
					int shopType = jsonData.instance.NpcPaiMaiData[paiMaiId.ToString()]["Type"][randomTypeIndex].I;
					int quality = jsonData.instance.NpcPaiMaiData[paiMaiId.ToString()]["quality"][randomTypeIndex].I;
					JSONObject randomItem = FactoryManager.inst.npcFactory.GetRandomItemByShopType(shopType, quality);
					int itemId = randomItem["id"].I;
					int price = randomItem["price"].I;
					if (!OfficialBiographyInterceptionHelper.TryCapture(BiographyEventKind.PaiMai, npcId, "NPCTeShu.NextNpcPaiMai", new JObject
					{
						["npcId"] = npcId,
						["itemId"] = itemId,
						["price"] = price,
						["sellerName"] = ""
					}))
					{
						NpcJieSuanManager.inst.AddItemToNpcBackpack(npcId, itemId, 1);
						NpcJieSuanManager.inst.npcNoteBook.NotePaiMai(npcId, itemId);
						NpcJieSuanManager.inst.npcSetField.AddNpcMoney(npcId, -price);
					}
				}
			}
		}
		return false;
	}
}
