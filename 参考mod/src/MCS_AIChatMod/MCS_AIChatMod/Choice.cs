using Newtonsoft.Json;

namespace MCS_AIChatMod;

public class Choice
{
	[JsonProperty("index")]
	public int Index { get; set; }

	[JsonProperty("message")]
	public ResponseMessage Message { get; set; }

	[JsonProperty("finish_reason")]
	public string FinishReason { get; set; }
}
