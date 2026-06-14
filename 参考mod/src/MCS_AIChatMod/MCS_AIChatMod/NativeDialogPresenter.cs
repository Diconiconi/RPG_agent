using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using BepInEx.Logging;
using Fungus;

namespace MCS_AIChatMod;

internal static class NativeDialogPresenter
{
	private static bool nextInitAttempted;

	internal static Task<bool> TryShowNpcReplyAsync(NPCInfo npcInfo, string text, ManualLogSource logger)
	{
		if (TryStartNextDialogEvent(npcInfo, text, logger, out var nextTask))
		{
			return nextTask;
		}
		if (TryShowWithSayDialog(npcInfo?.NpcID ?? 0, text, logger, out var sayDialogTask))
		{
			return sayDialogTask;
		}
		return System.Threading.Tasks.Task.FromResult(false);
	}

	private static bool TryStartNextDialogEvent(NPCInfo npcInfo, string text, ManualLogSource logger, out Task<bool> task)
	{
		task = null;
		try
		{
			Assembly nextAssembly = AppDomain.CurrentDomain.GetAssemblies()
				.FirstOrDefault((Assembly assembly) => string.Equals(assembly.GetName().Name, "Next", StringComparison.OrdinalIgnoreCase));
			if (nextAssembly == null)
			{
				return false;
			}
			Type analysisType = nextAssembly.GetType("SkySwordKill.Next.DialogSystem.DialogAnalysis");
			Type eventDataType = nextAssembly.GetType("SkySwordKill.Next.DialogSystem.DialogEventData");
			Type envType = nextAssembly.GetType("SkySwordKill.Next.DialogSystem.DialogEnvironment");
			if (analysisType == null || eventDataType == null || envType == null)
			{
				logger?.LogWarning((object)"[NativeDialogPresenter] Next 已加载，但未找到 DialogAnalysis/DialogEventData/DialogEnvironment。");
				return false;
			}

			EnsureNextDialogCommandsRegistered(analysisType, logger);

			object eventData = BuildNextDialogEventData(eventDataType, npcInfo, text);
			object env = BuildNextDialogEnvironment(envType, npcInfo);
			MethodInfo startMethod = analysisType.GetMethods(BindingFlags.Public | BindingFlags.Static)
				.FirstOrDefault((MethodInfo method) =>
					method.Name == "StartDialogEvent"
					&& method.GetParameters().Length == 2
					&& method.GetParameters()[0].ParameterType.FullName == "SkySwordKill.Next.DialogSystem.DialogEventData");
			if (startMethod == null)
			{
				logger?.LogWarning((object)"[NativeDialogPresenter] 未找到 Next StartDialogEvent(DialogEventData, DialogEnvironment)。");
				return false;
			}

			TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
			Action onComplete = null;
			EventInfo completeEvent = analysisType.GetEvent("OnDialogComplete", BindingFlags.Public | BindingFlags.Static);
			if (completeEvent != null)
			{
				onComplete = delegate
				{
					try
					{
						completeEvent.RemoveEventHandler(null, onComplete);
					}
					catch
					{
					}
					tcs.TrySetResult(true);
				};
				completeEvent.AddEventHandler(null, onComplete);
			}

			try
			{
				startMethod.Invoke(null, new[] { eventData, env });
				logger?.LogInfo((object)"[NativeDialogPresenter] 已通过 Next 临时剧情事件展示 NPC 回复。");
				if (completeEvent == null)
				{
					tcs.TrySetResult(true);
				}
				task = tcs.Task;
				return true;
			}
			catch
			{
				if (completeEvent != null && onComplete != null)
				{
					completeEvent.RemoveEventHandler(null, onComplete);
				}
				throw;
			}
		}
		catch (Exception ex)
		{
			logger?.LogWarning((object)("[NativeDialogPresenter] Next 对话展示失败，改用 SayDialog 兜底: " + GetExceptionMessage(ex)));
			task = null;
			return false;
		}
	}

	private static void EnsureNextDialogCommandsRegistered(Type analysisType, ManualLogSource logger)
	{
		if (nextInitAttempted)
		{
			return;
		}
		nextInitAttempted = true;
		try
		{
			analysisType.GetMethod("Init", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, Array.Empty<object>());
		}
		catch (Exception ex)
		{
			logger?.LogWarning((object)("[NativeDialogPresenter] Next DialogAnalysis.Init 调用失败，继续尝试展示: " + GetExceptionMessage(ex)));
		}
	}

	private static object BuildNextDialogEventData(Type eventDataType, NPCInfo npcInfo, string text)
	{
		object eventData = Activator.CreateInstance(eventDataType);
		int npcId = npcInfo?.NpcID ?? 0;
		string speaker = NormalizeNextToken(npcInfo?.Name, "NPC");

		SetProperty(eventData, "ID", "mcs_ai_chat_" + DateTime.UtcNow.Ticks);
		SetProperty(eventData, "Character", new Dictionary<string, int>
		{
			[speaker] = npcId
		});
		SetProperty(eventData, "Dialog", new[]
		{
			BuildNextSayLine(speaker, text)
		});
		SetProperty(eventData, "Option", Array.Empty<string>());
		return eventData;
	}

	private static object BuildNextDialogEnvironment(Type envType, NPCInfo npcInfo)
	{
		object env = Activator.CreateInstance(envType);
		int npcId = npcInfo?.NpcID ?? 0;
		string npcName = npcInfo?.Name ?? "NPC";

		TrySetField(env, "roleID", npcId);
		TrySetField(env, "roleBindID", npcId);
		TrySetField(env, "roleName", npcName);
		TrySetField(env, "tmpArgs", new Dictionary<string, int>());
		TrySetField(env, "customData", new Dictionary<string, object>());
		return env;
	}

	private static bool TryShowWithSayDialog(int npcId, string text, ManualLogSource logger, out Task<bool> task)
	{
		task = null;
		try
		{
			SayDialog sayDialog = SayDialog.GetSayDialog();
			if (sayDialog == null)
			{
				return false;
			}
			TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
			sayDialog.SetActive(state: true);
			sayDialog.SetCharacter(null, Math.Max(0, npcId));
			sayDialog.SetCharacterImage(null, Math.Max(0, npcId));
			sayDialog.FadeWhenDone = false;
			sayDialog.Say(text ?? string.Empty, clearPrevious: true, waitForInput: true, fadeWhenDone: false, stopVoiceover: true, waitForVO: false, null, delegate
			{
				tcs.TrySetResult(true);
			});
			logger?.LogInfo((object)"[NativeDialogPresenter] 已通过 Fungus SayDialog 展示 NPC 回复。");
			task = tcs.Task;
			return true;
		}
		catch (Exception ex)
		{
			logger?.LogWarning((object)("[NativeDialogPresenter] SayDialog 展示失败: " + GetExceptionMessage(ex)));
			return false;
		}
	}

	private static string BuildNextSayLine(string speaker, string text)
	{
		return NormalizeNextToken(speaker, "NPC") + "#" + NormalizeNextToken(text, string.Empty);
	}

	private static string NormalizeNextToken(string value, string fallback)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			value = fallback;
		}
		value = value ?? string.Empty;
		value = value.Replace('#', ' ').Replace('*', ' ').Replace('\r', ' ').Replace('\n', ' ').Trim();
		while (value.Contains("  "))
		{
			value = value.Replace("  ", " ");
		}
		return string.IsNullOrWhiteSpace(value) ? fallback : value;
	}

	private static void SetProperty(object target, string name, object value)
	{
		PropertyInfo property = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
		if (property == null)
		{
			throw new MissingMemberException(target.GetType().FullName, name);
		}
		property.SetValue(target, value, null);
	}

	private static void TrySetField(object target, string name, object value)
	{
		FieldInfo field = target.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
		if (field != null)
		{
			field.SetValue(target, value);
		}
	}

	private static string GetExceptionMessage(Exception ex)
	{
		if (ex is TargetInvocationException tie && tie.InnerException != null)
		{
			return tie.InnerException.Message;
		}
		return ex.Message;
	}

	private static string BuildNextSayLineForTest(string speaker, string text)
	{
		return BuildNextSayLine(speaker, text);
	}
}
