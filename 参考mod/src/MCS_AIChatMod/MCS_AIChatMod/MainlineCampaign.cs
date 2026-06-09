using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MCS_AIChatMod;

[Serializable]
public class MainlineCampaign
{
	public string CampaignId;

	public string Title;

	public string Premise;

	public string StoryBible;

	public int SeedNpcId;

	public string SeedNpcName;

	public int CurrentChapterIndex;

	public List<MainlineChapter> Chapters = new List<MainlineChapter>();

	public ContinuityMemorySnapshot Continuity = new ContinuityMemorySnapshot();

	[JsonConverter(typeof(StringEnumConverter))]
	public CampaignStatus Status = CampaignStatus.NotStarted;

	public DateTime CreatedAt;

	public MainlineChapter GetCurrentChapter()
	{
		if (CurrentChapterIndex >= 0 && CurrentChapterIndex < Chapters.Count)
		{
			return Chapters[CurrentChapterIndex];
		}
		return null;
	}
}
