using Newtonsoft.Json;

namespace MCS_AIChatMod;

public class FunctionCall
{
	[JsonProperty("name")]
	public string Name { get; set; }

	[JsonProperty("arguments")]
	public string Arguments { get; set; }
}
