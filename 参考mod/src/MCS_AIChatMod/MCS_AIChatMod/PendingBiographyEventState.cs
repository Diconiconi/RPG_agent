using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace MCS_AIChatMod;

[Serializable]
public class PendingBiographyEventState
{
	public string EventId;

	public int NpcId;

	public string NpcName;

	[JsonConverter(typeof(StringEnumConverter))]
	public BiographyEventKind BiographyEventKind = BiographyEventKind.Unknown;

	public string SourceMethodKey;

	public string CapturedAtGameTime;

	public string OriginalOfficialTime;

	public JObject OriginalPayload = new JObject();

	public string TimeCategory;

	public DateTime DeadlineGameTime;

	[JsonConverter(typeof(StringEnumConverter))]
	public PendingBiographyEventStatus Status = PendingBiographyEventStatus.Captured;

	public string CampaignId;

	public string Summary;

	public string PlayerFacingHint;

	public bool GenerationAttempted;

	public string LastGenerationError;

	public int ConsecutiveGenerationFailures;
}
