using UnityEngine;

/// <summary>
/// 北極熊自主行為控制：
/// - 在可移動範圍內隨機 Walk / Idle / Think / Sleep。
/// - 提供移動鎖，給拖拽、餵食、洗澡等互動暫停自主移動。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class PetAutonomousBehaviour : MonoBehaviour
{
    enum AutoState
    {
        Idle = 0,
        Walk = 1,
        Think = 2,
        Sleep = 3
    }

    [Header("移動範圍（世界座標）")]
    [SerializeField] Vector2 _moveAreaCenter = Vector2.zero;
    [SerializeField] Vector2 _moveAreaSize = new Vector2(8f, 4f);

    [Header("Walk")]
    [SerializeField] float _walkSpeed = 1.2f;
    [SerializeField] float _arrivalDistance = 0.05f;
    [SerializeField] Vector2 _walkDurationRange = new Vector2(1.6f, 3.2f);

    [Header("Idle / Think / Sleep 時長")]
    [SerializeField] Vector2 _idleDurationRange = new Vector2(0.8f, 2.2f);
    [SerializeField] Vector2 _thinkDurationRange = new Vector2(1.2f, 2.4f);
    [SerializeField] Vector2 _sleepDurationRange = new Vector2(2.2f, 4.5f);

    [Header("狀態權重（總和不需為 1）")]
    [SerializeField] float _walkWeight = 0.55f;
    [SerializeField] float _idleWeight = 0.2f;
    [SerializeField] float _thinkWeight = 0.15f;
    [SerializeField] float _sleepWeight = 0.1f;

    Animator _animator;
    SpriteRenderer _spriteRenderer;

    AutoState _state;
    AutoState _lastPlayedAnimState = (AutoState)(-1);
    Vector2 _walkTarget;
    float _stateTimer;
    int _movementLockCount;

    static readonly int IdleStateHash = Animator.StringToHash("Idle");
    static readonly int WalkStateHash = Animator.StringToHash("Walk");
    static readonly int ThinkStateHash = Animator.StringToHash("Think");
    static readonly int SleepStateHash = Animator.StringToHash("Sleep");
    static readonly int IdleStateFullPathHash = Animator.StringToHash("Base Layer.Idle");
    static readonly int WalkStateFullPathHash = Animator.StringToHash("Base Layer.Walk");
    static readonly int ThinkStateFullPathHash = Animator.StringToHash("Base Layer.Think");
    static readonly int SleepStateFullPathHash = Animator.StringToHash("Base Layer.Sleep");

    public bool IsMovementLocked
    {
        get { return _movementLockCount > 0; }
    }

    void Awake()
    {
        _animator = GetComponent<Animator>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Start()
    {
        EnterState(AutoState.Idle);
    }

    void Update()
    {
        if (IsMovementLocked)
            return;

        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;

        _stateTimer -= dt;
        if (_state == AutoState.Walk)
            UpdateWalk(dt);

        if (_stateTimer <= 0f)
            EnterState(PickNextStateByWeight());
    }

    public void AcquireMovementLock()
    {
        _movementLockCount++;
    }

    public void ReleaseMovementLock()
    {
        if (_movementLockCount <= 0)
            return;

        _movementLockCount--;
        if (_movementLockCount == 0)
            _stateTimer = 0f;
    }

    void EnterState(AutoState nextState)
    {
        _state = nextState;

        switch (_state)
        {
            case AutoState.Walk:
                _walkTarget = GetRandomPointInMoveArea();
                _stateTimer = GetRandomDuration(_walkDurationRange);
                PlayAnimationIfNeeded(AutoState.Walk);
                break;

            case AutoState.Think:
                _stateTimer = GetRandomDuration(_thinkDurationRange);
                PlayAnimationIfNeeded(AutoState.Think);
                break;

            case AutoState.Sleep:
                _stateTimer = GetRandomDuration(_sleepDurationRange);
                PlayAnimationIfNeeded(AutoState.Sleep);
                break;

            default:
                _stateTimer = GetRandomDuration(_idleDurationRange);
                PlayAnimationIfNeeded(AutoState.Idle);
                break;
        }
    }

    void UpdateWalk(float dt)
    {
        Vector3 current = transform.position;
        Vector2 current2D = new Vector2(current.x, current.y);
        Vector2 next2D = Vector2.MoveTowards(current2D, _walkTarget, _walkSpeed * dt);

        if (next2D.x > current2D.x)
            _spriteRenderer.flipX = false;
        else if (next2D.x < current2D.x)
            _spriteRenderer.flipX = true;

        transform.position = new Vector3(next2D.x, next2D.y, current.z);

        float sqrRemain = (_walkTarget - next2D).sqrMagnitude;
        float sqrArrival = _arrivalDistance * _arrivalDistance;
        if (sqrRemain <= sqrArrival)
        {
            _stateTimer = 0f;
            _walkTarget = GetRandomPointInMoveArea();
        }
    }

    AutoState PickNextStateByWeight()
    {
        float walk = Mathf.Max(0f, _walkWeight);
        float idle = Mathf.Max(0f, _idleWeight);
        float think = Mathf.Max(0f, _thinkWeight);
        float sleep = Mathf.Max(0f, _sleepWeight);

        float sum = walk + idle + think + sleep;
        if (sum <= 0.0001f)
            return AutoState.Walk;

        float roll = Random.value * sum;
        if (roll < walk) return AutoState.Walk;
        roll -= walk;
        if (roll < idle) return AutoState.Idle;
        roll -= idle;
        if (roll < think) return AutoState.Think;
        return AutoState.Sleep;
    }

    float GetRandomDuration(Vector2 minMax)
    {
        float min = minMax.x;
        float max = minMax.y;
        if (max < min)
            max = min;
        if (min < 0.05f)
            min = 0.05f;
        return Random.Range(min, max);
    }

    Vector2 GetRandomPointInMoveArea()
    {
        Vector2 half = _moveAreaSize * 0.5f;
        float minX = _moveAreaCenter.x - half.x;
        float maxX = _moveAreaCenter.x + half.x;
        float minY = _moveAreaCenter.y - half.y;
        float maxY = _moveAreaCenter.y + half.y;
        return new Vector2(Random.Range(minX, maxX), Random.Range(minY, maxY));
    }

    void PlayAnimationIfNeeded(AutoState animState)
    {
        if (_animator == null)
            return;
        if (_lastPlayedAnimState == animState)
            return;

        _lastPlayedAnimState = animState;

        int hash = 0;
        int fullPathHash = 0;
        switch (animState)
        {
            case AutoState.Walk:
                hash = WalkStateHash;
                fullPathHash = WalkStateFullPathHash;
                break;
            case AutoState.Think:
                hash = ThinkStateHash;
                fullPathHash = ThinkStateFullPathHash;
                break;
            case AutoState.Sleep:
                hash = SleepStateHash;
                fullPathHash = SleepStateFullPathHash;
                break;
            default:
                hash = IdleStateHash;
                fullPathHash = IdleStateFullPathHash;
                break;
        }

        int stateHash = _animator.HasState(0, hash) ? hash : fullPathHash;
        _animator.Play(stateHash, 0, 0f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.35f, 0.85f, 1f, 0.9f);
        Vector3 center = new Vector3(_moveAreaCenter.x, _moveAreaCenter.y, transform.position.z);
        Vector3 size = new Vector3(_moveAreaSize.x, _moveAreaSize.y, 0f);
        Gizmos.DrawWireCube(center, size);

        Gizmos.color = new Color(1f, 0.9f, 0.1f, 0.9f);
        Vector3 targetPos = new Vector3(_walkTarget.x, _walkTarget.y, transform.position.z);
        Gizmos.DrawSphere(targetPos, 0.08f);
    }
}
