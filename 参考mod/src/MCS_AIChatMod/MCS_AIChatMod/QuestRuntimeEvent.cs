using System;
using System.Collections.Generic;

namespace MCS_AIChatMod;

public sealed class QuestRuntimeEvent
{
	public string EventType;

	public string PrimaryId;

	public Dictionary<string, string> Payload = new Dictionary<string, string>();

	public DateTime RaisedAt;
}
