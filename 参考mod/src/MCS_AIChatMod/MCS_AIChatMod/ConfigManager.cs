using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace MCS_AIChatMod;

public static class ConfigManager
{
	public static ConfigEntry<string> selectedProviderConfig;

	public static ConfigEntry<string> apiEndpointConfig;

	public static ConfigEntry<string> modelNameConfig;

	public static ConfigEntry<string> customCompatibilityProfileConfig;

	public static ConfigEntry<string> npcPromptConfig;

	public static ConfigEntry<float> chatOpacity;

	public static ConfigEntry<int> recentDialogCount;

	public static ConfigEntry<int> longTermSummaryCount;

	public static ConfigEntry<int> longTermMaxCharsPerEntry;

	public static ConfigEntry<bool> includeNpcInventoryInPrompt;

	public static ConfigEntry<bool> includeNpcSkillsInPrompt;

	public static ConfigEntry<bool> questVerboseDeliveryDebug;

	public static ConfigEntry<bool> experimentalMainlineGenerationEnabled;

	private static Dictionary<string, ConfigEntry<string>> _providerApiKeys = new Dictionary<string, ConfigEntry<string>>();

	private static Dictionary<string, ConfigEntry<string>> _providerModelNames = new Dictionary<string, ConfigEntry<string>>();

	private static ConfigFile _configFile;

	private static FileSystemWatcher _promptFileWatcher;

	private static readonly object _promptReloadLock = new object();

	private static DateTime _lastPromptReloadUtc = DateTime.MinValue;

	private static readonly string[] _nonChatModelKeywords = new string[26]
	{
		"asr", "audio", "embed", "embedding", "tts", "speech", "voice", "whisper", "transcri", "translat",
		"dall-e", "image", "vision", "moderation", "rerank", "search", "similarity", "edit", "insert", "davinci",
		"babbage", "ada", "curie", "code-", "realtime", "canary"
	};

	public static readonly List<ProviderPreset> Providers = new List<ProviderPreset>
	{
		new ProviderPreset
		{
			DisplayName = "DeepSeek",
			ChatEndpoint = "https://api.deepseek.com/chat/completions",
			ModelsEndpoint = "https://api.deepseek.com/models",
			DefaultModel = "deepseek-chat"
		},
		new ProviderPreset
		{
			DisplayName = "通义千问 (Qwen)",
			ChatEndpoint = "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions",
			ModelsEndpoint = "https://dashscope.aliyuncs.com/compatible-mode/v1/models",
			DefaultModel = "qwen-max"
		},
		new ProviderPreset
		{
			DisplayName = "智谱AI (GLM)",
			ChatEndpoint = "https://open.bigmodel.cn/api/paas/v4/chat/completions",
			ModelsEndpoint = "https://open.bigmodel.cn/api/paas/v4/models",
			DefaultModel = "glm-4-flash"
		},
		new ProviderPreset
		{
			DisplayName = "月之暗面 (Kimi)",
			ChatEndpoint = "https://api.moonshot.cn/v1/chat/completions",
			ModelsEndpoint = "https://api.moonshot.cn/v1/models",
			DefaultModel = "moonshot-v1-8k"
		},
		new ProviderPreset
		{
			DisplayName = "百川智能",
			ChatEndpoint = "https://api.baichuan-ai.com/v1/chat/completions",
			ModelsEndpoint = "https://api.baichuan-ai.com/v1/models",
			DefaultModel = "Baichuan4"
		},
		new ProviderPreset
		{
			DisplayName = "MiniMax",
			ChatEndpoint = "https://api.minimaxi.com/v1/chat/completions",
			ModelsEndpoint = "https://api.minimaxi.com/v1/models",
			DefaultModel = "MiniMax-M2.5"
		},
		new ProviderPreset
		{
			DisplayName = "零一万物 (Yi)",
			ChatEndpoint = "https://api.lingyiwanwu.com/v1/chat/completions",
			ModelsEndpoint = "https://api.lingyiwanwu.com/v1/models",
			DefaultModel = "yi-large"
		},
		new ProviderPreset
		{
			DisplayName = "讯飞星火",
			ChatEndpoint = "https://spark-api-open.xf-yun.com/v1/chat/completions",
			ModelsEndpoint = "https://spark-api-open.xf-yun.com/v1/models",
			DefaultModel = "generalv3.5"
		},
		new ProviderPreset
		{
			DisplayName = "豆包 (字节)",
			ChatEndpoint = "https://ark.cn-beijing.volces.com/api/v3/chat/completions",
			ModelsEndpoint = "https://ark.cn-beijing.volces.com/api/v3/models",
			DefaultModel = "doubao-pro-4k"
		},
		new ProviderPreset
		{
			DisplayName = "OpenAI",
			ChatEndpoint = "https://api.openai.com/v1/chat/completions",
			ModelsEndpoint = "https://api.openai.com/v1/models",
			DefaultModel = "gpt-4o"
		},
		new ProviderPreset
		{
			DisplayName = "Google Gemini",
			ChatEndpoint = "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions",
			ModelsEndpoint = "https://generativelanguage.googleapis.com/v1beta/openai/models",
			DefaultModel = "gemini-2.0-flash"
		},
		new ProviderPreset
		{
			DisplayName = "Anthropic Claude",
			ChatEndpoint = "https://api.anthropic.com/v1/messages",
			ModelsEndpoint = "https://api.anthropic.com/v1/models",
			DefaultModel = "claude-sonnet-4-20250514"
		},
		new ProviderPreset
		{
			DisplayName = "Groq",
			ChatEndpoint = "https://api.groq.com/openai/v1/chat/completions",
			ModelsEndpoint = "https://api.groq.com/openai/v1/models",
			DefaultModel = "llama-3.3-70b-versatile"
		},
		new ProviderPreset
		{
			DisplayName = "Mistral AI",
			ChatEndpoint = "https://api.mistral.ai/v1/chat/completions",
			ModelsEndpoint = "https://api.mistral.ai/v1/models",
			DefaultModel = "mistral-large-latest"
		},
		new ProviderPreset
		{
			DisplayName = "Together AI",
			ChatEndpoint = "https://api.together.xyz/v1/chat/completions",
			ModelsEndpoint = "https://api.together.xyz/v1/models",
			DefaultModel = "meta-llama/Llama-3-70b-chat-hf"
		},
		new ProviderPreset
		{
			DisplayName = "Cohere",
			ChatEndpoint = "https://api.cohere.com/v2/chat",
			ModelsEndpoint = "https://api.cohere.com/v1/models",
			DefaultModel = "command-r-plus"
		},
		new ProviderPreset
		{
			DisplayName = "自定义 (Custom)",
			ChatEndpoint = "",
			ModelsEndpoint = "",
			DefaultModel = ""
		}
	};

	public static readonly List<CompatibilityProfileOption> CustomCompatibilityProfiles = new List<CompatibilityProfileOption>
	{
		new CompatibilityProfileOption
		{
			Value = "Auto",
			DisplayName = "Auto（自动判断）"
		},
		new CompatibilityProfileOption
		{
			Value = "OpenAiStandard",
			DisplayName = "OpenAI（原生）"
		},
		new CompatibilityProfileOption
		{
			Value = "GeminiOpenAi",
			DisplayName = "Gemini（OpenAI兼容）"
		},
		new CompatibilityProfileOption
		{
			Value = "GroqOpenAi",
			DisplayName = "Groq（OpenAI兼容）"
		},
		new CompatibilityProfileOption
		{
			Value = "QwenCompatible",
			DisplayName = "Qwen（兼容模式）"
		},
		new CompatibilityProfileOption
		{
			Value = "DeepSeekStandard",
			DisplayName = "DeepSeek（标准）"
		},
		new CompatibilityProfileOption
		{
			Value = "DeepSeekReasoning",
			DisplayName = "DeepSeek（Reasoning）"
		},
		new CompatibilityProfileOption
		{
			Value = "AnthropicNative",
			DisplayName = "Anthropic（原生）"
		},
		new CompatibilityProfileOption
		{
			Value = "CohereNative",
			DisplayName = "Cohere（原生）"
		},
		new CompatibilityProfileOption
		{
			Value = "GenericOpenAiCompatible",
			DisplayName = "Generic-safe（兼容）"
		}
	};

	public static string pluginDirectory;

	public static string defaultPromptFilePath;

	public static string userPromptFilePath;

	public static void Initialize(ConfigFile config)
	{
		_configFile = config;
		_providerApiKeys.Clear();
		_providerModelNames.Clear();
		selectedProviderConfig = config.Bind<string>("Provider", "SelectedProvider", "DeepSeek", "当前选中的 AI 服务商名称。");
		apiEndpointConfig = config.Bind<string>("Provider", "ApiEndpoint", "https://api.deepseek.com/chat/completions", "当前服务商的 Chat Completions Endpoint。");
		modelNameConfig = config.Bind<string>("Provider", "ModelName", "deepseek-chat", "当前使用的模型名称。");
		customCompatibilityProfileConfig = config.Bind<string>("Provider", "CustomCompatibilityProfile", "Auto", "自定义源兼容档位。Auto 为自动判断，其余值可强制指定传输与兼容规则。");
		npcPromptConfig = config.Bind<string>("AI", "Prompt", "", "NPC 世界观设定提示词。");
		foreach (ProviderPreset provider in Providers)
		{
			string safeKey = BuildProviderConfigSuffix(provider.DisplayName);
			ConfigEntry<string> keyEntry = config.Bind<string>("ApiKeys", "Key_" + safeKey, "", provider.DisplayName + " 的 API Key");
			_providerApiKeys[provider.DisplayName] = keyEntry;
			ConfigEntry<string> modelEntry = config.Bind<string>("ProviderModels", "Model_" + safeKey, provider.DefaultModel ?? "", provider.DisplayName + " 最近一次使用的模型名称");
			_providerModelNames[provider.DisplayName] = modelEntry;
		}
		MigrateCurrentProviderModelSetting();
		modelNameConfig.Value = GetSavedModelName(selectedProviderConfig.Value);
		chatOpacity = config.Bind<float>("Mod", "ChatOpacity", 0.85f, "对话框背景透明度 (0-1)。");
		recentDialogCount = config.Bind<int>("Mod", "RecentDialogCount", 12, "构建上下文时注入的最近对话条数。建议 6-20。");
		longTermSummaryCount = config.Bind<int>("Mod", "LongTermSummaryCount", 8, "构建上下文时注入的长期摘要条数。建议 4-20。");
		longTermMaxCharsPerEntry = config.Bind<int>("Mod", "LongTermMaxCharsPerEntry", 60, "长期摘要每条最大字符数。建议 40-120。");
		includeNpcInventoryInPrompt = config.Bind<bool>("Mod", "IncludeNpcInventoryInPrompt", false, "是否把NPC背包物品注入Prompt，开启会增加Token消耗。");
		includeNpcSkillsInPrompt = config.Bind<bool>("Mod", "IncludeNpcSkillsInPrompt", false, "是否把NPC功法/神通注入Prompt，开启会增加Token消耗。");
		questVerboseDeliveryDebug = config.Bind<bool>("Mod", "QuestVerboseDeliveryDebug", false, "是否输出 CollectItem / 传音交付链路的详细调试日志。默认关闭。");
		experimentalMainlineGenerationEnabled = config.Bind<bool>("Mod", "ExperimentalMainlineGenerationEnabled", false, "是否启用实验性主线推演。该功能处于极早期开发阶段，可能造成坏档或死档。");
		InitializePrompt();
		AIChatManager.logger.LogInfo((object)("[ConfigManager] 当前服务商: " + selectedProviderConfig.Value + ", 模型: " + modelNameConfig.Value));
	}

	public static void SaveConfig()
	{
		try
		{
			ConfigFile configFile = _configFile;
			if (configFile != null)
			{
				configFile.Save();
			}
			AIChatManager.logger.LogInfo((object)"[ConfigManager] Config saved to disk.");
		}
		catch (Exception ex)
		{
			AIChatManager.logger.LogError((object)("[ConfigManager] Failed to save config: " + ex.Message));
			throw;
		}
	}

	public static string GetUserPromptFilePath()
	{
		return userPromptFilePath;
	}

	public static void OpenUserPromptFile()
	{
		EnsureUserPromptFileExists();
		Process.Start(new ProcessStartInfo
		{
			FileName = userPromptFilePath,
			UseShellExecute = true
		});
		ManualLogSource logger = AIChatManager.logger;
		if (logger != null)
		{
			logger.LogInfo((object)("[ConfigManager] 已打开世界观 prompt 文件: " + userPromptFilePath));
		}
	}

	public static string GetApiKey(string providerDisplayName)
	{
		if (_providerApiKeys.ContainsKey(providerDisplayName))
		{
			return _providerApiKeys[providerDisplayName].Value;
		}
		return "";
	}

	public static void SetApiKey(string providerDisplayName, string key)
	{
		if (_providerApiKeys.ContainsKey(providerDisplayName))
		{
			_providerApiKeys[providerDisplayName].Value = key;
		}
	}

	public static string GetSavedModelName(string providerDisplayName)
	{
		if (_providerModelNames.TryGetValue(providerDisplayName, out var entry))
		{
			string configured = entry.Value?.Trim();
			if (!string.IsNullOrEmpty(configured))
			{
				return configured;
			}
		}
		return GetProviderPreset(providerDisplayName)?.DefaultModel?.Trim() ?? string.Empty;
	}

	public static void SetSavedModelName(string providerDisplayName, string modelName)
	{
		if (_providerModelNames.TryGetValue(providerDisplayName, out var entry))
		{
			entry.Value = modelName?.Trim() ?? string.Empty;
		}
	}

	public static string GetCurrentApiKey()
	{
		return GetApiKey(selectedProviderConfig.Value);
	}

	public static void ApplyProviderPreset(string providerDisplayName)
	{
		selectedProviderConfig.Value = providerDisplayName;
		ProviderPreset preset = GetProviderPreset(providerDisplayName);
		if (preset != null)
		{
			if (!string.IsNullOrEmpty(preset.ChatEndpoint))
			{
				apiEndpointConfig.Value = preset.ChatEndpoint;
			}
			modelNameConfig.Value = GetSavedModelName(providerDisplayName);
			AIChatManager.logger.LogInfo((object)("[ConfigManager] 已切换到: " + providerDisplayName));
		}
	}

	public static ProviderPreset GetProviderPreset(string providerDisplayName)
	{
		return Providers.Find((ProviderPreset p) => p.DisplayName == providerDisplayName);
	}

	public static bool IsLikelyNonChatModel(string modelName)
	{
		if (string.IsNullOrWhiteSpace(modelName))
		{
			return false;
		}
		string normalized = modelName.Trim().ToLowerInvariant();
		string[] nonChatModelKeywords = _nonChatModelKeywords;
		foreach (string keyword in nonChatModelKeywords)
		{
			if (normalized.Contains(keyword))
			{
				return true;
			}
		}
		return false;
	}

	public static string ResolveChatModelName(string providerDisplayName, string configuredModel)
	{
		string normalizedModel = NormalizeConfiguredChatModel(providerDisplayName, apiEndpointConfig?.Value, (!string.Equals(providerDisplayName, "自定义 (Custom)", StringComparison.Ordinal)) ? "Auto" : customCompatibilityProfileConfig?.Value, configuredModel);
		if (!string.IsNullOrEmpty(normalizedModel) && !IsLikelyNonChatModel(normalizedModel))
		{
			return normalizedModel;
		}
		ProviderPreset preset = GetProviderPreset(providerDisplayName);
		if (preset != null && !string.IsNullOrWhiteSpace(preset.DefaultModel) && !IsLikelyNonChatModel(preset.DefaultModel))
		{
			return preset.DefaultModel.Trim();
		}
		return normalizedModel;
	}

	public static string GetCompatibilityProfileDisplayName(string value)
	{
		string normalized = (string.IsNullOrWhiteSpace(value) ? "Auto" : value.Trim());
		CompatibilityProfileOption option = CustomCompatibilityProfiles.Find((CompatibilityProfileOption p) => string.Equals(p.Value, normalized, StringComparison.OrdinalIgnoreCase));
		return (option != null) ? option.DisplayName : normalized;
	}

	public static string NormalizeCompatibilityProfileValue(string value)
	{
		string normalized = (string.IsNullOrWhiteSpace(value) ? "Auto" : value.Trim());
		CompatibilityProfileOption option = CustomCompatibilityProfiles.Find((CompatibilityProfileOption p) => string.Equals(p.Value, normalized, StringComparison.OrdinalIgnoreCase));
		return (option != null) ? option.Value : "Auto";
	}

	private static string NormalizeConfiguredChatModel(string providerDisplayName, string endpoint, string customCompatibilityProfile, string configuredModel)
	{
		return BaseAIClient.NormalizeConfiguredModelNameForTest(providerDisplayName, endpoint, customCompatibilityProfile, configuredModel);
	}

	private static string BuildProviderConfigSuffix(string providerDisplayName)
	{
		return (providerDisplayName ?? string.Empty).Replace(" ", "_").Replace("(", string.Empty).Replace(")", string.Empty)
			.Replace("/", "_")
			.Replace("\\", "_");
	}

	private static void MigrateCurrentProviderModelSetting()
	{
		string providerName = selectedProviderConfig?.Value ?? string.Empty;
		if (string.IsNullOrEmpty(providerName) || !_providerModelNames.TryGetValue(providerName, out var entry))
		{
			return;
		}
		string currentGlobalModel = modelNameConfig?.Value?.Trim() ?? string.Empty;
		if (!string.IsNullOrEmpty(currentGlobalModel))
		{
			string currentProviderModel = entry.Value?.Trim() ?? string.Empty;
			string defaultModel = GetProviderPreset(providerName)?.DefaultModel?.Trim() ?? string.Empty;
			if (string.IsNullOrEmpty(currentProviderModel) || string.Equals(currentProviderModel, defaultModel, StringComparison.Ordinal))
			{
				entry.Value = currentGlobalModel;
			}
		}
	}

	public static bool IsConfigReady()
	{
		return !string.IsNullOrEmpty(GetCurrentApiKey()) && !string.IsNullOrEmpty(apiEndpointConfig.Value);
	}

	private static void InitializePrompt()
	{
		string pluginPath = Assembly.GetExecutingAssembly().Location;
		pluginDirectory = Path.GetDirectoryName(pluginPath);
		defaultPromptFilePath = Path.Combine(pluginDirectory, "prompt.txt");
		string promptDirectory = Path.Combine(Application.persistentDataPath, "MCS_AIChatMod");
		Directory.CreateDirectory(promptDirectory);
		userPromptFilePath = Path.Combine(promptDirectory, "prompt.txt");
		EnsureUserPromptFileExists();
		ReloadPromptFromDisk("startup", persistToConfig: false);
		StartPromptFileWatcher();
	}

	private static void EnsureUserPromptFileExists()
	{
		if (!File.Exists(userPromptFilePath))
		{
			string initialPrompt = "This is a default prompt from file.";
			if (File.Exists(defaultPromptFilePath))
			{
				initialPrompt = TextEncodingUtil.ReadAllTextWithFallback(defaultPromptFilePath);
			}
			TextEncodingUtil.WriteAllTextUtf8(userPromptFilePath, initialPrompt);
			AIChatManager.logger.LogInfo((object)("Created user prompt.txt from bundled default: " + userPromptFilePath));
		}
	}

	private static void ReloadPromptFromDisk(string reason, bool persistToConfig)
	{
		try
		{
			string promptText = TextEncodingUtil.ReadAllTextWithFallback(userPromptFilePath);
			ApplyPromptContent(promptText, reason, persistToConfig);
		}
		catch (Exception ex)
		{
			AIChatManager.logger.LogError((object)("Failed to read user prompt.txt: " + ex.Message));
			string fallbackPrompt = (File.Exists(defaultPromptFilePath) ? TextEncodingUtil.ReadAllTextWithFallback(defaultPromptFilePath) : "Fallback default prompt");
			ApplyPromptContent(fallbackPrompt, reason + "_fallback", persistToConfig);
		}
	}

	private static void ApplyPromptContent(string promptText, string reason, bool persistToConfig)
	{
		string resolvedPrompt = (string.IsNullOrEmpty(promptText) ? "Fallback default prompt" : promptText);
		bool changed = !string.Equals(npcPromptConfig.Value, resolvedPrompt, StringComparison.Ordinal);
		npcPromptConfig.Value = resolvedPrompt;
		if (changed)
		{
			AIChatManager.RefreshWorldPrompt(resolvedPrompt);
			ManualLogSource logger = AIChatManager.logger;
			if (logger != null)
			{
				logger.LogInfo((object)("[ConfigManager] Prompt reloaded (" + reason + "): " + userPromptFilePath));
			}
		}
		else
		{
			ManualLogSource logger2 = AIChatManager.logger;
			if (logger2 != null)
			{
				logger2.LogInfo((object)("[ConfigManager] Prompt reload skipped because content is unchanged (" + reason + ")."));
			}
		}
		if (!persistToConfig)
		{
			return;
		}
		try
		{
			SaveConfig();
		}
		catch (Exception arg)
		{
			ManualLogSource logger3 = AIChatManager.logger;
			if (logger3 != null)
			{
				logger3.LogError((object)$"[ConfigManager] 保存热更新后的 Prompt 配置失败: {arg}");
			}
		}
	}

	private static void StartPromptFileWatcher()
	{
		try
		{
			if (_promptFileWatcher != null)
			{
				_promptFileWatcher.EnableRaisingEvents = false;
				_promptFileWatcher.Dispose();
			}
			string directory = Path.GetDirectoryName(userPromptFilePath);
			string fileName = Path.GetFileName(userPromptFilePath);
			if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
			{
				return;
			}
			_promptFileWatcher = new FileSystemWatcher(directory, fileName)
			{
				NotifyFilter = (NotifyFilters.CreationTime | NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size),
				IncludeSubdirectories = false,
				EnableRaisingEvents = true
			};
			_promptFileWatcher.Changed += delegate
			{
				SchedulePromptReload("watch_changed");
			};
			_promptFileWatcher.Created += delegate
			{
				SchedulePromptReload("watch_created");
			};
			_promptFileWatcher.Renamed += delegate
			{
				SchedulePromptReload("watch_renamed");
			};
			_promptFileWatcher.Error += delegate(object _, ErrorEventArgs ex)
			{
				ManualLogSource logger3 = AIChatManager.logger;
				if (logger3 != null)
				{
					logger3.LogWarning((object)$"[ConfigManager] Prompt watcher error: {ex.GetException()}");
				}
			};
			ManualLogSource logger = AIChatManager.logger;
			if (logger != null)
			{
				logger.LogInfo((object)("[ConfigManager] Prompt watcher started: " + userPromptFilePath));
			}
		}
		catch (Exception arg)
		{
			ManualLogSource logger2 = AIChatManager.logger;
			if (logger2 != null)
			{
				logger2.LogError((object)$"[ConfigManager] Failed to start prompt watcher: {arg}");
			}
		}
	}

	private static void SchedulePromptReload(string reason)
	{
		ThreadPool.QueueUserWorkItem(delegate
		{
			try
			{
				Thread.Sleep(120);
				lock (_promptReloadLock)
				{
					DateTime utcNow = DateTime.UtcNow;
					if ((utcNow - _lastPromptReloadUtc).TotalMilliseconds < 150.0)
					{
						return;
					}
					_lastPromptReloadUtc = utcNow;
				}
				ReloadPromptFromDisk(reason, persistToConfig: true);
			}
			catch (Exception arg)
			{
				ManualLogSource logger = AIChatManager.logger;
				if (logger != null)
				{
					logger.LogError((object)$"[ConfigManager] Prompt reload worker failed: {arg}");
				}
			}
		});
	}
}
