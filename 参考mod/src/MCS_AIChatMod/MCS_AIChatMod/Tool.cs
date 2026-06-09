using Newtonsoft.Json;

namespace MCS_AIChatMod;

public class Tool
{
	[JsonProperty("type")]
	public string Type { get; set; }

	[JsonProperty("function")]
	public FunctionDefinition Function { get; set; }
}
