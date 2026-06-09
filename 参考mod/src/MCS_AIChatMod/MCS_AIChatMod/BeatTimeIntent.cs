using System;

namespace MCS_AIChatMod;

[Serializable]
public class BeatTimeIntent
{
	public string TimeBand;

	public string Urgency;

	public int MaxSpanMonths;

	public bool RequiresBeforeBreakthrough;
}
