using System;
using System.Collections.Generic;

namespace MCS_AIChatMod;

public sealed class QuestPredicateEvaluationContext
{
	public DateTime? CurrentGameTime;

	public Func<string, string, bool> IsSceneMatch;

	public Func<int?> GetCurrentSceneType;

	public Func<int?> GetFuBenRemainingDays;

	public Func<int, bool> IsNpcAlive;

	public Func<int, int, bool> IsNpcStatus;

	public Func<int, int, bool> IsNpcAction;

	public Func<int, bool> HasTalkedToNpc;

	public Func<int, bool> IsCyFriend;

	public Func<int, bool> IsNpcPresentInCurrentScene;

	public Func<int, bool> IsNpcPatrolPresent;

	public Func<int, string, string, bool> IsNpcPresentInScene;

	public Func<int, bool> HasRecentBattleVictory;

	public Func<Dictionary<string, string>, bool> HasRecentDeliveryMailSubmission;

	public Func<Dictionary<string, string>, int> GetPlayerItemCount;

	public Func<long> GetPlayerMoney;

	public Func<int> GetPlayerLevel;

	public Func<int, bool> HasPlayerSkill;

	public Func<int, bool> HasPlayerStaticSkill;

	public Func<int, int> GetNpcFavor;

	public Func<int, bool> IsOfficialTaskStarted;

	public Func<int, bool> IsOfficialTaskNotOutdated;

	public Func<int, int, bool> IsOfficialTaskStepFinished;

	public Func<int, bool> IsOfficialTaskFinishable;
}
