using Newtonsoft.Json;

namespace MCS_AIChatMod;

public class ToolCall
{
	[JsonProperty("id")]
	public string Id { get; set; }

	[JsonProperty("type")]
	public string Type { get; set; }

	[JsonProperty("function")]
	public FunctionCall Function { get; set; }
}
