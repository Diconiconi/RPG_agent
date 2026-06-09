using System;
using System.Collections.Generic;

namespace MCS_AIChatMod;

[Serializable]
public class QuestPredicateInvocation
{
	public string Opcode;

	public Dictionary<string, string> Params = new Dictionary<string, string>();

	public List<QuestPredicateInvocation> Children = new List<QuestPredicateInvocation>();
}
