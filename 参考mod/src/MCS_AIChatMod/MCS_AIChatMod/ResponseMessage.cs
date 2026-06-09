using System.Collections.Generic;
using Newtonsoft.Json;

namespace MCS_AIChatMod;

public class ResponseMessage
{
	[JsonProperty("role")]
	public string Role { get; set; }

	[JsonProperty("content")]
	public string Content { get; set; }

	[JsonProperty("tool_calls")]
	public List<ToolCall> ToolCalls { get; set; }
}
