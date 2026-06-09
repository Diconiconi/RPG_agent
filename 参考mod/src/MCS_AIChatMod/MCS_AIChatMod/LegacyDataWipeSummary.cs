using System.Collections.Generic;

namespace MCS_AIChatMod;

public class LegacyDataWipeSummary
{
	public int DeletedDialogHistoryFiles;

	public int PreservedCharacterCardFiles;

	public int DeletedQuestPersistenceFiles;

	public bool QuestSavesDirectoryDeleted;

	public List<string> Errors = new List<string>();

	public bool Succeeded => Errors == null || Errors.Count == 0;

	public void AddError(string error)
	{
		if (!string.IsNullOrWhiteSpace(error))
		{
			if (Errors == null)
			{
				Errors = new List<string>();
			}
			Errors.Add(error.Trim());
		}
	}

	public string BuildDisplayText()
	{
		string baseText = $"已清空过往：历史{DeletedDialogHistoryFiles}份，任务文件{DeletedQuestPersistenceFiles}项，保留角色卡{PreservedCharacterCardFiles}份";
		if (Errors == null || Errors.Count == 0)
		{
			return baseText;
		}
		return $"{baseText}（{Errors.Count}项失败，请查看日志）";
	}
}
