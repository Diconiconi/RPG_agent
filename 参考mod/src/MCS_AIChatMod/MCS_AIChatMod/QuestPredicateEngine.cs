using System;
using System.Collections.Generic;
using System.Globalization;

namespace MCS_AIChatMod;

public sealed class QuestPredicateEngine
{
	public bool EvaluateAll(IEnumerable<QuestPredicateInvocation> predicates, QuestPredicateEvaluationContext context)
	{
		if (predicates == null)
		{
			return true;
		}
		foreach (QuestPredicateInvocation predicate in predicates)
		{
			if (!Evaluate(predicate, context))
			{
				return false;
			}
		}
		return true;
	}

	private bool EvaluateAny(IEnumerable<QuestPredicateInvocation> predicates, QuestPredicateEvaluationContext context)
	{
		if (predicates == null)
		{
			return false;
		}
		foreach (QuestPredicateInvocation predicate in predicates)
		{
			if (Evaluate(predicate, context))
			{
				return true;
			}
		}
		return false;
	}

	private bool Evaluate(QuestPredicateInvocation predicate, QuestPredicateEvaluationContext context)
	{
		if (predicate == null || string.IsNullOrWhiteSpace(predicate.Opcode))
		{
			return false;
		}
		Dictionary<string, string> parameters = predicate.Params ?? new Dictionary<string, string>();
		return predicate.Opcode.Trim() switch
		{
			"logic.all" => EvaluateLogicAll(predicate, context),
			"logic.any" => EvaluateLogicAny(predicate, context),
			"logic.not" => EvaluateLogicNot(predicate, context),
			"time.compare" => EvaluateTimeCompare(parameters, context),
			"time.window" => EvaluateTimeWindow(parameters, context),
			"fuben.remaining_days" => EvaluateFuBenRemainingDays(parameters, context),
			"scene.type_is" => EvaluateSceneTypeIs(parameters, context),
			"scene.is" => EvaluateSceneIs(parameters, context),
			"npc.alive" => EvaluateNpcAlive(parameters, context),
			"npc.dead" => EvaluateNpcDead(parameters, context),
			"npc.status_is" => EvaluateNpcStatus(parameters, context),
			"npc.action_is" => EvaluateNpcAction(parameters, context),
			"npc.talked_to_player" => EvaluateNpcTalked(parameters, context),
			"npc.cy_friend" => EvaluateCyFriend(parameters, context),
			"npc.present_in_current_scene" => EvaluateNpcPresentInCurrentScene(parameters, context),
			"npc.patrol_present" => EvaluateNpcPatrolPresent(parameters, context),
			"scene.npc_present" => EvaluateSceneNpcPresent(parameters, context),
			"combat.victory_over_npc" => EvaluateCombatVictoryOverNpc(parameters, context),
			"mail.delivery_submitted" => EvaluateMailDeliverySubmitted(parameters, context),
			"player.has_item" => EvaluatePlayerHasItem(parameters, context),
			"player.money_compare" => EvaluatePlayerMoneyCompare(parameters, context),
			"player.level_compare" => EvaluatePlayerLevelCompare(parameters, context),
			"player.has_skill" => EvaluatePlayerHasSkill(parameters, context),
			"player.has_static_skill" => EvaluatePlayerHasStaticSkill(parameters, context),
			"player.favor_compare" => EvaluatePlayerFavorCompare(parameters, context),
			"official_task.started" => EvaluateOfficialTaskStarted(parameters, context),
			"task.official_not_outdated" => EvaluateOfficialTaskNotOutdated(parameters, context),
			"official_task.step_finished" => EvaluateOfficialTaskStepFinished(parameters, context),
			"official_task.finishable" => EvaluateOfficialTaskFinishable(parameters, context),
			_ => false,
		};
	}

	private bool EvaluateLogicAll(QuestPredicateInvocation predicate, QuestPredicateEvaluationContext context)
	{
		return predicate?.Children != null && predicate.Children.Count > 0 && EvaluateAll(predicate.Children, context);
	}

	private bool EvaluateLogicAny(QuestPredicateInvocation predicate, QuestPredicateEvaluationContext context)
	{
		return predicate?.Children != null && predicate.Children.Count > 0 && EvaluateAny(predicate.Children, context);
	}

	private bool EvaluateLogicNot(QuestPredicateInvocation predicate, QuestPredicateEvaluationContext context)
	{
		return predicate?.Children != null && predicate.Children.Count > 0 && !EvaluateAll(predicate.Children, context);
	}

	private static bool EvaluateTimeCompare(Dictionary<string, string> parameters, QuestPredicateEvaluationContext context)
	{
		if (context == null || !context.CurrentGameTime.HasValue)
		{
			return false;
		}
		if (!TryGetParam(parameters, "value", out var rawValue) || !DateTime.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out var targetTime))
		{
			return false;
		}
		string op = GetParam(parameters, "operator");
		DateTime now = context.CurrentGameTime.Value;
		string text = op;
		string text2 = text;
		switch (text2)
		{
		default:
			if (text2.Length == 0)
			{
				break;
			}
			goto case null;
		case null:
			switch (text2)
			{
			case null:
				break;
			case "<":
				return now < targetTime;
			case "<=":
				return now <= targetTime;
			case "=":
			case "==":
				return now == targetTime;
			case "!=":
				return now != targetTime;
			default:
				return false;
			}
			break;
		case ">":
			return now > targetTime;
		case ">=":
			break;
		}
		return now >= targetTime;
	}

	private static bool EvaluateTimeWindow(Dictionary<string, string> parameters, QuestPredicateEvaluationContext context)
	{
		if (context == null || !context.CurrentGameTime.HasValue || parameters == null)
		{
			return false;
		}
		DateTime start = default(DateTime);
		DateTime end = default(DateTime);
		string rawStart;
		bool hasStart = TryGetParam(parameters, "start", out rawStart) && DateTime.TryParse(rawStart, CultureInfo.InvariantCulture, DateTimeStyles.None, out start);
		string rawEnd;
		bool hasEnd = TryGetParam(parameters, "end", out rawEnd) && DateTime.TryParse(rawEnd, CultureInfo.InvariantCulture, DateTimeStyles.None, out end);
		if (!hasStart && !hasEnd)
		{
			return false;
		}
		DateTime now = context.CurrentGameTime.Value;
		if (hasStart && now < start)
		{
			return false;
		}
		if (hasEnd && now > end)
		{
			return false;
		}
		return true;
	}

	private static bool EvaluateFuBenRemainingDays(Dictionary<string, string> parameters, QuestPredicateEvaluationContext context)
	{
		if (context == null || context.GetFuBenRemainingDays == null || !TryGetInt(parameters, "days", out var targetDays))
		{
			return false;
		}
		int? currentDays = context.GetFuBenRemainingDays();
		if (!currentDays.HasValue)
		{
			return false;
		}
		return CompareInt(currentDays.Value, GetParam(parameters, "operator"), targetDays);
	}

	private static bool EvaluateSceneTypeIs(Dictionary<string, string> parameters, QuestPredicateEvaluationContext context)
	{
		if (context == null || context.GetCurrentSceneType == null || !TryGetInt(parameters, "sceneType", out var sceneType))
		{
			return false;
		}
		int? currentSceneType = context.GetCurrentSceneType();
		return currentSceneType.HasValue && currentSceneType.Value == sceneType;
	}

	private static bool EvaluateSceneIs(Dictionary<string, string> parameters, QuestPredicateEvaluationContext context)
	{
		if (context?.IsSceneMatch == null)
		{
			return false;
		}
		string sceneId = GetParam(parameters, "sceneId");
		string sceneName = GetParam(parameters, "sceneName");
		return context.IsSceneMatch(sceneId, sceneName);
	}

	private static bool EvaluateNpcAlive(Dictionary<string, string> parameters, QuestPredicateEvaluationContext context)
	{
		if (context == null || context.IsNpcAlive == null || !TryGetInt(parameters, "npcId", out var npcId))
		{
			return false;
		}
		return context.IsNpcAlive(npcId);
	}

	private static bool EvaluateNpcDead(Dictionary<string, string> parameters, QuestPredicateEvaluationContext context)
	{
		if (context == null || context.IsNpcAlive == null || !TryGetInt(parameters, "npcId", out var npcId))
		{
			return false;
		}
		return !context.IsNpcAlive(npcId);
	}

	private static bool EvaluateNpcStatus(Dictionary<string, string> parameters, QuestPredicateEvaluationContext context)
	{
		if (context == null || context.IsNpcStatus == null || !TryGetInt(parameters, "npcId", out var npcId) || !TryGetInt(parameters, "statusId", out var statusId))
		{
			return false;
		}
		return context.IsNpcStatus(npcId, statusId);
	}

	private static bool EvaluateNpcAction(Dictionary<string, string> parameters, QuestPredicateEvaluationContext context)
	{
		if (context == null || context.IsNpcAction == null || !TryGetInt(parameters, "npcId", out var npcId) || !TryGetInt(parameters, "actionId", out var actionId))
		{
			return false;
		}
		return context.IsNpcAction(npcId, actionId);
	}

	private static bool EvaluateNpcTalked(Dictionary<string, string> parameters, QuestPredicateEvaluationContext context)
	{
		if (context == null || context.HasTalkedToNpc == null || !TryGetInt(parameters, "npcId", out var npcId))
		{
			return false;
		}
		return context.HasTalkedToNpc(npcId);
	}

	private static bool EvaluateCyFriend(Dictionary<string, string> parameters, QuestPredicateEvaluationContext context)
	{
		if (context == null || context.IsCyFriend == null || !TryGetInt(parameters, "npcId", out var npcId))
		{
			return false;
		}
		return context.IsCyFriend(npcId);
	}

	private static bool EvaluateNpcPresentInCurrentScene(Dictionary<string, string> parameters, QuestPredicateEvaluationContext context)
	{
		if (context == null || context.IsNpcPresentInCurrentScene == null || !TryGetInt(parameters, "npcId", out var npcId))
		{
			return false;
		}
		return context.IsNpcPresentInCurrentScene(npcId);
	}

	private static bool EvaluateNpcPatrolPresent(Dictionary<string, string> parameters, QuestPredicateEvaluationContext context)
	{
		if (context == null || context.IsNpcPatrolPresent == null || !TryGetInt(parameters, "npcId", out var npcId))
		{
			return false;
		}
		return context.IsNpcPatrolPresent(npcId);
	}

	private static bool EvaluateSceneNpcPresent(Dictionary<string, string> parameters, QuestPredicateEvaluationContext context)
	{
		if (context == null || context.IsNpcPresentInScene == null || !TryGetInt(parameters, "npcId", out var npcId))
		{
			return false;
		}
		string sceneId = GetParam(parameters, "sceneId");
		string sceneName = GetParam(parameters, "sceneName");
		return context.IsNpcPresentInScene(npcId, sceneId, sceneName);
	}

	private static bool EvaluateCombatVictoryOverNpc(Dictionary<string, string> parameters, QuestPredicateEvaluationContext context)
	{
		if (context == null || context.HasRecentBattleVictory == null || !TryGetInt(parameters, "npcId", out var npcId))
		{
			return false;
		}
		return context.HasRecentBattleVictory(npcId);
	}

	private static bool EvaluateMailDeliverySubmitted(Dictionary<string, string> parameters, QuestPredicateEvaluationContext context)
	{
		if (context?.HasRecentDeliveryMailSubmission == null)
		{
			return false;
		}
		return context.HasRecentDeliveryMailSubmission(parameters);
	}

	private static bool EvaluatePlayerHasItem(Dictionary<string, string> parameters, QuestPredicateEvaluationContext context)
	{
		if (context?.GetPlayerItemCount == null)
		{
			return false;
		}
		int requiredCount = 1;
		if (TryGetInt(parameters, "count", out var explicitCount) && explicitCount > 0)
		{
			requiredCount = explicitCount;
		}
		int ownedCount = context.GetPlayerItemCount(parameters);
		return ownedCount >= requiredCount;
	}

	private static bool EvaluatePlayerMoneyCompare(Dictionary<string, string> parameters, QuestPredicateEvaluationContext context)
	{
		if (context == null || context.GetPlayerMoney == null || !TryGetLong(parameters, "amount", out var amount))
		{
			return false;
		}
		long currentMoney = context.GetPlayerMoney();
		string op = GetParam(parameters, "operator");
		string text = op;
		string text2 = text;
		switch (text2)
		{
		default:
			if (text2.Length == 0)
			{
				break;
			}
			goto case null;
		case null:
			switch (text2)
			{
			case null:
				break;
			case "<":
				return currentMoney < amount;
			case "<=":
				return currentMoney <= amount;
			case "=":
			case "==":
				return currentMoney == amount;
			case "!=":
				return currentMoney != amount;
			default:
				return false;
			}
			break;
		case ">":
			return currentMoney > amount;
		case ">=":
			break;
		}
		return currentMoney >= amount;
	}

	private static bool EvaluatePlayerLevelCompare(Dictionary<string, string> parameters, QuestPredicateEvaluationContext context)
	{
		if (context == null || context.GetPlayerLevel == null || !TryGetInt(parameters, "level", out var level))
		{
			return false;
		}
		return CompareInt(context.GetPlayerLevel(), GetParam(parameters, "operator"), level);
	}

	private static bool EvaluatePlayerHasSkill(Dictionary<string, string> parameters, QuestPredicateEvaluationContext context)
	{
		int skillId;
		return context != null && context.HasPlayerSkill != null && TryGetInt(parameters, "skillId", out skillId) && context.HasPlayerSkill(skillId);
	}

	private static bool EvaluatePlayerHasStaticSkill(Dictionary<string, string> parameters, QuestPredicateEvaluationContext context)
	{
		int skillId;
		return context != null && context.HasPlayerStaticSkill != null && TryGetInt(parameters, "skillId", out skillId) && context.HasPlayerStaticSkill(skillId);
	}

	private static bool EvaluatePlayerFavorCompare(Dictionary<string, string> parameters, QuestPredicateEvaluationContext context)
	{
		if (context == null || context.GetNpcFavor == null || !TryGetInt(parameters, "npcId", out var npcId) || !TryGetInt(parameters, "value", out var value))
		{
			return false;
		}
		return CompareInt(context.GetNpcFavor(npcId), GetParam(parameters, "operator"), value);
	}

	private static bool EvaluateOfficialTaskStarted(Dictionary<string, string> parameters, QuestPredicateEvaluationContext context)
	{
		if (context == null || context.IsOfficialTaskStarted == null || !TryGetInt(parameters, "taskId", out var taskId))
		{
			return false;
		}
		return context.IsOfficialTaskStarted(taskId);
	}

	private static bool EvaluateOfficialTaskNotOutdated(Dictionary<string, string> parameters, QuestPredicateEvaluationContext context)
	{
		if (context == null || context.IsOfficialTaskNotOutdated == null || !TryGetInt(parameters, "taskId", out var taskId))
		{
			return false;
		}
		return context.IsOfficialTaskNotOutdated(taskId);
	}

	private static bool EvaluateOfficialTaskStepFinished(Dictionary<string, string> parameters, QuestPredicateEvaluationContext context)
	{
		if (context == null || context.IsOfficialTaskStepFinished == null || !TryGetInt(parameters, "taskId", out var taskId) || !TryGetInt(parameters, "taskIndex", out var taskIndex))
		{
			return false;
		}
		return context.IsOfficialTaskStepFinished(taskId, taskIndex);
	}

	private static bool EvaluateOfficialTaskFinishable(Dictionary<string, string> parameters, QuestPredicateEvaluationContext context)
	{
		if (context == null || context.IsOfficialTaskFinishable == null || !TryGetInt(parameters, "taskId", out var taskId))
		{
			return false;
		}
		return context.IsOfficialTaskFinishable(taskId);
	}

	private static bool TryGetInt(Dictionary<string, string> parameters, string key, out int value)
	{
		value = 0;
		string raw;
		return TryGetParam(parameters, key, out raw) && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
	}

	private static bool TryGetLong(Dictionary<string, string> parameters, string key, out long value)
	{
		value = 0L;
		string raw;
		return TryGetParam(parameters, key, out raw) && long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
	}

	private static bool CompareInt(int currentValue, string op, int targetValue)
	{
		switch (op)
		{
		default:
			if (op.Length == 0)
			{
				break;
			}
			goto case null;
		case null:
			switch (op)
			{
			case null:
				break;
			case "<":
				return currentValue < targetValue;
			case "<=":
				return currentValue <= targetValue;
			case "=":
			case "==":
				return currentValue == targetValue;
			case "!=":
				return currentValue != targetValue;
			default:
				return false;
			}
			break;
		case ">":
			return currentValue > targetValue;
		case ">=":
			break;
		}
		return currentValue >= targetValue;
	}

	private static bool TryGetParam(Dictionary<string, string> parameters, string key, out string value)
	{
		value = null;
		return parameters != null && parameters.TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value);
	}

	private static string GetParam(Dictionary<string, string> parameters, string key)
	{
		string value;
		return TryGetParam(parameters, key, out value) ? value.Trim() : string.Empty;
	}
}
