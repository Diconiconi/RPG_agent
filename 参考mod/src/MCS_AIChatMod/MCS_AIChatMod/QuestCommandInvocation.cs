using System;
using System.Collections.Generic;

namespace MCS_AIChatMod;

[Serializable]
public class QuestCommandInvocation
{
	public string Opcode;

	public Dictionary<string, string> Params = new Dictionary<string, string>();
}
