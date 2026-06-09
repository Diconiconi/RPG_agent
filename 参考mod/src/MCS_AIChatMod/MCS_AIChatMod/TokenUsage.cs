using Newtonsoft.Json;

namespace MCS_AIChatMod;

public class TokenUsage
{
	[JsonProperty("prompt_tokens")]
	public int PromptTokens { get; set; }

	[JsonProperty("completion_tokens")]
	public int CompletionTokens { get; set; }

	[JsonProperty("total_tokens")]
	public int TotalTokens { get; set; }

	public bool HasUsage => PromptTokens > 0 || CompletionTokens > 0 || TotalTokens > 0;

	public int ResolvedTotalTokens => (TotalTokens > 0) ? TotalTokens : (PromptTokens + CompletionTokens);

	public TokenUsage Clone()
	{
		return new TokenUsage
		{
			PromptTokens = PromptTokens,
			CompletionTokens = CompletionTokens,
			TotalTokens = TotalTokens
		};
	}

	public void Add(TokenUsage other)
	{
		if (other != null)
		{
			PromptTokens += other.PromptTokens;
			CompletionTokens += other.CompletionTokens;
			TotalTokens += other.ResolvedTotalTokens;
		}
	}
}
