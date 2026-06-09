using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MCS_AIChatMod;

internal sealed class AIQuestStoryLineJsonConverter : JsonConverter
{
	public override bool CanConvert(Type objectType)
	{
		return objectType == typeof(AIQuestStoryLine);
	}

	public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
	{
		if (reader.TokenType == JsonToken.Null)
		{
			return null;
		}
		if (reader.TokenType == JsonToken.String)
		{
			string text = reader.Value?.ToString();
			return string.IsNullOrWhiteSpace(text) ? null : AIQuestStoryLine.CreateNpc(text.Trim());
		}
		JObject obj = JObject.Load(reader);
		AIQuestStoryLine line = new AIQuestStoryLine();
		serializer.Populate(obj.CreateReader(), line);
		if (string.IsNullOrWhiteSpace(line.Text))
		{
			return null;
		}
		line.Text = line.Text.Trim();
		if (line.SpeakerType != QuestStorySpeakerType.Npc)
		{
			line.SpeakerNpcId = 0;
		}
		return line;
	}

	public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
	{
		if (!(value is AIQuestStoryLine line))
		{
			writer.WriteNull();
			return;
		}
		writer.WriteStartObject();
		writer.WritePropertyName("speakerType");
		writer.WriteValue(line.SpeakerType.ToString());
		writer.WritePropertyName("text");
		writer.WriteValue(line.Text);
		if (line.SpeakerType == QuestStorySpeakerType.Npc && line.SpeakerNpcId > 0)
		{
			writer.WritePropertyName("speakerNpcId");
			writer.WriteValue(line.SpeakerNpcId);
		}
		writer.WriteEndObject();
	}
}
