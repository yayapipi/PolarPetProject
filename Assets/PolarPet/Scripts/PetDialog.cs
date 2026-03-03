using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 掛在 UI 對話框物件上：
/// 1) 跟隨目標 SpriteRenderer（可設定世界座標偏移）。
/// 2) 依文字內容自動調整 Frame 尺寸。
/// 3) 提供外部呼叫 API 修改文字並顯示。
/// 4) 顯示指定秒數後自動隱藏。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class PetDialog : MonoBehaviour
{
    [Header("跟隨目標")]
    [SerializeField] SpriteRenderer _targetSpriteRenderer;
    [Tooltip("未手動指定時，啟動時嘗試自動尋找目標 SpriteRenderer。")]
    [SerializeField] bool _autoFindTargetIfMissing = true;
    [Tooltip("對話框錨定於目標世界座標 + Offset。")]
    [SerializeField] Vector3 _worldOffset = new Vector3(0f, 1.2f, 0f);

    [Header("UI 參考")]
    [SerializeField] Canvas _canvas;
    [SerializeField] RectTransform _dialogRect;
    [Tooltip("可拉伸背景框（通常是對話框底圖）。")]
    [SerializeField] RectTransform _frameRect;
    [SerializeField] Text _dialogText;
    [Tooltip("要隱藏/顯示的物件，未指定時使用本物件。")]
    [SerializeField] GameObject _dialogRoot;

    [Header("字數自動縮放")]
    [SerializeField] Vector2 _padding = new Vector2(28f, 18f);
    [SerializeField] Vector2 _minFrameSize = new Vector2(120f, 60f);
    [SerializeField] Vector2 _maxFrameSize = new Vector2(420f, 240f);

    [Header("顯示控制")]
    [SerializeField] float _defaultVisibleSeconds = 2.5f;
    [SerializeField] bool _hideOnStart = true;

    Camera _mainCamera;
    Coroutine _hideCoroutine;

    void Awake()
    {
        if (_dialogRect == null)
            _dialogRect = transform as RectTransform;

        if (_canvas == null)
            _canvas = GetComponentInParent<Canvas>();

        if (_frameRect == null)
            _frameRect = _dialogRect;

        if (_dialogText == null)
            _dialogText = GetComponentInChildren<Text>(true);

        if (_dialogRoot == null)
            _dialogRoot = gameObject;

        _mainCamera = Camera.main;
        TryAutoResolveTarget();

        RefreshFrameSize();
    }

    void Start()
    {
        if (_hideOnStart)
            Hide();
    }

    void LateUpdate()
    {
        if (!isActiveAndEnabled)
            return;

        if (_dialogRoot != null && !_dialogRoot.activeSelf)
            return;

        FollowTarget();
    }

    /// <summary>
    /// 設定對話文字，不改變顯示狀態。
    /// </summary>
    public void SetText(string text)
    {
        if (_dialogText == null)
            return;

        _dialogText.text = text ?? string.Empty;
        RefreshFrameSize();
    }

    /// <summary>
    /// 由外部指定跟隨目標。
    /// </summary>
    public void SetFollowTarget(SpriteRenderer targetSpriteRenderer)
    {
        _targetSpriteRenderer = targetSpriteRenderer;
    }

    /// <summary>
    /// 顯示對話，並在指定秒數後自動隱藏。
    /// visibleSeconds < 0 時使用預設秒數；visibleSeconds == 0 時不自動隱藏。
    /// </summary>
    public void ShowText(string text, float visibleSeconds = -1f)
    {
        SetVisible(true);
        SetText(text);

        float duration = visibleSeconds < 0f ? _defaultVisibleSeconds : visibleSeconds;
        if (duration > 0f)
            ScheduleAutoHide(duration);
        else
            CancelAutoHide();
    }

    /// <summary>
    /// 單純顯示指定秒數（不改文字）。
    /// </summary>
    public void ShowForSeconds(float visibleSeconds)
    {
        SetVisible(true);

        if (visibleSeconds > 0f)
            ScheduleAutoHide(visibleSeconds);
        else
            CancelAutoHide();
    }

    /// <summary>
    /// 立即隱藏對話框。
    /// </summary>
    public void Hide()
    {
        CancelAutoHide();
        SetVisible(false);
    }

    void FollowTarget()
    {
        if (_targetSpriteRenderer == null || _dialogRect == null || _canvas == null)
            return;

        if (_mainCamera == null || !_mainCamera.isActiveAndEnabled)
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null)
                return;
        }

        Vector3 targetWorld = _targetSpriteRenderer.bounds.center + _worldOffset;

        if (_canvas.renderMode == RenderMode.WorldSpace)
        {
            _dialogRect.position = targetWorld;
            return;
        }

        // 用渲染 Sprite 的相機做真正的世界→螢幕投影
        Vector3 screenPos3D = _mainCamera.WorldToScreenPoint(targetWorld);
        if (screenPos3D.z < 0f)
            return;

        Vector2 screenPos = new Vector2(screenPos3D.x, screenPos3D.y);

        // Canvas 事件相機：Overlay 用 null，Camera 模式用 canvas.worldCamera
        Camera uiCamera = _canvas.renderMode == RenderMode.ScreenSpaceCamera
            ? _canvas.worldCamera
            : null;

        RectTransform parentRect = _dialogRect.parent as RectTransform;
        if (parentRect == null)
            return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect, screenPos, uiCamera, out Vector2 localPoint))
        {
            _dialogRect.localPosition = localPoint;
        }
    }

    void TryAutoResolveTarget()
    {
        if (_targetSpriteRenderer != null)
            return;
        if (!_autoFindTargetIfMissing)
            return;

        PetAutonomousBehaviour pet = FindFirstObjectByType<PetAutonomousBehaviour>();
        if (pet != null)
        {
            _targetSpriteRenderer = pet.GetComponent<SpriteRenderer>();
            if (_targetSpriteRenderer != null)
                return;
        }

        _targetSpriteRenderer = FindFirstObjectByType<SpriteRenderer>();
    }

    void RefreshFrameSize()
    {
        if (_dialogText == null || _frameRect == null)
            return;

        string text = _dialogText.text ?? string.Empty;

        TextGenerationSettings widthSettings = GetTextGenerationSettings();
        widthSettings.generateOutOfBounds = true;
        widthSettings.horizontalOverflow = HorizontalWrapMode.Overflow;
        widthSettings.verticalOverflow = VerticalWrapMode.Overflow;
        widthSettings.updateBounds = false;
        widthSettings.scaleFactor = 1f;
        widthSettings.generationExtents = new Vector2(float.MaxValue, float.MaxValue);

        float rawPreferredWidth = _dialogText.cachedTextGeneratorForLayout.GetPreferredWidth(text, widthSettings);
        if (_dialogText.pixelsPerUnit > 0f)
            rawPreferredWidth /= _dialogText.pixelsPerUnit;
        float targetWidth = Mathf.Clamp(rawPreferredWidth + _padding.x, _minFrameSize.x, _maxFrameSize.x);
        float textAreaWidth = Mathf.Max(1f, targetWidth - _padding.x);

        TextGenerationSettings heightSettings = GetTextGenerationSettings();
        heightSettings.generateOutOfBounds = true;
        heightSettings.horizontalOverflow = HorizontalWrapMode.Wrap;
        heightSettings.verticalOverflow = VerticalWrapMode.Overflow;
        heightSettings.updateBounds = false;
        heightSettings.scaleFactor = 1f;
        heightSettings.generationExtents = new Vector2(textAreaWidth, float.MaxValue);

        float wrappedHeight = _dialogText.cachedTextGeneratorForLayout.GetPreferredHeight(text, heightSettings);
        if (_dialogText.pixelsPerUnit > 0f)
            wrappedHeight /= _dialogText.pixelsPerUnit;
        float targetHeight = Mathf.Clamp(wrappedHeight + _padding.y, _minFrameSize.y, _maxFrameSize.y);

        _frameRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);
        _frameRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);

        if (_dialogRect != null && _dialogRect != _frameRect)
        {
            _dialogRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);
            _dialogRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);
        }
    }

    TextGenerationSettings GetTextGenerationSettings()
    {
        TextGenerationSettings settings = _dialogText.GetGenerationSettings(_dialogText.rectTransform.rect.size);
        settings.fontSize = _dialogText.fontSize;
        settings.font = _dialogText.font;
        settings.lineSpacing = _dialogText.lineSpacing;
        settings.richText = _dialogText.supportRichText;
        settings.textAnchor = _dialogText.alignment;
        settings.fontStyle = _dialogText.fontStyle;
        settings.color = _dialogText.color;
        settings.resizeTextForBestFit = _dialogText.resizeTextForBestFit;
        settings.resizeTextMinSize = _dialogText.resizeTextMinSize;
        settings.resizeTextMaxSize = _dialogText.resizeTextMaxSize;
        settings.pivot = _dialogText.rectTransform.pivot;
        return settings;
    }

    void SetVisible(bool isVisible)
    {
        if (_dialogRoot == null)
            return;

        if (_dialogRoot.activeSelf == isVisible)
            return;

        _dialogRoot.SetActive(isVisible);
    }

    void ScheduleAutoHide(float seconds)
    {
        CancelAutoHide();
        _hideCoroutine = StartCoroutine(HideAfterDelay(seconds));
    }

    void CancelAutoHide()
    {
        if (_hideCoroutine == null)
            return;

        StopCoroutine(_hideCoroutine);
        _hideCoroutine = null;
    }

    IEnumerator HideAfterDelay(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        _hideCoroutine = null;
        SetVisible(false);
    }
}
