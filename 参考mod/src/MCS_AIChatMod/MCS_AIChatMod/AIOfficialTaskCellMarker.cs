using System;
using UnityEngine;

namespace MCS_AIChatMod;

internal sealed class AIOfficialTaskCellMarker : MonoBehaviour
{
	public string EntryId;

	public string Title;

	public string StatusText;

	public string GuideText;

	public string RemainingTimeText;

	public string DescriptionText;

	internal void Apply(OfficialTaskUiEntry entry)
	{
		if (entry == null)
		{
			throw new InvalidOperationException("AI 官方任务 UI 条目不能为空。");
		}
		EntryId = entry.EntryId ?? string.Empty;
		Title = entry.Title ?? string.Empty;
		StatusText = entry.StatusText ?? string.Empty;
		GuideText = entry.GuideText ?? string.Empty;
		RemainingTimeText = entry.RemainingTimeText ?? string.Empty;
		DescriptionText = entry.DescriptionText ?? string.Empty;
	}
}
