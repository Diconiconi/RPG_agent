using System;

namespace MCS_AIChatMod;

[Serializable]
public enum PendingBiographyEventStatus
{
	Captured,
	ActiveStory,
	Resolved,
	Restored
}
