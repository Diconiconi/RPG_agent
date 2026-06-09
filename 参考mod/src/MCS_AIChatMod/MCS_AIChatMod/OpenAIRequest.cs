using System.Collections.Generic;
using Newtonsoft.Json;

namespace MCS_AIChatMod;

public class OpenAIRequest
{
	[JsonProperty("model")]
	public string Model { get; set; }

	[JsonProperty("messages")]
	public List<ChatMessage> Messages { get; set; }

	[JsonProperty("stream")]
	public bool Stream { get; set; }

	[JsonProperty("max_tokens", NullValueHandling = NullValueHandling.Ignore)]
	public int? MaxTokens { get; set; }

	[JsonProperty("temperature", NullValueHandling = NullValueHandling.Ignore)]
	public float? Temperature { get; set; }

	[JsonProperty("top_p", NullValueHandling = NullValueHandling.Ignore)]
	public float? TopP { get; set; }

	[JsonProperty("frequency_penalty", NullValueHandling = NullValueHandling.Ignore)]
	public float? FrequencyPenalty { get; set; }

	[JsonProperty("presence_penalty", NullValueHandling = NullValueHandling.Ignore)]
	public float? PresencePenalty { get; set; }

	[JsonProperty("tools", NullValueHandling = NullValueHandling.Ignore)]
	public List<Tool> Tools { get; set; }
}
