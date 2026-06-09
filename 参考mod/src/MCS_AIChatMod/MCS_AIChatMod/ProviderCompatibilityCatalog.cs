using LlmTornado.Code;

namespace MCS_AIChatMod;

internal static class ProviderCompatibilityCatalog
{
	public static ProviderCompatibilityProfile Parse(string profileName)
	{
		if (string.IsNullOrWhiteSpace(profileName))
		{
			return ProviderCompatibilityProfile.Auto;
		}
		switch (profileName.Trim())
		{
		case "OpenAiStandard":
		case "OpenAI":
			return ProviderCompatibilityProfile.OpenAiStandard;
		case "GeminiOpenAi":
		case "Gemini":
			return ProviderCompatibilityProfile.GeminiOpenAi;
		case "GroqOpenAi":
		case "Groq":
			return ProviderCompatibilityProfile.GroqOpenAi;
		case "QwenCompatible":
		case "Qwen":
			return ProviderCompatibilityProfile.QwenCompatible;
		case "DeepSeekStandard":
			return ProviderCompatibilityProfile.DeepSeekStandard;
		case "DeepSeekReasoning":
		case "DeepSeekReasoner":
			return ProviderCompatibilityProfile.DeepSeekReasoning;
		case "AnthropicNative":
		case "Anthropic":
			return ProviderCompatibilityProfile.AnthropicNative;
		case "CohereNative":
		case "Cohere":
			return ProviderCompatibilityProfile.CohereNative;
		case "GenericOpenAiCompatible":
		case "GenericSafe":
		case "Generic-safe":
			return ProviderCompatibilityProfile.GenericOpenAiCompatible;
		default:
			return ProviderCompatibilityProfile.Auto;
		}
	}

	public static ProviderCompatibilityRules GetRules(ProviderCompatibilityProfile profile)
	{
		return profile switch
		{
			ProviderCompatibilityProfile.GeminiOpenAi => new ProviderCompatibilityRules
			{
				Profile = profile,
				TransportKind = CompatibilityTransportKind.OpenAiCompatibleHttp,
				StripFrequencyPenalty = true,
				StripPresencePenalty = true,
				StripInternalToolMetadata = true,
				NormalizeGeminiModelPrefix = true
			},
			ProviderCompatibilityProfile.GroqOpenAi => new ProviderCompatibilityRules
			{
				Profile = profile,
				TransportKind = CompatibilityTransportKind.OpenAiCompatibleHttp,
				NullOutZeroTemperature = true,
				StripInternalToolMetadata = true
			},
			ProviderCompatibilityProfile.QwenCompatible => new ProviderCompatibilityRules
			{
				Profile = profile,
				TransportKind = CompatibilityTransportKind.OpenAiCompatibleHttp,
				DisableStreamWhenToolsPresent = true
			},
			ProviderCompatibilityProfile.DeepSeekStandard => new ProviderCompatibilityRules
			{
				Profile = profile,
				TransportKind = CompatibilityTransportKind.NativeTornado,
				NativeProvider = LLmProviders.DeepSeek
			},
			ProviderCompatibilityProfile.DeepSeekReasoning => new ProviderCompatibilityRules
			{
				Profile = profile,
				TransportKind = CompatibilityTransportKind.NativeTornado,
				NativeProvider = LLmProviders.DeepSeek,
				DisableTools = true
			},
			ProviderCompatibilityProfile.AnthropicNative => new ProviderCompatibilityRules
			{
				Profile = profile,
				TransportKind = CompatibilityTransportKind.NativeTornado,
				NativeProvider = LLmProviders.Anthropic
			},
			ProviderCompatibilityProfile.CohereNative => new ProviderCompatibilityRules
			{
				Profile = profile,
				TransportKind = CompatibilityTransportKind.NativeTornado,
				NativeProvider = LLmProviders.Cohere
			},
			ProviderCompatibilityProfile.OpenAiStandard => new ProviderCompatibilityRules
			{
				Profile = profile,
				TransportKind = CompatibilityTransportKind.NativeTornado,
				NativeProvider = LLmProviders.OpenAi
			},
			_ => new ProviderCompatibilityRules
			{
				Profile = profile,
				TransportKind = CompatibilityTransportKind.OpenAiCompatibleHttp,
				StripInternalToolMetadata = true
			},
		};
	}
}
