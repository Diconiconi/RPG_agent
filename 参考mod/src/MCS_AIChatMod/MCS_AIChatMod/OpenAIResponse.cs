using System.Collections.Generic;
using Newtonsoft.Json;

namespace MCS_AIChatMod;

public class OpenAIResponse
{
	[JsonProperty("id")]
	public string Id { get; set; }

	[JsonProperty("choices")]
	public List<Choice> Choices { get; set; }

	[JsonProperty("model")]
	public string Model { get; set; }

	[JsonProperty("usage")]
	public TokenUsage Usage { get; set; }
}
