using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Logging;
using Fungus;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MCS_AIChatMod;

public class UIManager : MonoBehaviour
{
	private class NpcFriendEntry
	{
		public int NpcId;

		public string NpcName;

		public int MessageCount;

		public DateTime LastGameDate;

		public string LastGameTimeText;
	}

	private GameObject _canvasGO;

	private GraphicRaycaster _graphicRaycaster;

	private GameObject _chatPanel;

	private Text _titleText;

	private ScrollRect _chatScroll;

	private RectTransform _chatContentRT;

	private InputField _inputField;

	private Button _sendBtn;

	private GameObject _settingsPanel;

	private Text _providerBtnText;

	private GameObject _providerListGO;

	private GameObject _compatibilityProfileRowGO;

	private Text _compatibilityProfileBtnText;

	private GameObject _compatibilityProfileListGO;

	private GameObject _modelListGO;

	private Transform _modelListContent;

	private InputField _endpointInput;

	private InputField _apiKeyInput;

	private InputField _modelNameInput;

	private Text _statusText;

	private bool _settingsVisible = false;

	private int _selectedProviderIdx = 0;

	private string _selectedCompatibilityProfile = "Auto";

	private GameObject _modConfigPanel;

	private bool _modConfigVisible = false;

	private InputField _opacityInput;

	private Button _wipeLegacyDataBtn;

	private float _wipeLegacyDataConfirmUntilRealtime = -1f;

	private Text _experimentalMainlineStatusText;

	private Button _experimentalMainlineDisableBtn;

	private GameObject _modelConfigPanel;

	private bool _modelConfigVisible = false;

	private InputField _recentDialogCountInput;

	private InputField _longTermSummaryCountInput;

	private InputField _longTermMaxCharsInput;

	private GameObject _donatePanel;

	private GameObject _eventsPanel;

	private RectTransform _eventsContentRT;

	private ScrollRect _eventsScroll;

	private Text _eventsMainlineHintText;

	private Button _eventsGenerateMainlineBtn;

	private GameObject _mainlineExperimentalWarningPanel;

	private GameObject _friendPanel;

	private RectTransform _friendContentRT;

	private ScrollRect _friendScroll;

	private bool _isFirstTimeSetup = false;

	private int _currentNpcId = -1;

	private List<string> _fetchedModels = new List<string>();

	private static Font _uiFont;

	private const int CANVAS_SORT_ORDER = 32000;

	private const float CHAT_W = 860f;

	private const float CHAT_H = 810f;

	private const float SETTINGS_W = 340f;

	private const float LEGACY_WIPE_CONFIRM_WINDOW_SECONDS = 4f;

	private const string EXPERIMENTAL_MAINLINE_WARNING_MESSAGE = "该功能处于极早期的开发阶段，有可能造成坏档、死档等风险，请谨慎开启。";

	private static readonly Color PANEL_COLOR = new Color(0.16f, 0.16f, 0.2f, 0.65f);

	private static readonly Color HEADER_COLOR = new Color(0.1f, 0.1f, 0.13f, 1f);

	private static readonly Color INPUT_BG = new Color(0.22f, 0.22f, 0.28f, 1f);

	private static readonly Color BTN_COLOR = new Color(0.28f, 0.5f, 0.85f, 1f);

	private static readonly Color BTN_DANGER = new Color(0.85f, 0.3f, 0.3f, 1f);

	private static readonly Color BTN_SECONDARY = new Color(0.3f, 0.3f, 0.36f, 1f);

	private static readonly Color TEXT_PRIMARY = new Color(0.92f, 0.92f, 0.95f, 1f);

	private static readonly Color TEXT_SECONDARY = new Color(0.6f, 0.62f, 0.68f, 1f);

	private static readonly Color PLAYER_BUBBLE = new Color(0.15f, 0.35f, 0.65f, 0.85f);

	private static readonly Color NPC_BUBBLE = new Color(0.22f, 0.22f, 0.28f, 0.85f);

	private static readonly Color NARRATOR_BUBBLE = new Color(0.18f, 0.18f, 0.18f, 0.65f);

	private static readonly Color LIST_ITEM_HOVER = new Color(0.28f, 0.5f, 0.85f, 0.4f);

	private static readonly string[] QUEST_LOADING_STAGE_NAMES = new string[5] { "整理任务素材", "构建任务提示词", "连接模型服务", "生成任务内容", "校验并写入任务" };

	private Text _loadingTitleText;

	private Text _loadingStageText;

	private Text _loadingProgressText;

	private Text _loadingEstimateText;

	private Text _loadingHintText;

	private readonly List<Text> _loadingStageItemTexts = new List<Text>();

	private Coroutine _loadingCoroutine;

	private string _loadingCurrentMessage = "准备开始生成任务...";

	private int _loadingCurrentStep;

	private int _loadingTotalSteps = QUEST_LOADING_STAGE_NAMES.Length;

	private float _loadingElapsedSeconds;

	public static UIManager Instance { get; private set; }

	private static Font GetFont()
	{
		if (_uiFont != null)
		{
			return _uiFont;
		}
		try
		{
			_uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
		}
		catch (Exception arg)
		{
			ManualLogSource logger = AIChatManager.logger;
			if (logger != null)
			{
				logger.LogWarning((object)$"[UIManager] 加载内置字体失败: {arg}");
			}
		}
		if (_uiFont == null)
		{
			try
			{
				_uiFont = Font.CreateDynamicFontFromOSFont("Arial", 14);
			}
			catch (Exception arg2)
			{
				ManualLogSource logger2 = AIChatManager.logger;
				if (logger2 != null)
				{
					logger2.LogWarning((object)$"[UIManager] 加载系统字体失败: {arg2}");
				}
			}
		}
		return _uiFont;
	}

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			UnityEngine.Object.Destroy(this);
			return;
		}
		Instance = this;
		try
		{
			AIChatManager.logger.LogInfo((object)"[UIManager] 开始构建 UI...");
			CreateCanvas();
			BuildChatPanel();
			BuildSettingsPanel();
			BuildEventsPanel();
			BuildFriendListPanel();
			BuildModConfigPanel();
			BuildModelConfigPanel();
			BuildDonatePanel();
			BuildMainlineExperimentalWarningPanel();
			_chatPanel.SetActive(value: false);
			_settingsPanel.SetActive(value: false);
			_modConfigPanel.SetActive(value: false);
			_modelConfigPanel.SetActive(value: false);
			_mainlineExperimentalWarningPanel.SetActive(value: false);
			AIChatManager.logger.LogInfo((object)"[UIManager] UI 构建完成");
		}
		catch (Exception arg)
		{
			AIChatManager.logger.LogError((object)$"[UIManager] UI 构建失败: {arg}");
		}
	}

	private void Update()
	{
		if (IsLegacyWipeConfirmationExpired(Time.unscaledTime, _wipeLegacyDataConfirmUntilRealtime))
		{
			ResetLegacyWipeConfirmation();
		}
		if (_chatPanel != null && _chatPanel.activeSelf && IsGameDialogActive())
		{
			HideChatPanel();
		}
		if (!Input.GetKeyDown(KeyCode.B) || (_inputField != null && _inputField.isFocused) || IsGameDialogActive())
		{
			return;
		}
		if (_chatPanel != null && _chatPanel.activeSelf)
		{
			_chatPanel.SetActive(value: false);
			if (_settingsPanel != null)
			{
				_settingsPanel.SetActive(value: false);
			}
			if (_modConfigPanel != null)
			{
				_modConfigPanel.SetActive(value: false);
			}
			if (_modelConfigPanel != null)
			{
				_modelConfigPanel.SetActive(value: false);
			}
			HideMainlineExperimentalWarningPanel();
			_settingsVisible = false;
			_modConfigVisible = false;
			_modelConfigVisible = false;
			ResetLegacyWipeConfirmation();
		}
		else if (_chatPanel != null)
		{
			_chatPanel.SetActive(value: true);
			if (_settingsPanel != null)
			{
				_settingsPanel.SetActive(value: false);
			}
			if (_modConfigPanel != null)
			{
				_modConfigPanel.SetActive(value: false);
			}
			if (_modelConfigPanel != null)
			{
				_modelConfigPanel.SetActive(value: false);
			}
			_settingsVisible = false;
			_modConfigVisible = false;
			_modelConfigVisible = false;
			ShowChatPanel(AIChatManager.npcInfo?.Name ?? "");
		}
	}

	public void ShowChatPanel(string npcName)
	{
		bool panelVisible = _chatPanel != null && _chatPanel.activeSelf;
		if (!ConfigManager.IsConfigReady())
		{
			_isFirstTimeSetup = true;
			if (panelVisible)
			{
				ShowSettingsOnly();
				SetStatusText("首次使用，请先完成 API 配置后点保存");
			}
			return;
		}
		if (_titleText != null)
		{
			_titleText.text = (string.IsNullOrEmpty(npcName) ? "对话中" : ("与 " + npcName + " 对话中"));
		}
		int newNpcId = AIChatManager.npcInfo?.NpcID ?? (-1);
		if (_currentNpcId != newNpcId && newNpcId > 0)
		{
			_currentNpcId = newNpcId;
			ClearMessages();
			ReloadChatHistory();
			AIChatManager.logger.LogInfo((object)$"[UIManager] NPC 切换到 {npcName} (ID:{newNpcId})，已重新加载对话历史");
		}
		if (panelVisible)
		{
			if (_settingsPanel != null)
			{
				_settingsPanel.SetActive(value: false);
			}
			_settingsVisible = false;
		}
	}

	public bool IsChatPanelVisible()
	{
		return _chatPanel != null && _chatPanel.activeSelf;
	}

	public void HideChatPanel()
	{
		if (_chatPanel != null)
		{
			_chatPanel.SetActive(value: false);
		}
		if (_settingsPanel != null)
		{
			_settingsPanel.SetActive(value: false);
		}
		if (_modConfigPanel != null)
		{
			_modConfigPanel.SetActive(value: false);
		}
		if (_modelConfigPanel != null)
		{
			_modelConfigPanel.SetActive(value: false);
		}
		if (_mainlineExperimentalWarningPanel != null)
		{
			_mainlineExperimentalWarningPanel.SetActive(value: false);
		}
		_settingsVisible = false;
		_modConfigVisible = false;
		_modelConfigVisible = false;
		ResetLegacyWipeConfirmation();
	}

	private bool IsGameDialogActive()
	{
		try
		{
			SayDialog sayDialog = SayDialog.GetSayDialog();
			if (sayDialog != null && sayDialog.gameObject.activeInHierarchy)
			{
				return true;
			}
		}
		catch (Exception arg)
		{
			ManualLogSource logger = AIChatManager.logger;
			if (logger != null)
			{
				logger.LogWarning((object)$"[UIManager] IsGameDialogActive failed: {arg}");
			}
		}
		return false;
	}

	public bool IsPointerOverModUI()
	{
		if (_graphicRaycaster == null || EventSystem.current == null)
		{
			return false;
		}
		PointerEventData eventData = new PointerEventData(EventSystem.current)
		{
			position = Input.mousePosition
		};
		List<RaycastResult> results = new List<RaycastResult>();
		_graphicRaycaster.Raycast(eventData, results);
		return results.Count > 0;
	}

	public void AppendMessage(string speaker, string message, bool isPlayer, string gameTimeText = null, string tokenUsageText = null, QuestStorySpeakerType? speakerType = null)
	{
		if (!(_chatContentRT == null))
		{
			QuestStorySpeakerType resolvedSpeakerType = (QuestStorySpeakerType)(((int?)speakerType) ?? (isPlayer ? 1 : 0));
			GameObject entryGO = new GameObject("ChatEntry");
			entryGO.transform.SetParent(_chatContentRT, worldPositionStays: false);
			Image bg = entryGO.AddComponent<Image>();
			bg.color = resolvedSpeakerType switch
			{
				QuestStorySpeakerType.Narrator => NARRATOR_BUBBLE,
				QuestStorySpeakerType.Player => PLAYER_BUBBLE,
				_ => NPC_BUBBLE,
			};
			VerticalLayoutGroup layout = entryGO.AddComponent<VerticalLayoutGroup>();
			layout.padding = new RectOffset(12, 12, 8, 8);
			layout.childForceExpandWidth = true;
			layout.childForceExpandHeight = false;
			ContentSizeFitter fitter = entryGO.AddComponent<ContentSizeFitter>();
			fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
			Color speakerColor = resolvedSpeakerType switch
			{
				QuestStorySpeakerType.Narrator => new Color(0.85f, 0.85f, 0.85f),
				QuestStorySpeakerType.Player => new Color(0.7f, 0.9f, 1f),
				_ => new Color(1f, 0.85f, 0.5f),
			};
			CreateText(entryGO.transform, speaker, 14, speakerColor, FontStyle.Bold);
			string metaText = string.Empty;
			if (!string.IsNullOrEmpty(gameTimeText))
			{
				metaText = gameTimeText;
			}
			if (!string.IsNullOrEmpty(tokenUsageText))
			{
				metaText = (string.IsNullOrEmpty(metaText) ? tokenUsageText : (metaText + "  |  " + tokenUsageText));
			}
			if (!string.IsNullOrEmpty(metaText))
			{
				CreateText(entryGO.transform, metaText, 11, TEXT_SECONDARY);
			}
			CreateText(entryGO.transform, message, 15, Color.white);
			Canvas.ForceUpdateCanvases();
			if (_chatScroll != null)
			{
				_chatScroll.verticalNormalizedPosition = 0f;
			}
		}
	}

	public void ClearMessages()
	{
		if (!(_chatContentRT == null))
		{
			for (int i = _chatContentRT.childCount - 1; i >= 0; i--)
			{
				UnityEngine.Object.Destroy(_chatContentRT.GetChild(i).gameObject);
			}
		}
	}

	public void SetInputText(string text)
	{
		if (_inputField != null)
		{
			_inputField.text = text;
		}
	}

	public void SetInputInteractable(bool interactable)
	{
		if (_inputField != null)
		{
			_inputField.interactable = interactable;
		}
		if (_sendBtn != null)
		{
			_sendBtn.interactable = interactable;
		}
	}

	public void ReloadChatHistory()
	{
		if (NPCDialog.Instance == null || !NPCDialog.Instance.LoadDialogHistory())
		{
			return;
		}
		List<NPCDialog.DialogHistoryEntryView> history = NPCDialog.Instance.GetDialogHistoryEntries();
		foreach (NPCDialog.DialogHistoryEntryView line in history)
		{
			bool isPlayer = line.Speaker == "你";
			AppendMessage(line.Speaker, line.Message, isPlayer, line.GameTimeText, line.TokenUsageText, line.SpeakerType);
		}
	}

	private void CreateCanvas()
	{
		_canvasGO = new GameObject("AIChatMod_Canvas");
		_canvasGO.transform.SetParent(base.transform);
		Canvas canvas = _canvasGO.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = 32000;
		CanvasScaler scaler = _canvasGO.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920f, 1080f);
		scaler.matchWidthOrHeight = 0.5f;
		_graphicRaycaster = _canvasGO.AddComponent<GraphicRaycaster>();
		if (UnityEngine.Object.FindObjectOfType<EventSystem>() == null)
		{
			GameObject esGO = new GameObject("AIChatMod_EventSystem");
			esGO.transform.SetParent(base.transform);
			esGO.AddComponent<EventSystem>();
			esGO.AddComponent<StandaloneInputModule>();
		}
	}

	private void BuildChatPanel()
	{
		_chatPanel = MakePanel(_canvasGO.transform, "ChatPanel", 860f, 810f, PANEL_COLOR);
		RectTransform chatRT = _chatPanel.GetComponent<RectTransform>();
		Vector2 anchorMin = (chatRT.anchorMax = new Vector2(0.5f, 0.5f));
		chatRT.anchorMin = anchorMin;
		chatRT.anchoredPosition = new Vector2(-170f, 0f);
		GameObject header = MakePanel(_chatPanel.transform, "Header", 0f, 48f, HEADER_COLOR);
		StretchTop(header, 48f);
		header.AddComponent<UIDragger>().target = chatRT;
		_titleText = CreateText(header.transform, "对话中", 18, TEXT_PRIMARY);
		StretchFill(_titleText.gameObject, 15f, 0f, -240f, 0f);
		_titleText.alignment = TextAnchor.MiddleLeft;
		Button donateBtn = MakeButton(header.transform, "打赏作者", 92f, 34f, new Color(0.85f, 0.65f, 0.13f, 1f));
		AnchorRight(donateBtn, -186f, 92f, 34f);
		donateBtn.onClick.AddListener(delegate
		{
			if (_donatePanel != null)
			{
				_donatePanel.SetActive(value: true);
			}
		});
		Button modCfgBtn = MakeButton(header.transform, "Mod配置", 80f, 34f, new Color(0.45f, 0.35f, 0.7f, 1f));
		AnchorRight(modCfgBtn, -90f, 80f, 34f);
		modCfgBtn.onClick.AddListener(ToggleModConfig);
		Button closeBtn = MakeButton(header.transform, "X", 38f, 34f, BTN_DANGER);
		AnchorRight(closeBtn, -44f, 38f, 34f);
		closeBtn.onClick.AddListener(HideChatPanel);
		GameObject toolbar = MakePanel(_chatPanel.transform, "Toolbar", 0f, 38f, new Color(0.14f, 0.14f, 0.17f));
		StretchTopBelow(toolbar, 48f, 38f);
		HorizontalLayoutGroup tbLayout = toolbar.AddComponent<HorizontalLayoutGroup>();
		tbLayout.padding = new RectOffset(10, 10, 3, 3);
		tbLayout.spacing = 10f;
		tbLayout.childAlignment = TextAnchor.MiddleLeft;
		tbLayout.childForceExpandWidth = false;
		Button clearBtn = MakeLayoutButton(toolbar.transform, "清空历史", 110f, 30f, BTN_DANGER);
		clearBtn.onClick.AddListener(OnClearHistory);
		Button cardBtn = MakeLayoutButton(toolbar.transform, "角色卡", 90f, 30f, BTN_SECONDARY);
		cardBtn.onClick.AddListener(OnOpenCharacterCard);
		Button eventsBtn = MakeLayoutButton(toolbar.transform, "生平", 80f, 30f, new Color(0.55f, 0.35f, 0.7f, 1f));
		eventsBtn.onClick.AddListener(OnShowNpcEvents);
		Button friendBtn = MakeLayoutButton(toolbar.transform, "好友", 80f, 30f, new Color(0.35f, 0.55f, 0.7f, 1f));
		friendBtn.onClick.AddListener(OnShowFriendList);
		float scrollTop = 86f;
		float scrollBottom = 56f;
		BuildScrollArea(_chatPanel.transform, scrollTop, scrollBottom);
		GameObject inputBar = MakePanel(_chatPanel.transform, "InputBar", 0f, scrollBottom, new Color(0.14f, 0.14f, 0.17f));
		StretchBottom(inputBar, scrollBottom);
		HorizontalLayoutGroup ibLayout = inputBar.AddComponent<HorizontalLayoutGroup>();
		ibLayout.padding = new RectOffset(10, 10, 8, 8);
		ibLayout.spacing = 8f;
		ibLayout.childForceExpandHeight = true;
		_inputField = MakeInputField(inputBar.transform, "在这里输入消息...", 0f, 0f);
		_inputField.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
		_inputField.onEndEdit.AddListener(OnInputEndEdit);
		_sendBtn = MakeLayoutButton(inputBar.transform, "发送", 80f, 0f, BTN_COLOR);
		_sendBtn.onClick.AddListener(OnSendMessage);
	}

	private void BuildScrollArea(Transform parent, float top, float bottom)
	{
		GameObject scrollGO = new GameObject("ChatScrollView");
		scrollGO.transform.SetParent(parent, worldPositionStays: false);
		Image scrollBg = scrollGO.AddComponent<Image>();
		scrollBg.color = new Color(0.13f, 0.13f, 0.16f, 0.3f);
		RectTransform scrollRT = scrollGO.GetComponent<RectTransform>();
		scrollRT.anchorMin = Vector2.zero;
		scrollRT.anchorMax = Vector2.one;
		scrollRT.offsetMin = new Vector2(8f, bottom);
		scrollRT.offsetMax = new Vector2(-8f, 0f - top);
		GameObject vpGO = new GameObject("Viewport");
		vpGO.transform.SetParent(scrollGO.transform, worldPositionStays: false);
		vpGO.AddComponent<Image>().color = Color.white;
		vpGO.AddComponent<RectMask2D>();
		RectTransform vpRT = vpGO.GetComponent<RectTransform>();
		vpRT.anchorMin = Vector2.zero;
		vpRT.anchorMax = Vector2.one;
		vpRT.offsetMin = Vector2.zero;
		vpRT.offsetMax = Vector2.zero;
		GameObject contentGO = new GameObject("Content");
		contentGO.transform.SetParent(vpGO.transform, worldPositionStays: false);
		_chatContentRT = contentGO.AddComponent<RectTransform>();
		_chatContentRT.anchorMin = new Vector2(0f, 1f);
		_chatContentRT.anchorMax = new Vector2(1f, 1f);
		_chatContentRT.pivot = new Vector2(0.5f, 1f);
		_chatContentRT.sizeDelta = new Vector2(0f, 0f);
		VerticalLayoutGroup cl = contentGO.AddComponent<VerticalLayoutGroup>();
		cl.spacing = 8f;
		cl.padding = new RectOffset(5, 5, 5, 5);
		cl.childForceExpandWidth = true;
		cl.childForceExpandHeight = false;
		contentGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		_chatScroll = scrollGO.AddComponent<ScrollRect>();
		_chatScroll.horizontal = false;
		_chatScroll.vertical = true;
		_chatScroll.movementType = ScrollRect.MovementType.Clamped;
		_chatScroll.scrollSensitivity = 30f;
		_chatScroll.content = _chatContentRT;
		_chatScroll.viewport = vpRT;
	}

	private void BuildSettingsPanel()
	{
		_settingsPanel = MakePanel(_canvasGO.transform, "SettingsPanel", 340f, 810f, PANEL_COLOR);
		RectTransform srt = _settingsPanel.GetComponent<RectTransform>();
		Vector2 anchorMin = (srt.anchorMax = new Vector2(0.5f, 0.5f));
		srt.anchorMin = anchorMin;
		float settingsX = 435f;
		srt.anchoredPosition = new Vector2(settingsX, 0f);
		GameObject contentGO = new GameObject("SettingsContent");
		contentGO.transform.SetParent(_settingsPanel.transform, worldPositionStays: false);
		RectTransform contentRT = contentGO.AddComponent<RectTransform>();
		contentRT.anchorMin = Vector2.zero;
		contentRT.anchorMax = Vector2.one;
		contentRT.offsetMin = new Vector2(15f, 15f);
		contentRT.offsetMax = new Vector2(-15f, -15f);
		VerticalLayoutGroup vl = contentGO.AddComponent<VerticalLayoutGroup>();
		vl.spacing = 10f;
		vl.childForceExpandWidth = true;
		vl.childForceExpandHeight = false;
		Transform p = contentGO.transform;
		CreateText(p, "API 设置", 20, TEXT_PRIMARY, FontStyle.Bold);
		CreateText(p, "服务商", 13, TEXT_SECONDARY);
		Button providerBtn = MakeLayoutButton(p, "DeepSeek", 0f, 34f, INPUT_BG);
		_providerBtnText = providerBtn.GetComponentInChildren<Text>();
		providerBtn.onClick.AddListener(ToggleProviderList);
		_providerListGO = MakeScrollableList(p, "ProviderList", 200f);
		Transform providerContent = _providerListGO.transform.Find("Viewport/Content");
		for (int i = 0; i < ConfigManager.Providers.Count; i++)
		{
			int idx = i;
			Button itemBtn = MakeLayoutButton(providerContent, ConfigManager.Providers[i].DisplayName, 0f, 28f, Color.clear);
			itemBtn.onClick.AddListener(delegate
			{
				SelectProvider(idx);
			});
		}
		_providerListGO.SetActive(value: false);
		MakeSpacer(p, 5f);
		CreateText(p, "API Endpoint", 13, TEXT_SECONDARY);
		_endpointInput = MakeInputField(p, "https://api.example.com/v1/chat/completions", 0f, 34f);
		GameObject compatibilityRow = new GameObject("CompatibilityProfileRow");
		compatibilityRow.transform.SetParent(p, worldPositionStays: false);
		VerticalLayoutGroup compatibilityLayout = compatibilityRow.AddComponent<VerticalLayoutGroup>();
		compatibilityLayout.spacing = 6f;
		compatibilityLayout.childForceExpandWidth = true;
		compatibilityLayout.childForceExpandHeight = false;
		compatibilityRow.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		_compatibilityProfileRowGO = compatibilityRow;
		CreateText(compatibilityRow.transform, "兼容档位（仅自定义源）", 13, TEXT_SECONDARY);
		Button compatibilityBtn = MakeLayoutButton(compatibilityRow.transform, "Auto（自动判断）", 0f, 34f, INPUT_BG);
		_compatibilityProfileBtnText = compatibilityBtn.GetComponentInChildren<Text>();
		compatibilityBtn.onClick.AddListener(ToggleCompatibilityProfileList);
		_compatibilityProfileListGO = MakeScrollableList(compatibilityRow.transform, "CompatibilityProfileList", 180f);
		Transform compatibilityContent = _compatibilityProfileListGO.transform.Find("Viewport/Content");
		foreach (CompatibilityProfileOption option in ConfigManager.CustomCompatibilityProfiles)
		{
			string optionValue = option.Value;
			string optionDisplay = option.DisplayName;
			Button itemBtn2 = MakeLayoutButton(compatibilityContent, optionDisplay, 0f, 28f, Color.clear);
			itemBtn2.onClick.AddListener(delegate
			{
				SelectCompatibilityProfile(optionValue);
			});
		}
		_compatibilityProfileListGO.SetActive(value: false);
		CreateText(p, "API Key", 13, TEXT_SECONDARY);
		_apiKeyInput = MakeInputField(p, "sk-...", 0f, 34f);
		_apiKeyInput.contentType = InputField.ContentType.Password;
		CreateText(p, "模型名称", 13, TEXT_SECONDARY);
		_modelNameInput = MakeInputField(p, "model-name", 0f, 34f);
		Button fetchBtn = MakeLayoutButton(p, "刷新模型列表", 0f, 32f, new Color(0.3f, 0.55f, 0.4f));
		fetchBtn.onClick.AddListener(OnFetchModels);
		_modelListGO = MakeScrollableList(p, "ModelList", 180f);
		_modelListContent = _modelListGO.transform.Find("Viewport/Content");
		_modelListGO.SetActive(value: false);
		_statusText = CreateText(p, "", 12, TEXT_SECONDARY);
		MakeSpacer(p, 5f);
		Button saveBtn = MakeLayoutButton(p, "保存配置", 0f, 40f, BTN_COLOR);
		saveBtn.onClick.AddListener(OnSaveSettings);
		LoadConfigToUI();
	}

	private void BuildModConfigPanel()
	{
		_modConfigPanel = MakePanel(_canvasGO.transform, "ModConfigPanel", 340f, 810f, PANEL_COLOR);
		RectTransform mrt = _modConfigPanel.GetComponent<RectTransform>();
		Vector2 anchorMin = (mrt.anchorMax = new Vector2(0.5f, 0.5f));
		mrt.anchorMin = anchorMin;
		float modX = 435f;
		mrt.anchoredPosition = new Vector2(modX, 0f);
		GameObject contentGO = new GameObject("ModConfigContent");
		contentGO.transform.SetParent(_modConfigPanel.transform, worldPositionStays: false);
		RectTransform contentRT = contentGO.AddComponent<RectTransform>();
		contentRT.anchorMin = Vector2.zero;
		contentRT.anchorMax = Vector2.one;
		contentRT.offsetMin = new Vector2(15f, 15f);
		contentRT.offsetMax = new Vector2(-15f, -15f);
		VerticalLayoutGroup vl = contentGO.AddComponent<VerticalLayoutGroup>();
		vl.spacing = 10f;
		vl.childForceExpandWidth = true;
		vl.childForceExpandHeight = false;
		Transform p = contentGO.transform;
		CreateText(p, "Mod 配置", 20, TEXT_PRIMARY, FontStyle.Bold);
		CreateText(p, "对话框透明度 (0.0 ~ 1.0)", 13, TEXT_SECONDARY);
		_opacityInput = MakeInputField(p, "0.85", 0f, 34f);
		_opacityInput.contentType = InputField.ContentType.DecimalNumber;
		_opacityInput.text = ConfigManager.chatOpacity.Value.ToString("F2");
		MakeSpacer(p, 5f);
		Button saveBtn = MakeLayoutButton(p, "保存Mod配置", 0f, 40f, BTN_COLOR);
		saveBtn.onClick.AddListener(OnSaveModConfig);
		Button worldPromptBtn = MakeLayoutButton(p, "编辑世界观", 0f, 36f, new Color(0.5f, 0.42f, 0.18f, 1f));
		worldPromptBtn.onClick.AddListener(OnOpenWorldPromptFile);
		Button modelCfgBtn = MakeLayoutButton(p, "上下文设置", 0f, 36f, new Color(0.35f, 0.5f, 0.75f, 1f));
		modelCfgBtn.onClick.AddListener(ToggleModelConfig);
		MakeSpacer(p, 8f);
		CreateText(p, "实验性主线推演", 16, TEXT_PRIMARY, FontStyle.Bold);
		_experimentalMainlineStatusText = CreateText(p, GetExperimentalMainlineStatusLabel(ConfigManager.experimentalMainlineGenerationEnabled.Value), 12, TEXT_SECONDARY);
		_experimentalMainlineDisableBtn = MakeLayoutButton(p, GetExperimentalMainlineDisableButtonLabel(ConfigManager.experimentalMainlineGenerationEnabled.Value), 0f, 36f, BTN_DANGER);
		_experimentalMainlineDisableBtn.onClick.AddListener(OnDisableExperimentalMainlineGeneration);
		UpdateExperimentalMainlineControls();
		MakeSpacer(p, 8f);
		GameObject actionRow = new GameObject("ActionRow");
		actionRow.transform.SetParent(p, worldPositionStays: false);
		HorizontalLayoutGroup actionLayout = actionRow.AddComponent<HorizontalLayoutGroup>();
		actionLayout.spacing = 8f;
		actionLayout.childAlignment = TextAnchor.MiddleCenter;
		actionLayout.childForceExpandWidth = true;
		actionLayout.childForceExpandHeight = false;
		Button apiBtn = MakeLayoutButton(actionRow.transform, "API设置", 0f, 38f, BTN_SECONDARY);
		apiBtn.onClick.AddListener(ToggleSettings);
		MakeSpacer(p, 8f);
		_wipeLegacyDataBtn = MakeLayoutButton(p, GetLegacyWipeButtonLabel(confirmPending: false), 0f, 38f, BTN_DANGER);
		_wipeLegacyDataBtn.onClick.AddListener(OnWipeLegacyData);
	}

	private void ToggleModConfig()
	{
		_modConfigVisible = !_modConfigVisible;
		if (_modConfigPanel != null)
		{
			_modConfigPanel.SetActive(_modConfigVisible);
		}
		if (!_modConfigVisible)
		{
			ResetLegacyWipeConfirmation();
		}
		if (_modConfigVisible)
		{
			UpdateExperimentalMainlineControls();
			_settingsVisible = false;
			if (_settingsPanel != null)
			{
				_settingsPanel.SetActive(value: false);
			}
			_modelConfigVisible = false;
			if (_modelConfigPanel != null)
			{
				_modelConfigPanel.SetActive(value: false);
			}
		}
	}

	private static string GetLegacyWipeButtonLabel(bool confirmPending)
	{
		return confirmPending ? "再次点击确认清空" : "删除过往";
	}

	private static bool IsLegacyWipeConfirmationExpired(float nowRealtime, float confirmUntilRealtime)
	{
		return confirmUntilRealtime > 0f && nowRealtime >= confirmUntilRealtime;
	}

	internal static string GetLegacyWipeButtonLabelForTest(bool confirmPending)
	{
		return GetLegacyWipeButtonLabel(confirmPending);
	}

	internal static bool IsLegacyWipeConfirmationExpiredForTest(float nowRealtime, float confirmUntilRealtime)
	{
		return IsLegacyWipeConfirmationExpired(nowRealtime, confirmUntilRealtime);
	}

	internal static bool ShouldRequireExperimentalMainlineConfirmation(bool enabled)
	{
		return !enabled;
	}

	internal static string GetMainlineEventsGenerateButtonLabelForTest(bool waitingBoundAnchor)
	{
		return GetMainlineEventsGenerateButtonLabel(waitingBoundAnchor);
	}

	private static string GetMainlineEventsGenerateButtonLabel(bool waitingBoundAnchor)
	{
		return waitingBoundAnchor ? "已绑定，等待生平事件" : "推演主线";
	}

	private static string GetMainlineEventsHintLabel(bool waitingBoundAnchor)
	{
		return waitingBoundAnchor ? "当前人物已绑定为主线锚点，等待其下一条官方生平事件被改写" : "查看NPC生平，推演主线";
	}

	internal static string GetExperimentalMainlineWarningMessage()
	{
		return "该功能处于极早期的开发阶段，有可能造成坏档、死档等风险，请谨慎开启。";
	}

	internal static string GetExperimentalMainlineStatusLabel(bool enabled)
	{
		return enabled ? "实验性主线推演：已启用" : "实验性主线推演：未启用";
	}

	internal static string GetExperimentalMainlineDisableButtonLabel(bool enabled)
	{
		return enabled ? "关闭实验性主线推演" : "实验性主线推演当前未启用";
	}

	private bool IsLegacyWipeConfirmationArmed()
	{
		return _wipeLegacyDataConfirmUntilRealtime > Time.unscaledTime;
	}

	private void UpdateLegacyWipeButtonLabel()
	{
		if (!(_wipeLegacyDataBtn == null))
		{
			Text label = _wipeLegacyDataBtn.GetComponentInChildren<Text>();
			if (label != null)
			{
				label.text = GetLegacyWipeButtonLabel(IsLegacyWipeConfirmationArmed());
			}
		}
	}

	private void ResetLegacyWipeConfirmation()
	{
		_wipeLegacyDataConfirmUntilRealtime = -1f;
		UpdateLegacyWipeButtonLabel();
	}

	private void ArmLegacyWipeConfirmation()
	{
		_wipeLegacyDataConfirmUntilRealtime = Time.unscaledTime + 4f;
		UpdateLegacyWipeButtonLabel();
	}

	private void OnWipeLegacyData()
	{
		if (!IsLegacyWipeConfirmationArmed())
		{
			ArmLegacyWipeConfirmation();
			try
			{
				UIPopTip.Inst?.Pop("危险操作：再次点击确认清空过往记录");
				return;
			}
			catch (Exception arg)
			{
				ManualLogSource logger = AIChatManager.logger;
				if (logger != null)
				{
					logger.LogWarning((object)$"[UIManager] Legacy wipe confirm prompt failed: {arg}");
				}
				return;
			}
		}
		ResetLegacyWipeConfirmation();
		LegacyDataWipeSummary summary = AIChatManager.ClearLegacyModDataPreserveCharacterCards();
		ClearMessages();
		ReloadChatHistory();
		RefreshFriendList();
		string resultText = summary?.BuildDisplayText() ?? "已清空过往";
		try
		{
			UIPopTip.Inst?.Pop(resultText);
		}
		catch (Exception arg2)
		{
			ManualLogSource logger2 = AIChatManager.logger;
			if (logger2 != null)
			{
				logger2.LogWarning((object)$"[UIManager] Legacy wipe result prompt failed: {arg2}");
			}
		}
	}

	private void OnSaveModConfig()
	{
		try
		{
			if (float.TryParse(_opacityInput.text, out var opacity))
			{
				opacity = Mathf.Clamp01(opacity);
				ConfigManager.chatOpacity.Value = opacity;
				ApplyChatOpacity(opacity);
			}
			ConfigManager.SaveConfig();
			try
			{
				UIPopTip.Inst?.Pop("Mod配置已保存");
			}
			catch (Exception arg)
			{
				ManualLogSource logger = AIChatManager.logger;
				if (logger != null)
				{
					logger.LogWarning((object)$"[UIManager] Mod配置保存提示失败: {arg}");
				}
			}
			AIChatManager.logger.LogInfo((object)"[UIManager] Mod配置已保存");
		}
		catch (Exception ex)
		{
			AIChatManager.logger.LogError((object)("[UIManager] 保存Mod配置失败: " + ex.Message));
		}
	}

	private void OnOpenWorldPromptFile()
	{
		try
		{
			ConfigManager.OpenUserPromptFile();
			try
			{
				UIPopTip.Inst?.Pop("世界观文件已打开，保存后会自动同步并立即生效。");
			}
			catch (Exception arg)
			{
				ManualLogSource logger = AIChatManager.logger;
				if (logger != null)
				{
					logger.LogWarning((object)$"[UIManager] 世界观提示弹窗失败: {arg}");
				}
			}
		}
		catch (Exception arg2)
		{
			AIChatManager.logger.LogError((object)$"[UIManager] 打开世界观文件失败: {arg2}");
			try
			{
				UIPopTip.Inst?.Pop("打开世界观文件失败，请查看日志。");
			}
			catch (Exception arg3)
			{
				ManualLogSource logger2 = AIChatManager.logger;
				if (logger2 != null)
				{
					logger2.LogWarning((object)$"[UIManager] 世界观失败提示弹窗失败: {arg3}");
				}
			}
		}
	}

	private void BuildModelConfigPanel()
	{
		_modelConfigPanel = MakePanel(_canvasGO.transform, "ModelConfigPanel", 340f, 810f, PANEL_COLOR);
		RectTransform mrt = _modelConfigPanel.GetComponent<RectTransform>();
		Vector2 anchorMin = (mrt.anchorMax = new Vector2(0.5f, 0.5f));
		mrt.anchorMin = anchorMin;
		float modX = 435f;
		mrt.anchoredPosition = new Vector2(modX, 0f);
		GameObject contentGO = new GameObject("ModelConfigContent");
		contentGO.transform.SetParent(_modelConfigPanel.transform, worldPositionStays: false);
		RectTransform contentRT = contentGO.AddComponent<RectTransform>();
		contentRT.anchorMin = Vector2.zero;
		contentRT.anchorMax = Vector2.one;
		contentRT.offsetMin = new Vector2(15f, 15f);
		contentRT.offsetMax = new Vector2(-15f, -15f);
		VerticalLayoutGroup vl = contentGO.AddComponent<VerticalLayoutGroup>();
		vl.spacing = 8f;
		vl.childForceExpandWidth = true;
		vl.childForceExpandHeight = false;
		Transform p = contentGO.transform;
		CreateText(p, "上下文设置", 20, TEXT_PRIMARY, FontStyle.Bold);
		CreateText(p, "用于控制记忆窗口与摘要长度，不再允许设置模型采样参数", 12, TEXT_SECONDARY);
		MakeSpacer(p, 4f);
		CreateText(p, "最近对话条数 (4~30)", 13, TEXT_SECONDARY);
		_recentDialogCountInput = MakeInputField(p, "12", 0f, 34f);
		_recentDialogCountInput.contentType = InputField.ContentType.IntegerNumber;
		CreateText(p, "长期摘要条数 (2~30)", 13, TEXT_SECONDARY);
		_longTermSummaryCountInput = MakeInputField(p, "8", 0f, 34f);
		_longTermSummaryCountInput.contentType = InputField.ContentType.IntegerNumber;
		CreateText(p, "摘要单条最大字符 (20~200)", 13, TEXT_SECONDARY);
		_longTermMaxCharsInput = MakeInputField(p, "60", 0f, 34f);
		_longTermMaxCharsInput.contentType = InputField.ContentType.IntegerNumber;
		MakeSpacer(p, 8f);
		Button saveBtn = MakeLayoutButton(p, "保存上下文设置", 0f, 40f, BTN_COLOR);
		saveBtn.onClick.AddListener(OnSaveModelConfig);
		Button backBtn = MakeLayoutButton(p, "返回Mod配置", 0f, 34f, BTN_SECONDARY);
		backBtn.onClick.AddListener(delegate
		{
			_modelConfigVisible = false;
			if (_modelConfigPanel != null)
			{
				_modelConfigPanel.SetActive(value: false);
			}
			_modConfigVisible = true;
			if (_modConfigPanel != null)
			{
				_modConfigPanel.SetActive(value: true);
			}
		});
		LoadModelConfigToUI();
	}

	private void ToggleModelConfig()
	{
		_modelConfigVisible = !_modelConfigVisible;
		if (_modelConfigPanel != null)
		{
			_modelConfigPanel.SetActive(_modelConfigVisible);
		}
		ResetLegacyWipeConfirmation();
		if (_modelConfigVisible)
		{
			LoadModelConfigToUI();
			_settingsVisible = false;
			_modConfigVisible = false;
			if (_settingsPanel != null)
			{
				_settingsPanel.SetActive(value: false);
			}
			if (_modConfigPanel != null)
			{
				_modConfigPanel.SetActive(value: false);
			}
		}
	}

	private void LoadModelConfigToUI()
	{
		if (_recentDialogCountInput != null)
		{
			_recentDialogCountInput.text = ConfigManager.recentDialogCount.Value.ToString();
		}
		if (_longTermSummaryCountInput != null)
		{
			_longTermSummaryCountInput.text = ConfigManager.longTermSummaryCount.Value.ToString();
		}
		if (_longTermMaxCharsInput != null)
		{
			_longTermMaxCharsInput.text = ConfigManager.longTermMaxCharsPerEntry.Value.ToString();
		}
	}

	private void OnSaveModelConfig()
	{
		try
		{
			if (int.TryParse(_recentDialogCountInput.text, out var recentCount))
			{
				ConfigManager.recentDialogCount.Value = Mathf.Clamp(recentCount, 4, 30);
			}
			if (int.TryParse(_longTermSummaryCountInput.text, out var summaryCount))
			{
				ConfigManager.longTermSummaryCount.Value = Mathf.Clamp(summaryCount, 2, 30);
			}
			if (int.TryParse(_longTermMaxCharsInput.text, out var maxChars))
			{
				ConfigManager.longTermMaxCharsPerEntry.Value = Mathf.Clamp(maxChars, 20, 200);
			}
			ConfigManager.SaveConfig();
			LoadModelConfigToUI();
			try
			{
				UIPopTip.Inst?.Pop("上下文设置已保存");
			}
			catch (Exception arg)
			{
				ManualLogSource logger = AIChatManager.logger;
				if (logger != null)
				{
					logger.LogWarning((object)$"[UIManager] 上下文设置保存提示失败: {arg}");
				}
			}
			AIChatManager.logger.LogInfo((object)"[UIManager] 上下文设置已保存");
		}
		catch (Exception ex)
		{
			AIChatManager.logger.LogError((object)("[UIManager] 保存上下文设置失败: " + ex.Message));
		}
	}

	private void UpdateExperimentalMainlineControls()
	{
		bool enabled = ConfigManager.experimentalMainlineGenerationEnabled.Value;
		if (_experimentalMainlineStatusText != null)
		{
			_experimentalMainlineStatusText.text = GetExperimentalMainlineStatusLabel(enabled);
		}
		if (!(_experimentalMainlineDisableBtn == null))
		{
			Text label = _experimentalMainlineDisableBtn.GetComponentInChildren<Text>();
			if (label != null)
			{
				label.text = GetExperimentalMainlineDisableButtonLabel(enabled);
			}
			_experimentalMainlineDisableBtn.interactable = enabled;
			Image image = _experimentalMainlineDisableBtn.GetComponent<Image>();
			if (image != null)
			{
				image.color = (enabled ? BTN_DANGER : BTN_SECONDARY);
			}
		}
	}

	private void OnDisableExperimentalMainlineGeneration()
	{
		if (ConfigManager.experimentalMainlineGenerationEnabled.Value)
		{
			ConfigManager.experimentalMainlineGenerationEnabled.Value = false;
			ConfigManager.SaveConfig();
			UpdateExperimentalMainlineControls();
			UIPopTip.Inst?.Pop("已关闭实验性主线推演。下次开启将再次显示风险警示。");
		}
	}

	private void ApplyChatOpacity(float opacity)
	{
		if (_chatPanel != null)
		{
			CanvasGroup cg = _chatPanel.GetComponent<CanvasGroup>();
			if (cg != null)
			{
				cg.alpha = opacity;
			}
		}
		if (_modConfigPanel != null)
		{
			CanvasGroup cg2 = _modConfigPanel.GetComponent<CanvasGroup>();
			if (cg2 != null)
			{
				cg2.alpha = opacity;
			}
		}
		if (_modelConfigPanel != null)
		{
			CanvasGroup cg3 = _modelConfigPanel.GetComponent<CanvasGroup>();
			if (cg3 != null)
			{
				cg3.alpha = opacity;
			}
		}
	}

	private void BuildDonatePanel()
	{
		_donatePanel = new GameObject("DonatePanel");
		_donatePanel.transform.SetParent(_canvasGO.transform, worldPositionStays: false);
		RectTransform drt = _donatePanel.AddComponent<RectTransform>();
		drt.anchorMin = Vector2.zero;
		drt.anchorMax = Vector2.one;
		Vector2 offsetMin = (drt.offsetMax = Vector2.zero);
		drt.offsetMin = offsetMin;
		Image bgImg = _donatePanel.AddComponent<Image>();
		bgImg.color = new Color(0f, 0f, 0f, 0.7f);
		Button bgBtn = _donatePanel.AddComponent<Button>();
		bgBtn.onClick.AddListener(delegate
		{
			_donatePanel.SetActive(value: false);
		});
		GameObject card = new GameObject("DonateCard");
		card.transform.SetParent(_donatePanel.transform, worldPositionStays: false);
		RectTransform cardRT = card.AddComponent<RectTransform>();
		offsetMin = (cardRT.anchorMax = new Vector2(0.5f, 0.5f));
		cardRT.anchorMin = offsetMin;
		cardRT.sizeDelta = new Vector2(500f, 420f);
		Image cardImg = card.AddComponent<Image>();
		cardImg.color = new Color(0.14f, 0.14f, 0.18f, 0.95f);
		VerticalLayoutGroup vl = card.AddComponent<VerticalLayoutGroup>();
		vl.spacing = 10f;
		vl.padding = new RectOffset(20, 20, 20, 20);
		vl.childForceExpandWidth = true;
		vl.childForceExpandHeight = false;
		vl.childAlignment = TextAnchor.UpperCenter;
		card.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		Text titleTxt = CreateText(card.transform, "☕ 感谢支持", 22, new Color(0.95f, 0.85f, 0.4f), FontStyle.Bold);
		titleTxt.alignment = TextAnchor.MiddleCenter;
		Text subtitleTxt = CreateText(card.transform, "如果这个Mod对您有帮助，欢迎打赏支持作者继续开发 ❤", 14, TEXT_SECONDARY);
		subtitleTxt.alignment = TextAnchor.MiddleCenter;
		MakeSpacer(card.transform, 5f);
		GameObject qrRow = new GameObject("QRRow");
		qrRow.transform.SetParent(card.transform, worldPositionStays: false);
		RectTransform qrRowRT = qrRow.AddComponent<RectTransform>();
		LayoutElement qrRowLE = qrRow.AddComponent<LayoutElement>();
		qrRowLE.preferredHeight = 250f;
		HorizontalLayoutGroup hl = qrRow.AddComponent<HorizontalLayoutGroup>();
		hl.spacing = 20f;
		hl.childForceExpandWidth = true;
		hl.childForceExpandHeight = true;
		hl.childAlignment = TextAnchor.MiddleCenter;
		BuildQRColumn(qrRow.transform, "微信扫码", "MCS_AIChatMod.DonateAssets.wechatpay.qr", new Color(0.07f, 0.72f, 0.33f));
		BuildQRColumn(qrRow.transform, "支付宝扫码", "MCS_AIChatMod.DonateAssets.alipay.qr", new Color(0f, 0.47f, 0.95f));
		MakeSpacer(card.transform, 5f);
		Button closeBtn = MakeLayoutButton(card.transform, "关闭", 0f, 36f, BTN_SECONDARY);
		closeBtn.onClick.AddListener(delegate
		{
			_donatePanel.SetActive(value: false);
		});
		_donatePanel.SetActive(value: false);
	}

	private void BuildQRColumn(Transform parent, string label, string resourceName, Color labelColor)
	{
		GameObject col = new GameObject(label);
		col.transform.SetParent(parent, worldPositionStays: false);
		VerticalLayoutGroup colVL = col.AddComponent<VerticalLayoutGroup>();
		colVL.spacing = 6f;
		colVL.childForceExpandWidth = true;
		colVL.childForceExpandHeight = false;
		colVL.childAlignment = TextAnchor.MiddleCenter;
		Text lblTxt = CreateText(col.transform, label, 15, labelColor, FontStyle.Bold);
		lblTxt.alignment = TextAnchor.MiddleCenter;
		LayoutElement lblLE = lblTxt.gameObject.AddComponent<LayoutElement>();
		lblLE.preferredHeight = 22f;
		GameObject qrGO = new GameObject("QRImage");
		qrGO.transform.SetParent(col.transform, worldPositionStays: false);
		Image qrImg = qrGO.AddComponent<Image>();
		LayoutElement qrLE = qrGO.AddComponent<LayoutElement>();
		qrLE.preferredWidth = 180f;
		qrLE.preferredHeight = 180f;
		try
		{
			qrImg.sprite = EmbeddedDonateAssets.LoadSprite(resourceName);
			if (qrImg.sprite != null)
			{
				qrImg.preserveAspect = true;
				return;
			}
			qrImg.color = new Color(0.3f, 0.3f, 0.3f);
			Text placeholder = CreateText(qrGO.transform, "内嵌收款码加载失败", 11, TEXT_SECONDARY);
			placeholder.alignment = TextAnchor.MiddleCenter;
		}
		catch (Exception ex)
		{
			AIChatManager.logger.LogWarning((object)("[UIManager] 加载内嵌打赏图片失败: " + ex.Message));
			qrImg.color = new Color(0.3f, 0.3f, 0.3f);
		}
	}

	private void ToggleSettings()
	{
		_settingsVisible = !_settingsVisible;
		if (_settingsPanel != null)
		{
			_settingsPanel.SetActive(_settingsVisible);
		}
		ResetLegacyWipeConfirmation();
		if (_settingsVisible)
		{
			LoadConfigToUI();
		}
		if (_settingsVisible)
		{
			_modConfigVisible = false;
			if (_modConfigPanel != null)
			{
				_modConfigPanel.SetActive(value: false);
			}
			_modelConfigVisible = false;
			if (_modelConfigPanel != null)
			{
				_modelConfigPanel.SetActive(value: false);
			}
		}
	}

	private void ShowSettingsOnly()
	{
		_settingsVisible = true;
		_modConfigVisible = false;
		ResetLegacyWipeConfirmation();
		if (_chatPanel != null)
		{
			_chatPanel.SetActive(value: true);
		}
		if (_settingsPanel != null)
		{
			_settingsPanel.SetActive(value: true);
		}
		if (_modConfigPanel != null)
		{
			_modConfigPanel.SetActive(value: false);
		}
		if (_modelConfigPanel != null)
		{
			_modelConfigPanel.SetActive(value: false);
		}
		LoadConfigToUI();
	}

	private void ToggleProviderList()
	{
		if (_providerListGO != null)
		{
			_providerListGO.SetActive(!_providerListGO.activeSelf);
		}
	}

	private void ToggleCompatibilityProfileList()
	{
		if (_compatibilityProfileListGO != null && _compatibilityProfileRowGO != null && _compatibilityProfileRowGO.activeSelf)
		{
			_compatibilityProfileListGO.SetActive(!_compatibilityProfileListGO.activeSelf);
		}
	}

	private void SelectCompatibilityProfile(string value)
	{
		_selectedCompatibilityProfile = ConfigManager.NormalizeCompatibilityProfileValue(value);
		if (_compatibilityProfileBtnText != null)
		{
			_compatibilityProfileBtnText.text = ConfigManager.GetCompatibilityProfileDisplayName(_selectedCompatibilityProfile);
		}
		if (_compatibilityProfileListGO != null)
		{
			_compatibilityProfileListGO.SetActive(value: false);
		}
		AIChatManager.logger.LogInfo((object)("[UIManager] 切换兼容档位: " + _selectedCompatibilityProfile));
	}

	private void UpdateCompatibilityProfileVisibility()
	{
		bool isCustomProvider = _selectedProviderIdx >= 0 && _selectedProviderIdx < ConfigManager.Providers.Count && string.Equals(ConfigManager.Providers[_selectedProviderIdx].DisplayName, "自定义 (Custom)", StringComparison.Ordinal);
		if (_compatibilityProfileRowGO != null)
		{
			_compatibilityProfileRowGO.SetActive(isCustomProvider);
		}
		if (!isCustomProvider && _compatibilityProfileListGO != null)
		{
			_compatibilityProfileListGO.SetActive(value: false);
		}
		if (_compatibilityProfileBtnText != null)
		{
			_compatibilityProfileBtnText.text = ConfigManager.GetCompatibilityProfileDisplayName(_selectedCompatibilityProfile);
		}
	}

	private void SelectProvider(int idx)
	{
		if (idx >= 0 && idx < ConfigManager.Providers.Count)
		{
			SaveCurrentKeyToProvider();
			SaveCurrentModelToProvider();
			_selectedProviderIdx = idx;
			ProviderPreset preset = ConfigManager.Providers[idx];
			if (_providerBtnText != null)
			{
				_providerBtnText.text = preset.DisplayName;
			}
			if (_endpointInput != null && !string.IsNullOrEmpty(preset.ChatEndpoint))
			{
				_endpointInput.text = preset.ChatEndpoint;
			}
			if (_modelNameInput != null)
			{
				_modelNameInput.text = ConfigManager.GetSavedModelName(preset.DisplayName);
			}
			if (_apiKeyInput != null)
			{
				_apiKeyInput.text = ConfigManager.GetApiKey(preset.DisplayName);
			}
			if (_providerListGO != null)
			{
				_providerListGO.SetActive(value: false);
			}
			UpdateCompatibilityProfileVisibility();
			AIChatManager.logger.LogInfo((object)("[UIManager] 切换服务商: " + preset.DisplayName));
		}
	}

	private async void OnFetchModels()
	{
		SetStatusText("正在获取模型列表...");
		try
		{
			BaseAIClient tempClient = new BaseAIClient();
			string key = ((_apiKeyInput != null) ? _apiKeyInput.text : "");
			if (!string.IsNullOrEmpty(key))
			{
				tempClient.SetAuthToken(key);
			}
			string providerName = ((_selectedProviderIdx >= 0 && _selectedProviderIdx < ConfigManager.Providers.Count) ? ConfigManager.Providers[_selectedProviderIdx].DisplayName : (ConfigManager.selectedProviderConfig?.Value ?? ""));
			tempClient.SetProviderHint(providerName);
			tempClient.SetCustomCompatibilityProfile(string.Equals(providerName, "自定义 (Custom)", StringComparison.Ordinal) ? _selectedCompatibilityProfile : "Auto");
			string modelsUrl = null;
			if (_selectedProviderIdx >= 0 && _selectedProviderIdx < ConfigManager.Providers.Count)
			{
				modelsUrl = ConfigManager.Providers[_selectedProviderIdx].ModelsEndpoint;
			}
			if (string.IsNullOrEmpty(modelsUrl) && _endpointInput != null)
			{
				string ep = _endpointInput.text;
				if (!string.IsNullOrEmpty(ep))
				{
					modelsUrl = BaseAIClient.InferModelsEndpointFromChatEndpoint(ep);
				}
			}
			if (string.IsNullOrEmpty(modelsUrl))
			{
				SetStatusText("无法推断 Models Endpoint");
				return;
			}
			List<string> allModels = await tempClient.FetchModelsAsync(modelsUrl);
			_fetchedModels = FilterTextModels(allModels);
			if (_fetchedModels.Count > 0)
			{
				PopulateModelList(_fetchedModels);
				bool autoSelectedModel = false;
				if (_modelNameInput != null)
				{
					string currentModel = ((_modelNameInput.text != null) ? _modelNameInput.text.Trim() : "");
					if (string.IsNullOrEmpty(currentModel) || ConfigManager.IsLikelyNonChatModel(currentModel))
					{
						_modelNameInput.text = _fetchedModels[0];
						autoSelectedModel = true;
					}
				}
				SetStatusText(autoSelectedModel ? $"已自动切换到可用聊天模型: {_fetchedModels[0]}（共 {allModels.Count} 个）" : $"获取到 {_fetchedModels.Count} 个文本模型（共 {allModels.Count} 个）");
			}
			else
			{
				SetStatusText($"共 {allModels.Count} 个模型，无文本模型，请手动输入");
			}
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			SetStatusText("获取失败: " + ex2.Message);
			AIChatManager.logger.LogWarning((object)$"[UIManager] FetchModels: {ex2}");
		}
	}

	private List<string> FilterTextModels(List<string> models)
	{
		List<string> result = new List<string>();
		if (models == null)
		{
			return result;
		}
		foreach (string m in models)
		{
			if (!string.IsNullOrWhiteSpace(m) && !ConfigManager.IsLikelyNonChatModel(m) && !result.Contains(m))
			{
				result.Add(m);
			}
		}
		return result;
	}

	private void PopulateModelList(List<string> models)
	{
		if (_modelListContent == null || _modelListGO == null)
		{
			return;
		}
		for (int i = _modelListContent.childCount - 1; i >= 0; i--)
		{
			UnityEngine.Object.Destroy(_modelListContent.GetChild(i).gameObject);
		}
		foreach (string modelName in models)
		{
			string name = modelName;
			Button btn = MakeLayoutButton(_modelListContent, name, 0f, 26f, Color.clear);
			btn.onClick.AddListener(delegate
			{
				if (_modelNameInput != null)
				{
					_modelNameInput.text = name;
				}
				if (_selectedProviderIdx >= 0 && _selectedProviderIdx < ConfigManager.Providers.Count)
				{
					ConfigManager.SetSavedModelName(ConfigManager.Providers[_selectedProviderIdx].DisplayName, name);
				}
				if (_modelListGO != null)
				{
					_modelListGO.SetActive(value: false);
				}
				SetStatusText("已选择模型: " + name);
			});
		}
		_modelListGO.SetActive(value: true);
	}

	private void OnSaveSettings()
	{
		try
		{
			string providerName = "";
			if (_selectedProviderIdx >= 0 && _selectedProviderIdx < ConfigManager.Providers.Count)
			{
				providerName = ConfigManager.Providers[_selectedProviderIdx].DisplayName;
				ConfigManager.selectedProviderConfig.Value = providerName;
			}
			if (_endpointInput != null)
			{
				ConfigManager.apiEndpointConfig.Value = _endpointInput.text.Trim();
			}
			if (_modelNameInput != null)
			{
				string modelName = _modelNameInput.text.Trim();
				ConfigManager.modelNameConfig.Value = modelName;
				if (!string.IsNullOrEmpty(providerName))
				{
					ConfigManager.SetSavedModelName(providerName, modelName);
				}
			}
			ConfigManager.customCompatibilityProfileConfig.Value = _selectedCompatibilityProfile;
			if (_apiKeyInput != null && !string.IsNullOrEmpty(providerName))
			{
				ConfigManager.SetApiKey(providerName, _apiKeyInput.text.Trim());
			}
			ConfigManager.SaveConfig();
			bool clientReady = AIChatManager.RebuildClientFromConfig();
			AIChatManager.logger.LogInfo((object)("[UIManager] 配置已保存 → Provider: " + providerName + ", Model: " + ConfigManager.modelNameConfig.Value + ", Compatibility: " + ConfigManager.customCompatibilityProfileConfig.Value));
			_settingsVisible = false;
			if (_settingsPanel != null)
			{
				_settingsPanel.SetActive(value: false);
			}
			int currentNpcId = AIChatManager.npcInfo?.NpcID ?? (-1);
			if (_isFirstTimeSetup && ConfigManager.IsConfigReady())
			{
				_isFirstTimeSetup = false;
				if (clientReady && currentNpcId > 0 && (UnityEngine.Object)(object)AIChatManager.Instance != null)
				{
					AIChatManager.Instance.StartChatWithNpc(currentNpcId);
				}
				else
				{
					SetStatusText("配置已保存，但当前没有可自动恢复的对话目标。");
				}
			}
			else
			{
				SetStatusText(clientReady ? "配置已保存，API 客户端已刷新。" : "配置已保存，但 API 客户端刷新失败。");
			}
		}
		catch (Exception ex)
		{
			AIChatManager.logger.LogError((object)$"[UIManager] 保存配置出错: {ex}");
			SetStatusText("保存失败: " + ex.Message);
		}
	}

	private void OnInputEndEdit(string text)
	{
		if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) && !string.IsNullOrEmpty(text.Trim()))
		{
			OnSendMessage();
		}
	}

	private void OnSendMessage()
	{
		if (!(_inputField == null) && !string.IsNullOrEmpty(_inputField.text.Trim()))
		{
			string msg = _inputField.text.Trim();
			_inputField.text = "";
			NPCDialog.Instance?.OnUISendMessage(msg);
			if (EventSystem.current != null)
			{
				EventSystem.current.SetSelectedGameObject(_inputField.gameObject);
			}
		}
	}

	private void OnClearHistory()
	{
		NPCDialog.Instance?.ClearHistory();
		ClearMessages();
	}

	private void OnOpenCharacterCard()
	{
		if (NPCDialog.Instance != null)
		{
			NPCDialog.Instance.OpenCharacterCard();
		}
		else
		{
			AIChatManager.logger.LogWarning((object)"[UIManager] NPCDialog 实例不存在");
		}
	}

	private void BuildEventsPanel()
	{
		_eventsPanel = MakePanel(_canvasGO.transform, "EventsPanel", 860f, 810f, new Color(0.12f, 0.12f, 0.16f, 0.95f));
		RectTransform ert = _eventsPanel.GetComponent<RectTransform>();
		Vector2 anchorMin = (ert.anchorMax = new Vector2(0.5f, 0.5f));
		ert.anchorMin = anchorMin;
		ert.anchoredPosition = new Vector2(-170f, 0f);
		GameObject header = MakePanel(_eventsPanel.transform, "EventsHeader", 0f, 48f, HEADER_COLOR);
		StretchTop(header, 48f);
		Text titleTxt = CreateText(header.transform, "NPC 生平事件", 18, TEXT_PRIMARY, FontStyle.Bold);
		StretchFill(titleTxt.gameObject, 15f, 0f, -55f, 0f);
		titleTxt.alignment = TextAnchor.MiddleLeft;
		Button closeEventsBtn = MakeButton(header.transform, "X", 38f, 34f, BTN_DANGER);
		AnchorRight(closeEventsBtn, -8f, 38f, 34f);
		closeEventsBtn.onClick.AddListener(delegate
		{
			_eventsPanel.SetActive(value: false);
		});
		GameObject scrollGO = new GameObject("EventsScrollView");
		scrollGO.transform.SetParent(_eventsPanel.transform, worldPositionStays: false);
		scrollGO.AddComponent<Image>().color = new Color(0.13f, 0.13f, 0.16f, 0.3f);
		RectTransform scrollRT = scrollGO.GetComponent<RectTransform>();
		scrollRT.anchorMin = Vector2.zero;
		scrollRT.anchorMax = Vector2.one;
		scrollRT.offsetMin = new Vector2(8f, 60f);
		scrollRT.offsetMax = new Vector2(-8f, -48f);
		GameObject vpGO = new GameObject("Viewport");
		vpGO.transform.SetParent(scrollGO.transform, worldPositionStays: false);
		vpGO.AddComponent<Image>().color = Color.white;
		vpGO.AddComponent<RectMask2D>();
		RectTransform vpRT = vpGO.GetComponent<RectTransform>();
		vpRT.anchorMin = Vector2.zero;
		vpRT.anchorMax = Vector2.one;
		vpRT.offsetMin = Vector2.zero;
		vpRT.offsetMax = Vector2.zero;
		GameObject contentGO = new GameObject("Content");
		contentGO.transform.SetParent(vpGO.transform, worldPositionStays: false);
		_eventsContentRT = contentGO.AddComponent<RectTransform>();
		_eventsContentRT.anchorMin = new Vector2(0f, 1f);
		_eventsContentRT.anchorMax = new Vector2(1f, 1f);
		_eventsContentRT.pivot = new Vector2(0.5f, 1f);
		_eventsContentRT.sizeDelta = new Vector2(0f, 0f);
		VerticalLayoutGroup cl = contentGO.AddComponent<VerticalLayoutGroup>();
		cl.spacing = 6f;
		cl.padding = new RectOffset(10, 10, 10, 10);
		cl.childForceExpandWidth = true;
		cl.childForceExpandHeight = false;
		contentGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		_eventsScroll = scrollGO.AddComponent<ScrollRect>();
		_eventsScroll.horizontal = false;
		_eventsScroll.vertical = true;
		_eventsScroll.movementType = ScrollRect.MovementType.Clamped;
		_eventsScroll.scrollSensitivity = 30f;
		_eventsScroll.content = _eventsContentRT;
		_eventsScroll.viewport = vpRT;
		GameObject bottomBar = MakePanel(_eventsPanel.transform, "EventsBottom", 0f, 52f, new Color(0.14f, 0.14f, 0.17f));
		StretchBottom(bottomBar, 52f);
		HorizontalLayoutGroup bbLayout = bottomBar.AddComponent<HorizontalLayoutGroup>();
		bbLayout.padding = new RectOffset(10, 10, 8, 8);
		bbLayout.spacing = 10f;
		bbLayout.childAlignment = TextAnchor.MiddleRight;
		bbLayout.childForceExpandWidth = false;
		bbLayout.childForceExpandHeight = true;
		_eventsMainlineHintText = CreateText(bottomBar.transform, GetMainlineEventsHintLabel(waitingBoundAnchor: false), 13, TEXT_SECONDARY);
		_eventsMainlineHintText.gameObject.GetComponent<LayoutElement>().flexibleWidth = 1f;
		_eventsGenerateMainlineBtn = MakeLayoutButton(bottomBar.transform, GetMainlineEventsGenerateButtonLabel(waitingBoundAnchor: false), 100f, 34f, new Color(0.85f, 0.55f, 0.18f, 1f));
		_eventsGenerateMainlineBtn.onClick.AddListener(OnGenerateMainline);
		_eventsPanel.SetActive(value: false);
	}

	private void OnShowNpcEvents()
	{
		if (_eventsPanel == null)
		{
			return;
		}
		if (AIQuestManager.Instance != null && AIQuestManager.Instance.IsGenerating)
		{
			ShowEventsLoading(_loadingCurrentMessage, resetProgressState: false);
			return;
		}
		NPCInfo npcInfo = AIChatManager.npcInfo;
		if (npcInfo == null || npcInfo.NpcID == 0)
		{
			AIChatManager.logger.LogWarning((object)"[UIManager] 无法显示生平：NPC 信息为空");
			return;
		}
		if (_eventsContentRT != null)
		{
			for (int i = _eventsContentRT.childCount - 1; i >= 0; i--)
			{
				UnityEngine.Object.Destroy(_eventsContentRT.GetChild(i).gameObject);
			}
		}
		List<UINPCEventData> events = npcInfo.Events;
		if (events == null || events.Count == 0)
		{
			CreateText(_eventsContentRT, npcInfo.Name + " 暂无记录的生平事件。", 14, TEXT_SECONDARY, FontStyle.Italic);
		}
		else
		{
			List<UINPCEventData> sortedEvents = events.OrderByDescending((UINPCEventData e) => e.EventTime).ToList();
			CreateText(_eventsContentRT, $"{npcInfo.Name} 的生平 ({sortedEvents.Count} 条记录)", 16, TEXT_PRIMARY, FontStyle.Bold);
			foreach (UINPCEventData evt in sortedEvents)
			{
				GameObject card = MakePanel(_eventsContentRT, "EventCard", 0f, 0f, new Color(0.2f, 0.2f, 0.26f, 0.8f));
				VerticalLayoutGroup cardLayout = card.AddComponent<VerticalLayoutGroup>();
				cardLayout.padding = new RectOffset(12, 12, 8, 8);
				cardLayout.spacing = 4f;
				cardLayout.childForceExpandWidth = true;
				cardLayout.childForceExpandHeight = false;
				card.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
				card.AddComponent<LayoutElement>().flexibleWidth = 1f;
				string timeStr = FormatNpcEventDateText(evt);
				CreateText(card.transform, timeStr, 12, new Color(0.6f, 0.75f, 1f), FontStyle.Italic);
				string desc = ((!string.IsNullOrEmpty(evt.EventDesc)) ? evt.EventDesc : "(无描述)");
				CreateText(card.transform, desc, 14, TEXT_PRIMARY);
			}
		}
		RefreshEventsMainlineActionState(npcInfo);
		_eventsPanel.SetActive(value: true);
		Canvas.ForceUpdateCanvases();
		if (_eventsScroll != null)
		{
			_eventsScroll.verticalNormalizedPosition = 1f;
		}
	}

	private void RefreshEventsMainlineActionState(NPCInfo npcInfo)
	{
		string status;
		bool waitingBoundAnchor = AIQuestManager.Instance != null && npcInfo != null && AIQuestManager.Instance.IsWaitingMainlineAnchorForNpc(npcInfo.NpcID, out status);
		if (_eventsMainlineHintText != null)
		{
			_eventsMainlineHintText.text = GetMainlineEventsHintLabel(waitingBoundAnchor);
		}
		if (_eventsGenerateMainlineBtn != null)
		{
			Text label = _eventsGenerateMainlineBtn.GetComponentInChildren<Text>();
			if (label != null)
			{
				label.text = GetMainlineEventsGenerateButtonLabel(waitingBoundAnchor);
			}
		}
	}

	private void OnGenerateMainline()
	{
		NPCInfo npcInfo = AIChatManager.npcInfo;
		if (AIQuestManager.Instance != null && npcInfo != null && AIQuestManager.Instance.IsWaitingMainlineAnchorForNpc(npcInfo.NpcID, out var waitingStatus))
		{
			ShowEventsInfo(waitingStatus);
			RefreshEventsMainlineActionState(npcInfo);
		}
		else if (ShouldRequireExperimentalMainlineConfirmation(ConfigManager.experimentalMainlineGenerationEnabled.Value))
		{
			ShowMainlineExperimentalWarningPanel();
		}
		else
		{
			StartMainlineGenerationFromEventsPanel();
		}
	}

	private void StartMainlineGenerationFromEventsPanel()
	{
		NPCInfo npcInfo = AIChatManager.npcInfo;
		if (npcInfo == null || npcInfo.NpcID == 0)
		{
			AIChatManager.logger.LogWarning((object)"[UIManager] 无法推演主线：NPC 信息为空");
			return;
		}
		if (npcInfo.Events == null || npcInfo.Events.Count == 0)
		{
			AIChatManager.logger.LogWarning((object)"[UIManager] 无法推演主线：NPC 没有生平事件");
			return;
		}
		if (AIQuestManager.Instance == null)
		{
			AIChatManager.logger.LogError((object)"[UIManager] AIQuestManager 实例不存在");
			return;
		}
		if (!QuestCyMailHelper.HasNpcCyFriend(npcInfo.NpcID))
		{
			AIChatManager.logger.LogInfo((object)$"[UIManager] 阻止推演主线：未添加 NPC 传音符, npc={npcInfo.NpcID}/{npcInfo.Name}");
			try
			{
				UIPopTip.Inst?.Pop("尚未取得传音符，无法以此人为锚点推演主线。");
				return;
			}
			catch (Exception arg)
			{
				ManualLogSource logger = AIChatManager.logger;
				if (logger != null)
				{
					logger.LogWarning((object)$"[UIManager] 推演主线阻止提示失败: {arg}");
				}
				return;
			}
		}
		if (AIQuestManager.Instance.IsGenerating)
		{
			AIChatManager.logger.LogInfo((object)"[UIManager] 主线生成中，切回生成面板");
			ShowEventsLoading(_loadingCurrentMessage, resetProgressState: false);
			return;
		}
		ShowEventsLoading("正在推演主线...");
		string startStatusMessage;
		MainlineGenerationStartResult startResult = AIQuestManager.Instance.StartGenerateMainline(npcInfo, delegate(QuestProgram program)
		{
			AIChatManager.logger.LogInfo((object)("[UIManager] 主线 beat 生成完成: " + program?.Title));
			ShowQuestProgramPreview(program);
		}, delegate(string error)
		{
			AIChatManager.logger.LogError((object)("[UIManager] 主线 beat 生成失败: " + error));
			ShowEventsError("主线生成失败: " + error);
		}, delegate(string message, int current, int total, float stageProgress)
		{
			UpdateEventsLoadingProgress(message, current, total, stageProgress);
		}, out startStatusMessage);
		if (startResult == MainlineGenerationStartResult.WaitingForBiographyEvent)
		{
			AIChatManager.logger.LogInfo((object)("[UIManager] 主线锚点已进入等待官方生平事件状态: " + startStatusMessage));
			RefreshEventsMainlineActionState(npcInfo);
			ShowEventsInfo(startStatusMessage);
		}
	}

	private void BuildMainlineExperimentalWarningPanel()
	{
		_mainlineExperimentalWarningPanel = MakePanel(_canvasGO.transform, "MainlineExperimentalWarningPanel", 0f, 0f, new Color(0f, 0f, 0f, 0.72f));
		RectTransform overlayRT = _mainlineExperimentalWarningPanel.GetComponent<RectTransform>();
		overlayRT.anchorMin = Vector2.zero;
		overlayRT.anchorMax = Vector2.one;
		overlayRT.offsetMin = Vector2.zero;
		overlayRT.offsetMax = Vector2.zero;
		_mainlineExperimentalWarningPanel.transform.SetAsLastSibling();
		GameObject dialog = MakePanel(_mainlineExperimentalWarningPanel.transform, "MainlineExperimentalWarningDialog", 540f, 290f, new Color(0.16f, 0.16f, 0.2f, 0.96f));
		RectTransform dialogRT = dialog.GetComponent<RectTransform>();
		Vector2 anchorMin = (dialogRT.anchorMax = new Vector2(0.5f, 0.5f));
		dialogRT.anchorMin = anchorMin;
		dialogRT.anchoredPosition = Vector2.zero;
		GameObject contentGO = new GameObject("MainlineExperimentalWarningContent");
		contentGO.transform.SetParent(dialog.transform, worldPositionStays: false);
		RectTransform contentRT = contentGO.AddComponent<RectTransform>();
		contentRT.anchorMin = Vector2.zero;
		contentRT.anchorMax = Vector2.one;
		contentRT.offsetMin = new Vector2(18f, 18f);
		contentRT.offsetMax = new Vector2(-18f, -18f);
		VerticalLayoutGroup layout = contentGO.AddComponent<VerticalLayoutGroup>();
		layout.spacing = 10f;
		layout.childForceExpandWidth = true;
		layout.childForceExpandHeight = false;
		layout.childAlignment = TextAnchor.UpperLeft;
		CreateText(contentGO.transform, "实验性主线推演警示", 20, new Color(1f, 0.82f, 0.42f), FontStyle.Bold);
		CreateText(contentGO.transform, GetExperimentalMainlineWarningMessage(), 15, TEXT_PRIMARY);
		CreateText(contentGO.transform, "确认后才会启用该功能；你也可以稍后在 Mod 配置中随时关闭。", 12, TEXT_SECONDARY);
		MakeSpacer(contentGO.transform, 6f);
		GameObject buttonRow = new GameObject("MainlineExperimentalWarningButtons");
		buttonRow.transform.SetParent(contentGO.transform, worldPositionStays: false);
		HorizontalLayoutGroup buttonLayout = buttonRow.AddComponent<HorizontalLayoutGroup>();
		buttonLayout.spacing = 10f;
		buttonLayout.childForceExpandWidth = true;
		buttonLayout.childForceExpandHeight = false;
		buttonLayout.childAlignment = TextAnchor.MiddleCenter;
		Button cancelBtn = MakeLayoutButton(buttonRow.transform, "取消", 0f, 38f, BTN_SECONDARY);
		cancelBtn.onClick.AddListener(HideMainlineExperimentalWarningPanel);
		Button confirmBtn = MakeLayoutButton(buttonRow.transform, "确认开启", 0f, 38f, BTN_DANGER);
		confirmBtn.onClick.AddListener(OnConfirmMainlineExperimentalWarning);
	}

	private void ShowMainlineExperimentalWarningPanel()
	{
		if (!(_mainlineExperimentalWarningPanel == null))
		{
			_mainlineExperimentalWarningPanel.transform.SetAsLastSibling();
			_mainlineExperimentalWarningPanel.SetActive(value: true);
		}
	}

	private void HideMainlineExperimentalWarningPanel()
	{
		if (_mainlineExperimentalWarningPanel != null)
		{
			_mainlineExperimentalWarningPanel.SetActive(value: false);
		}
	}

	private void OnConfirmMainlineExperimentalWarning()
	{
		ConfigManager.experimentalMainlineGenerationEnabled.Value = true;
		ConfigManager.SaveConfig();
		UpdateExperimentalMainlineControls();
		HideMainlineExperimentalWarningPanel();
		StartMainlineGenerationFromEventsPanel();
	}

	private void ShowEventsLoading(string message, bool resetProgressState = true)
	{
		if (resetProgressState)
		{
			_loadingCurrentStep = 0;
			_loadingTotalSteps = QUEST_LOADING_STAGE_NAMES.Length;
			_loadingCurrentMessage = (string.IsNullOrEmpty(message) ? "准备开始生成任务..." : message);
			_loadingElapsedSeconds = 0f;
		}
		else if (!string.IsNullOrEmpty(message))
		{
			_loadingCurrentMessage = message;
		}
		RebuildEventsLoadingView();
		if (_loadingCoroutine == null)
		{
			_loadingCoroutine = StartCoroutine(LoadingTimerAnimation());
		}
	}

	private void RebuildEventsLoadingView()
	{
		if (!(_eventsContentRT == null))
		{
			for (int i = _eventsContentRT.childCount - 1; i >= 0; i--)
			{
				UnityEngine.Object.Destroy(_eventsContentRT.GetChild(i).gameObject);
			}
			_loadingStageItemTexts.Clear();
			GameObject root = MakePanel(_eventsContentRT, "QuestLoadingRoot", 0f, 0f, Color.clear);
			VerticalLayoutGroup rootLayout = root.AddComponent<VerticalLayoutGroup>();
			rootLayout.padding = new RectOffset(24, 24, 24, 24);
			rootLayout.spacing = 12f;
			rootLayout.childAlignment = TextAnchor.UpperLeft;
			rootLayout.childForceExpandWidth = true;
			rootLayout.childForceExpandHeight = false;
			LayoutElement rootLE = root.AddComponent<LayoutElement>();
			rootLE.flexibleWidth = 1f;
			rootLE.minHeight = 600f;
			rootLE.preferredHeight = 600f;
			_loadingTitleText = CreateText(root.transform, "任务生成中", 30, new Color(0.95f, 0.9f, 0.55f), FontStyle.Bold);
			ConfigureLoadingText(_loadingTitleText, 30, 20, 42, TextAnchor.UpperLeft);
			_loadingProgressText = CreateText(root.transform, "已等待 0s", 22, TEXT_PRIMARY, FontStyle.Bold);
			ConfigureLoadingText(_loadingProgressText, 22, 15, 32, TextAnchor.UpperLeft);
			_loadingEstimateText = CreateText(root.transform, GetQuestLoadingEstimateText(), 19, new Color(0.78f, 0.82f, 0.9f));
			ConfigureLoadingText(_loadingEstimateText, 19, 13, 28, TextAnchor.UpperLeft);
			_loadingStageText = CreateText(root.transform, "准备进入生成流程", 22, new Color(0.72f, 0.87f, 1f), FontStyle.Bold);
			ConfigureLoadingText(_loadingStageText, 22, 15, 32, TextAnchor.UpperLeft);
			_loadingHintText = CreateText(root.transform, _loadingCurrentMessage, 17, TEXT_SECONDARY);
			ConfigureLoadingText(_loadingHintText, 17, 12, 24, TextAnchor.UpperLeft);
			GameObject stageList = MakePanel(root.transform, "QuestLoadingStageList", 0f, 0f, Color.clear);
			VerticalLayoutGroup stageListLayout = stageList.AddComponent<VerticalLayoutGroup>();
			stageListLayout.spacing = 6f;
			stageListLayout.childAlignment = TextAnchor.UpperLeft;
			stageListLayout.childForceExpandWidth = true;
			stageListLayout.childForceExpandHeight = false;
			stageList.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
			for (int j = 0; j < QUEST_LOADING_STAGE_NAMES.Length; j++)
			{
				Text stageText = CreateText(stageList.transform, string.Empty, 17, TEXT_SECONDARY);
				ConfigureLoadingText(stageText, 17, 12, 24, TextAnchor.UpperLeft);
				_loadingStageItemTexts.Add(stageText);
			}
			_eventsPanel.SetActive(value: true);
			_eventsPanel.transform.SetAsLastSibling();
			Canvas.ForceUpdateCanvases();
			if (_eventsScroll != null)
			{
				_eventsScroll.verticalNormalizedPosition = 1f;
			}
			RefreshLoadingDisplay();
		}
	}

	private void UpdateEventsLoadingProgress(string message, int currentStep, int totalSteps, float stageProgress = -1f)
	{
		_loadingCurrentStep = Mathf.Max(0, currentStep);
		_loadingTotalSteps = Mathf.Max(1, totalSteps);
		if (!string.IsNullOrEmpty(message))
		{
			_loadingCurrentMessage = message;
		}
		RefreshLoadingDisplay();
	}

	private IEnumerator LoadingTimerAnimation()
	{
		while (true)
		{
			_loadingElapsedSeconds += 0.12f;
			RefreshLoadingDisplay();
			yield return new WaitForSecondsRealtime(0.12f);
		}
	}

	private void StopLoadingTimer()
	{
		if (_loadingCoroutine != null)
		{
			StopCoroutine(_loadingCoroutine);
			_loadingCoroutine = null;
		}
		_loadingTitleText = null;
		_loadingStageText = null;
		_loadingProgressText = null;
		_loadingEstimateText = null;
		_loadingHintText = null;
		_loadingStageItemTexts.Clear();
		_loadingCurrentMessage = "准备开始生成任务...";
		_loadingCurrentStep = 0;
		_loadingTotalSteps = QUEST_LOADING_STAGE_NAMES.Length;
		_loadingElapsedSeconds = 0f;
	}

	private void RefreshLoadingDisplay()
	{
		if (_loadingTitleText != null)
		{
			_loadingTitleText.text = "任务生成中";
		}
		if (_loadingStageText != null)
		{
			if (_loadingCurrentStep <= 0)
			{
				_loadingStageText.text = "准备进入生成流程";
			}
			else
			{
				_loadingStageText.text = $"第 {_loadingCurrentStep}/{Mathf.Max(1, _loadingTotalSteps)} 阶段 · {GetQuestLoadingStageName(_loadingCurrentStep)}";
			}
		}
		if (_loadingProgressText != null)
		{
			_loadingProgressText.text = "已等待 " + FormatQuestLoadingElapsed(_loadingElapsedSeconds);
		}
		if (_loadingEstimateText != null)
		{
			_loadingEstimateText.text = GetQuestLoadingEstimateText();
		}
		if (_loadingHintText != null)
		{
			_loadingHintText.text = (string.IsNullOrEmpty(_loadingCurrentMessage) ? "准备开始生成任务..." : _loadingCurrentMessage);
		}
		for (int i = 0; i < _loadingStageItemTexts.Count; i++)
		{
			Text stageText = _loadingStageItemTexts[i];
			int stageNumber = i + 1;
			string stageName = GetQuestLoadingStageName(stageNumber);
			if (stageNumber < _loadingCurrentStep)
			{
				stageText.text = "✓ " + stageName;
				stageText.color = new Color(0.52f, 0.88f, 0.58f);
				stageText.fontStyle = FontStyle.Normal;
			}
			else if (stageNumber == _loadingCurrentStep && _loadingCurrentStep > 0)
			{
				stageText.text = "● " + stageName;
				stageText.color = new Color(0.72f, 0.87f, 1f);
				stageText.fontStyle = FontStyle.Bold;
			}
			else
			{
				stageText.text = "○ " + stageName;
				stageText.color = TEXT_SECONDARY;
				stageText.fontStyle = FontStyle.Normal;
			}
		}
	}

	private string GetQuestLoadingStageName(int stepNumber)
	{
		int index = Mathf.Clamp(stepNumber - 1, 0, QUEST_LOADING_STAGE_NAMES.Length - 1);
		return QUEST_LOADING_STAGE_NAMES[index];
	}

	private void ConfigureLoadingText(Text text, int baseSize, int minSize, int maxSize, TextAnchor alignment)
	{
		if (!(text == null))
		{
			float scale = Mathf.Clamp(Mathf.Min((float)Screen.width / 1920f, (float)Screen.height / 1080f), 0.9f, 1.55f);
			int scaledMax = Mathf.Clamp(Mathf.RoundToInt((float)baseSize * scale), minSize, maxSize);
			int scaledMin = Mathf.Clamp(Mathf.RoundToInt((float)scaledMax * 0.72f), minSize, scaledMax);
			text.alignment = alignment;
			text.resizeTextForBestFit = true;
			text.resizeTextMinSize = scaledMin;
			text.resizeTextMaxSize = scaledMax;
			text.horizontalOverflow = HorizontalWrapMode.Wrap;
			text.verticalOverflow = VerticalWrapMode.Overflow;
		}
	}

	private string FormatQuestLoadingElapsed(float elapsedSeconds)
	{
		int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(elapsedSeconds));
		if (totalSeconds < 60)
		{
			return $"{totalSeconds}s";
		}
		int minutes = totalSeconds / 60;
		int seconds = totalSeconds % 60;
		return $"{minutes}m {seconds:D2}s";
	}

	private string GetQuestLoadingEstimateText()
	{
		string providerName = ConfigManager.selectedProviderConfig?.Value ?? "当前供应商";
		int minSeconds = 300;
		int maxSeconds = 600;
		switch (providerName)
		{
		case "Groq":
			minSeconds = 300;
			maxSeconds = 360;
			break;
		case "DeepSeek":
		case "OpenAI":
		case "Mistral AI":
			minSeconds = 300;
			maxSeconds = 420;
			break;
		case "通义千问 (Qwen)":
		case "智谱AI (GLM)":
		case "月之暗面 (Kimi)":
		case "百川智能":
		case "零一万物 (Yi)":
		case "Google Gemini":
			minSeconds = 330;
			maxSeconds = 480;
			break;
		case "MiniMax":
		case "豆包 (字节)":
			minSeconds = 360;
			maxSeconds = 540;
			break;
		case "讯飞星火":
		case "Anthropic Claude":
		case "Together AI":
		case "Cohere":
			minSeconds = 420;
			maxSeconds = 600;
			break;
		default:
			minSeconds = 300;
			maxSeconds = 600;
			break;
		}
		return $"当前供应商：{providerName}，预计等待 {minSeconds}-{maxSeconds}s";
	}

	private void ShowEventsError(string message)
	{
		StopLoadingTimer();
		if (!(_eventsContentRT == null))
		{
			for (int i = _eventsContentRT.childCount - 1; i >= 0; i--)
			{
				UnityEngine.Object.Destroy(_eventsContentRT.GetChild(i).gameObject);
			}
			CreateText(_eventsContentRT, "❌ " + message, 14, new Color(1f, 0.4f, 0.4f));
			CreateText(_eventsContentRT, "请关闭面板后重试", 13, TEXT_SECONDARY);
		}
	}

	private void ShowEventsInfo(string message)
	{
		StopLoadingTimer();
		if (!(_eventsContentRT == null))
		{
			for (int i = _eventsContentRT.childCount - 1; i >= 0; i--)
			{
				UnityEngine.Object.Destroy(_eventsContentRT.GetChild(i).gameObject);
			}
			CreateText(_eventsContentRT, "\ud83e\udded " + message, 14, new Color(0.95f, 0.82f, 0.42f));
			CreateText(_eventsContentRT, "后续可在主线追踪栏查看等待状态。", 13, TEXT_SECONDARY);
		}
	}

	private void ShowQuestProgramPreview(QuestProgram program)
	{
		StopLoadingTimer();
		if (_eventsContentRT == null || program == null)
		{
			return;
		}
		for (int i = _eventsContentRT.childCount - 1; i >= 0; i--)
		{
			UnityEngine.Object.Destroy(_eventsContentRT.GetChild(i).gameObject);
		}
		CreateText(_eventsContentRT, "\ud83d\udcd6 " + program.Title, 18, new Color(1f, 0.85f, 0.3f), FontStyle.Bold);
		CreateText(_eventsContentRT, $"主线 · 节点数：{program.Nodes?.Count ?? 0}", 13, TEXT_SECONDARY);
		if (program.AnchorNpcId > 0)
		{
			CreateText(_eventsContentRT, $"锚点人物：{program.AnchorNpcName} (ID:{program.AnchorNpcId})", 13, TEXT_SECONDARY);
		}
		if (!string.IsNullOrWhiteSpace(program.Summary))
		{
			GameObject storyCard = MakePanel(_eventsContentRT, "ProgramSummaryCard", 0f, 0f, new Color(0.18f, 0.22f, 0.3f, 0.9f));
			VerticalLayoutGroup storyLayout = storyCard.AddComponent<VerticalLayoutGroup>();
			storyLayout.padding = new RectOffset(12, 12, 8, 8);
			storyLayout.childForceExpandWidth = true;
			storyLayout.childForceExpandHeight = false;
			storyCard.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
			storyCard.AddComponent<LayoutElement>().flexibleWidth = 1f;
			CreateText(storyCard.transform, program.Summary, 14, TEXT_PRIMARY);
		}
		if (program.WorldContract != null)
		{
			string contractSummary = $"世界契约：NPC {program.WorldContract.AllowedNpcIds?.Count ?? 0} 个，地点 {program.WorldContract.AllowedSceneIds?.Count ?? 0} 个";
			CreateText(_eventsContentRT, contractSummary, 13, TEXT_SECONDARY);
		}
		GameObject btnRow = MakePanel(_eventsContentRT, "ProgramPreviewBtnRow", 0f, 0f, Color.clear);
		HorizontalLayoutGroup btnLayout = btnRow.AddComponent<HorizontalLayoutGroup>();
		btnLayout.spacing = 10f;
		btnLayout.childAlignment = TextAnchor.MiddleCenter;
		btnLayout.childForceExpandWidth = false;
		btnLayout.childForceExpandHeight = false;
		btnRow.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		btnRow.AddComponent<LayoutElement>().flexibleWidth = 1f;
		Button acceptBtn = MakeLayoutButton(btnRow.transform, "接受主线", 120f, 36f, BTN_COLOR);
		acceptBtn.onClick.AddListener(delegate
		{
			if (AIQuestManager.Instance != null && AIQuestManager.Instance.AcceptQuestProgram(program.ProgramId))
			{
				_eventsPanel.SetActive(value: false);
			}
		});
		Button rejectBtn = MakeLayoutButton(btnRow.transform, "拒绝主线", 120f, 36f, BTN_DANGER);
		rejectBtn.onClick.AddListener(delegate
		{
			if (AIQuestManager.Instance != null)
			{
				AIQuestManager.Instance.RejectQuestProgram(program.ProgramId);
				_eventsPanel.SetActive(value: false);
			}
		});
		Canvas.ForceUpdateCanvases();
		if (_eventsScroll != null)
		{
			_eventsScroll.verticalNormalizedPosition = 1f;
		}
	}

	private void BuildFriendListPanel()
	{
		_friendPanel = MakePanel(_canvasGO.transform, "FriendPanel", 860f, 810f, new Color(0.1f, 0.12f, 0.16f, 0.95f));
		RectTransform frt = _friendPanel.GetComponent<RectTransform>();
		Vector2 anchorMin = (frt.anchorMax = new Vector2(0.5f, 0.5f));
		frt.anchorMin = anchorMin;
		frt.anchoredPosition = new Vector2(-170f, 0f);
		GameObject header = MakePanel(_friendPanel.transform, "FriendHeader", 0f, 48f, HEADER_COLOR);
		StretchTop(header, 48f);
		Text titleTxt = CreateText(header.transform, "\ud83d\udc65 好友列表", 18, TEXT_PRIMARY, FontStyle.Bold);
		StretchFill(titleTxt.gameObject, 15f, 0f, -55f, 0f);
		titleTxt.alignment = TextAnchor.MiddleLeft;
		Button closeFriendBtn = MakeButton(header.transform, "X", 38f, 34f, BTN_DANGER);
		AnchorRight(closeFriendBtn, -8f, 38f, 34f);
		closeFriendBtn.onClick.AddListener(delegate
		{
			_friendPanel.SetActive(value: false);
		});
		GameObject scrollGO = new GameObject("FriendScrollView");
		scrollGO.transform.SetParent(_friendPanel.transform, worldPositionStays: false);
		scrollGO.AddComponent<Image>().color = new Color(0.13f, 0.13f, 0.16f, 0.3f);
		RectTransform scrollRT = scrollGO.GetComponent<RectTransform>();
		scrollRT.anchorMin = Vector2.zero;
		scrollRT.anchorMax = Vector2.one;
		scrollRT.offsetMin = new Vector2(8f, 8f);
		scrollRT.offsetMax = new Vector2(-8f, -48f);
		GameObject vpGO = new GameObject("Viewport");
		vpGO.transform.SetParent(scrollGO.transform, worldPositionStays: false);
		vpGO.AddComponent<Image>().color = Color.white;
		vpGO.AddComponent<RectMask2D>();
		RectTransform vpRT = vpGO.GetComponent<RectTransform>();
		vpRT.anchorMin = Vector2.zero;
		vpRT.anchorMax = Vector2.one;
		vpRT.offsetMin = Vector2.zero;
		vpRT.offsetMax = Vector2.zero;
		GameObject contentGO = new GameObject("Content");
		contentGO.transform.SetParent(vpGO.transform, worldPositionStays: false);
		_friendContentRT = contentGO.AddComponent<RectTransform>();
		_friendContentRT.anchorMin = new Vector2(0f, 1f);
		_friendContentRT.anchorMax = new Vector2(1f, 1f);
		_friendContentRT.pivot = new Vector2(0.5f, 1f);
		_friendContentRT.sizeDelta = new Vector2(0f, 0f);
		VerticalLayoutGroup cl = contentGO.AddComponent<VerticalLayoutGroup>();
		cl.spacing = 6f;
		cl.padding = new RectOffset(10, 10, 10, 10);
		cl.childForceExpandWidth = true;
		cl.childForceExpandHeight = false;
		contentGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		_friendScroll = scrollGO.AddComponent<ScrollRect>();
		_friendScroll.horizontal = false;
		_friendScroll.vertical = true;
		_friendScroll.movementType = ScrollRect.MovementType.Clamped;
		_friendScroll.scrollSensitivity = 30f;
		_friendScroll.content = _friendContentRT;
		_friendScroll.viewport = vpRT;
		_friendPanel.SetActive(value: false);
	}

	private void OnShowFriendList()
	{
		if (!(_friendPanel == null))
		{
			RefreshFriendList();
			_friendPanel.SetActive(value: true);
		}
	}

	private void RefreshFriendList()
	{
		if (_friendContentRT == null)
		{
			return;
		}
		for (int i = _friendContentRT.childCount - 1; i >= 0; i--)
		{
			UnityEngine.Object.Destroy(_friendContentRT.GetChild(i).gameObject);
		}
		try
		{
			string dataPath = Application.persistentDataPath;
			if (!Directory.Exists(dataPath))
			{
				CreateText(_friendContentRT, "数据目录不存在", 14, TEXT_SECONDARY);
				return;
			}
			List<NpcFriendEntry> npcEntries = (from summary in NPCDialog.BuildDialogHistorySummaries(dataPath)
				select new NpcFriendEntry
				{
					NpcId = summary.NpcId,
					NpcName = summary.NpcName,
					MessageCount = summary.MessageCount,
					LastGameDate = summary.LastGameDate,
					LastGameTimeText = summary.LastGameTimeText
				}).ToList();
			if (npcEntries.Count == 0)
			{
				CreateText(_friendContentRT, "还没有对话过的 NPC", 16, TEXT_SECONDARY, FontStyle.Italic);
				CreateText(_friendContentRT, "与 NPC 交谈后，他们会出现在这里", 13, TEXT_SECONDARY);
				return;
			}
			npcEntries.Sort((NpcFriendEntry a, NpcFriendEntry b) => b.LastGameDate.CompareTo(a.LastGameDate));
			int activeNpcId = AIChatManager.npcInfo?.NpcID ?? (-1);
			foreach (NpcFriendEntry entry in npcEntries)
			{
				bool isActive = entry.NpcId == activeNpcId;
				Color cardBg = (isActive ? new Color(0.2f, 0.3f, 0.4f, 0.9f) : new Color(0.18f, 0.2f, 0.26f, 0.8f));
				GameObject card = MakePanel(_friendContentRT, $"Friend_{entry.NpcId}", 0f, 0f, cardBg);
				HorizontalLayoutGroup cardLayout = card.AddComponent<HorizontalLayoutGroup>();
				cardLayout.padding = new RectOffset(14, 14, 10, 10);
				cardLayout.spacing = 12f;
				cardLayout.childAlignment = TextAnchor.MiddleLeft;
				cardLayout.childForceExpandWidth = false;
				cardLayout.childForceExpandHeight = false;
				card.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
				card.AddComponent<LayoutElement>().flexibleWidth = 1f;
				GameObject infoGO = new GameObject("Info");
				infoGO.transform.SetParent(card.transform, worldPositionStays: false);
				VerticalLayoutGroup infoLayout = infoGO.AddComponent<VerticalLayoutGroup>();
				infoLayout.spacing = 2f;
				infoLayout.childForceExpandWidth = true;
				infoLayout.childForceExpandHeight = false;
				infoGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
				LayoutElement infoLE = infoGO.AddComponent<LayoutElement>();
				infoLE.flexibleWidth = 1f;
				string statusMark = (isActive ? " \ud83d\udcac" : "");
				CreateText(infoGO.transform, entry.NpcName + statusMark, 16, isActive ? new Color(0.7f, 0.9f, 1f) : TEXT_PRIMARY, FontStyle.Bold);
				CreateText(infoGO.transform, $"ID:{entry.NpcId}  |  {entry.MessageCount} 条消息  |  上次对话:{entry.LastGameTimeText}", 12, TEXT_SECONDARY);
				if (!isActive)
				{
					NpcFriendEntry switchEntry = entry;
					Button switchBtn = MakeLayoutButton(card.transform, "切换", 70f, 34f, new Color(0.3f, 0.5f, 0.7f, 1f));
					switchBtn.onClick.AddListener(delegate
					{
						SwitchToNpc(switchEntry.NpcId, switchEntry.NpcName);
						_friendPanel.SetActive(value: false);
					});
				}
				else
				{
					CreateText(card.transform, "当前", 14, new Color(0.5f, 0.9f, 0.5f), FontStyle.Bold);
				}
			}
		}
		catch (Exception ex)
		{
			AIChatManager.logger.LogError((object)$"[UIManager] 好友列表刷新失败: {ex}");
			CreateText(_friendContentRT, "加载失败: " + ex.Message, 14, new Color(1f, 0.4f, 0.4f));
		}
		Canvas.ForceUpdateCanvases();
		if (_friendScroll != null)
		{
			_friendScroll.verticalNormalizedPosition = 1f;
		}
	}

	private void SwitchToNpc(int npcId, string npcName)
	{
		AIChatManager.logger.LogInfo((object)$"[UIManager] 切换到 NPC: {npcName} (ID:{npcId})");
		if ((UnityEngine.Object)(object)AIChatManager.Instance != null)
		{
			AIChatManager.Instance.StartChatWithNpc(npcId);
		}
		ShowChatPanel(AIChatManager.npcInfo?.Name ?? npcName);
		if (_chatPanel != null)
		{
			_chatPanel.SetActive(value: true);
		}
	}

	private string FormatNpcEventDateText(UINPCEventData evt)
	{
		if (evt == null)
		{
			return "0001年01月01日";
		}
		if (evt.EventTime != DateTime.MinValue)
		{
			return evt.EventTime.ToString("yyyy年MM月dd日");
		}
		if (!string.IsNullOrWhiteSpace(evt.EventTimeStr))
		{
			return NPCDialog.NormalizeGameTimeTextValue(evt.EventTimeStr);
		}
		return "0001年01月01日";
	}

	private string GetLastDialogGameTimeText(string[] historyLines)
	{
		if (historyLines == null || historyLines.Length == 0)
		{
			return "0001年01月01日";
		}
		for (int i = historyLines.Length - 1; i >= 0; i--)
		{
			if (NPCDialog.TryGetGameTimeTextFromHistoryLine(historyLines[i], out var gameTimeText))
			{
				return gameTimeText;
			}
		}
		return "0001年01月01日";
	}

	private void SaveCurrentKeyToProvider()
	{
		if (!(_apiKeyInput == null) && _selectedProviderIdx >= 0 && _selectedProviderIdx < ConfigManager.Providers.Count)
		{
			string name = ConfigManager.Providers[_selectedProviderIdx].DisplayName;
			string key = _apiKeyInput.text.Trim();
			if (!string.IsNullOrEmpty(key))
			{
				ConfigManager.SetApiKey(name, key);
			}
		}
	}

	private void SaveCurrentModelToProvider()
	{
		if (!(_modelNameInput == null) && _selectedProviderIdx >= 0 && _selectedProviderIdx < ConfigManager.Providers.Count)
		{
			string name = ConfigManager.Providers[_selectedProviderIdx].DisplayName;
			ConfigManager.SetSavedModelName(name, _modelNameInput.text.Trim());
		}
	}

	private void LoadConfigToUI()
	{
		try
		{
			string currentProvider = ConfigManager.selectedProviderConfig?.Value ?? "DeepSeek";
			int idx = ConfigManager.Providers.FindIndex((ProviderPreset p) => p.DisplayName == currentProvider);
			if (idx < 0)
			{
				idx = ConfigManager.Providers.Count - 1;
			}
			_selectedProviderIdx = idx;
			if (_providerBtnText != null)
			{
				_providerBtnText.text = ConfigManager.Providers[idx].DisplayName;
			}
			if (_endpointInput != null)
			{
				_endpointInput.text = ConfigManager.apiEndpointConfig?.Value ?? "";
			}
			_selectedCompatibilityProfile = ConfigManager.NormalizeCompatibilityProfileValue(ConfigManager.customCompatibilityProfileConfig?.Value);
			if (_apiKeyInput != null)
			{
				_apiKeyInput.text = ConfigManager.GetApiKey(currentProvider);
			}
			if (_modelNameInput != null)
			{
				_modelNameInput.text = ConfigManager.GetSavedModelName(currentProvider);
			}
			UpdateCompatibilityProfileVisibility();
		}
		catch (Exception arg)
		{
			AIChatManager.logger.LogError((object)$"[UIManager] LoadConfigToUI 失败: {arg}");
		}
	}

	private void SetStatusText(string msg)
	{
		if (_statusText != null)
		{
			_statusText.text = msg;
		}
	}

	private GameObject MakePanel(Transform parent, string name, float w, float h, Color color)
	{
		GameObject go = new GameObject(name);
		go.transform.SetParent(parent, worldPositionStays: false);
		go.AddComponent<Image>().color = color;
		go.GetComponent<RectTransform>().sizeDelta = new Vector2(w, h);
		CanvasGroup cg = go.AddComponent<CanvasGroup>();
		cg.alpha = 1f;
		return go;
	}

	private Text CreateText(Transform parent, string text, int size, Color color, FontStyle style = FontStyle.Normal)
	{
		GameObject go = new GameObject("Txt");
		go.transform.SetParent(parent, worldPositionStays: false);
		Text t = go.AddComponent<Text>();
		t.text = text;
		t.font = GetFont();
		t.fontSize = size;
		t.color = color;
		t.fontStyle = style;
		t.horizontalOverflow = HorizontalWrapMode.Wrap;
		t.verticalOverflow = VerticalWrapMode.Overflow;
		go.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		go.AddComponent<LayoutElement>().flexibleWidth = 1f;
		return t;
	}

	private Button MakeButton(Transform parent, string label, float w, float h, Color bgColor)
	{
		GameObject go = new GameObject("Btn_" + label);
		go.transform.SetParent(parent, worldPositionStays: false);
		Image img = go.AddComponent<Image>();
		img.color = bgColor;
		Button btn = go.AddComponent<Button>();
		btn.targetGraphic = img;
		go.GetComponent<RectTransform>().sizeDelta = new Vector2(w, h);
		GameObject tGO = new GameObject("Lbl");
		tGO.transform.SetParent(go.transform, worldPositionStays: false);
		Text t = tGO.AddComponent<Text>();
		t.text = label;
		t.font = GetFont();
		t.fontSize = 14;
		t.color = TEXT_PRIMARY;
		t.alignment = TextAnchor.MiddleCenter;
		RectTransform tRT = tGO.GetComponent<RectTransform>();
		tRT.anchorMin = Vector2.zero;
		tRT.anchorMax = Vector2.one;
		tRT.offsetMin = Vector2.zero;
		tRT.offsetMax = Vector2.zero;
		return btn;
	}

	private Button MakeLayoutButton(Transform parent, string label, float w, float h, Color bgColor)
	{
		Button btn = MakeButton(parent, label, w, h, bgColor);
		LayoutElement le = btn.gameObject.AddComponent<LayoutElement>();
		if (w > 0f)
		{
			le.minWidth = w;
		}
		le.preferredHeight = h;
		le.flexibleWidth = 1f;
		return btn;
	}

	private InputField MakeInputField(Transform parent, string ph, float w, float h)
	{
		GameObject go = new GameObject("Input");
		go.transform.SetParent(parent, worldPositionStays: false);
		go.AddComponent<Image>().color = INPUT_BG;
		if (w > 0f && h > 0f)
		{
			go.GetComponent<RectTransform>().sizeDelta = new Vector2(w, h);
		}
		LayoutElement le = go.AddComponent<LayoutElement>();
		if (h > 0f)
		{
			le.preferredHeight = h;
		}
		le.flexibleWidth = 1f;
		GameObject tGO = new GameObject("Text");
		tGO.transform.SetParent(go.transform, worldPositionStays: false);
		Text t = tGO.AddComponent<Text>();
		t.font = GetFont();
		t.fontSize = 14;
		t.color = TEXT_PRIMARY;
		t.supportRichText = false;
		t.horizontalOverflow = HorizontalWrapMode.Overflow;
		t.verticalOverflow = VerticalWrapMode.Overflow;
		RectTransform tRT = tGO.GetComponent<RectTransform>();
		tRT.anchorMin = Vector2.zero;
		tRT.anchorMax = Vector2.one;
		tRT.offsetMin = new Vector2(8f, 2f);
		tRT.offsetMax = new Vector2(-8f, -2f);
		GameObject phGO = new GameObject("Placeholder");
		phGO.transform.SetParent(go.transform, worldPositionStays: false);
		Text pt = phGO.AddComponent<Text>();
		pt.text = ph;
		pt.font = GetFont();
		pt.fontSize = 14;
		pt.fontStyle = FontStyle.Italic;
		pt.color = TEXT_SECONDARY;
		pt.horizontalOverflow = HorizontalWrapMode.Overflow;
		RectTransform pRT = phGO.GetComponent<RectTransform>();
		pRT.anchorMin = Vector2.zero;
		pRT.anchorMax = Vector2.one;
		pRT.offsetMin = new Vector2(8f, 2f);
		pRT.offsetMax = new Vector2(-8f, -2f);
		InputField input = go.AddComponent<InputField>();
		input.textComponent = t;
		input.placeholder = pt;
		return input;
	}

	private void MakeSpacer(Transform parent, float height)
	{
		GameObject go = new GameObject("Spacer");
		go.transform.SetParent(parent, worldPositionStays: false);
		go.AddComponent<LayoutElement>().preferredHeight = height;
	}

	private GameObject MakeScrollableList(Transform parent, string name, float maxHeight)
	{
		GameObject go = new GameObject(name);
		go.transform.SetParent(parent, worldPositionStays: false);
		go.AddComponent<Image>().color = new Color(0.18f, 0.18f, 0.22f, 1f);
		LayoutElement le = go.AddComponent<LayoutElement>();
		le.preferredHeight = maxHeight;
		le.flexibleWidth = 1f;
		GameObject vpGO = new GameObject("Viewport");
		vpGO.transform.SetParent(go.transform, worldPositionStays: false);
		vpGO.AddComponent<Image>().color = Color.clear;
		vpGO.AddComponent<RectMask2D>();
		RectTransform vpRT = vpGO.GetComponent<RectTransform>();
		vpRT.anchorMin = Vector2.zero;
		vpRT.anchorMax = Vector2.one;
		vpRT.offsetMin = Vector2.zero;
		vpRT.offsetMax = Vector2.zero;
		GameObject contentGO = new GameObject("Content");
		contentGO.transform.SetParent(vpGO.transform, worldPositionStays: false);
		RectTransform contentRT = contentGO.AddComponent<RectTransform>();
		contentRT.anchorMin = new Vector2(0f, 1f);
		contentRT.anchorMax = new Vector2(1f, 1f);
		contentRT.pivot = new Vector2(0.5f, 1f);
		contentRT.sizeDelta = new Vector2(0f, 0f);
		VerticalLayoutGroup cl = contentGO.AddComponent<VerticalLayoutGroup>();
		cl.spacing = 2f;
		cl.padding = new RectOffset(4, 4, 4, 4);
		cl.childForceExpandWidth = true;
		cl.childForceExpandHeight = false;
		contentGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		ScrollRect scroll = go.AddComponent<ScrollRect>();
		scroll.horizontal = false;
		scroll.vertical = true;
		scroll.movementType = ScrollRect.MovementType.Clamped;
		scroll.scrollSensitivity = 20f;
		scroll.content = contentRT;
		scroll.viewport = vpRT;
		return go;
	}

	private void StretchTop(GameObject go, float h)
	{
		RectTransform rt = go.GetComponent<RectTransform>();
		rt.anchorMin = new Vector2(0f, 1f);
		rt.anchorMax = new Vector2(1f, 1f);
		rt.pivot = new Vector2(0.5f, 1f);
		rt.anchoredPosition = Vector2.zero;
		rt.sizeDelta = new Vector2(0f, h);
	}

	private void StretchTopBelow(GameObject go, float topOffset, float h)
	{
		RectTransform rt = go.GetComponent<RectTransform>();
		rt.anchorMin = new Vector2(0f, 1f);
		rt.anchorMax = new Vector2(1f, 1f);
		rt.pivot = new Vector2(0.5f, 1f);
		rt.anchoredPosition = new Vector2(0f, 0f - topOffset);
		rt.sizeDelta = new Vector2(0f, h);
	}

	private void StretchBottom(GameObject go, float h)
	{
		RectTransform rt = go.GetComponent<RectTransform>();
		rt.anchorMin = new Vector2(0f, 0f);
		rt.anchorMax = new Vector2(1f, 0f);
		rt.pivot = new Vector2(0.5f, 0f);
		rt.anchoredPosition = Vector2.zero;
		rt.sizeDelta = new Vector2(0f, h);
	}

	private void StretchFill(GameObject go, float l, float b, float r, float t)
	{
		RectTransform rt = go.GetComponent<RectTransform>();
		rt.anchorMin = Vector2.zero;
		rt.anchorMax = Vector2.one;
		rt.offsetMin = new Vector2(l, b);
		rt.offsetMax = new Vector2(r, t);
	}

	private void AnchorRight(Button btn, float rightOffset, float w, float h)
	{
		RectTransform rt = btn.GetComponent<RectTransform>();
		rt.anchorMin = new Vector2(1f, 0.5f);
		rt.anchorMax = new Vector2(1f, 0.5f);
		rt.pivot = new Vector2(1f, 0.5f);
		rt.anchoredPosition = new Vector2(rightOffset, 0f);
		rt.sizeDelta = new Vector2(w, h);
	}
}
