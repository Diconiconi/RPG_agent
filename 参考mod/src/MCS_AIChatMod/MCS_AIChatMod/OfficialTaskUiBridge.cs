using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.UI;

namespace MCS_AIChatMod;

internal static class OfficialTaskUiBridge
{
	private static readonly FieldInfo TaskUiTemplateField = typeof(TaskUIManager).GetField("_TaskCell", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ?? throw new InvalidOperationException("TaskUIManager._TaskCell 不存在，无法注入 AI 主线到官方任务 UI。");

	private static readonly FieldInfo TaskUiListField = typeof(TaskUIManager).GetField("TaskList", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ?? throw new InvalidOperationException("TaskUIManager.TaskList 不存在，无法管理官方任务 UI 选中态。");

	private static readonly FieldInfo TaskUiIsOldField = typeof(TaskUIManager).GetField("isOld", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ?? throw new InvalidOperationException("TaskUIManager.isOld 不存在，无法判断官方任务 UI 是否处于旧时分页。");

	private static readonly FieldInfo TaskUiCurTypeField = typeof(TaskUIManager).GetField("curType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ?? throw new InvalidOperationException("TaskUIManager.curType 不存在，无法判断官方任务 UI 当前分页。");

	private static readonly FieldInfo TaskUiCurIsChuanWenField = typeof(TaskUIManager).GetField("CurisChuanWen", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ?? throw new InvalidOperationException("TaskUIManager.CurisChuanWen 不存在，无法判断官方任务 UI 是否处于传闻分页。");

	private static readonly FieldInfo TaskCellDescManagerField = typeof(TaskRenWuCell).GetField("taskDescManager", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ?? throw new InvalidOperationException("TaskRenWuCell.taskDescManager 不存在，无法展示 AI 主线详情。");

	private static readonly FieldInfo TaskCellTaskNameField = typeof(TaskRenWuCell).GetField("TaskName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ?? throw new InvalidOperationException("TaskRenWuCell.TaskName 不存在，无法展示 AI 主线条目标题。");

	private static readonly FieldInfo TaskCellClickImageField = typeof(TaskRenWuCell).GetField("ClikeImage", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ?? throw new InvalidOperationException("TaskRenWuCell.ClikeImage 不存在，无法显示 AI 主线选中态。");

	private static readonly FieldInfo TaskCellDefaultImageField = typeof(TaskRenWuCell).GetField("DefaultImage", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ?? throw new InvalidOperationException("TaskRenWuCell.DefaultImage 不存在，无法显示 AI 主线默认态。");

	private static readonly MethodInfo TaskDescClearMethod = typeof(TaskDescManager).GetMethod("clearCurTaskDesc", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ?? throw new InvalidOperationException("TaskDescManager.clearCurTaskDesc 不存在，无法清理 AI 主线详情。");

	private static readonly MethodInfo TaskDescSetTitleMethod = typeof(TaskDescManager).GetMethod("setTitle", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ?? throw new InvalidOperationException("TaskDescManager.setTitle 不存在，无法展示 AI 主线标题。");

	private static readonly MethodInfo TaskDescSetTaskNameMethod = typeof(TaskDescManager).GetMethod("setDescTaskName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ?? throw new InvalidOperationException("TaskDescManager.setDescTaskName 不存在，无法展示 AI 主线详情标题。");

	private static readonly MethodInfo TaskDescSetDescMethod = typeof(TaskDescManager).GetMethod("setDesc", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ?? throw new InvalidOperationException("TaskDescManager.setDesc 不存在，无法展示 AI 主线详情正文。");

	private static readonly FieldInfo TaskDescHasTimeField = typeof(TaskDescManager).GetField("HasTime", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ?? throw new InvalidOperationException("TaskDescManager.HasTime 不存在，无法展示 AI 主线剩余时间。");

	private static readonly FieldInfo TaskDescTaskIndexTemplateField = typeof(TaskDescManager).GetField("TaskIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ?? throw new InvalidOperationException("TaskDescManager.TaskIndex 不存在，无法展示 AI 主线任务进度。");

	private static readonly Color InjectedCellSelectedColor = new Color(0.94509804f, 73f / 85f, 0.6627451f);

	private static readonly Color InjectedCellDefaultColor = new Color(0.70980394f, 0.94509804f, 78f / 85f);

	internal static void InjectAiMainlineEntries(TaskUIManager taskUiManager)
	{
		if (AIQuestManager.Instance == null || taskUiManager == null)
		{
			return;
		}
		GameObject templateCell = ResolveTaskCellTemplate(taskUiManager);
		Transform listRoot = templateCell.transform.parent ?? throw new InvalidOperationException("官方任务列表模板没有父节点，无法注入 AI 主线条目。");
		ClearInjectedEntries(listRoot);
		if (!ShouldInjectIntoCurrentOfficialTaskView(taskUiManager, out var curType, out var isOld, out var isChuanWen))
		{
			ManualLogSource logger = AIChatManager.logger;
			if (logger != null)
			{
				logger.LogInfo((object)$"[OfficialTaskUi] Skip AI mainline injection: curType={curType}, isOld={isOld}, isChuanWen={isChuanWen}");
			}
			return;
		}
		List<OfficialTaskUiEntry> entries = AIQuestManager.Instance.BuildOfficialTaskUiEntries();
		if (entries.Count == 0)
		{
			ManualLogSource logger2 = AIChatManager.logger;
			if (logger2 != null)
			{
				logger2.LogInfo((object)$"[OfficialTaskUi] No AI mainline entries to inject: curType={curType}, isOld={isOld}, isChuanWen={isChuanWen}");
			}
			return;
		}
		foreach (OfficialTaskUiEntry entry in entries)
		{
			CreateInjectedCell(listRoot, templateCell, entry);
		}
		if (listRoot is RectTransform rectTransform)
		{
			LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
		}
		ManualLogSource logger3 = AIChatManager.logger;
		if (logger3 != null)
		{
			logger3.LogInfo((object)$"[OfficialTaskUi] Injected {entries.Count} AI mainline entries into official task UI: curType={curType}, isOld={isOld}, isChuanWen={isChuanWen}");
		}
	}

	internal static bool TryHandleAiCellClick(TaskRenWuCell taskCell)
	{
		if (taskCell == null)
		{
			return false;
		}
		AIOfficialTaskCellMarker marker = taskCell.GetComponent<AIOfficialTaskCellMarker>();
		if (marker == null)
		{
			return false;
		}
		TaskDescManager descManager = ResolveTaskDescManager(taskCell);
		AIOfficialTaskDetailPayload detailPayload = BuildDetailPayload(marker);
		TaskDescClearMethod.Invoke(descManager, null);
		descManager.gameObject.SetActive(value: true);
		TaskDescSetTitleMethod.Invoke(descManager, new object[3] { detailPayload.StatusLabel, "任务进度", "任务描述" });
		TaskDescSetTaskNameMethod.Invoke(descManager, new object[1] { detailPayload.TaskName });
		TaskDescSetDescMethod.Invoke(descManager, new object[1] { detailPayload.DescriptionText });
		SetDetailStatusValue(descManager, detailPayload.StatusValue);
		PopulateDetailProgress(descManager, detailPayload.ProgressLines);
		ClearOfficialSelection(taskCell.transform.parent);
		SetCellSelected(taskCell, isSelected: true);
		ManualLogSource logger = AIChatManager.logger;
		if (logger != null)
		{
			logger.LogInfo((object)("[OfficialTaskUi] Clicked AI mainline entry: entryId=" + marker.EntryId + ", title=" + marker.Title));
		}
		return true;
	}

	private static GameObject ResolveTaskCellTemplate(TaskUIManager taskUiManager)
	{
		GameObject templateCell = TaskUiTemplateField.GetValue(taskUiManager) as GameObject;
		if (templateCell == null)
		{
			throw new InvalidOperationException("TaskUIManager._TaskCell 为空，无法注入 AI 主线到官方任务 UI。");
		}
		return templateCell;
	}

	private static void ClearInjectedEntries(Transform listRoot)
	{
		AIOfficialTaskCellMarker[] componentsInChildren = listRoot.GetComponentsInChildren<AIOfficialTaskCellMarker>(includeInactive: true);
		foreach (AIOfficialTaskCellMarker marker in componentsInChildren)
		{
			if (marker != null)
			{
				UnityEngine.Object.Destroy(marker.gameObject);
			}
		}
	}

	private static void CreateInjectedCell(Transform listRoot, GameObject templateCell, OfficialTaskUiEntry entry)
	{
		GameObject cellObject = UnityEngine.Object.Instantiate(templateCell, listRoot);
		cellObject.name = "AIOfficialTaskCell_" + SanitizeEntryName(entry.EntryId);
		cellObject.SetActive(value: true);
		TaskRenWuCell taskCell = cellObject.GetComponent<TaskRenWuCell>() ?? throw new InvalidOperationException("官方任务模板缺少 TaskRenWuCell，无法注入 AI 主线条目。");
		DetachInjectedCellFromOfficialTaskList(taskCell);
		SetCellSelected(taskCell, isSelected: false);
		taskCell.setIsChuanWen(flag: false);
		taskCell.setTaskName(entry.Title ?? string.Empty);
		Text taskNameText = TaskCellTaskNameField.GetValue(taskCell) as Text;
		if (taskNameText == null)
		{
			throw new InvalidOperationException("TaskRenWuCell.TaskName 为空，无法展示 AI 主线条目标题。");
		}
		taskNameText.text = entry.Title ?? string.Empty;
		taskNameText.gameObject.SetActive(value: true);
		AIOfficialTaskCellMarker marker = cellObject.GetComponent<AIOfficialTaskCellMarker>() ?? cellObject.AddComponent<AIOfficialTaskCellMarker>();
		marker.Apply(entry);
	}

	private static void DetachInjectedCellFromOfficialTaskList(TaskRenWuCell taskCell)
	{
		TaskUIManager taskUiManager = ResolveTaskUiManagerInstance();
		if (!(TaskUiListField.GetValue(taskUiManager) is List<TaskRenWuCell> officialCells))
		{
			throw new InvalidOperationException("TaskUIManager.TaskList 不是有效的任务条目列表，无法从官方列表中移除 AI 主线条目。");
		}
		officialCells.Remove(taskCell);
	}

	private static string SanitizeEntryName(string entryId)
	{
		if (string.IsNullOrWhiteSpace(entryId))
		{
			return "unnamed";
		}
		char[] chars = entryId.Select((char ch) => char.IsLetterOrDigit(ch) ? ch : '_').ToArray();
		return new string(chars);
	}

	private static bool ShouldInjectIntoCurrentOfficialTaskView(TaskUIManager taskUiManager, out int curType, out bool isOld, out bool isChuanWen)
	{
		curType = ((TaskUiCurTypeField.GetValue(taskUiManager) is int curTypeValue) ? curTypeValue : (-1));
		object value = TaskUiIsOldField.GetValue(taskUiManager);
		bool isOldValue = default(bool);
		int num;
		if (value is bool)
		{
			isOldValue = (bool)value;
			num = 1;
		}
		else
		{
			num = 0;
		}
		isOld = (byte)((uint)num & (isOldValue ? 1u : 0u)) != 0;
		value = TaskUiCurIsChuanWenField.GetValue(taskUiManager);
		bool isChuanWenValue = default(bool);
		int num2;
		if (value is bool)
		{
			isChuanWenValue = (bool)value;
			num2 = 1;
		}
		else
		{
			num2 = 0;
		}
		isChuanWen = (byte)((uint)num2 & (isChuanWenValue ? 1u : 0u)) != 0;
		return ShouldInjectIntoCurrentOfficialTaskView(curType, isOld, isChuanWen);
	}

	internal static bool ShouldInjectIntoCurrentOfficialTaskViewForTest(int curType, bool isOld, bool isChuanWen)
	{
		return ShouldInjectIntoCurrentOfficialTaskView(curType, isOld, isChuanWen);
	}

	private static bool ShouldInjectIntoCurrentOfficialTaskView(int curType, bool isOld, bool isChuanWen)
	{
		return curType == 1 && !isOld;
	}

	internal static AIOfficialTaskDetailPayload BuildDetailPayloadForTest(OfficialTaskUiEntry entry)
	{
		if (entry == null)
		{
			throw new InvalidOperationException("AI 官方任务 UI 条目不能为空。");
		}
		AIOfficialTaskCellMarker marker = new AIOfficialTaskCellMarker();
		marker.Apply(entry);
		return BuildDetailPayload(marker);
	}

	private static AIOfficialTaskDetailPayload BuildDetailPayload(AIOfficialTaskCellMarker marker)
	{
		if (marker == null)
		{
			throw new InvalidOperationException("AI 官方任务 UI 标记不能为空。");
		}
		string remainingTimeValue = ExtractRemainingTimeValue(marker.RemainingTimeText);
		string statusLabel = (string.IsNullOrWhiteSpace(remainingTimeValue) ? "任务状态:" : "剩余时间:");
		string statusValue = (string.IsNullOrWhiteSpace(remainingTimeValue) ? (marker.StatusText ?? string.Empty) : remainingTimeValue);
		return new AIOfficialTaskDetailPayload
		{
			StatusLabel = statusLabel,
			StatusValue = statusValue,
			ProgressLines = BuildProgressLines(marker.GuideText, marker.StatusText),
			TaskName = marker.Title,
			DescriptionText = marker.DescriptionText
		};
	}

	private static string ExtractRemainingTimeValue(string remainingTimeText)
	{
		string trimmed = remainingTimeText?.Trim() ?? string.Empty;
		if (string.IsNullOrWhiteSpace(trimmed))
		{
			return string.Empty;
		}
		return trimmed.StartsWith("剩余时间:", StringComparison.Ordinal) ? trimmed.Substring("剩余时间:".Length).Trim() : trimmed;
	}

	private static List<string> BuildProgressLines(string guideText, string statusText)
	{
		IEnumerable<string> rawLines = SplitNonEmptyLines(guideText);
		List<string> progressLines = rawLines.ToList();
		if (progressLines.Count > 0)
		{
			return progressLines;
		}
		if (!string.IsNullOrWhiteSpace(statusText))
		{
			progressLines.Add(statusText.Trim());
		}
		return progressLines;
	}

	private static IEnumerable<string> SplitNonEmptyLines(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			yield break;
		}
		string[] parts = text.Split(new char[2] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
		string[] array = parts;
		foreach (string part in array)
		{
			string trimmed = part.Trim();
			if (!string.IsNullOrWhiteSpace(trimmed))
			{
				yield return trimmed;
			}
		}
	}

	private static TaskDescManager ResolveTaskDescManager(TaskRenWuCell taskCell)
	{
		TaskDescManager descManager = TaskCellDescManagerField.GetValue(taskCell) as TaskDescManager;
		if (descManager == null)
		{
			throw new InvalidOperationException("TaskRenWuCell.taskDescManager 为空，无法展示 AI 主线详情。");
		}
		return descManager;
	}

	private static void SetDetailStatusValue(TaskDescManager descManager, string statusValue)
	{
		Text hasTimeText = TaskDescHasTimeField.GetValue(descManager) as Text;
		if (hasTimeText == null)
		{
			throw new InvalidOperationException("TaskDescManager.HasTime 为空，无法展示 AI 主线剩余时间。");
		}
		hasTimeText.text = statusValue ?? string.Empty;
	}

	private static void PopulateDetailProgress(TaskDescManager descManager, List<string> progressLines)
	{
		GameObject taskIndexTemplate = TaskDescTaskIndexTemplateField.GetValue(descManager) as GameObject;
		if (taskIndexTemplate == null)
		{
			throw new InvalidOperationException("TaskDescManager.TaskIndex 为空，无法展示 AI 主线任务进度。");
		}
		IEnumerable<string> lines = progressLines ?? Enumerable.Empty<string>();
		foreach (string line in lines.Where((string value) => !string.IsNullOrWhiteSpace(value)))
		{
			GameObject progressCellObject = UnityEngine.Object.Instantiate(taskIndexTemplate, taskIndexTemplate.transform.parent);
			progressCellObject.SetActive(value: true);
			TaskIndexCell progressCell = progressCellObject.GetComponent<TaskIndexCell>() ?? throw new InvalidOperationException("TaskDescManager.TaskIndex 模板缺少 TaskIndexCell，无法展示 AI 主线任务进度。");
			progressCell.setContent(line.Trim());
		}
	}

	internal static void ClearInjectedSelectionAfterOfficialClick(Transform listRoot)
	{
		ClearInjectedSelection(listRoot);
	}

	private static void ClearOfficialSelection(Transform listRoot)
	{
		TaskUIManager taskUiManager = ResolveTaskUiManagerInstance();
		if (!(TaskUiListField.GetValue(taskUiManager) is List<TaskRenWuCell> officialCells))
		{
			throw new InvalidOperationException("TaskUIManager.TaskList 不是有效的任务条目列表，无法清理官方选中态。");
		}
		foreach (TaskRenWuCell taskCell in officialCells.Where((TaskRenWuCell taskRenWuCell) => taskRenWuCell != null))
		{
			taskCell.SetNoSelect();
		}
		AIOfficialTaskCellMarker[] componentsInChildren = listRoot.GetComponentsInChildren<AIOfficialTaskCellMarker>(includeInactive: true);
		foreach (AIOfficialTaskCellMarker marker in componentsInChildren)
		{
			TaskRenWuCell injectedCell = marker.GetComponent<TaskRenWuCell>();
			if (injectedCell != null)
			{
				SetCellSelected(injectedCell, isSelected: false);
			}
		}
	}

	private static void ClearInjectedSelection(Transform listRoot)
	{
		AIOfficialTaskCellMarker[] componentsInChildren = listRoot.GetComponentsInChildren<AIOfficialTaskCellMarker>(includeInactive: true);
		foreach (AIOfficialTaskCellMarker marker in componentsInChildren)
		{
			TaskRenWuCell injectedCell = marker.GetComponent<TaskRenWuCell>();
			if (injectedCell != null)
			{
				SetCellSelected(injectedCell, isSelected: false);
			}
		}
	}

	private static TaskUIManager ResolveTaskUiManagerInstance()
	{
		TaskUIManager taskUiManager = TaskUIManager.inst;
		if (taskUiManager == null)
		{
			throw new InvalidOperationException("TaskUIManager.inst 为空，无法更新官方任务 UI 选中态。");
		}
		return taskUiManager;
	}

	private static void SetCellSelected(TaskRenWuCell taskCell, bool isSelected)
	{
		GameObject clickImage = TaskCellClickImageField.GetValue(taskCell) as GameObject;
		if (clickImage != null)
		{
			clickImage.SetActive(isSelected);
		}
		GameObject defaultImage = TaskCellDefaultImageField.GetValue(taskCell) as GameObject;
		if (defaultImage != null)
		{
			defaultImage.SetActive(!isSelected);
		}
		Text taskNameText = TaskCellTaskNameField.GetValue(taskCell) as Text;
		if (taskNameText != null)
		{
			taskNameText.color = (isSelected ? InjectedCellSelectedColor : InjectedCellDefaultColor);
		}
	}
}
