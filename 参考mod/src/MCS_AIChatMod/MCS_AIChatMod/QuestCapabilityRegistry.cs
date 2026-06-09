using System;
using System.Collections.Generic;
using System.Linq;

namespace MCS_AIChatMod;

public static class QuestCapabilityRegistry
{
	private static readonly Dictionary<string, QuestCapabilityDescriptor> PredicateDescriptors = BuildPredicateDescriptors();

	private static readonly Dictionary<string, QuestCapabilityDescriptor> CommandDescriptors = BuildCommandDescriptors();

	public static QuestCapabilityDescriptor GetPredicateDescriptor(string opcode)
	{
		return GetDescriptor(PredicateDescriptors, opcode);
	}

	public static QuestCapabilityDescriptor GetCommandDescriptor(string opcode)
	{
		return GetDescriptor(CommandDescriptors, opcode);
	}

	internal static QuestCapabilityDescriptor GetPredicateDescriptorForTest(string opcode)
	{
		return GetPredicateDescriptor(opcode);
	}

	internal static QuestCapabilityDescriptor GetCommandDescriptorForTest(string opcode)
	{
		return GetCommandDescriptor(opcode);
	}

	public static IReadOnlyList<QuestCapabilityDescriptor> GetPredicateDescriptors()
	{
		return PredicateDescriptors.Values.OrderBy((QuestCapabilityDescriptor descriptor) => descriptor.Opcode, StringComparer.OrdinalIgnoreCase).ToList();
	}

	public static IReadOnlyList<QuestCapabilityDescriptor> GetCommandDescriptors()
	{
		return CommandDescriptors.Values.OrderBy((QuestCapabilityDescriptor descriptor) => descriptor.Opcode, StringComparer.OrdinalIgnoreCase).ToList();
	}

	private static QuestCapabilityDescriptor GetDescriptor(Dictionary<string, QuestCapabilityDescriptor> source, string opcode)
	{
		if (string.IsNullOrWhiteSpace(opcode))
		{
			return null;
		}
		source.TryGetValue(opcode.Trim(), out var descriptor);
		return descriptor;
	}

	private static Dictionary<string, QuestCapabilityDescriptor> BuildPredicateDescriptors()
	{
		return new Dictionary<string, QuestCapabilityDescriptor>(StringComparer.OrdinalIgnoreCase)
		{
			["logic.all"] = new QuestCapabilityDescriptor
			{
				Opcode = "logic.all",
				Kind = QuestCapabilityKind.Predicate,
				OfficialBridge = "QuestPredicateEngine.EvaluateAll",
				AllowedTracks = MainlineOnlyTracks()
			},
			["logic.any"] = new QuestCapabilityDescriptor
			{
				Opcode = "logic.any",
				Kind = QuestCapabilityKind.Predicate,
				OfficialBridge = "QuestPredicateEngine.EvaluateAny",
				AllowedTracks = MainlineOnlyTracks()
			},
			["logic.not"] = new QuestCapabilityDescriptor
			{
				Opcode = "logic.not",
				Kind = QuestCapabilityKind.Predicate,
				OfficialBridge = "QuestPredicateEngine.EvaluateNot",
				AllowedTracks = MainlineOnlyTracks()
			},
			["time.compare"] = new QuestCapabilityDescriptor
			{
				Opcode = "time.compare",
				Kind = QuestCapabilityKind.Predicate,
				OfficialBridge = "worldTimeMag.getNowTime",
				AllowedTracks = MainlineOnlyTracks(),
				ParameterSchema = new Dictionary<string, string>
				{
					["operator"] = "string",
					["value"] = "datetime"
				}
			},
			["time.window"] = new QuestCapabilityDescriptor
			{
				Opcode = "time.window",
				Kind = QuestCapabilityKind.Predicate,
				OfficialBridge = "worldTimeMag.getNowTime",
				AllowedTracks = MainlineOnlyTracks(),
				ParameterSchema = new Dictionary<string, string>
				{
					["start"] = "datetime",
					["end"] = "datetime"
				}
			},
			["fuben.remaining_days"] = new QuestCapabilityDescriptor
			{
				Opcode = "fuben.remaining_days",
				Kind = QuestCapabilityKind.Predicate,
				OfficialBridge = "player.fubenContorl[screen].ResidueTimeDay",
				AllowedTracks = MainlineOnlyTracks(),
				ParameterSchema = new Dictionary<string, string>
				{
					["operator"] = "string",
					["days"] = "int"
				}
			},
			["scene.type_is"] = new QuestCapabilityDescriptor
			{
				Opcode = "scene.type_is",
				Kind = QuestCapabilityKind.Predicate,
				OfficialBridge = "SceneEx.GetNowSceneType",
				AllowedTracks = MainlineOnlyTracks(),
				ParameterSchema = new Dictionary<string, string> { ["sceneType"] = "int" }
			},
			["scene.is"] = new QuestCapabilityDescriptor
			{
				Opcode = "scene.is",
				Kind = QuestCapabilityKind.Predicate,
				OfficialBridge = "QuestElementPicker.BuildSceneHierarchy",
				AllowedTracks = MainlineOnlyTracks(),
				ParameterSchema = new Dictionary<string, string> { ["sceneId"] = "string" }
			},
			["npc.alive"] = new QuestCapabilityDescriptor
			{
				Opcode = "npc.alive",
				Kind = QuestCapabilityKind.Predicate,
				OfficialBridge = "NpcJieSuanManager.inst.IsDeath",
				AllowedTracks = MainlineOnlyTracks(),
				ParameterSchema = new Dictionary<string, string> { ["npcId"] = "int" }
			},
			["npc.dead"] = new QuestCapabilityDescriptor
			{
				Opcode = "npc.dead",
				Kind = QuestCapabilityKind.Predicate,
				OfficialBridge = "NPCDeath.SetNpcDeath|NpcJieSuanManager.inst.IsDeath",
				AllowedTracks = MainlineOnlyTracks(),
				ParameterSchema = new Dictionary<string, string> { ["npcId"] = "int" }
			},
			["npc.status_is"] = new QuestCapabilityDescriptor
			{
				Opcode = "npc.status_is",
				Kind = QuestCapabilityKind.Predicate,
				OfficialBridge = "NpcJieSuanManager.inst.npcStatus.IsInTargetStatus",
				AllowedTracks = MainlineOnlyTracks(),
				ParameterSchema = new Dictionary<string, string>
				{
					["npcId"] = "int",
					["statusId"] = "int"
				}
			},
			["npc.action_is"] = new QuestCapabilityDescriptor
			{
				Opcode = "npc.action_is",
				Kind = QuestCapabilityKind.Predicate,
				OfficialBridge = "NpcJieSuanManager.inst.GetNpcData()['ActionId']",
				AllowedTracks = MainlineOnlyTracks(),
				ParameterSchema = new Dictionary<string, string>
				{
					["npcId"] = "int",
					["actionId"] = "int"
				}
			},
			["npc.talked_to_player"] = new QuestCapabilityDescriptor
			{
				Opcode = "npc.talked_to_player",
				Kind = QuestCapabilityKind.Predicate,
				OfficialBridge = "AIQuestManager.NotifyTalkToNpc",
				AllowedTracks = MainlineOnlyTracks(),
				ParameterSchema = new Dictionary<string, string> { ["npcId"] = "int" }
			},
			["npc.cy_friend"] = new QuestCapabilityDescriptor
			{
				Opcode = "npc.cy_friend",
				Kind = QuestCapabilityKind.Predicate,
				OfficialBridge = "emailDateMag.IsFriend",
				AllowedTracks = MainlineOnlyTracks(),
				ParameterSchema = new Dictionary<string, string> { ["npcId"] = "int" }
			},
			["npc.present_in_current_scene"] = new QuestCapabilityDescriptor
			{
				Opcode = "npc.present_in_current_scene",
				Kind = QuestCapabilityKind.Predicate,
				OfficialBridge = "QuestElementPicker.GetNpcCurrentScene|NPCMap|NpcJieSuanManager.inst.GetXunLuoNpcList",
				AllowedTracks = MainlineOnlyTracks(),
				ParameterSchema = new Dictionary<string, string> { ["npcId"] = "int" }
			},
			["npc.patrol_present"] = new QuestCapabilityDescriptor
			{
				Opcode = "npc.patrol_present",
				Kind = QuestCapabilityKind.Predicate,
				OfficialBridge = "NpcJieSuanManager.inst.GetXunLuoNpcList",
				AllowedTracks = MainlineOnlyTracks(),
				ParameterSchema = new Dictionary<string, string> { ["npcId"] = "int" }
			},
			["scene.npc_present"] = new QuestCapabilityDescriptor
			{
				Opcode = "scene.npc_present",
				Kind = QuestCapabilityKind.Predicate,
				OfficialBridge = "QuestElementPicker.GetNpcCurrentScene|QuestElementPicker.BuildSceneHierarchy",
				AllowedTracks = MainlineOnlyTracks(),
				ParameterSchema = new Dictionary<string, string>
				{
					["npcId"] = "int",
					["sceneId"] = "string"
				}
			},
			["combat.victory_over_npc"] = new QuestCapabilityDescriptor
			{
				Opcode = "combat.victory_over_npc",
				Kind = QuestCapabilityKind.Predicate,
				OfficialBridge = "Fight.FightResultMag.ShowVictory|AIQuestManager.NotifyBattleVictory",
				AllowedTracks = MainlineOnlyTracks(),
				ParameterSchema = new Dictionary<string, string> { ["npcId"] = "int" }
			},
			["mail.delivery_submitted"] = new QuestCapabilityDescriptor
			{
				Opcode = "mail.delivery_submitted",
				Kind = QuestCapabilityKind.Predicate,
				OfficialBridge = "CyEmailCell.Submit|AIQuestManager.NotifyQuestDeliveryMailSubmitted",
				AllowedTracks = MainlineOnlyTracks(),
				ParameterSchema = new Dictionary<string, string>
				{
					["npcId"] = "int",
					["itemId"] = "int",
					["mailId"] = "int"
				}
			},
			["player.has_item"] = new QuestCapabilityDescriptor
			{
				Opcode = "player.has_item",
				Kind = QuestCapabilityKind.Predicate,
				OfficialBridge = "Avatar.getItemNum",
				AllowedTracks = MainlineOnlyTracks(),
				ParameterSchema = new Dictionary<string, string>
				{
					["itemId"] = "int",
					["count"] = "int"
				}
			},
			["player.money_compare"] = new QuestCapabilityDescriptor
			{
				Opcode = "player.money_compare",
				Kind = QuestCapabilityKind.Predicate,
				OfficialBridge = "Avatar.money",
				AllowedTracks = MainlineOnlyTracks(),
				ParameterSchema = new Dictionary<string, string>
				{
					["operator"] = "string",
					["amount"] = "int"
				}
			},
			["player.level_compare"] = new QuestCapabilityDescriptor
			{
				Opcode = "player.level_compare",
				Kind = QuestCapabilityKind.Predicate,
				OfficialBridge = "Avatar.level",
				AllowedTracks = MainlineOnlyTracks(),
				ParameterSchema = new Dictionary<string, string>
				{
					["operator"] = "string",
					["level"] = "int"
				}
			},
			["player.has_skill"] = new QuestCapabilityDescriptor
			{
				Opcode = "player.has_skill",
				Kind = QuestCapabilityKind.Predicate,
				OfficialBridge = "PlayerEx.HasSkill",
				AllowedTracks = MainlineOnlyTracks(),
				ParameterSchema = new Dictionary<string, string> { ["skillId"] = "int" }
			},
			["player.has_static_skill"] = new QuestCapabilityDescriptor
			{
				Opcode = "player.has_static_skill",
				Kind = QuestCapabilityKind.Predicate,
				OfficialBridge = "PlayerEx.HasStaticSkill",
				AllowedTracks = MainlineOnlyTracks(),
				ParameterSchema = new Dictionary<string, string> { ["skillId"] = "int" }
			},
			["player.favor_compare"] = new QuestCapabilityDescriptor
			{
				Opcode = "player.favor_compare",
				Kind = QuestCapabilityKind.Predicate,
				OfficialBridge = "NPCEx.GetFavor",
				AllowedTracks = MainlineOnlyTracks(),
				ParameterSchema = new Dictionary<string, string>
				{
					["npcId"] = "int",
					["operator"] = "string",
					["value"] = "int"
				}
			},
			["official_task.started"] = new QuestCapabilityDescriptor
			{
				Opcode = "official_task.started",
				Kind = QuestCapabilityKind.Predicate,
				OfficialBridge = "KBEngine.TaskMag.isHasTask",
				AllowedTracks = MainlineOnlyTracks(),
				ParameterSchema = new Dictionary<string, string> { ["taskId"] = "int" }
			},
			["task.official_not_outdated"] = new QuestCapabilityDescriptor
			{
				Opcode = "task.official_not_outdated",
				Kind = QuestCapabilityKind.Predicate,
				OfficialBridge = "TaskUIManager.checkIsGuoShi",
				AllowedTracks = MainlineOnlyTracks(),
				ParameterSchema = new Dictionary<string, string> { ["taskId"] = "int" }
			},
			["official_task.step_finished"] = new QuestCapabilityDescriptor
			{
				Opcode = "official_task.step_finished",
				Kind = QuestCapabilityKind.Predicate,
				OfficialBridge = "Fungus.SetTaskIndexFinish.Do|KBEngine.TaskMag._TaskData.finishIndex",
				AllowedTracks = MainlineOnlyTracks(),
				ParameterSchema = new Dictionary<string, string>
				{
					["taskId"] = "int",
					["taskIndex"] = "int"
				}
			},
			["official_task.finishable"] = new QuestCapabilityDescriptor
			{
				Opcode = "official_task.finishable",
				Kind = QuestCapabilityKind.Predicate,
				OfficialBridge = "TaskInfoJsonData|KBEngine.TaskMag._TaskData.finishIndex",
				AllowedTracks = MainlineOnlyTracks(),
				ParameterSchema = new Dictionary<string, string> { ["taskId"] = "int" }
			}
		};
	}

	private static Dictionary<string, QuestCapabilityDescriptor> BuildCommandDescriptors()
	{
		return new Dictionary<string, QuestCapabilityDescriptor>(StringComparer.OrdinalIgnoreCase)
		{
			["npc.set_action"] = new QuestCapabilityDescriptor
			{
				Opcode = "npc.set_action",
				Kind = QuestCapabilityKind.Command,
				OfficialBridge = "NPCEx.SetNPCAction",
				AllowedTracks = MainlineOnlyTracks(),
				RequiresWorldContract = true,
				ParameterSchema = new Dictionary<string, string>
				{
					["npcId"] = "int",
					["actionId"] = "int"
				}
			},
			["npc.add_favor"] = new QuestCapabilityDescriptor
			{
				Opcode = "npc.add_favor",
				Kind = QuestCapabilityKind.Command,
				OfficialBridge = "NPCEx.AddFavor",
				AllowedTracks = MainlineOnlyTracks(),
				RequiresWorldContract = true,
				IsPermanentWorldMutation = true,
				ParameterSchema = new Dictionary<string, string>
				{
					["npcId"] = "int",
					["amount"] = "int"
				}
			},
			["npc.add_cy_friend"] = new QuestCapabilityDescriptor
			{
				Opcode = "npc.add_cy_friend",
				Kind = QuestCapabilityKind.Command,
				OfficialBridge = "Avatar.AddFriend",
				AllowedTracks = MainlineOnlyTracks(),
				RequiresWorldContract = true,
				IsPermanentWorldMutation = true,
				ParameterSchema = new Dictionary<string, string> { ["npcId"] = "int" }
			},
			["npc.set_status"] = new QuestCapabilityDescriptor
			{
				Opcode = "npc.set_status",
				Kind = QuestCapabilityKind.Command,
				OfficialBridge = "NpcJieSuanManager.inst.npcStatus.SetNpcStatus",
				AllowedTracks = MainlineOnlyTracks(),
				RequiresWorldContract = true,
				ParameterSchema = new Dictionary<string, string>
				{
					["npcId"] = "int",
					["statusId"] = "int"
				}
			},
			["npc.relocate"] = new QuestCapabilityDescriptor
			{
				Opcode = "npc.relocate",
				Kind = QuestCapabilityKind.Command,
				OfficialBridge = "NPCMap.AddNpcToThreeScene|AddNpcToFuBen|CallZhangMenToThis",
				AllowedTracks = MainlineOnlyTracks(),
				RequiresWorldContract = true,
				ParameterSchema = new Dictionary<string, string>
				{
					["npcId"] = "int",
					["sceneId"] = "string"
				}
			},
			["npc.kill"] = new QuestCapabilityDescriptor
			{
				Opcode = "npc.kill",
				Kind = QuestCapabilityKind.Command,
				OfficialBridge = "NPCDeath.SetNpcDeath",
				AllowedTracks = MainlineOnlyTracks(),
				RequiresWorldContract = true,
				IsDangerous = true,
				IsPermanentWorldMutation = true,
				ParameterSchema = new Dictionary<string, string>
				{
					["npcId"] = "int",
					["deathType"] = "int",
					["killerNpcId"] = "int"
				}
			},
			["player.add_item"] = new QuestCapabilityDescriptor
			{
				Opcode = "player.add_item",
				Kind = QuestCapabilityKind.Command,
				OfficialBridge = "Avatar.addItem|Avatar.removeItem",
				AllowedTracks = MainlineOnlyTracks(),
				RequiresWorldContract = true,
				IsPermanentWorldMutation = true,
				ParameterSchema = new Dictionary<string, string>
				{
					["itemId"] = "int",
					["count"] = "int"
				}
			},
			["player.add_money"] = new QuestCapabilityDescriptor
			{
				Opcode = "player.add_money",
				Kind = QuestCapabilityKind.Command,
				OfficialBridge = "Avatar.AddMoney",
				AllowedTracks = MainlineOnlyTracks(),
				RequiresWorldContract = true,
				IsPermanentWorldMutation = true,
				ParameterSchema = new Dictionary<string, string> { ["amount"] = "int" }
			},
			["player.learn_skill"] = new QuestCapabilityDescriptor
			{
				Opcode = "player.learn_skill",
				Kind = QuestCapabilityKind.Command,
				OfficialBridge = "Avatar.addHasSkillList",
				AllowedTracks = MainlineOnlyTracks(),
				RequiresWorldContract = true,
				IsPermanentWorldMutation = true,
				ParameterSchema = new Dictionary<string, string> { ["skillId"] = "int" }
			},
			["player.learn_static_skill"] = new QuestCapabilityDescriptor
			{
				Opcode = "player.learn_static_skill",
				Kind = QuestCapabilityKind.Command,
				OfficialBridge = "Avatar.addHasStaticSkillList",
				AllowedTracks = MainlineOnlyTracks(),
				RequiresWorldContract = true,
				IsPermanentWorldMutation = true,
				ParameterSchema = new Dictionary<string, string>
				{
					["skillId"] = "int",
					["level"] = "int"
				}
			},
			["mail.send_notification"] = new QuestCapabilityDescriptor
			{
				Opcode = "mail.send_notification",
				Kind = QuestCapabilityKind.Command,
				OfficialBridge = "QuestCyMailHelper.CreateQuestMailData|EmailDataMag.AddNewEmail",
				AllowedTracks = MainlineOnlyTracks(),
				RequiresWorldContract = true,
				IsPermanentWorldMutation = true,
				ParameterSchema = new Dictionary<string, string>
				{
					["npcId"] = "int",
					["message"] = "string",
					["historyMessage"] = "string"
				}
			},
			["official_task.start"] = new QuestCapabilityDescriptor
			{
				Opcode = "official_task.start",
				Kind = QuestCapabilityKind.Command,
				OfficialBridge = "KBEngine.TaskMag.addTask",
				AllowedTracks = MainlineOnlyTracks(),
				RequiresWorldContract = true,
				IsPermanentWorldMutation = true,
				ParameterSchema = new Dictionary<string, string> { ["taskId"] = "int" }
			},
			["official_task.finish_step"] = new QuestCapabilityDescriptor
			{
				Opcode = "official_task.finish_step",
				Kind = QuestCapabilityKind.Command,
				OfficialBridge = "Fungus.SetTaskIndexFinish.Do",
				AllowedTracks = MainlineOnlyTracks(),
				RequiresWorldContract = true,
				IsPermanentWorldMutation = true,
				ParameterSchema = new Dictionary<string, string>
				{
					["taskId"] = "int",
					["taskIndex"] = "int"
				}
			},
			["combat.start_standard"] = new QuestCapabilityDescriptor
			{
				Opcode = "combat.start_standard",
				Kind = QuestCapabilityKind.Command,
				OfficialBridge = "Fungus.StartFight.Do",
				AllowedTracks = MainlineOnlyTracks(),
				RequiresWorldContract = true,
				ParameterSchema = new Dictionary<string, string>
				{
					["npcId"] = "int",
					["fightMusic"] = "string"
				}
			},
			["scene.load"] = new QuestCapabilityDescriptor
			{
				Opcode = "scene.load",
				Kind = QuestCapabilityKind.Command,
				OfficialBridge = "AvatarTransfer|CmdSceneLoad",
				AllowedTracks = MainlineOnlyTracks(),
				RequiresWorldContract = true,
				IsDangerous = true,
				ParameterSchema = new Dictionary<string, string> { ["sceneId"] = "string" }
			}
		};
	}

	private static List<QuestTrackType> MainlineOnlyTracks()
	{
		return new List<QuestTrackType> { QuestTrackType.Mainline };
	}
}
