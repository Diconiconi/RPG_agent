using LlmTornado.Code;

namespace MCS_AIChatMod;

internal sealed class ResolvedTransportPlan
{
	public ProviderCompatibilityProfile Profile;

	public CompatibilityTransportKind TransportKind;

	public LLmProviders NativeProvider;

	public ProviderCompatibilityRules Rules;
}
