using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MCS_AIChatMod;

[Serializable]
public class QuestCapabilityDescriptor
{
	public string Opcode;

	[JsonConverter(typeof(StringEnumConverter))]
	public QuestCapabilityKind Kind;

	public Dictionary<string, string> ParameterSchema = new Dictionary<string, string>();

	public List<QuestTrackType> AllowedTracks = new List<QuestTrackType>();

	public string OfficialBridge;

	public bool RequiresWorldContract;

	public bool IsPermanentWorldMutation;

	public bool IsDangerous;
}
