using LlmTornado.Code;

namespace MCS_AIChatMod;

internal sealed class ProviderCompatibilityRules
{
	public ProviderCompatibilityProfile Profile;

	public CompatibilityTransportKind TransportKind;

	public LLmProviders NativeProvider;

	public bool StripFrequencyPenalty;

	public bool StripPresencePenalty;

	public bool StripInternalToolMetadata;

	public bool DisableStreamWhenToolsPresent;

	public bool DisableTools;

	public bool NormalizeGeminiModelPrefix;

	public bool NullOutZeroTemperature;
}
