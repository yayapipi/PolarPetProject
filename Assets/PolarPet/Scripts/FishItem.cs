using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Fish 道具：可在場景中拖拽，放到 Tag=Pet 的物件上餵食。
/// 餵食時會讓寵物播放 Eat 動畫、播放吃東西音效，並讓魚消失。
///
/// 需求：
/// - Fish Prefab 需有 Collider2D（建議 Is Trigger）。
/// - 場景需有 EventSystem，Main Camera 需有 Physics2DRaycaster（腳本會自動補上）。
/// - Pet（或其碰撞器/Root）需設定 Tag = "Pet"。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class FishItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    const string PetTag = "Pet";

    [Header("拖拽")]
    [Tooltip("拖拽時把 Fish 提到最上層（以 sortingOrder 實作）。")]
    [SerializeField] bool _raiseSortingWhileDragging = true;

    [Header("餵食偵測")]
    [Tooltip("放手時用 OverlapPoint 偵測 Pet 的 Layer；預設為 Everything。")]
    [SerializeField] LayerMask _petLayerMask = ~0;

    [Tooltip("若為 true，Awake 時會強制把 Collider2D 設為 IsTrigger，方便放到寵物身上偵測。")]
    [SerializeField] bool _forceTriggerCollider = true;

    [Header("吃東西音效")]
    [Tooltip("吃東西時播放的音效。優先在 Pet 的 AudioSource 播放；沒有則在 Fish 自己播放。")]
    [SerializeField] AudioClip _eatSfx;

    [Header("消失")]
    [Tooltip("餵食成功後立刻隱藏 SpriteRenderer。")]
    [SerializeField] bool _hideSpriteOnConsume = true;

    Camera _mainCamera;
    float _dragPlaneZ;
    Vector3 _dragOffsetWorld;

    Collider2D _collider2D;
    Rigidbody2D _rb2D;
    SpriteRenderer _spriteRenderer;
    AudioSource _audioSource;

    bool _isDragging;
    Vector3 _dragTargetWorld;
    bool _isConsumed;

    int _originalSortingOrder;
    bool _hasOriginalSorting;

    readonly Collider2D[] _overlapResults = new Collider2D[8];

    void Awake()
    {
        _mainCamera = Camera.main;
        _dragPlaneZ = transform.position.z;

        _collider2D = GetComponent<Collider2D>();
        _rb2D = GetComponent<Rigidbody2D>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _audioSource = GetComponent<AudioSource>();

        EnsureEventSystemExists();

        if (_mainCamera != null && _mainCamera.GetComponent<Physics2DRaycaster>() == null)
            _mainCamera.gameObject.AddComponent<Physics2DRaycaster>();

        if (_forceTriggerCollider && _collider2D != null && !_collider2D.isTrigger)
            _collider2D.isTrigger = true;

        if (_rb2D != null)
        {
            _rb2D.gravityScale = 0f;
            _rb2D.bodyType = RigidbodyType2D.Kinematic;
            _rb2D.simulated = true;
        }

        if (_spriteRenderer != null)
        {
            _originalSortingOrder = _spriteRenderer.sortingOrder;
            _hasOriginalSorting = true;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_isConsumed) return;
        if (_mainCamera == null) return;

        Vector3 pointerWorld = GetPointerWorldPosition(eventData.position);
        _dragOffsetWorld = transform.position - pointerWorld;

        _isDragging = true;
        _dragTargetWorld = transform.position;

        if (_raiseSortingWhileDragging && _spriteRenderer != null)
            _spriteRenderer.sortingOrder = 999;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_isConsumed) return;
        if (!_isDragging) return;
        if (_mainCamera == null) return;

        Vector3 worldPos = GetPointerWorldPosition(eventData.position);
        worldPos += _dragOffsetWorld;
        _dragTargetWorld = worldPos;

        if (_rb2D == null)
            transform.position = _dragTargetWorld;
    }

    void FixedUpdate()
    {
        if (_isConsumed) return;
        if (!_isDragging) return;
        if (_rb2D == null) return;

        _rb2D.MovePosition(_dragTargetWorld);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_isConsumed) return;

        _isDragging = false;

        if (_raiseSortingWhileDragging && _spriteRenderer != null && _hasOriginalSorting)
            _spriteRenderer.sortingOrder = _originalSortingOrder;

        TryConsumeIfDroppedOnPet();
    }

    void TryConsumeIfDroppedOnPet()
    {
        Vector2 p = transform.position;
        int count = Physics2D.OverlapPointNonAlloc(p, _overlapResults, _petLayerMask);
        if (count <= 0) return;

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = _overlapResults[i];
            if (hit == null) continue;

            // 需求：使用 Tag = Pet 檢查
            if (hit.CompareTag(PetTag))
            {
                ConsumeOnPet(hit.transform);
                return;
            }

            // 常見情況：碰撞器在子物件，Tag 設在 Root
            Transform root = hit.transform.root;
            if (root != null && root.CompareTag(PetTag))
            {
                ConsumeOnPet(root);
                return;
            }
        }
    }

    void ConsumeOnPet(Transform petTransform)
    {
        if (_isConsumed) return;
        _isConsumed = true;

        if (_collider2D != null)
            _collider2D.enabled = false;

        PlayEatThenIdleOnPet(petTransform);

        bool playedOnPet = TryPlayEatSfxOnPet(petTransform);
        if (!playedOnPet)
            PlayEatSfxOnSelfAndScheduleDestroy();
        else
            Destroy(gameObject);

        if (_hideSpriteOnConsume && _spriteRenderer != null)
            _spriteRenderer.enabled = false;
    }

    void PlayEatThenIdleOnPet(Transform petTransform)
    {
        var sequence = petTransform.GetComponentInParent<PetEatSequence>();
        if (sequence == null)
            sequence = petTransform.gameObject.AddComponent<PetEatSequence>();

        sequence.PlayEatThenIdle();
    }

    bool TryPlayEatSfxOnPet(Transform petTransform)
    {
        if (_eatSfx == null) return true; // 沒音效就不阻擋銷毀

        AudioSource petAudio = petTransform.GetComponentInParent<AudioSource>();
        if (petAudio == null) return false;

        petAudio.PlayOneShot(_eatSfx);
        return true;
    }

    void PlayEatSfxOnSelfAndScheduleDestroy()
    {
        if (_eatSfx == null)
        {
            Destroy(gameObject);
            return;
        }

        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f;
        }

        _audioSource.PlayOneShot(_eatSfx);
        float delay = Mathf.Max(0.01f, _eatSfx.length);
        Destroy(gameObject, delay);
    }

    Vector3 GetPointerWorldPosition(Vector2 screenPosition)
    {
        float camZ = _mainCamera.transform.position.z;
        float dist = Mathf.Abs(camZ - _dragPlaneZ);
        Vector3 screen = new Vector3(screenPosition.x, screenPosition.y, dist);
        Vector3 worldPos = _mainCamera.ScreenToWorldPoint(screen);
        worldPos.z = _dragPlaneZ;
        return worldPos;
    }

    static void EnsureEventSystemExists()
    {
        if (EventSystem.current != null) return;

        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();

        // 優先支援新版 Input System（若專案已安裝 Unity.InputSystem）
        var inputSystemUiModuleType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputSystemUiModuleType != null)
        {
            go.AddComponent(inputSystemUiModuleType);
            return;
        }

        // 後備：舊版 StandaloneInputModule（若專案使用舊 Input Manager）
        go.AddComponent<StandaloneInputModule>();
    }
}

/// <summary>
/// 掛在 Pet 上的輕量序列：播放 Eat，播完自動回 Idle。
/// 由 FishItem 在餵食時動態 AddComponent，避免 Fish 被銷毀後無法等待動畫完成。
/// </summary>
[DisallowMultipleComponent]
public sealed class PetEatSequence : MonoBehaviour
{
    Animator _animator;
    PetAutonomousBehaviour _petAutonomousBehaviour;
    Coroutine _routine;
    bool _hasMovementLock;

    const float ReturnToIdleDelaySeconds = 1f;

    static readonly int EatStateHash = Animator.StringToHash("Eat");
    static readonly int IdleStateHash = Animator.StringToHash("Idle");
    static readonly int EatStateFullPathHash = Animator.StringToHash("Base Layer.Eat");
    static readonly int IdleStateFullPathHash = Animator.StringToHash("Base Layer.Idle");

    void Awake()
    {
        _animator = GetComponent<Animator>();
        if (_animator == null)
            _animator = GetComponentInParent<Animator>();

        _petAutonomousBehaviour = GetComponent<PetAutonomousBehaviour>();
        if (_petAutonomousBehaviour == null)
            _petAutonomousBehaviour = GetComponentInParent<PetAutonomousBehaviour>();
    }

    public void PlayEatThenIdle()
    {
        if (_animator == null) return;

        AcquireMovementLock();

        if (_routine != null)
            StopCoroutine(_routine);
        _routine = StartCoroutine(PlayRoutine());
    }

    System.Collections.IEnumerator PlayRoutine()
    {
        int eatHash = _animator.HasState(0, EatStateHash) ? EatStateHash : EatStateFullPathHash;
        int idleHash = _animator.HasState(0, IdleStateHash) ? IdleStateHash : IdleStateFullPathHash;

        _animator.Play(eatHash, 0, 0f);
        yield return new WaitForSeconds(ReturnToIdleDelaySeconds);

        _animator.Play(idleHash, 0, 0f);
        ReleaseMovementLock();
        _routine = null;
    }

    void OnDisable()
    {
        ReleaseMovementLock();
        _routine = null;
    }

    void AcquireMovementLock()
    {
        if (_hasMovementLock) return;
        if (_petAutonomousBehaviour == null) return;

        _petAutonomousBehaviour.AcquireMovementLock();
        _hasMovementLock = true;
    }

    void ReleaseMovementLock()
    {
        if (!_hasMovementLock) return;

        if (_petAutonomousBehaviour != null)
            _petAutonomousBehaviour.ReleaseMovementLock();
        _hasMovementLock = false;
    }
}
