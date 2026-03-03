using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 寵物滑鼠拖拽：點擊拖拽時播放 Drag 動畫、放手恢復 Idle，拖動時依左右 Flip，拖拽時顯示下方陰影。
/// 需在寵物上掛 Collider2D，且場景需要 EventSystem 與 Main Camera 需有 Physics2DRaycaster（腳本會自動補上）。
/// </summary>
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(SpriteRenderer))]
public class PetDragController : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("陰影（拖拽時顯示在下方）")]
    [Tooltip("陰影是寵物身上的子物件 GameObject；拖拽時 SetActive(true)，放手 SetActive(false)。")]
    [SerializeField] GameObject _shadowObject;

    Animator _animator;
    SpriteRenderer _spriteRenderer;
    PetAutonomousBehaviour _petAutonomousBehaviour;
    Camera _mainCamera;
    float _dragPlaneZ;
    Vector3 _dragOffsetWorld;
    bool _hasMovementLock;

    static readonly int DragStateHash = Animator.StringToHash("Drag");
    static readonly int IdleStateHash = Animator.StringToHash("Idle");
    static readonly int DragStateFullPathHash = Animator.StringToHash("Base Layer.Drag");
    static readonly int IdleStateFullPathHash = Animator.StringToHash("Base Layer.Idle");

    void Awake()
    {
        _animator = GetComponent<Animator>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _petAutonomousBehaviour = GetComponent<PetAutonomousBehaviour>();
        _mainCamera = Camera.main;
        _dragPlaneZ = transform.position.z;

        EnsureEventSystemExists();

        if (_mainCamera != null && _mainCamera.GetComponent<UnityEngine.EventSystems.Physics2DRaycaster>() == null)
            _mainCamera.gameObject.AddComponent<UnityEngine.EventSystems.Physics2DRaycaster>();

        if (_shadowObject != null)
            _shadowObject.SetActive(false);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_mainCamera == null) return;

        // 讓拖拽時不會「瞬間跳到滑鼠中心」，保留點擊位置與寵物之間的偏移。
        Vector3 pointerWorld = GetPointerWorldPosition(eventData.position);
        _dragOffsetWorld = transform.position - pointerWorld;

        AcquireMovementLock();
        PlayDragAnimation();
        SetShadowActive(true);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_mainCamera == null) return;

        float deltaX = eventData.delta.x;
        if (deltaX > 0f)
            _spriteRenderer.flipX = false;
        else if (deltaX < 0f)
            _spriteRenderer.flipX = true;

        Vector3 worldPos = GetPointerWorldPosition(eventData.position);
        worldPos += _dragOffsetWorld;
        transform.position = worldPos;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        ReleaseMovementLock();
        PlayIdleAnimation();
        SetShadowActive(false);
    }

    void OnDisable()
    {
        ReleaseMovementLock();
        SetShadowActive(false);
    }

    void SetShadowActive(bool isActive)
    {
        if (_shadowObject == null) return;
        if (_shadowObject.activeSelf == isActive) return;
        _shadowObject.SetActive(isActive);
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

    void PlayDragAnimation()
    {
        if (_animator == null) return;
        int hash = _animator.HasState(0, DragStateHash) ? DragStateHash : DragStateFullPathHash;
        _animator.Play(hash, 0, 0f);
    }

    void PlayIdleAnimation()
    {
        if (_animator == null) return;
        int hash = _animator.HasState(0, IdleStateHash) ? IdleStateHash : IdleStateFullPathHash;
        _animator.Play(hash, 0, 0f);
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
