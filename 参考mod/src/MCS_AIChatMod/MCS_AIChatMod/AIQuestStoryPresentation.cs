using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MCS_AIChatMod;

[Serializable]
public class AIQuestStoryPresentation
{
	[JsonConverter(typeof(StringEnumConverter))]
	public QuestStorySpeakerType SpeakerType;

	public string SpeakerName;

	public int CharacterId;

	public bool ShowCharacterImage;

	public bool IsPlayerBubble;

	public bool IsNarrator;
}
