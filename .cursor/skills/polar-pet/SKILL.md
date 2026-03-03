---
name: polar-pet
description: Guides incremental development of Polar Pet, a 2D Unity pixel-art AI polar bear pet game for PC Windows. Covers pet behaviors (move, sleep, idle, think), mouse drag, feeding, bathing, UI drag-out items, Tab chat, mirror AI outfit change, and affection/expression from chat. Use when implementing or editing Polar Pet features, or when the user asks to add Polar Pet functionality step by step.
---

# Polar Pet — 開發指引

## 專案概覽

- **名稱**: Polar Pet（北極寵物）
- **類型**: 2D 可愛 AI 北極熊寵物互動遊戲
- **引擎**: Unity
- **平台**: PC Windows
- **美術**: 像素風格、可愛療癒、放鬆、白色北極氛圍

## 開發原則（必守）

**僅實作使用者當下指示的功能，不要一次開發全部。**

- 每次只做使用者明確要求的一塊（例如：先做寵物移動與待機，或先做拖拽）。
- 若使用者說「先做 OO」「幫我加 XX」「下一步做 YY」，只做該項並確認完成再等下一步指示。
- 不要主動補齊整份功能清單；完整功能列表放在 [reference.md](reference.md) 供對照與規劃用。

## 實作時注意

1. **與專案規範一致**：C# 與 Unity 實作須符合本專案中的 `unity-senior-csharp` 技能（效能、Input System、命名等）。
2. **平台與輸入**：目標為 PC Windows，操作以滑鼠為主（點擊、拖拽）。
3. **風格**：程式與場景設計需符合「像素、可愛、北極、療癒」的整體調性。

## 功能清單（對照用）

完整功能與子項見 [reference.md](reference.md)。摘要如下：

- **寵物行為**: 場景內自主移動、睡覺、待機、思考
- **互動**: 滑鼠拖拽寵物、食物拖到寵物身上餵食、肥皂搓澡
- **UI**: 從 UI 拖出食物與肥皂
- **AI**: Tab 開啟對話輸入、鏡子開啟 AI 換裝、聊天影響好感度與表情

實作時只做使用者指定的項目，其餘僅作參考。
