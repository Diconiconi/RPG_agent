using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MCS_AIChatMod;

[Serializable]
[JsonConverter(typeof(AIQuestStoryLineJsonConverter))]
public class AIQuestStoryLine
{
	[JsonConverter(typeof(StringEnumConverter))]
	public QuestStorySpeakerType SpeakerType = QuestStorySpeakerType.Npc;

	public string Text;

	public int SpeakerNpcId;

	public static AIQuestStoryLine CreateNpc(string text, int speakerNpcId = 0)
	{
		return new AIQuestStoryLine
		{
			SpeakerType = QuestStorySpeakerType.Npc,
			Text = text,
			SpeakerNpcId = speakerNpcId
		};
	}

	public static AIQuestStoryLine CreatePlayer(string text)
	{
		return new AIQuestStoryLine
		{
			SpeakerType = QuestStorySpeakerType.Player,
			Text = text
		};
	}

	public static AIQuestStoryLine CreateNarrator(string text)
	{
		return new AIQuestStoryLine
		{
			SpeakerType = QuestStorySpeakerType.Narrator,
			Text = text
		};
	}
}
