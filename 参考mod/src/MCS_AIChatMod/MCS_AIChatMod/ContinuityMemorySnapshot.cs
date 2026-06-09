using System;
using System.Collections.Generic;

namespace MCS_AIChatMod;

[Serializable]
public class ContinuityMemorySnapshot
{
	public string Summary;

	public List<string> RecentWorldChanges = new List<string>();

	public DateTime LastUpdatedAt;
}
