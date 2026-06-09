using System;

namespace MCS_AIChatMod;

public sealed class QuestEventBus
{
	public event Action<QuestRuntimeEvent> EventRaised;

	public void Publish(QuestRuntimeEvent runtimeEvent)
	{
		this.EventRaised?.Invoke(runtimeEvent);
	}
}
