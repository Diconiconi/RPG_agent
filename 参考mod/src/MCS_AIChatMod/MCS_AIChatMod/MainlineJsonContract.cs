using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace MCS_AIChatMod;

[Serializable]
public class MainlineJsonContract
{
	public string ContractId;

	public string Goal;

	public List<MainlineJsonFieldContract> FieldContracts = new List<MainlineJsonFieldContract>();

	public List<MainlineJsonRuleContract> Rules = new List<MainlineJsonRuleContract>();

	public JObject OutputShell = new JObject();
}
