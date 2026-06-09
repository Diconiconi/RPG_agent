using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using LlmTornado;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.ChatFunctions;
using LlmTornado.Code;
using LlmTornado.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MCS_AIChatMod;

public class BaseAIClient
{
	private static readonly string[] KnownChatEndpointSuffixes = new string[6] { "/chat/completions", "/text/chatcompletion_v2", "/messages", "/v2/chat", "/chat", "/completions" };

	protected string _apiEndpoint;

	protected string _defaultModel;

	protected string _authToken;

	protected string _providerHint;

	protected string _customCompatibilityProfileName = "Auto";

	protected readonly Dictionary<string, string> _headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

	public virtual async Task<OpenAIResponse> GenerateResponseAsync(ChatRequest request)
	{
		if (request == null)
		{
			throw new ArgumentNullException("request");
		}
		ChatRequest effectiveRequest = CloneRequest(request);
		if (string.IsNullOrEmpty(effectiveRequest.Model))
		{
			effectiveRequest.Model = _defaultModel;
		}
		ResolvedTransportPlan plan = ResolveTransportPlan(_providerHint, _apiEndpoint, effectiveRequest.Model, _customCompatibilityProfileName);
		ChatRequest normalizedRequest = NormalizeRequestForCompatibilityCore(effectiveRequest, plan);
		if (string.IsNullOrWhiteSpace(normalizedRequest.Model))
		{
			throw new InvalidOperationException("未指定模型名称。");
		}
		if (string.IsNullOrWhiteSpace(_apiEndpoint))
		{
			throw new InvalidOperationException("未指定 API Endpoint。");
		}
		LogCompatibilityAdjustments(effectiveRequest, normalizedRequest, plan);
		if (plan.TransportKind == CompatibilityTransportKind.OpenAiCompatibleHttp)
		{
			return await GenerateOpenAiCompatibleResponseAsync(normalizedRequest, plan);
		}
		Uri baseUri = ResolveApiBaseUri(_apiEndpoint);
		LlmTornado.Chat.ChatRequest tornadoRequest = BuildTornadoRequestCore(normalizedRequest, plan.NativeProvider);
		TornadoApi api = new TornadoApi(baseUri, _authToken ?? string.Empty, plan.NativeProvider);
		ChatResult chatResult;
		try
		{
			chatResult = await api.Chat.CreateChatCompletion(tornadoRequest);
		}
		catch (Exception ex)
		{
			AIChatManager.logger.LogError((object)$"[BaseAIClient] 原生请求失败: profile={plan.Profile}, provider={plan.NativeProvider}, endpoint={_apiEndpoint}, base={baseUri}, model={normalizedRequest.Model}, error={ex.Message}");
			throw;
		}
		if (chatResult == null)
		{
			throw new InvalidOperationException("LlmTornado 未返回有效 ChatResult。");
		}
		return MapChatResultToOpenAiResponseCore(chatResult, normalizedRequest.Model);
	}

	public async Task<List<string>> FetchModelsAsync(string modelsEndpoint)
	{
		List<string> result = new List<string>();
		if (string.IsNullOrWhiteSpace(modelsEndpoint))
		{
			return result;
		}
		try
		{
			AIChatManager.logger.LogInfo((object)("[FetchModels] 正在请求: " + modelsEndpoint));
			using (HttpClient client = new HttpClient())
			{
				client.Timeout = TimeSpan.FromSeconds(30.0);
				ApplyHeaders(client.DefaultRequestHeaders);
				client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
				HttpResponseMessage response = await client.GetAsync(modelsEndpoint);
				string content = await response.Content.ReadAsStringAsync();
				AIChatManager.logger.LogInfo((object)$"[FetchModels] 状态码: {(int)response.StatusCode}");
				if (!response.IsSuccessStatusCode)
				{
					AIChatManager.logger.LogWarning((object)$"[FetchModels] 请求失败: {(int)response.StatusCode}, 内容: {SafePreview(content, 500)}");
					return result;
				}
				if (string.IsNullOrWhiteSpace(content))
				{
					AIChatManager.logger.LogWarning((object)"[FetchModels] 响应内容为空");
					return result;
				}
				JObject json = JObject.Parse(content);
				if (json["data"] is JArray data)
				{
					foreach (JToken item in data)
					{
						string id = item["id"]?.ToString();
						if (!string.IsNullOrWhiteSpace(id))
						{
							result.Add(NormalizeFetchedModelId(_providerHint, modelsEndpoint, _customCompatibilityProfileName, id));
						}
					}
				}
				else if (json["models"] is JArray models)
				{
					foreach (JToken item2 in models)
					{
						string id2 = (item2["id"] ?? item2["name"] ?? item2["model"])?.ToString();
						if (!string.IsNullOrWhiteSpace(id2))
						{
							result.Add(NormalizeFetchedModelId(_providerHint, modelsEndpoint, _customCompatibilityProfileName, id2));
						}
					}
				}
				else
				{
					AIChatManager.logger.LogWarning((object)("[FetchModels] 未找到 data/models 字段，原始响应: " + SafePreview(content, 300)));
				}
			}
			AIChatManager.logger.LogInfo((object)string.Format("[FetchModels] 获取到 {0} 个模型: {1}", result.Count, string.Join(", ", result)));
		}
		catch (Exception ex)
		{
			AIChatManager.logger.LogWarning((object)("[FetchModels] 异常: " + ex.Message));
		}
		return result;
	}

	public void SetAuthToken(string token)
	{
		_authToken = token ?? string.Empty;
		_headers["Authorization"] = "Bearer " + _authToken;
	}

	public void SetCustomHeader(string key, string value)
	{
		if (!string.IsNullOrWhiteSpace(key))
		{
			_headers[key] = value ?? string.Empty;
		}
	}

	public void SetApiEndpoint(string url)
	{
		_apiEndpoint = url;
	}

	public void SetDefaultModel(string modelName)
	{
		_defaultModel = modelName;
	}

	public void SetProviderHint(string providerDisplayName)
	{
		_providerHint = providerDisplayName;
	}

	public void SetCustomCompatibilityProfile(string profileName)
	{
		_customCompatibilityProfileName = (string.IsNullOrWhiteSpace(profileName) ? "Auto" : profileName.Trim());
	}

	internal static string ResolveProviderNameForTest(string endpoint)
	{
		return ResolveProviderForConfiguration(null, endpoint).ToString();
	}

	internal static string ResolveCompatibilityProfileNameForTest(string providerDisplayName, string endpoint, string modelName, string customProfileOverride)
	{
		return ResolveTransportPlan(providerDisplayName, endpoint, modelName, customProfileOverride).Profile.ToString();
	}

	internal static string ResolveTransportKindNameForTest(string providerDisplayName, string endpoint, string modelName, string customProfileOverride)
	{
		return ResolveTransportPlan(providerDisplayName, endpoint, modelName, customProfileOverride).TransportKind.ToString();
	}

	internal static ChatRequest NormalizeRequestForCompatibilityForTest(string providerDisplayName, string endpoint, string customProfileOverride, ChatRequest request)
	{
		ResolvedTransportPlan plan = ResolveTransportPlan(providerDisplayName, endpoint, request?.Model, customProfileOverride);
		return NormalizeRequestForCompatibilityCore(request, plan);
	}

	internal static LlmTornado.Chat.ChatRequest BuildTornadoRequestForTest(ChatRequest request)
	{
		return BuildTornadoRequestCore(request, LLmProviders.Custom);
	}

	internal static OpenAIResponse MapChatResultToOpenAIResponseForTest(ChatResult result, string model)
	{
		return MapChatResultToOpenAiResponseCore(result, model);
	}

	internal static void SanitizeCompatibleOpenAiPayloadForTest(JObject payload)
	{
		SanitizeCompatibleOpenAiPayload(payload);
	}

	internal static string NormalizeFetchedModelIdForTest(string modelsEndpoint, string modelId)
	{
		return NormalizeFetchedModelId(null, modelsEndpoint, "Auto", modelId);
	}

	internal static string NormalizeConfiguredModelNameForTest(string providerDisplayName, string endpoint, string customProfileOverride, string modelName)
	{
		ResolvedTransportPlan plan = ResolveTransportPlan(providerDisplayName, endpoint, modelName, customProfileOverride);
		return NormalizeModelNameForProfile(modelName, plan.Profile);
	}

	internal static string InferModelsEndpointForTest(string endpoint)
	{
		return InferModelsEndpoint(endpoint);
	}

	internal static string InferModelsEndpointFromChatEndpoint(string endpoint)
	{
		return InferModelsEndpoint(endpoint);
	}

	internal static string FormatCompatibleRequestFailureForTest(string profileName, string endpoint, string modelName, string payloadJson, Exception ex)
	{
		return FormatCompatibleRequestFailureDiagnostic(profileName, endpoint, modelName, payloadJson, ex, 600);
	}

	private static LlmTornado.Chat.ChatRequest BuildTornadoRequestCore(ChatRequest request, LLmProviders provider)
	{
		LlmTornado.Chat.ChatRequest chatRequest = new LlmTornado.Chat.ChatRequest();
		chatRequest.Model = CreateTornadoModel(request?.Model, provider);
		chatRequest.Messages = request?.Messages?.Select(MapToTornadoMessage).ToList() ?? new List<LlmTornado.Chat.ChatMessage>();
		chatRequest.Stream = request?.Stream;
		chatRequest.MaxTokens = request?.MaxTokens;
		chatRequest.Temperature = request?.Temperature;
		chatRequest.TopP = request?.TopP;
		chatRequest.Tools = request?.Tools?.Select(MapToTornadoTool).ToList();
		return chatRequest;
	}

	private static OpenAIResponse MapChatResultToOpenAiResponseCore(ChatResult result, string model)
	{
		OpenAIResponse openAIResponse = new OpenAIResponse();
		openAIResponse.Id = result?.Id;
		openAIResponse.Model = model;
		openAIResponse.Choices = result?.Choices?.Select(MapToOpenAiChoice).ToList() ?? new List<Choice>();
		openAIResponse.Usage = MapUsage(result?.Usage);
		return openAIResponse;
	}

	private static Choice MapToOpenAiChoice(ChatChoice choice)
	{
		return new Choice
		{
			Index = (choice?.Index ?? 0),
			FinishReason = choice?.FinishReason?.ToString(),
			Message = MapToOpenAiMessage(choice?.Message)
		};
	}

	private static ResponseMessage MapToOpenAiMessage(LlmTornado.Chat.ChatMessage message)
	{
		if (message == null)
		{
			return new ResponseMessage();
		}
		ResponseMessage responseMessage = new ResponseMessage();
		responseMessage.Role = MapToOpenAiRole(message.Role);
		responseMessage.Content = message.Content;
		responseMessage.ToolCalls = message.ToolCalls?.Select(MapToOpenAiToolCall).ToList();
		return responseMessage;
	}

	private static ToolCall MapToOpenAiToolCall(LlmTornado.ChatFunctions.ToolCall call)
	{
		if (call == null)
		{
			return null;
		}
		return new ToolCall
		{
			Id = call.Id,
			Type = (string.IsNullOrWhiteSpace(call.Type) ? "function" : call.Type),
			Function = new FunctionCall
			{
				Name = call.FunctionCall?.Name,
				Arguments = call.FunctionCall?.Arguments
			}
		};
	}

	private static TokenUsage MapUsage(ChatUsage usage)
	{
		if (usage == null)
		{
			return null;
		}
		int promptTokens = SumNullable(usage.PromptTokenDetails?.TextTokens, usage.PromptTokenDetails?.AudioTokens, usage.PromptTokenDetails?.ImageTokens, usage.PromptTokenDetails?.CachedTokens, usage.CacheCreationTokens, usage.CacheReadTokens);
		int completionTokens = usage.CompletionTokens;
		return new TokenUsage
		{
			PromptTokens = promptTokens,
			CompletionTokens = completionTokens,
			TotalTokens = promptTokens + completionTokens
		};
	}

	private static int SumNullable(params int?[] values)
	{
		int total = 0;
		if (values == null)
		{
			return total;
		}
		foreach (int? value in values)
		{
			total += value.GetValueOrDefault();
		}
		return total;
	}

	private static ChatModel CreateTornadoModel(string modelName, LLmProviders provider)
	{
		if (string.IsNullOrWhiteSpace(modelName))
		{
			return null;
		}
		return (provider == LLmProviders.Custom) ? new ChatModel(modelName) : new ChatModel(modelName, provider);
	}

	private static LlmTornado.Chat.ChatMessage MapToTornadoMessage(ChatMessage message)
	{
		if (message == null)
		{
			return new LlmTornado.Chat.ChatMessage();
		}
		LlmTornado.Chat.ChatMessage tornadoMessage = new LlmTornado.Chat.ChatMessage(MapRole(message.Role), message.Content)
		{
			ToolCallId = message.ToolCallId
		};
		if (message.ToolCalls != null && message.ToolCalls.Count > 0)
		{
			tornadoMessage.ToolCalls = message.ToolCalls.Select(MapToTornadoToolCall).ToList();
		}
		return tornadoMessage;
	}

	private static LlmTornado.ChatFunctions.ToolCall MapToTornadoToolCall(ToolCall call)
	{
		if (call == null)
		{
			return null;
		}
		return new LlmTornado.ChatFunctions.ToolCall
		{
			Id = call.Id,
			Type = (string.IsNullOrWhiteSpace(call.Type) ? "function" : call.Type),
			FunctionCall = new LlmTornado.ChatFunctions.FunctionCall
			{
				Name = call.Function?.Name,
				Arguments = call.Function?.Arguments
			}
		};
	}

	private static LlmTornado.Common.Tool MapToTornadoTool(Tool tool)
	{
		if (tool?.Function == null)
		{
			return null;
		}
		return new LlmTornado.Common.Tool(new ToolFunction(tool.Function.Name, tool.Function.Description ?? string.Empty, tool.Function.Parameters));
	}

	private static ChatMessageRoles MapRole(string role)
	{
		return (role ?? string.Empty).Trim().ToLowerInvariant() switch
		{
			"system" => ChatMessageRoles.System,
			"assistant" => ChatMessageRoles.Assistant,
			"tool" => ChatMessageRoles.Tool,
			_ => ChatMessageRoles.User,
		};
	}

	private static string MapToOpenAiRole(ChatMessageRoles? role)
	{
		return role switch
		{
			ChatMessageRoles.System => "system",
			ChatMessageRoles.Assistant => "assistant",
			ChatMessageRoles.Tool => "tool",
			_ => "user",
		};
	}

	private async Task<OpenAIResponse> GenerateOpenAiCompatibleResponseAsync(ChatRequest request, ResolvedTransportPlan plan)
	{
		string payloadJson = JsonConvert.SerializeObject(BuildOpenAiCompatibleRequest(request), new JsonSerializerSettings
		{
			NullValueHandling = NullValueHandling.Ignore
		});
		try
		{
			using HttpClient client = new HttpClient();
			using HttpRequestMessage httpRequest = new HttpRequestMessage(HttpMethod.Post, _apiEndpoint);
			client.Timeout = TimeSpan.FromSeconds(600.0);
			ApplyHeaders(httpRequest.Headers);
			httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
			httpRequest.Content = new StringContent(payloadJson, Encoding.UTF8, "application/json");
			HttpResponseMessage response = await client.SendAsync(httpRequest);
			string content = await response.Content.ReadAsStringAsync();
			if (!response.IsSuccessStatusCode)
			{
				throw new HttpRequestException($"[{content}] (status code: {response.StatusCode})");
			}
			OpenAIResponse parsed = JsonConvert.DeserializeObject<OpenAIResponse>(content);
			if (parsed == null)
			{
				throw new InvalidOperationException("兼容接口返回内容无法解析为 OpenAIResponse。");
			}
			if (string.IsNullOrWhiteSpace(parsed.Model))
			{
				parsed.Model = request.Model;
			}
			if (parsed.Choices == null)
			{
				parsed.Choices = new List<Choice>();
			}
			return parsed;
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			AIChatManager.logger.LogError((object)FormatCompatibleRequestFailureDiagnostic(plan.Profile.ToString(), _apiEndpoint, request.Model, payloadJson, ex2, 600));
			throw;
		}
	}

	private static OpenAIRequest BuildOpenAiCompatibleRequest(ChatRequest request)
	{
		OpenAIRequest openAIRequest = new OpenAIRequest();
		openAIRequest.Model = request?.Model;
		openAIRequest.Messages = request?.Messages?.Select(CloneMessage).ToList() ?? new List<ChatMessage>();
		openAIRequest.Stream = request?.Stream ?? false;
		openAIRequest.MaxTokens = request?.MaxTokens;
		openAIRequest.Temperature = request?.Temperature;
		openAIRequest.TopP = request?.TopP;
		openAIRequest.FrequencyPenalty = request?.FrequencyPenalty;
		openAIRequest.PresencePenalty = request?.PresencePenalty;
		openAIRequest.Tools = request?.Tools?.Select(CloneTool).ToList();
		return openAIRequest;
	}

	private static LLmProviders ResolveProviderForConfiguration(string providerDisplayName, string endpoint)
	{
		if (UsesCompatibleChatRouting(endpoint))
		{
			return LLmProviders.Custom;
		}
		switch (providerDisplayName)
		{
		case "DeepSeek":
			return LLmProviders.DeepSeek;
		case "OpenAI":
			return LLmProviders.OpenAi;
		case "Google Gemini":
			return LLmProviders.Google;
		case "Anthropic Claude":
			return LLmProviders.Anthropic;
		case "Groq":
			return LLmProviders.Groq;
		case "Mistral AI":
			return LLmProviders.Mistral;
		case "Cohere":
			return LLmProviders.Cohere;
		case "月之暗面 (Kimi)":
			return LLmProviders.MoonshotAi;
		case "通义千问 (Qwen)":
			return LLmProviders.Alibaba;
		default:
		{
			if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
			{
				string host = uri.Host.ToLowerInvariant();
				if (host.Contains("api.openai.com"))
				{
					return LLmProviders.OpenAi;
				}
				if (host.Contains("api.deepseek.com"))
				{
					return LLmProviders.DeepSeek;
				}
				if (host.Contains("openrouter.ai"))
				{
					return LLmProviders.OpenRouter;
				}
				if (host.Contains("api.anthropic.com"))
				{
					return LLmProviders.Anthropic;
				}
				if (host.Contains("api.groq.com"))
				{
					return LLmProviders.Groq;
				}
				if (host.Contains("api.mistral.ai"))
				{
					return LLmProviders.Mistral;
				}
				if (host.Contains("api.cohere.com"))
				{
					return LLmProviders.Cohere;
				}
				if (host.Contains("generativelanguage.googleapis.com"))
				{
					return LLmProviders.Google;
				}
				if (host.Contains("dashscope.aliyuncs.com"))
				{
					return LLmProviders.Alibaba;
				}
				if (host.Contains("api.moonshot.cn"))
				{
					return LLmProviders.MoonshotAi;
				}
			}
			return LLmProviders.Custom;
		}
		}
	}

	private static ResolvedTransportPlan ResolveTransportPlan(string providerDisplayName, string endpoint, string modelName, string customProfileOverride)
	{
		ProviderCompatibilityProfile profile = ResolveCompatibilityProfile(providerDisplayName, endpoint, modelName, customProfileOverride);
		ProviderCompatibilityRules rules = ProviderCompatibilityCatalog.GetRules(profile);
		return new ResolvedTransportPlan
		{
			Profile = profile,
			TransportKind = rules.TransportKind,
			NativeProvider = ResolveNativeProvider(profile, providerDisplayName, endpoint, rules.NativeProvider),
			Rules = rules
		};
	}

	private static ProviderCompatibilityProfile ResolveCompatibilityProfile(string providerDisplayName, string endpoint, string modelName, string customProfileOverride)
	{
		ProviderCompatibilityProfile manualProfile = ProviderCompatibilityCatalog.Parse(customProfileOverride);
		if (manualProfile != ProviderCompatibilityProfile.Auto)
		{
			return manualProfile;
		}
		string provider = (providerDisplayName ?? string.Empty).Trim();
		string host = "";
		string path = "";
		if (Uri.TryCreate(endpoint ?? string.Empty, UriKind.Absolute, out var uri))
		{
			host = (uri.Host ?? string.Empty).ToLowerInvariant();
			path = (uri.AbsolutePath ?? string.Empty).ToLowerInvariant();
		}
		string normalizedModel = (modelName ?? string.Empty).Trim().ToLowerInvariant();
		if (string.Equals(provider, "Anthropic Claude", StringComparison.Ordinal) || host.Contains("api.anthropic.com"))
		{
			return ProviderCompatibilityProfile.AnthropicNative;
		}
		if (string.Equals(provider, "Cohere", StringComparison.Ordinal) || host.Contains("api.cohere.com"))
		{
			return ProviderCompatibilityProfile.CohereNative;
		}
		if (string.Equals(provider, "DeepSeek", StringComparison.Ordinal) || host.Contains("api.deepseek.com"))
		{
			if (normalizedModel.Contains("reasoner") || normalizedModel.Contains("reasoning"))
			{
				return ProviderCompatibilityProfile.DeepSeekReasoning;
			}
			return ProviderCompatibilityProfile.DeepSeekStandard;
		}
		if (string.Equals(provider, "OpenAI", StringComparison.Ordinal) || host.Contains("api.openai.com"))
		{
			return ProviderCompatibilityProfile.OpenAiStandard;
		}
		if (string.Equals(provider, "Mistral AI", StringComparison.Ordinal) || host.Contains("api.mistral.ai"))
		{
			return ProviderCompatibilityProfile.OpenAiStandard;
		}
		if (string.Equals(provider, "月之暗面 (Kimi)", StringComparison.Ordinal) || host.Contains("api.moonshot.cn"))
		{
			return ProviderCompatibilityProfile.OpenAiStandard;
		}
		if (string.Equals(provider, "Google Gemini", StringComparison.Ordinal) || (host.Contains("generativelanguage.googleapis.com") && path.Contains("/openai/")))
		{
			return ProviderCompatibilityProfile.GeminiOpenAi;
		}
		if (string.Equals(provider, "Groq", StringComparison.Ordinal) || (host.Contains("api.groq.com") && path.Contains("/openai/")))
		{
			return ProviderCompatibilityProfile.GroqOpenAi;
		}
		if (string.Equals(provider, "通义千问 (Qwen)", StringComparison.Ordinal) || (host.Contains("dashscope.aliyuncs.com") && path.Contains("/compatible-mode/")))
		{
			return ProviderCompatibilityProfile.QwenCompatible;
		}
		if (string.Equals(provider, "智谱AI (GLM)", StringComparison.Ordinal) || string.Equals(provider, "百川智能", StringComparison.Ordinal) || string.Equals(provider, "零一万物 (Yi)", StringComparison.Ordinal) || string.Equals(provider, "讯飞星火", StringComparison.Ordinal) || string.Equals(provider, "豆包 (字节)", StringComparison.Ordinal) || string.Equals(provider, "Together AI", StringComparison.Ordinal) || string.Equals(provider, "MiniMax", StringComparison.Ordinal))
		{
			return ProviderCompatibilityProfile.GenericOpenAiCompatible;
		}
		if (host.Contains("open.bigmodel.cn") || host.Contains("baichuan-ai.com") || host.Contains("lingyiwanwu.com") || host.Contains("xf-yun.com") || host.Contains("volces.com") || host.Contains("together.xyz") || host.Contains("minimaxi.com") || host.Contains("minimax.chat"))
		{
			return ProviderCompatibilityProfile.GenericOpenAiCompatible;
		}
		if (path.Contains("/openai/") || path.Contains("/compatible-mode/"))
		{
			return ProviderCompatibilityProfile.GenericOpenAiCompatible;
		}
		if (UsesCompatibleChatRouting(endpoint))
		{
			return ProviderCompatibilityProfile.GenericOpenAiCompatible;
		}
		return ProviderCompatibilityProfile.GenericOpenAiCompatible;
	}

	private static LLmProviders ResolveNativeProvider(ProviderCompatibilityProfile profile, string providerDisplayName, string endpoint, LLmProviders defaultProvider)
	{
		if (profile != ProviderCompatibilityProfile.OpenAiStandard)
		{
			return defaultProvider;
		}
		string provider = (providerDisplayName ?? string.Empty).Trim();
		if (string.Equals(provider, "Mistral AI", StringComparison.Ordinal))
		{
			return LLmProviders.Mistral;
		}
		if (string.Equals(provider, "月之暗面 (Kimi)", StringComparison.Ordinal))
		{
			return LLmProviders.MoonshotAi;
		}
		if (string.Equals(provider, "OpenAI", StringComparison.Ordinal))
		{
			return LLmProviders.OpenAi;
		}
		if (Uri.TryCreate(endpoint ?? string.Empty, UriKind.Absolute, out var uri))
		{
			string host = (uri.Host ?? string.Empty).ToLowerInvariant();
			if (host.Contains("api.mistral.ai"))
			{
				return LLmProviders.Mistral;
			}
			if (host.Contains("api.moonshot.cn"))
			{
				return LLmProviders.MoonshotAi;
			}
			if (host.Contains("api.openai.com"))
			{
				return LLmProviders.OpenAi;
			}
		}
		return defaultProvider;
	}

	private static ChatRequest NormalizeRequestForCompatibilityCore(ChatRequest request, ResolvedTransportPlan plan)
	{
		ChatRequest normalized = CloneRequest(request);
		if (normalized == null)
		{
			normalized = new ChatRequest();
		}
		normalized.Model = NormalizeModelNameForProfile(normalized.Model, plan.Profile);
		ProviderCompatibilityRules rules = plan.Rules ?? ProviderCompatibilityCatalog.GetRules(plan.Profile);
		if (rules.StripFrequencyPenalty)
		{
			normalized.FrequencyPenalty = null;
		}
		if (rules.StripPresencePenalty)
		{
			normalized.PresencePenalty = null;
		}
		if (rules.DisableTools)
		{
			normalized.Tools = null;
		}
		if (rules.DisableStreamWhenToolsPresent && normalized.Stream && normalized.Tools != null && normalized.Tools.Count > 0)
		{
			normalized.Stream = false;
		}
		if (rules.NullOutZeroTemperature && normalized.Temperature.HasValue && Math.Abs(normalized.Temperature.Value) < 0.0001f)
		{
			normalized.Temperature = null;
		}
		return normalized;
	}

	private static string NormalizeModelNameForProfile(string modelName, ProviderCompatibilityProfile profile)
	{
		string normalizedModel = ((modelName == null) ? "" : modelName.Trim());
		if (string.IsNullOrEmpty(normalizedModel))
		{
			return normalizedModel;
		}
		if (profile == ProviderCompatibilityProfile.GeminiOpenAi && normalizedModel.StartsWith("models/", StringComparison.OrdinalIgnoreCase))
		{
			return normalizedModel.Substring("models/".Length);
		}
		return normalizedModel;
	}

	private static bool UsesCompatibleChatRouting(string endpoint)
	{
		if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
		{
			return false;
		}
		string path = (uri.AbsolutePath ?? string.Empty).ToLowerInvariant();
		return path.Contains("/compatible-mode/") || path.Contains("/openai/");
	}

	private static string NormalizeFetchedModelId(string providerDisplayName, string modelsEndpoint, string customProfileOverride, string modelId)
	{
		ResolvedTransportPlan plan = ResolveTransportPlan(providerDisplayName, modelsEndpoint, null, customProfileOverride);
		return NormalizeModelNameForProfile(modelId, plan.Profile);
	}

	private static string InferModelsEndpoint(string endpoint)
	{
		string trimmed = endpoint?.Trim().TrimEnd('/');
		if (string.IsNullOrWhiteSpace(trimmed))
		{
			return "";
		}
		if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && uri.Host.IndexOf("api.cohere.com", StringComparison.OrdinalIgnoreCase) >= 0 && uri.AbsolutePath.EndsWith("/v2/chat", StringComparison.OrdinalIgnoreCase))
		{
			return uri.Scheme + "://" + uri.Host + "/v1/models";
		}
		string[] knownChatEndpointSuffixes = KnownChatEndpointSuffixes;
		foreach (string suffix in knownChatEndpointSuffixes)
		{
			if (trimmed.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
			{
				return trimmed.Substring(0, trimmed.Length - suffix.Length) + "/models";
			}
		}
		return trimmed.EndsWith("/models", StringComparison.OrdinalIgnoreCase) ? trimmed : (trimmed + "/models");
	}

	private static void SanitizeCompatibleOpenAiPayload(JObject payload)
	{
		if (payload == null)
		{
			return;
		}
		payload.Remove("frequency_penalty");
		payload.Remove("presence_penalty");
		if (!(payload["tools"] is JArray tools))
		{
			return;
		}
		foreach (JObject tool in tools.OfType<JObject>())
		{
			tool.Remove("ResolvedName");
			tool.Remove("ResolvedDescription");
			tool.Remove("resolvedName");
			tool.Remove("resolvedDescription");
		}
	}

	private static Uri ResolveApiBaseUri(string endpoint)
	{
		if (string.IsNullOrWhiteSpace(endpoint))
		{
			throw new InvalidOperationException("API Endpoint 为空。");
		}
		string trimmed = endpoint.Trim().TrimEnd('/');
		string[] knownChatEndpointSuffixes = KnownChatEndpointSuffixes;
		foreach (string suffix in knownChatEndpointSuffixes)
		{
			if (trimmed.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
			{
				trimmed = trimmed.Substring(0, trimmed.Length - suffix.Length);
				break;
			}
		}
		return new Uri(trimmed, UriKind.Absolute);
	}

	private static ChatRequest CloneRequest(ChatRequest request)
	{
		if (request == null)
		{
			return null;
		}
		ChatRequest chatRequest = new ChatRequest();
		chatRequest.Model = request.Model;
		chatRequest.Stream = request.Stream;
		chatRequest.MaxTokens = request.MaxTokens;
		chatRequest.Temperature = request.Temperature;
		chatRequest.TopP = request.TopP;
		chatRequest.FrequencyPenalty = request.FrequencyPenalty;
		chatRequest.PresencePenalty = request.PresencePenalty;
		chatRequest.Messages = request.Messages?.Select(CloneMessage).ToList() ?? new List<ChatMessage>();
		chatRequest.Tools = request.Tools?.Select(CloneTool).ToList();
		return chatRequest;
	}

	private static ChatMessage CloneMessage(ChatMessage message)
	{
		if (message == null)
		{
			return null;
		}
		ChatMessage chatMessage = new ChatMessage();
		chatMessage.Role = message.Role;
		chatMessage.Content = message.Content;
		chatMessage.ToolCallId = message.ToolCallId;
		chatMessage.ToolCalls = message.ToolCalls?.Select(CloneToolCall).ToList();
		return chatMessage;
	}

	private static Tool CloneTool(Tool tool)
	{
		if (tool == null)
		{
			return null;
		}
		return new Tool
		{
			Type = tool.Type,
			Function = ((tool.Function == null) ? null : new FunctionDefinition
			{
				Name = tool.Function.Name,
				Description = tool.Function.Description,
				Parameters = tool.Function.Parameters
			})
		};
	}

	private static ToolCall CloneToolCall(ToolCall toolCall)
	{
		if (toolCall == null)
		{
			return null;
		}
		return new ToolCall
		{
			Id = toolCall.Id,
			Type = toolCall.Type,
			Function = ((toolCall.Function == null) ? null : new FunctionCall
			{
				Name = toolCall.Function.Name,
				Arguments = toolCall.Function.Arguments
			})
		};
	}

	private static void LogCompatibilityAdjustments(ChatRequest original, ChatRequest adjusted, ResolvedTransportPlan plan)
	{
		if (original != null && adjusted != null && plan != null)
		{
			List<string> adjustments = new List<string>();
			if (!string.Equals(original.Model, adjusted.Model, StringComparison.Ordinal))
			{
				adjustments.Add("model=" + original.Model + "->" + adjusted.Model);
			}
			if (original.Stream && !adjusted.Stream)
			{
				adjustments.Add("stream=true->false");
			}
			if (original.Tools != null && original.Tools.Count > 0 && (adjusted.Tools == null || adjusted.Tools.Count == 0))
			{
				adjustments.Add("tools=disabled");
			}
			if (original.FrequencyPenalty.HasValue && !adjusted.FrequencyPenalty.HasValue)
			{
				adjustments.Add("frequency_penalty=removed");
			}
			if (original.PresencePenalty.HasValue && !adjusted.PresencePenalty.HasValue)
			{
				adjustments.Add("presence_penalty=removed");
			}
			if (original.Temperature.HasValue && !adjusted.Temperature.HasValue && Math.Abs(original.Temperature.Value) < 0.0001f)
			{
				adjustments.Add("temperature=0->null");
			}
			if (adjustments.Count != 0)
			{
				AIChatManager.logger.LogInfo((object)string.Format("[BaseAIClient] 兼容档位 {0} 已调整请求: {1}", plan.Profile, string.Join(", ", adjustments)));
			}
		}
	}

	private void ApplyHeaders(HttpRequestHeaders headers)
	{
		foreach (KeyValuePair<string, string> header in _headers)
		{
			if (string.Equals(header.Key, "Authorization", StringComparison.OrdinalIgnoreCase))
			{
				string value = header.Value ?? string.Empty;
				if (value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
				{
					string token = value.Substring("Bearer ".Length);
					headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
				}
				else if (!string.IsNullOrWhiteSpace(value))
				{
					headers.TryAddWithoutValidation(header.Key, value);
				}
			}
			else
			{
				headers.TryAddWithoutValidation(header.Key, header.Value ?? string.Empty);
			}
		}
	}

	private static string SafePreview(string content, int maxChars)
	{
		if (string.IsNullOrEmpty(content))
		{
			return string.Empty;
		}
		return content.Substring(0, Math.Min(content.Length, maxChars));
	}

	private static string FormatCompatibleRequestFailureDiagnostic(string profileName, string endpoint, string modelName, string payloadJson, Exception ex, int timeoutSeconds)
	{
		string exceptionType = ex?.GetType().Name ?? "UnknownException";
		string innerType = ex?.InnerException?.GetType().Name ?? string.Empty;
		string innerMessage = ex?.InnerException?.Message ?? string.Empty;
		string cancellationHint = ((ex is TaskCanceledException || ex is OperationCanceledException) ? "timeout_or_transport_cancellation" : string.Empty);
		List<string> parts = new List<string>
		{
			"[BaseAIClient] 兼容请求失败",
			"profile=" + profileName,
			"endpoint=" + endpoint,
			"model=" + modelName,
			$"timeoutSeconds={timeoutSeconds}",
			"exceptionType=" + exceptionType,
			"error=" + ex?.Message
		};
		if (!string.IsNullOrEmpty(cancellationHint))
		{
			parts.Add("cancellationKind=" + cancellationHint);
		}
		if (!string.IsNullOrEmpty(innerType))
		{
			parts.Add("innerExceptionType=" + innerType);
		}
		if (!string.IsNullOrEmpty(innerMessage))
		{
			parts.Add("innerError=" + innerMessage);
		}
		parts.Add("payloadPreview=" + SafePreview(payloadJson, 1600));
		return string.Join(", ", parts);
	}
}
