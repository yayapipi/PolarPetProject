using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class UIHorizontalLoopMover : MonoBehaviour
{
    [Header("Move Settings")]
    [SerializeField, Min(0f)] float _speed = 300f;
    [SerializeField] float _leftX = -800f;
    [SerializeField] float _rightX = 800f;
    [SerializeField] bool _useUnscaledTime = true;
    [SerializeField] bool _snapToLeftOnEnable = true;

    RectTransform _rectTransform;
    float _fixedY;

    void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _fixedY = _rectTransform.anchoredPosition.y;
    }

    void OnEnable()
    {
        if (_snapToLeftOnEnable)
            ResetToLeft();
    }

    void Update()
    {
        if (_rectTransform == null)
            return;

        if (_rightX <= _leftX)
            return;

        float dt = _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float nextX = _rectTransform.anchoredPosition.x + (_speed * dt);

        if (nextX > _rightX)
            nextX = _leftX;

        _rectTransform.anchoredPosition = new Vector2(nextX, _fixedY);
    }

    [ContextMenu("Reset To Left")]
    public void ResetToLeft()
    {
        if (_rectTransform == null)
            _rectTransform = GetComponent<RectTransform>();

        _fixedY = _rectTransform.anchoredPosition.y;
        _rectTransform.anchoredPosition = new Vector2(_leftX, _fixedY);
    }

    void OnValidate()
    {
        if (_speed < 0f)
            _speed = 0f;
    }
}
