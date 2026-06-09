using HarmonyLib;
using KBEngine;

namespace MCS_AIChatMod;

[HarmonyPatch(typeof(CyEmailCell), "Init")]
internal static class QuestCyMailCellInitPatch
{
	private static void Postfix(CyEmailCell __instance, EmailData emailData, bool isDeath)
	{
		if (QuestCyMailHelper.TryGetQuestMailData(emailData, out var mailData))
		{
			ConfigureQuestMailCell(__instance, emailData, mailData, isDeath);
		}
	}

	private static void ConfigureQuestMailCell(CyEmailCell cell, EmailData emailData, JSONObject mailData, bool isDeath)
	{
		if (!(cell == null) && !(CyUIMag.inst?.cyEmail == null))
		{
			string npcName = (mailData.HasField("AvatarName") ? mailData["AvatarName"].Str : cell.sendName.text);
			int itemId = (mailData.HasField("ItemID") ? mailData["ItemID"].I : 0);
			int itemCount = QuestCyMailHelper.GetMailItemCount(mailData);
			cell.sendName.text = npcName;
			cell.sendTime.color = CyUIMag.inst.cyEmail.titleColors[0];
			cell.sendName.color = CyUIMag.inst.cyEmail.titleColors[0];
			cell.bg.sprite = CyUIMag.inst.cyEmail.titleSprites[0];
			cell.content.text = "\u3000" + (mailData.HasField("info") ? mailData["info"].Str : string.Empty);
			ResetSubmitButton(cell);
			cell.item.gameObject.transform.parent.gameObject.SetActive(value: false);
			if (itemId > 0)
			{
				cell.item.Init();
				cell.item.SetItem(itemId);
				cell.item.Count = itemCount;
			}
			string mailType = QuestCyMailHelper.GetMailType(mailData);
			if (mailType == "Delivery")
			{
				ConfigureDeliveryCell(cell, emailData, mailData, isDeath, itemId, itemCount);
			}
			else
			{
				ConfigureRewardCell(cell, mailData, itemId, itemCount);
			}
			cell.UpdateSize();
		}
	}

	private static void ConfigureRewardCell(CyEmailCell cell, JSONObject mailData, int itemId, int itemCount)
	{
		if (itemId <= 0)
		{
			return;
		}
		if (mailData.HasField("ItemHasGet") && mailData["ItemHasGet"].b)
		{
			cell.item.ShowHasGet();
			cell.submitBtn.gameObject.SetActive(value: false);
			return;
		}
		cell.submitBtn.Init(CyUIMag.inst.cyEmail.btnSprites[0], "领取", delegate
		{
			cell.submitBtn.gameObject.SetActive(value: false);
			mailData.SetField("ItemHasGet", val: true);
			Tools.instance.getPlayer().addItem(itemId, itemCount, Tools.CreateItemSeid(itemId));
			cell.item.ShowHasGet();
			cell.UpdateSize();
		});
	}

	private static void ConfigureDeliveryCell(CyEmailCell cell, EmailData emailData, JSONObject mailData, bool isDeath, int itemId, int itemCount)
	{
		if (itemId <= 0)
		{
			return;
		}
		if (mailData.HasField("SubmitComplete") && mailData["SubmitComplete"].b)
		{
			cell.item.ShowHasTiJiao();
			cell.submitBtn.gameObject.SetActive(value: false);
			return;
		}
		cell.submitBtn.gameObject.SetActive(value: true);
		if (isDeath)
		{
			cell.submitBtn.gameObject.SetActive(value: false);
			return;
		}
		int hasNum = CyUIMag.inst.cyEmail.GetPlayerItemNum(itemId);
		if (hasNum >= itemCount)
		{
			cell.item.SetCustomCountText($"{hasNum}/{itemCount}", CyUIMag.inst.cyEmail.numColors[0]);
			cell.submitBtn.Init(CyUIMag.inst.cyEmail.btnSprites[0], "提交", delegate
			{
				int playerItemNum = CyUIMag.inst.cyEmail.GetPlayerItemNum(itemId);
				if (playerItemNum < itemCount)
				{
					UIPopTip.Inst.Pop("物品不足");
					ConfigureDeliveryCell(cell, emailData, mailData, isDeath, itemId, itemCount);
					cell.UpdateSize();
				}
				else
				{
					Avatar player = Tools.instance.getPlayer();
					cell.submitBtn.gameObject.SetActive(value: false);
					player.removeItem(itemId, itemCount);
					mailData.SetField("SubmitComplete", val: true);
					cell.item.ShowHasTiJiao();
					int mailFavorAmount = QuestCyMailHelper.GetMailFavorAmount(mailData);
					int num = 0;
					try
					{
						num = jsonData.instance.ItemJsonData[itemId.ToString()]["price"].I * itemCount;
					}
					catch
					{
					}
					if (mailFavorAmount > 0)
					{
						NPCEx.AddFavor(emailData.npcId, mailFavorAmount, addQingFen: false);
					}
					if (num > 0)
					{
						NPCEx.AddQingFen(emailData.npcId, num);
					}
					try
					{
						player.emailDateMag.AuToSendToPlayer(emailData.npcId, 997, 997, player.worldTimeMag.nowTime);
						player.emailDateMag.NewToHasRead(emailData.npcId.ToString());
					}
					catch
					{
					}
					NpcJieSuanManager.inst.AddItemToNpcBackpack(QuestCyMailHelper.NormalizeNpcId(emailData.npcId), itemId, itemCount);
					CyUIMag.inst.cyEmail.TiJiaoCallBack();
					cell.UpdateSize();
					AIQuestManager.Instance?.NotifyQuestDeliveryMailSubmitted(mailData.HasField("QuestId") ? mailData["QuestId"].Str : string.Empty, mailData.HasField("StepId") ? mailData["StepId"].Str : string.Empty, mailData.HasField("id") ? mailData["id"].I : 0, emailData.npcId, itemId);
				}
			});
		}
		else
		{
			cell.item.SetCustomCountText($"{hasNum}/{itemCount}", CyUIMag.inst.cyEmail.numColors[1]);
			cell.submitBtn.Init(CyUIMag.inst.cyEmail.btnSprites[1], "<color=#e4e4e4>提交</color>", null);
		}
	}

	private static void ResetSubmitButton(CyEmailCell cell)
	{
		if (!(cell?.submitBtn?.btnCell == null))
		{
			cell.submitBtn.btnCell.mouseUp.RemoveAllListeners();
			cell.submitBtn.btnCell.Disable = false;
			cell.submitBtn.gameObject.SetActive(value: false);
		}
	}
}
