using System;
using System.Collections.Generic;

namespace MCS_AIChatMod;

[Serializable]
public class QuestRuntimeState
{
	public string CurrentNodeId;

	public List<string> ActivatedNodeIds = new List<string>();

	public List<string> CompletedNodeIds = new List<string>();

	public List<string> ExecutedCommandKeys = new List<string>();

	public List<string> PendingNarrativeNodeIds = new List<string>();

	public List<QuestForcedNpcState> ActiveForcedNpcStates = new List<QuestForcedNpcState>();

	public string FailedReason;

	public DateTime AcceptedAtGameTime;

	public DateTime DeadlineGameTime;
}
