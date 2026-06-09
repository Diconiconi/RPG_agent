using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MCS_AIChatMod;

[Serializable]
public class MainlineChapter
{
	public string ChapterId;

	public string Title;

	public string Summary;

	public string Theme;

	public List<int> KeyNpcIds = new List<int>();

	public List<string> KeySceneIds = new List<string>();

	public List<string> ResourceNeeds = new List<string>();

	public ChapterTimeIntent TimeIntent = new ChapterTimeIntent();

	public MainlineBeat CurrentBeat;

	public PendingBiographyEventState PendingBiographyEvent;

	[JsonConverter(typeof(StringEnumConverter))]
	public MainlineChapterStatus Status = MainlineChapterStatus.NotStarted;

	public MainlineBeat GetCurrentBeat()
	{
		return CurrentBeat;
	}
}
