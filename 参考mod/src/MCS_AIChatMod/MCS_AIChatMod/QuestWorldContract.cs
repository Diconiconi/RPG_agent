using System;
using System.Collections.Generic;

namespace MCS_AIChatMod;

[Serializable]
public class QuestWorldContract
{
	public List<int> AllowedNpcIds = new List<int>();

	public List<string> AllowedSceneIds = new List<string>();

	public List<string> SelectedPredicates = new List<string>();

	public List<string> SelectedCommands = new List<string>();
}
