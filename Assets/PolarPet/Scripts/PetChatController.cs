using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using ElevenLab;
using Newtonsoft.Json;
using PolarPet.Scripts.AICore.OpenAI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 寵物聊天控制：
/// - Tab 開關輸入框
/// - InputField 送出後呼叫 OpenAI
/// - 送出後清空輸入並關閉輸入框
/// - 等待回應時顯示思考中，回應後顯示到 PetDialog
/// </summary>
[DisallowMultipleComponent]
public sealed class PetChatController : MonoBehaviour
{
    enum ChatRole
    {
        User = 0,
        Assistant = 1
    }

    [System.Serializable]
    sealed class ChatTurn
    {
        public ChatRole role;
        public string content;
    }

    [Header("References")]
    [SerializeField] OpenAICore _openAI;
    [SerializeField] PetDialog _petDialog;
    [SerializeField] ElevenLabsCore _elevenLabs;
    [SerializeField] AudioSource _voiceAudioSource;

    [Header("Input UI")]
    [Tooltip("整個輸入框容器（建議指定）。未指定時使用 InputField 的 GameObject。")]
    [SerializeField] GameObject _inputRoot;
    [SerializeField] InputField _inputField;
    [SerializeField] bool _hideInputOnStart = true;
    [SerializeField] bool _lockInputWhileRequest = true;

    [Header("Prompt")]
    [TextArea(2, 6)]
    [SerializeField] string _systemHint = "你是可愛的北極熊寵物，回覆簡短、自然、溫暖。";

    [Header("RAG (Vector Store)")]
    [SerializeField] bool _enableRag;
    [Tooltip("OpenAI Vector Store ID（例如：vs_xxx）。啟用 RAG 後會用此資料庫進行檢索。")]
    [SerializeField] string _vectorStoreId;
    [Min(1)]
    [SerializeField] int _ragMaxNumResults = 3;

    [Header("Dialog Text")]
    [SerializeField] string _thinkingMessage = "...思考中（等一下下）";
    [SerializeField] string _emptyReplyMessage = "(沒有收到文字回應)";

    [Header("Emotion UI")]
    [SerializeField] Slider _emotionSlider;

    [Header("Voice (ElevenLabs)")]
    [SerializeField] bool _enableVoiceReply = true;
    [SerializeField] string _elevenLabsModelId = "eleven_multilingual_v2";

    [Header("Memory")]
    [SerializeField] bool _persistHistoryToPlayerPrefs = true;
    [SerializeField] string _historySaveKey = "PolarPet.ChatHistory";

    bool _isRequestInFlight;
    Coroutine _focusCoroutine;
    readonly List<ChatTurn> _history = new List<ChatTurn>(16);

#if ENABLE_INPUT_SYSTEM
    InputAction _toggleInputAction;
#endif

    void Awake()
    {
        if (_petDialog != null)
        {
            SpriteRenderer selfSpriteRenderer = GetComponent<SpriteRenderer>();
            if (selfSpriteRenderer != null)
                _petDialog.SetFollowTarget(selfSpriteRenderer);
        }

#if ENABLE_INPUT_SYSTEM
        _toggleInputAction = new InputAction("ToggleChatInput", InputActionType.Button, "<Keyboard>/tab");
#endif

        LoadHistory();
    }

    void OnEnable()
    {
        if (_inputField != null)
            _inputField.onEndEdit.AddListener(OnInputEndEdit);

#if ENABLE_INPUT_SYSTEM
        if (_toggleInputAction != null)
        {
            _toggleInputAction.performed += OnToggleInputPerformed;
            _toggleInputAction.Enable();
        }
#endif
    }

    void Start()
    {
        if (_hideInputOnStart)
            SetInputVisible(false);
    }

    void OnDisable()
    {
        if (_inputField != null)
            _inputField.onEndEdit.RemoveListener(OnInputEndEdit);

#if ENABLE_INPUT_SYSTEM
        if (_toggleInputAction != null)
        {
            _toggleInputAction.performed -= OnToggleInputPerformed;
            _toggleInputAction.Disable();
        }
#endif

        if (_focusCoroutine != null)
        {
            StopCoroutine(_focusCoroutine);
            _focusCoroutine = null;
        }
    }

#if !ENABLE_INPUT_SYSTEM
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
            ToggleInputVisibility();
    }
#endif

#if ENABLE_INPUT_SYSTEM
    void OnToggleInputPerformed(InputAction.CallbackContext _)
    {
        ToggleInputVisibility();
    }
#endif

    void ToggleInputVisibility()
    {
        bool nextVisible = !IsInputVisible();
        SetInputVisible(nextVisible);

        if (nextVisible)
            FocusInputField();
        else
            ClearSelectedInput();
    }

    void FocusInputField()
    {
        if (_inputField == null)
            return;

        if (_focusCoroutine != null)
            StopCoroutine(_focusCoroutine);
        _focusCoroutine = StartCoroutine(FocusInputFieldNextFrame());
    }

    IEnumerator FocusInputFieldNextFrame()
    {
        yield return null;

        _focusCoroutine = null;
        if (_inputField == null || !IsInputVisible())
            yield break;

        _inputField.Select();
        _inputField.ActivateInputField();
    }

    void OnInputEndEdit(string value)
    {
        if (!WasSubmitKeyPressedThisFrame())
            return;

        SendUserText(value);
    }

    void SendUserText(string rawText)
    {
        if (_isRequestInFlight)
            return;

        if (_openAI == null)
        {
            Debug.LogError("PetChatController: OpenAICore 參考未設定。");
            return;
        }

        if (_petDialog == null)
        {
            Debug.LogError("PetChatController: PetDialog 參考未設定。");
            return;
        }

        string userText = rawText != null ? rawText.Trim() : string.Empty;
        if (string.IsNullOrEmpty(userText))
            return;

        _isRequestInFlight = true;
        if (_lockInputWhileRequest && _inputField != null)
            _inputField.interactable = false;

        _petDialog.ShowText(_thinkingMessage, 0f);
        ClearAndCloseInput();

        string prompt = BuildPrompt(userText);
        _openAI.SendChat(
            prompt,
            OnReplyReceived,
            GetActiveVectorStoreId(),
            Mathf.Max(1, _ragMaxNumResults)
        );
    }

    string BuildPrompt(string userText)
    {
        AddHistory(ChatRole.User, userText);

        var builder = new StringBuilder(512);
        if (!string.IsNullOrWhiteSpace(_systemHint))
        {
            builder.Append("[System]\n");
            builder.Append(_systemHint.Trim());
            builder.Append("\n\n");
        }

        for (int i = 0; i < _history.Count; i++)
        {
            ChatTurn turn = _history[i];
            if (turn == null || string.IsNullOrWhiteSpace(turn.content))
                continue;

            builder.Append(turn.role == ChatRole.User ? "[User]\n" : "[Assistant]\n");
            builder.Append(turn.content);
            builder.Append("\n\n");
        }

        builder.Append("[Assistant]\n");
        return builder.ToString();
    }

    void OnReplyReceived(string reply)
    {
        _isRequestInFlight = false;
        if (_lockInputWhileRequest && _inputField != null)
            _inputField.interactable = true;

        AIPetResponse aiResponse = ParseAiPetResponse(reply);
        ApplyEmotionToSlider(aiResponse.Emotion);

        string safeReply = string.IsNullOrWhiteSpace(aiResponse.Content) ? _emptyReplyMessage : aiResponse.Content;
        AddHistory(ChatRole.Assistant, safeReply);

        if (_petDialog != null)
            _petDialog.ShowText(safeReply);

        if (!string.IsNullOrWhiteSpace(aiResponse.Content))
            SpeakAiReply(aiResponse.Content.Trim());
    }

    AIPetResponse ParseAiPetResponse(string rawReply)
    {
        if (string.IsNullOrWhiteSpace(rawReply))
            return new AIPetResponse { Content = null, Emotion = "neutral" };

        try
        {
            AIPetResponse parsed = JsonConvert.DeserializeObject<AIPetResponse>(rawReply);
            if (parsed == null)
                return new AIPetResponse { Content = rawReply, Emotion = "neutral" };

            if (string.IsNullOrWhiteSpace(parsed.Emotion))
                parsed.Emotion = "neutral";

            return parsed;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"PetChatController: AI 回傳非 JSON，改用純文字內容。{ex.Message}");
            return new AIPetResponse { Content = rawReply, Emotion = "neutral" };
        }
    }

    void ApplyEmotionToSlider(string emotion)
    {
        if (_emotionSlider == null)
            return;

        string normalizedEmotion = NormalizeEmotion(emotion);
        float nextValue = _emotionSlider.value;

        if (normalizedEmotion == "good")
            nextValue += 1f;
        else if (normalizedEmotion == "bad")
            nextValue -= 1f;

        nextValue = Mathf.Clamp(nextValue, _emotionSlider.minValue, _emotionSlider.maxValue);
        _emotionSlider.value = nextValue;
    }

    static string NormalizeEmotion(string emotion)
    {
        if (string.IsNullOrWhiteSpace(emotion))
            return "neutral";

        return emotion.Trim().ToLowerInvariant();
    }

    string GetActiveVectorStoreId()
    {
        if (!_enableRag)
            return null;

        if (string.IsNullOrWhiteSpace(_vectorStoreId))
            return null;

        return _vectorStoreId.Trim();
    }

    void SpeakAiReply(string text)
    {
        if (!_enableVoiceReply)
            return;
        if (_elevenLabs == null)
            return;
        if (_voiceAudioSource == null)
        {
            Debug.LogWarning("PetChatController: Voice AudioSource 未設定，略過語音播放。");
            return;
        }
        if (string.IsNullOrWhiteSpace(text))
            return;

        var request = new ElevenLabsRequest
        {
            text = text,
            model_id = string.IsNullOrWhiteSpace(_elevenLabsModelId)
                ? "eleven_multilingual_v2"
                : _elevenLabsModelId
        };

        _elevenLabs.Speak(
            request,
            clip =>
            {
                if (clip == null)
                    return;

                _voiceAudioSource.Stop();
                _voiceAudioSource.clip = clip;
                _voiceAudioSource.Play();
            },
            error =>
            {
                Debug.LogError($"PetChatController: ElevenLabs 語音生成失敗: {error}");
            }
        );
    }

    void ClearAndCloseInput()
    {
        if (_inputField != null)
        {
            _inputField.text = string.Empty;
            _inputField.DeactivateInputField();
        }

        SetInputVisible(false);
        ClearSelectedInput();
    }

    void ClearSelectedInput()
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    bool IsInputVisible()
    {
        if (_inputRoot != null)
            return _inputRoot.activeSelf;

        if (_inputField != null)
            return _inputField.gameObject.activeSelf;

        return false;
    }

    void SetInputVisible(bool isVisible)
    {
        if (_inputRoot != null)
        {
            if (_inputRoot.activeSelf != isVisible)
                _inputRoot.SetActive(isVisible);
            return;
        }

        if (_inputField != null && _inputField.gameObject.activeSelf != isVisible)
            _inputField.gameObject.SetActive(isVisible);
    }

    static bool WasSubmitKeyPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null)
            return false;

        return Keyboard.current.enterKey.wasPressedThisFrame
               || Keyboard.current.numpadEnterKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#endif
    }

    void AddHistory(ChatRole role, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return;

        _history.Add(new ChatTurn
        {
            role = role,
            content = content.Trim()
        });

        SaveHistory();
    }

    [ContextMenu("Clear Chat History")]
    public void ClearHistory()
    {
        _history.Clear();

        if (_persistHistoryToPlayerPrefs && !string.IsNullOrWhiteSpace(_historySaveKey))
            PlayerPrefs.DeleteKey(_historySaveKey);
    }

    [System.Serializable]
    sealed class ChatHistoryData
    {
        public List<ChatTurn> turns = new List<ChatTurn>();
    }

    void SaveHistory()
    {
        if (!_persistHistoryToPlayerPrefs)
            return;
        if (string.IsNullOrWhiteSpace(_historySaveKey))
            return;

        var data = new ChatHistoryData
        {
            turns = _history
        };
        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(_historySaveKey, json);
    }

    void LoadHistory()
    {
        _history.Clear();

        if (!_persistHistoryToPlayerPrefs)
            return;
        if (string.IsNullOrWhiteSpace(_historySaveKey))
            return;
        if (!PlayerPrefs.HasKey(_historySaveKey))
            return;

        string json = PlayerPrefs.GetString(_historySaveKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
            return;

        ChatHistoryData data = JsonUtility.FromJson<ChatHistoryData>(json);
        if (data?.turns == null || data.turns.Count == 0)
            return;

        for (int i = 0; i < data.turns.Count; i++)
        {
            ChatTurn turn = data.turns[i];
            if (turn == null || string.IsNullOrWhiteSpace(turn.content))
                continue;

            _history.Add(new ChatTurn
            {
                role = turn.role,
                content = turn.content.Trim()
            });
        }
    }
}
