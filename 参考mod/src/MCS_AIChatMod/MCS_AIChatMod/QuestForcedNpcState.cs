using System;

namespace MCS_AIChatMod;

[Serializable]
public class QuestForcedNpcState
{
	public int NpcId;

	public string NpcName;

	public string OriginalLocationId;

	public int OriginalActionId;

	public int OriginalStatusId;

	public bool HadLockAction;

	public int OriginalLockActionId;
}
