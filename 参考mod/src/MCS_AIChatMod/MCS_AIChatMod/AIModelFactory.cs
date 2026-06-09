using System;

namespace MCS_AIChatMod;

public static class AIModelFactory
{
	public static BaseAIClient CreateFromConfig()
	{
		BaseAIClient client = new BaseAIClient();
		string endpoint = ConfigManager.apiEndpointConfig.Value;
		string providerName = ConfigManager.selectedProviderConfig.Value;
		string configuredModel = ConfigManager.modelNameConfig.Value;
		string model = ConfigManager.ResolveChatModelName(providerName, configuredModel);
		string compatibilityProfile = (string.Equals(providerName, "自定义 (Custom)", StringComparison.Ordinal) ? ConfigManager.NormalizeCompatibilityProfileValue(ConfigManager.customCompatibilityProfileConfig?.Value) : "Auto");
		string apiKey = ConfigManager.GetCurrentApiKey();
		if (string.IsNullOrEmpty(endpoint))
		{
			AIChatManager.logger.LogError((object)"[AIModelFactory] Endpoint 为空，无法创建客户端！");
			return null;
		}
		if (!string.Equals(model, configuredModel, StringComparison.Ordinal))
		{
			if (string.IsNullOrEmpty(configuredModel))
			{
				AIChatManager.logger.LogInfo((object)("[AIModelFactory] 未配置模型，使用默认聊天模型: " + model));
			}
			else if (!string.IsNullOrEmpty(model))
			{
				AIChatManager.logger.LogWarning((object)("[AIModelFactory] 配置模型 '" + configuredModel + "' 不适用于聊天补全，已回退到 '" + model + "'"));
			}
			if (!string.IsNullOrEmpty(model))
			{
				ConfigManager.modelNameConfig.Value = model;
				ConfigManager.SetSavedModelName(providerName, model);
			}
		}
		client.SetApiEndpoint(endpoint);
		client.SetProviderHint(providerName);
		client.SetCustomCompatibilityProfile(compatibilityProfile);
		if (!string.IsNullOrEmpty(model))
		{
			client.SetDefaultModel(model);
		}
		if (!string.IsNullOrEmpty(apiKey))
		{
			client.SetAuthToken(apiKey);
		}
		AIChatManager.logger.LogInfo((object)("[AIModelFactory] 客户端已创建 → Endpoint: " + endpoint + ", Model: " + model + ", Compatibility: " + compatibilityProfile));
		return client;
	}
}
