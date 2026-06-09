using System;
using System.Collections.Generic;

namespace MCS_AIChatMod;

[Serializable]
public class MainlineBeatGamePlan
{
	public List<int> UsedNpcIds = new List<int>();

	public List<string> UsedSceneIds = new List<string>();

	public List<MainlineSemanticFlowStep> FlowSteps = new List<MainlineSemanticFlowStep>();
}
