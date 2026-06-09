using System.Collections.Generic;
using System.Globalization;

namespace MCS_AIChatMod;

public sealed class QuestCommandExecutor
{
	public bool CanExecute(QuestProgram program, QuestCommandInvocation command)
	{
		if (program == null || command == null)
		{
			return false;
		}
		QuestCapabilityDescriptor descriptor = QuestCapabilityRegistry.GetCommandDescriptor(command.Opcode);
		if (descriptor == null)
		{
			return false;
		}
		if (!descriptor.AllowedTracks.Contains(program.TrackType))
		{
			return false;
		}
		if (!descriptor.RequiresWorldContract)
		{
			return true;
		}
		QuestWorldContract contract = program.WorldContract;
		if (contract == null)
		{
			return false;
		}
		Dictionary<string, string> parameters = command.Params ?? new Dictionary<string, string>();
		switch (descriptor.Opcode)
		{
		case "npc.set_action":
		case "npc.add_favor":
		case "npc.add_cy_friend":
		case "npc.set_status":
		case "npc.kill":
			return IsNpcAllowed(contract, parameters);
		case "npc.relocate":
			return IsNpcAllowed(contract, parameters) && IsSceneAllowed(contract, parameters);
		case "player.add_item":
		case "player.learn_skill":
		case "player.learn_static_skill":
		case "player.add_money":
		case "official_task.start":
		case "official_task.finish_step":
			return true;
		case "mail.send_notification":
			return IsNpcAllowed(contract, parameters);
		case "combat.start_standard":
			return IsNpcAllowed(contract, parameters);
		case "scene.load":
			return IsSceneAllowed(contract, parameters);
		default:
			return true;
		}
	}

	public bool Execute(QuestProgram program, QuestCommandInvocation command)
	{
		return CanExecute(program, command);
	}

	private static bool IsNpcAllowed(QuestWorldContract contract, Dictionary<string, string> parameters)
	{
		if (contract?.AllowedNpcIds == null || contract.AllowedNpcIds.Count == 0)
		{
			return false;
		}
		string rawNpcId;
		int npcId;
		return parameters.TryGetValue("npcId", out rawNpcId) && int.TryParse(rawNpcId, NumberStyles.Integer, CultureInfo.InvariantCulture, out npcId) && contract.AllowedNpcIds.Contains(npcId);
	}

	private static bool IsSceneAllowed(QuestWorldContract contract, Dictionary<string, string> parameters)
	{
		if (contract?.AllowedSceneIds == null || contract.AllowedSceneIds.Count == 0)
		{
			return false;
		}
		string sceneId;
		return parameters.TryGetValue("sceneId", out sceneId) && !string.IsNullOrWhiteSpace(sceneId) && contract.AllowedSceneIds.Contains(sceneId.Trim());
	}
}
