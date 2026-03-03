using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Soap 道具：可在場景拖拽，放在寵物身上來回搓澡時持續產生泡泡，
/// 並讓寵物播放 Think 動畫；離開/停止搓澡時回到 Idle。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class SoapItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    const string PetTag = "Pet";

    [Header("拖拽")]
    [SerializeField] bool _raiseSortingWhileDragging = true;
    [SerializeField] bool _forceTriggerCollider = true;

    [Header("寵物偵測")]
    [Tooltip("用 OverlapPoint 偵測寵物 Layer。")]
    [SerializeField] LayerMask _petLayerMask = ~0;

    [Header("搓澡判定")]
    [Tooltip("拖拽速度大於此值才會持續生成泡泡（世界座標/秒）。")]
    [SerializeField] float _scrubMinSpeed = 0.2f;

    [Header("泡泡生成")]
    [SerializeField] GameObject _bubblePrefab;
    [Tooltip("泡泡在肥皂附近隨機生成半徑（世界座標）。")]
    [SerializeField] float _bubbleSpawnRadius = 0.25f;
    [Tooltip("泡泡生成間隔最小值（秒）。")]
    [SerializeField] float _bubbleSpawnIntervalMin = 0.05f;
    [Tooltip("泡泡生成間隔最大值（秒）。")]
    [SerializeField] float _bubbleSpawnIntervalMax = 0.16f;

    [Header("洗澡音效")]
    [Tooltip("Soap 碰到 Pet 時循環播放，離開時停止。")]
    [SerializeField] AudioClip _bathLoopSfx;
    [SerializeField][Range(0f, 1f)] float _bathLoopVolume = 1f;

    Camera _mainCamera;
    float _dragPlaneZ;
    Vector3 _dragOffsetWorld;
    Vector3 _dragTargetWorld;
    Vector3 _lastDragWorld;

    Collider2D _collider2D;
    Rigidbody2D _rb2D;
    SpriteRenderer _spriteRenderer;
    AudioSource _audioSource;

    bool _isDragging;
    bool _isScrubbing;
    float _nextBubbleSpawnTime;

    int _originalSortingOrder;
    bool _hasOriginalSorting;

    Transform _currentPetRoot;
    PetBathThinkSequence _currentPetThinkSequence;

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
        EnsurePhysics2DRaycaster();

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

        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        _audioSource.playOnAwake = false;
        _audioSource.loop = true;
        _audioSource.spatialBlend = 0f;
        _audioSource.volume = _bathLoopVolume;
    }

    void OnDisable()
    {
        StopScrub();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_mainCamera == null) return;

        Vector3 pointerWorld = GetPointerWorldPosition(eventData.position);
        _dragOffsetWorld = transform.position - pointerWorld;

        _isDragging = true;
        _dragTargetWorld = transform.position;
        _lastDragWorld = transform.position;

        if (_raiseSortingWhileDragging && _spriteRenderer != null)
            _spriteRenderer.sortingOrder = 999;
    }

    public void OnDrag(PointerEventData eventData)
    {
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
        if (!_isDragging) return;
        if (_rb2D == null) return;

        _rb2D.MovePosition(_dragTargetWorld);
    }

    void Update()
    {
        if (!_isDragging)
        {
            if (_isScrubbing)
                StopScrub();
            return;
        }

        if (!TryGetPetRootAtCurrentPosition(out Transform petRoot))
        {
            if (_isScrubbing)
                StopScrub();
            _lastDragWorld = transform.position;
            return;
        }

        Vector3 currentPos = transform.position;
        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;

        StartOrKeepScrub(petRoot);

        float speed = (currentPos - _lastDragWorld).magnitude / dt;
        _lastDragWorld = currentPos;

        if (speed < _scrubMinSpeed)
            return;
        TrySpawnBubble();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        _isDragging = false;
        StopScrub();

        if (_raiseSortingWhileDragging && _spriteRenderer != null && _hasOriginalSorting)
            _spriteRenderer.sortingOrder = _originalSortingOrder;
    }

    void StartOrKeepScrub(Transform petRoot)
    {
        if (_currentPetRoot == petRoot && _isScrubbing)
            return;

        StopScrub();

        _currentPetRoot = petRoot;
        _currentPetThinkSequence = _currentPetRoot.GetComponent<PetBathThinkSequence>();
        if (_currentPetThinkSequence == null)
            _currentPetThinkSequence = _currentPetRoot.gameObject.AddComponent<PetBathThinkSequence>();

        _currentPetThinkSequence.BeginThink();
        StartBathLoopSfx();
        _isScrubbing = true;
        _nextBubbleSpawnTime = Time.time;
    }

    void StopScrub()
    {
        _isScrubbing = false;

        if (_currentPetThinkSequence != null)
            _currentPetThinkSequence.EndThink();

        StopBathLoopSfx();

        _currentPetRoot = null;
        _currentPetThinkSequence = null;
    }

    void TrySpawnBubble()
    {
        if (_bubblePrefab == null) return;
        if (Time.time < _nextBubbleSpawnTime) return;

        Vector2 offset = Random.insideUnitCircle * _bubbleSpawnRadius;
        Vector3 spawnPos = transform.position + new Vector3(offset.x, offset.y, 0f);
        spawnPos.z = transform.position.z;

        Instantiate(_bubblePrefab, spawnPos, Quaternion.identity);

        float interval = Random.Range(_bubbleSpawnIntervalMin, _bubbleSpawnIntervalMax);
        if (interval < 0.01f)
            interval = 0.01f;
        _nextBubbleSpawnTime = Time.time + interval;
    }

    bool TryGetPetRootAtCurrentPosition(out Transform petRoot)
    {
        petRoot = null;

        Vector2 p = transform.position;
        int count = Physics2D.OverlapPointNonAlloc(p, _overlapResults, _petLayerMask);
        if (count <= 0) return false;

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = _overlapResults[i];
            if (hit == null) continue;

            if (hit.CompareTag(PetTag))
            {
                petRoot = hit.transform.root != null ? hit.transform.root : hit.transform;
                return true;
            }

            Transform root = hit.transform.root;
            if (root != null && root.CompareTag(PetTag))
            {
                petRoot = root;
                return true;
            }
        }

        return false;
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

        var inputSystemUiModuleType =
            System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputSystemUiModuleType != null)
        {
            go.AddComponent(inputSystemUiModuleType);
            return;
        }

        go.AddComponent<StandaloneInputModule>();
    }

    void EnsurePhysics2DRaycaster()
    {
        if (_mainCamera == null) return;
        if (_mainCamera.GetComponent<Physics2DRaycaster>() != null) return;
        _mainCamera.gameObject.AddComponent<Physics2DRaycaster>();
    }

    void StartBathLoopSfx()
    {
        if (_bathLoopSfx == null) return;
        if (_audioSource == null) return;
        if (_audioSource.isPlaying) return;

        _audioSource.clip = _bathLoopSfx;
        _audioSource.volume = _bathLoopVolume;
        _audioSource.Play();
    }

    void StopBathLoopSfx()
    {
        if (_audioSource == null) return;
        if (!_audioSource.isPlaying) return;
        _audioSource.Stop();
    }
}

/// <summary>
/// 寵物被搓澡時播放 Think，結束後回 Idle。
/// </summary>
[DisallowMultipleComponent]
public sealed class PetBathThinkSequence : MonoBehaviour
{
    Animator _animator;
    PetAutonomousBehaviour _petAutonomousBehaviour;
    int _activeThinkRequests;
    bool _hasMovementLock;

    static readonly int ThinkStateHash = Animator.StringToHash("Think");
    static readonly int IdleStateHash = Animator.StringToHash("Idle");
    static readonly int ThinkStateFullPathHash = Animator.StringToHash("Base Layer.Think");
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

    public void BeginThink()
    {
        if (_animator == null) return;

        _activeThinkRequests++;
        if (_activeThinkRequests > 1) return;

        AcquireMovementLock();

        int thinkHash = _animator.HasState(0, ThinkStateHash) ? ThinkStateHash : ThinkStateFullPathHash;
        _animator.Play(thinkHash, 0, 0f);
    }

    public void EndThink()
    {
        if (_animator == null) return;
        if (_activeThinkRequests <= 0) return;

        _activeThinkRequests--;
        if (_activeThinkRequests > 0) return;

        int idleHash = _animator.HasState(0, IdleStateHash) ? IdleStateHash : IdleStateFullPathHash;
        _animator.Play(idleHash, 0, 0f);
        ReleaseMovementLock();
    }

    void OnDisable()
    {
        _activeThinkRequests = 0;
        ReleaseMovementLock();
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
