using System.Collections.Generic;

namespace MCS_AIChatMod;

public class ChatRequest
{
	public List<ChatMessage> Messages { get; set; } = new List<ChatMessage>();

	public string Model { get; set; }

	public bool Stream { get; set; } = false;

	public int? MaxTokens { get; set; }

	public float? Temperature { get; set; }

	public float? TopP { get; set; }

	public float? FrequencyPenalty { get; set; }

	public float? PresencePenalty { get; set; }

	public List<Tool> Tools { get; set; }
}
