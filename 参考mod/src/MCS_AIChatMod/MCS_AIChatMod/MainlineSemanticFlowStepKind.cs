using System;

namespace MCS_AIChatMod;

[Serializable]
public enum MainlineSemanticFlowStepKind
{
	EncounterAtScene,
	VisitSceneGate,
	FormalTalkGate,
	ReportBackInPerson,
	PlayScriptBlock,
	PresentChoice,
	AcquireItemGate,
	DeliverItemInPerson,
	CombatGate,
	SceneTransition
}
