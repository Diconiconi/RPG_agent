using System;
using System.Collections.Generic;

namespace MCS_AIChatMod;

[Serializable]
public class QuestNodeWorldControl
{
	public string SceneId;

	public List<QuestNpcPresenceRequirement> NpcRequirements = new List<QuestNpcPresenceRequirement>();
}
