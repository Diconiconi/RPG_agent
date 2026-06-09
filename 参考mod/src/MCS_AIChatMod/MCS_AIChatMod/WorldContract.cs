using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace MCS_AIChatMod;

[Serializable]
public class WorldContract
{
	public string ContractId;

	public string Summary;

	[JsonProperty("allowedNpcIds")]
	public List<int> AllowedNpcIds = new List<int>();

	[JsonProperty("allowedSceneIds")]
	public List<string> AllowedSceneIds = new List<string>();

	[JsonProperty("selectedPredicates")]
	public List<string> SelectedPredicates = new List<string>();

	[JsonProperty("selectedCommands")]
	public List<string> SelectedCommands = new List<string>();
}
