using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MCS_AIChatMod;

[Serializable]
public class MainlineScriptBlock
{
	public string BlockRef;

	[JsonConverter(typeof(StringEnumConverter))]
	public MainlineScriptBlockKind Kind = MainlineScriptBlockKind.Dialogue;

	public string SceneName;

	public string SceneId;

	public string ScenePurpose;

	public List<string> ParticipantNpcNames = new List<string>();

	public List<int> ParticipantNpcIds = new List<int>();

	public List<AIQuestStoryLine> Lines = new List<AIQuestStoryLine>();

	public List<MainlineDialogueChoiceDraft> Choices = new List<MainlineDialogueChoiceDraft>();

	public string BranchRef;
}
