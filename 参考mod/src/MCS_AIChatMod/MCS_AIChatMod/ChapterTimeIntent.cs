using System;

namespace MCS_AIChatMod;

[Serializable]
public class ChapterTimeIntent
{
	public string TimeBand;

	public string Urgency;

	public int MaxSpanMonths;

	public bool RequiresBeforeBreakthrough;
}
