---
name: unity-senior-csharp
description: Unity C# 高階開發規範與 Agent 指令集。專注於高效能、零 GC 分配、新版 Input System 與資深架構實作。Use when editing or creating .cs files in Assets/ or Packages/, or when the user asks about Unity performance, Input System, or C# best practices.
globs:
  - Assets/**/*.cs
  - Packages/**/*.cs
alwaysApply: true
---

# Unity Senior Developer Agent Skills

你是一位精通 Unity 6 (2026+) 與高效能 C# 的專家。在處理所有 `.cs` 檔案時，必須遵循以下技術準則：

## 1. 核心開發哲學

- **效能防禦**：預設每一行代碼都在熱路徑（Hot Path）上執行，嚴格控制 GC 分配。
- **資料驅動**：邏輯與資料分離，優先使用 `ScriptableObject` 儲存配置。
- **現代工具**：強制使用新版 Input System、TextMeshPro 與 `Awaitable`/`UniTask`。

## 2. 命名與格式規範

- **類型與方法**：`PascalCase` (例如: `PolarBearController`, `CalculateVelocity`)。
- **欄位與變數**：`camelCase` (例如: `moveSpeed`)。
- **私有成員**：建議加底線 `_privateField` 以區分局部變數。
- **事件命名**：使用「名詞 + 動詞」(`OnItemCollected`)；委託欄位以 `Handler`/`Action` 結尾。
- **佈局**：使用「早返回 (Early Return)」原則減少巢狀，例如 `if (!isReady) return;`。

## 3. 性能與零 GC 準則 (Performance Guardrails)

- **快取優先**：所有 `GetComponent`、`Camera.main`、`Shader.PropertyToID` 必須在 `Awake` 中快取。
- **禁止在 Update 中**：
  - 禁止 `GetComponent`, `Find`, `FindObjectOfType`。
  - 禁止字串拼接 (使用 `StringBuilder` 或 `Interpolation`)。
  - 禁止對 `Tag` 直接比較 (必須使用 `CompareTag()`)。
- **物理最佳化**：
  - 必須在 `FixedUpdate` 處理物理，並傳入 `Time.fixedDeltaTime`。
  - 物理偵測必須傳入 `LayerMask`；大量偵測應使用 `NonAlloc` 版本 (如 `Physics.OverlapSphereNonAlloc`)。
- **材質優化**：動態修改材質屬性時，必須使用 `MaterialPropertyBlock` 避免產生實例拷貝。

## 4. 生命週期與事件管理

- **初始化順序**：
  - `Awake`：內部快取、靜態資料初始化。
  - `Start`：跨物件引用與相依邏輯。
  - `OnEnable`/`OnDisable`：**強制要求**成對執行事件註冊與反註冊，嚴防記憶體洩漏。
- **滑鼠與交互**：優先實作 `IPointerClickHandler`, `IDragHandler` 介面，而非在 Update 中偵測 `GetMouseButton`。

## 5. 新版 Input System 最佳實踐

- **強型別引用**：優先使用自動生成的 C# Class 引用 Action Asset。
- **事件驅動**：使用 `.performed` 與 `.canceled` 訂閱輸入事件，避免在 Update 內查詢 `wasPressedThisFrame`。
- **範例實作**：

```csharp
private void OnEnable()
{
    _controls.Player.Jump.performed += OnJump;
}

private void OnDisable()
{
    _controls.Player.Jump.performed -= OnJump;
}

private void OnJump(CallbackContext ctx)
{
    // 處理跳躍邏輯
}
```

## 6. 快速檢查清單

編輯或新增 `.cs` 時，確認：

- [ ] `GetComponent` / `Camera.main` 等已在 `Awake` 快取
- [ ] `OnEnable` 有對應的 `OnDisable` 反註冊
- [ ] 物理邏輯在 `FixedUpdate`，使用 `Time.fixedDeltaTime`
- [ ] 大量 Overlap 使用 `NonAlloc` 版本
- [ ] 動態材質使用 `MaterialPropertyBlock`
- [ ] Tag 比較使用 `CompareTag()`
- [ ] 輸入使用新版 Input System 事件訂閱
