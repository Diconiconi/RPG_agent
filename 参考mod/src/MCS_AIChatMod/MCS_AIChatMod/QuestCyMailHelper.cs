using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using JSONClass;
using KBEngine;

namespace MCS_AIChatMod;

internal static class QuestCyMailHelper
{
	internal const string QuestMailFlagKey = "MCSQuestMail";

	internal const string QuestMailTypeKey = "QuestMailType";

	internal const string QuestIdKey = "QuestId";

	internal const string StepIdKey = "StepId";

	internal const string ItemCountKey = "ItemCount";

	internal const string FavorAmountKey = "FavorAmount";

	internal const string SubmitCompleteKey = "SubmitComplete";

	internal const string DeliveryMailType = "Delivery";

	internal const string RewardMailType = "Reward";

	internal const string NotificationMailType = "Notification";

	internal static int NormalizeNpcId(int npcId)
	{
		try
		{
			if (NpcJieSuanManager.inst != null && NpcJieSuanManager.inst.ImportantNpcBangDingDictionary.ContainsKey(npcId))
			{
				return NpcJieSuanManager.inst.ImportantNpcBangDingDictionary[npcId];
			}
		}
		catch
		{
		}
		return npcId;
	}

	internal static bool HasNpcCyFriend(int npcId)
	{
		Avatar player = PlayerEx.Player;
		return player?.emailDateMag != null && player.emailDateMag.IsFriend(NormalizeNpcId(npcId));
	}

	internal static string GetCurrentGameTimeString()
	{
		try
		{
			Avatar player = Tools.instance?.getPlayer();
			if (player != null)
			{
				return player.worldTimeMag.getNowTime().ToString("yyyy-MM-dd HH:mm:ss");
			}
		}
		catch
		{
		}
		return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
	}

	internal static int BuildQuestMailId(string questId, string stepId, string mailType, int itemId, int extraIndex)
	{
		int hash = 17;
		string raw = $"{questId}|{stepId}|{mailType}|{itemId}|{extraIndex}";
		for (int i = 0; i < raw.Length; i++)
		{
			hash = hash * 31 + raw[i];
		}
		if (hash == int.MinValue)
		{
			hash = int.MaxValue;
		}
		return -Math.Abs(hash);
	}

	internal static JSONObject CreateQuestMailData(int mailId, int npcId, string npcName, string message, string sendTime, string mailType, string questId, string stepId, int itemId, int itemCount, int favorAmount)
	{
		JSONObject data = new JSONObject();
		data.SetField("id", mailId);
		data.SetField("AvatarID", npcId);
		data.SetField("AvatarName", npcName ?? "NPC");
		data.SetField("info", message ?? string.Empty);
		data.SetField("sendTime", sendTime ?? GetCurrentGameTimeString());
		data.SetField("CanCaoZuo", val: false);
		data.SetField("MCSQuestMail", val: true);
		data.SetField("QuestMailType", mailType ?? "Reward");
		data.SetField("QuestId", questId ?? string.Empty);
		data.SetField("StepId", stepId ?? string.Empty);
		data.SetField("ItemID", itemId);
		data.SetField("ItemCount", Math.Max(1, itemCount));
		data.SetField("FavorAmount", Math.Max(0, favorAmount));
		data.SetField("ItemHasGet", val: false);
		data.SetField("SubmitComplete", val: false);
		return data;
	}

	internal static bool TryGetQuestMailData(EmailData emailData, out JSONObject mailData)
	{
		mailData = null;
		if (emailData == null || !emailData.isOld)
		{
			return false;
		}
		Avatar player = PlayerEx.Player;
		if (player?.NewChuanYingList == null)
		{
			return false;
		}
		string mailIdKey = emailData.oldId.ToString();
		if (!player.NewChuanYingList.HasField(mailIdKey))
		{
			return false;
		}
		JSONObject candidate = player.NewChuanYingList[mailIdKey];
		if (candidate == null || !candidate.HasField("MCSQuestMail") || !candidate["MCSQuestMail"].b)
		{
			return false;
		}
		mailData = candidate;
		return true;
	}

	internal static bool IsCyEmailRenderable(EmailData emailData, JSONObject newChuanYingList, out string reason)
	{
		reason = string.Empty;
		if (emailData == null)
		{
			reason = "EmailData is null";
			return false;
		}
		if (string.IsNullOrWhiteSpace(emailData.sendTime))
		{
			reason = "sendTime is empty";
			return false;
		}
		if (emailData.isOld)
		{
			string mailKey = emailData.oldId.ToString();
			if (newChuanYingList == null || !newChuanYingList.HasField(mailKey) || newChuanYingList[mailKey] == null)
			{
				reason = "missing NewChuanYingList entry for oldId=" + mailKey;
				return false;
			}
			if (emailData.oldId >= 2082912)
			{
				return true;
			}
			JSONObject mailData = newChuanYingList[mailKey];
			string[] array = new string[3] { "info", "AvatarName", "CanCaoZuo" };
			foreach (string requiredField in array)
			{
				if (!mailData.HasField(requiredField))
				{
					reason = "missing field " + requiredField + " for oldId=" + mailKey;
					return false;
				}
			}
			return true;
		}
		if (emailData.isPlayer)
		{
			if (!CyPlayeQuestionData.DataDict.ContainsKey(emailData.questionId))
			{
				reason = $"missing player question {emailData.questionId}";
				return false;
			}
			return true;
		}
		if (emailData.isAnswer)
		{
			if (!jsonData.instance.CyNpcAnswerData.HasField(emailData.answerId.ToString()))
			{
				reason = $"missing answer template {emailData.answerId}";
				return false;
			}
			return true;
		}
		if (emailData.content == null || emailData.content.Count < 2)
		{
			reason = "content tuple is missing";
			return false;
		}
		string contentId = emailData.content[0].ToString();
		string dirKey = $"dir{emailData.content[1]}";
		if (!jsonData.instance.CyNpcDuiBaiData.HasField(contentId) || jsonData.instance.CyNpcDuiBaiData[contentId] == null || !jsonData.instance.CyNpcDuiBaiData[contentId].HasField(dirKey))
		{
			reason = "missing dialogue template content=" + contentId + ", dir=" + dirKey;
			return false;
		}
		return true;
	}

	internal static int SanitizeCyEmailBucket(Dictionary<string, List<EmailData>> emailBuckets, string npcKey, JSONObject newChuanYingList, out string report)
	{
		report = string.Empty;
		if (emailBuckets == null || string.IsNullOrWhiteSpace(npcKey) || !emailBuckets.TryGetValue(npcKey, out var emails) || emails == null || emails.Count == 0)
		{
			return 0;
		}
		List<EmailData> validEmails = new List<EmailData>(emails.Count);
		List<string> removedReports = new List<string>();
		for (int i = 0; i < emails.Count; i++)
		{
			EmailData email = emails[i];
			if (IsCyEmailRenderable(email, newChuanYingList, out var reason))
			{
				validEmails.Add(email);
				continue;
			}
			string label = ((email == null) ? $"index={i}" : $"oldId={email.oldId}, sendTime={email.sendTime ?? string.Empty}");
			removedReports.Add(label + ", reason=" + reason);
		}
		if (removedReports.Count == 0)
		{
			return 0;
		}
		emails.Clear();
		emails.AddRange(validEmails);
		if (emails.Count == 0)
		{
			emailBuckets.Remove(npcKey);
		}
		report = string.Join(" | ", removedReports);
		return removedReports.Count;
	}

	internal static int SanitizeCyEmailBucketsForNpc(int npcId, out string report)
	{
		report = string.Empty;
		Avatar player = PlayerEx.Player;
		if (player?.emailDateMag == null)
		{
			return 0;
		}
		string primaryKey = npcId.ToString();
		string normalizedKey = NormalizeNpcId(npcId).ToString();
		List<string> reportParts = new List<string>();
		int removed = 0;
		foreach (string key in new string[2] { primaryKey, normalizedKey }.Distinct(StringComparer.Ordinal))
		{
			removed += SanitizeCyEmailBucket(player.emailDateMag.newEmailDictionary, key, player.NewChuanYingList, out var newReport);
			removed += SanitizeCyEmailBucket(player.emailDateMag.hasReadEmailDictionary, key, player.NewChuanYingList, out var readReport);
			if (!string.IsNullOrWhiteSpace(newReport))
			{
				reportParts.Add("new[" + key + "] " + newReport);
			}
			if (!string.IsNullOrWhiteSpace(readReport))
			{
				reportParts.Add("read[" + key + "] " + readReport);
			}
		}
		report = string.Join(" || ", reportParts);
		return removed;
	}

	internal static void ConfigureInvalidCyEmailCell(CyEmailCell cell, EmailData emailData, string reason)
	{
		if (cell == null)
		{
			return;
		}
		if (cell.sendTime != null)
		{
			try
			{
				cell.sendTime.text = ((!string.IsNullOrWhiteSpace(emailData?.sendTime)) ? DateTime.Parse(emailData.sendTime).ToString("yyyy年M月d日") : string.Empty);
			}
			catch
			{
				cell.sendTime.text = emailData?.sendTime ?? string.Empty;
			}
		}
		if (cell.sendName != null)
		{
			cell.sendName.text = ((!string.IsNullOrWhiteSpace(emailData?.npcName)) ? emailData.npcName : $"传音数据异常({emailData?.npcId ?? 0})");
		}
		if (cell.content != null)
		{
			cell.content.text = "\u3000这封传音的数据已损坏，已自动跳过显示。";
		}
		if (cell.item?.gameObject?.transform?.parent?.gameObject != null)
		{
			cell.item.gameObject.transform.parent.gameObject.SetActive(value: false);
		}
		if (cell.submitBtn?.gameObject != null)
		{
			cell.submitBtn.gameObject.SetActive(value: false);
		}
		if (CyUIMag.inst?.cyEmail != null)
		{
			if (cell.sendTime != null && CyUIMag.inst.cyEmail.titleColors.Count > 2)
			{
				cell.sendTime.color = CyUIMag.inst.cyEmail.titleColors[2];
			}
			if (cell.sendName != null && CyUIMag.inst.cyEmail.titleColors.Count > 2)
			{
				cell.sendName.color = CyUIMag.inst.cyEmail.titleColors[2];
			}
			if (cell.bg != null && CyUIMag.inst.cyEmail.titleSprites.Count > 2)
			{
				cell.bg.sprite = CyUIMag.inst.cyEmail.titleSprites[2];
			}
		}
		ManualLogSource logger = AIChatManager.logger;
		if (logger != null)
		{
			logger.LogWarning((object)$"[QuestCyMail] Skipped invalid cy email render: npc={emailData?.npcId ?? 0}, oldId={emailData?.oldId ?? 0}, reason={reason}");
		}
		try
		{
			cell.UpdateSize();
		}
		catch
		{
		}
	}

	internal static bool IsCyEmailRenderableForTest(EmailData emailData, JSONObject newChuanYingList, out string reason)
	{
		return IsCyEmailRenderable(emailData, newChuanYingList, out reason);
	}

	internal static int SanitizeCyEmailBucketForTest(Dictionary<string, List<EmailData>> emailBuckets, string npcKey, JSONObject newChuanYingList, out string report)
	{
		return SanitizeCyEmailBucket(emailBuckets, npcKey, newChuanYingList, out report);
	}

	internal static int GetMailItemCount(JSONObject mailData)
	{
		if (mailData == null || !mailData.HasField("ItemCount"))
		{
			return 1;
		}
		return Math.Max(1, mailData["ItemCount"].I);
	}

	internal static string GetMailType(JSONObject mailData)
	{
		return (mailData != null && mailData.HasField("QuestMailType")) ? mailData["QuestMailType"].Str : string.Empty;
	}

	internal static int GetMailFavorAmount(JSONObject mailData)
	{
		if (mailData == null || !mailData.HasField("FavorAmount"))
		{
			return 0;
		}
		return Math.Max(0, mailData["FavorAmount"].I);
	}
}
