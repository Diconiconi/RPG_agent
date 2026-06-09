using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MCS_AIChatMod;

[Serializable]
public class QuestNpcPresenceRequirement
{
	public int NpcId;

	[JsonConverter(typeof(StringEnumConverter))]
	public QuestNpcPresenceMode Mode = QuestNpcPresenceMode.Encounter;
}
