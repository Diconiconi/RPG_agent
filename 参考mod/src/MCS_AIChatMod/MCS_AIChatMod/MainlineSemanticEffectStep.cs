using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace MCS_AIChatMod;

[Serializable]
public class MainlineSemanticEffectStep
{
	public string EffectRef;

	[JsonConverter(typeof(StringEnumConverter))]
	public MainlineSemanticEffectStepKind Kind = MainlineSemanticEffectStepKind.FavorChange;

	public string TargetNpcName;

	public int TargetNpcId;

	public JObject Payload = new JObject();
}
