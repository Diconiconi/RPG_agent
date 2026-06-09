using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MCS_AIChatMod;

[Serializable]
public class OfficialTaskUiEntry
{
	public string EntryId;

	[JsonConverter(typeof(StringEnumConverter))]
	public OfficialTaskUiEntryKind Kind = OfficialTaskUiEntryKind.ActiveProgram;

	public string Title;

	public string StatusText;

	public string GuideText;

	public string RemainingTimeText;

	public string DescriptionText;

	public string ProgramId;
}
