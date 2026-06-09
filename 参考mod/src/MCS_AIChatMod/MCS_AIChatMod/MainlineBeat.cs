using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MCS_AIChatMod;

[Serializable]
public class MainlineBeat
{
	public string BeatId;

	public string Title;

	public string Summary;

	public BeatTimeIntent TimeIntent = new BeatTimeIntent();

	public List<int> KeyNpcIds = new List<int>();

	public List<string> KeyLocationIds = new List<string>();

	public MainlineRawNarrativeDraft RawNarrativeDraft = new MainlineRawNarrativeDraft();

	public MainlineBeatStoryDraft StoryDraft = new MainlineBeatStoryDraft();

	public MainlineBeatGamePlan GamePlan = new MainlineBeatGamePlan();

	public List<string> QuestIds = new List<string>();

	public WorldContract WorldContract = new WorldContract();

	[JsonConverter(typeof(StringEnumConverter))]
	public MainlineBeatStatus Status = MainlineBeatStatus.Planned;
}
