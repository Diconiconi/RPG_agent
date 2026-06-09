using System.Collections.Generic;
using Newtonsoft.Json;

namespace MCS_AIChatMod;

public class ChatMessage
{
	[JsonProperty("role")]
	public string Role { get; set; }

	[JsonProperty("content")]
	public string Content { get; set; }

	[JsonProperty("tool_calls", NullValueHandling = NullValueHandling.Ignore)]
	public List<ToolCall> ToolCalls { get; set; }

	[JsonProperty("tool_call_id", NullValueHandling = NullValueHandling.Ignore)]
	public string ToolCallId { get; set; }

	public ChatMessage()
	{
	}

	public ChatMessage(string role, string content)
	{
		Role = role;
		Content = content;
	}

	public ChatMessage(string role, string content, string toolCallId = null)
	{
		Role = role;
		Content = content;
		ToolCallId = toolCallId;
	}
}
