using Newtonsoft.Json;

namespace MCS_AIChatMod;

public class FunctionDefinition
{
	[JsonProperty("name")]
	public string Name { get; set; }

	[JsonProperty("description")]
	public string Description { get; set; }

	[JsonProperty("parameters")]
	public object Parameters { get; set; }
}
