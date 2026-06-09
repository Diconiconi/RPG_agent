using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace MCS_AIChatMod;

[Serializable]
public class MainlineSemanticFlowStep
{
	public string StepRef;

	[JsonConverter(typeof(StringEnumConverter))]
	public MainlineSemanticFlowStepKind Kind = MainlineSemanticFlowStepKind.PlayScriptBlock;

	public string SourceBlockRef;

	public string SceneName;

	public string SceneId;

	public string TargetNpcName;

	public int TargetNpcId;

	public string BranchRef;

	public JObject Payload = new JObject();

	public List<MainlineSemanticEffectStep> Effects = new List<MainlineSemanticEffectStep>();
}
