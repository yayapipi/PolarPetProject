using UnityEngine;

/// <summary>
/// 泡泡效果：生成後緩慢上浮，生命週期後淡出並自動銷毀。
/// 掛在 Bubble Prefab 上即可。
/// </summary>
[DisallowMultipleComponent]
public sealed class BubbleFloatFade : MonoBehaviour
{
    [Header("生命週期")]
    [SerializeField] float _lifetimeMin = 0.8f;
    [SerializeField] float _lifetimeMax = 1.6f;

    [Header("移動")]
    [SerializeField] float _floatSpeedMin = 0.35f;
    [SerializeField] float _floatSpeedMax = 0.95f;
    [Tooltip("左右漂移速度範圍（世界座標/秒）。")]
    [SerializeField] float _horizontalDriftAbsMax = 0.2f;

    [Header("淡出")]
    [Tooltip("開始淡出的時間比例（0~1）。例如 0.6 表示後 40% 時間淡出。")]
    [SerializeField] float _fadeStartNormalizedTime = 0.6f;

    SpriteRenderer _spriteRenderer;
    Color _baseColor;

    float _lifetime;
    float _elapsed;
    float _verticalSpeed;
    float _horizontalSpeed;

    void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer != null)
            _baseColor = _spriteRenderer.color;
        else
            _baseColor = Color.white;

        _lifetime = Random.Range(_lifetimeMin, _lifetimeMax);
        if (_lifetime < 0.05f)
            _lifetime = 0.05f;

        _verticalSpeed = Random.Range(_floatSpeedMin, _floatSpeedMax);
        _horizontalSpeed = Random.Range(-_horizontalDriftAbsMax, _horizontalDriftAbsMax);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        _elapsed += dt;

        transform.position += new Vector3(_horizontalSpeed * dt, _verticalSpeed * dt, 0f);

        if (_spriteRenderer != null)
            UpdateAlpha();

        if (_elapsed >= _lifetime)
            Destroy(gameObject);
    }

    void UpdateAlpha()
    {
        float fadeStart = Mathf.Clamp01(_fadeStartNormalizedTime);
        float t = Mathf.Clamp01(_elapsed / _lifetime);

        float alpha = _baseColor.a;
        if (t >= fadeStart)
        {
            float fadeT = (t - fadeStart) / Mathf.Max(0.0001f, 1f - fadeStart);
            alpha = Mathf.Lerp(_baseColor.a, 0f, fadeT);
        }

        Color c = _baseColor;
        c.a = alpha;
        _spriteRenderer.color = c;
    }
}
