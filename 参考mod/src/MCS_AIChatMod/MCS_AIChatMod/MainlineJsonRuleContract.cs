using System;

namespace MCS_AIChatMod;

[Serializable]
public class MainlineJsonRuleContract
{
	public string RuleId;

	public string Scope;

	public string Condition;

	public string Requirement;

	public string ErrorMessage;
}
