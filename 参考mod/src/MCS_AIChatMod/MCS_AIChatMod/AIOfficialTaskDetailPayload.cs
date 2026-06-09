using System.Collections.Generic;

namespace MCS_AIChatMod;

internal sealed class AIOfficialTaskDetailPayload
{
	public string StatusLabel;

	public string StatusValue;

	public List<string> ProgressLines = new List<string>();

	public string TaskName;

	public string DescriptionText;
}
