using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MCS_AIChatMod;

[Serializable]
public class QuestProgram
{
	public string ProgramId;

	public string Title;

	public string Summary;

	public int AnchorNpcId;

	public string AnchorNpcName;

	[JsonConverter(typeof(StringEnumConverter))]
	public QuestTrackType TrackType = QuestTrackType.Mainline;

	public string CampaignId;

	public string ChapterId;

	public string BeatId;

	public int Priority;

	[JsonConverter(typeof(StringEnumConverter))]
	public QuestProgramStatus Status = QuestProgramStatus.Draft;

	public string ContinuitySummary;

	public QuestWorldContract WorldContract = new QuestWorldContract();

	public List<QuestCommandInvocation> OnAccepted = new List<QuestCommandInvocation>();

	public List<QuestCommandInvocation> OnCompleted = new List<QuestCommandInvocation>();

	public List<QuestCommandInvocation> OnFailed = new List<QuestCommandInvocation>();

	public List<QuestNode> Nodes = new List<QuestNode>();

	public QuestRuntimeState Runtime = new QuestRuntimeState();
}
