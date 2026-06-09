using System;

namespace MCS_AIChatMod;

[Serializable]
public enum MainlineScriptBlockKind
{
	Encounter,
	Dialogue,
	Choice,
	Fallout,
	DeliveryPrompt,
	CombatPrelude
}
