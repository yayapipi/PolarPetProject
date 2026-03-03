using PolarPet.Scripts.AICore.Gemini;
using PolarPet.Scripts.AICore.FalAI.FalAI.Rembg;
using PolarPet.Scripts.AICore.Utility;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PolarBearOutfitPanelController : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] GameObject _panelRoot;

    [Header("References")]
    [SerializeField] GeminiCore _geminiCore;
    [SerializeField] FalRembgCore _falRembgCore;
    [SerializeField] Image _polarBearImage;
    [SerializeField] GameObject _generatingMaskObject;

    [Header("Buttons")]
    [SerializeField] Button _closeButton;
    [SerializeField] Button _clothingButton1;
    [SerializeField] Button _clothingButton2;
    [SerializeField] Button _clothingButton3;

    [Header("Clothing Sprites")]
    [SerializeField] Sprite _clothingSprite1;
    [SerializeField] Sprite _clothingSprite2;
    [SerializeField] Sprite _clothingSprite3;

    [Header("Gemini Prompt")]
    [TextArea(2, 6)]
    [SerializeField] string _compositeInstruction =
        "Put the clothing item from the second image onto the polar bear in the first image. Keep the polar bear identity and style consistent.";

    [Header("Fal Rembg")]
    [SerializeField] bool _cropToBbox = true;

    bool _isGenerating;

    void Awake()
    {
        if (_panelRoot == null)
            _panelRoot = gameObject;

        BindButtons();
        SetGeneratingMaskVisible(false);
        SetButtonsInteractable(true);

        if (_panelRoot.activeSelf)
            _panelRoot.SetActive(false);
    }

    void OnDestroy()
    {
        UnbindButtons();
    }

    public void OpenPanel()
    {
        _isGenerating = false;
        SetGeneratingMaskVisible(false);
        SetButtonsInteractable(true);

        if (!_panelRoot.activeSelf)
            _panelRoot.SetActive(true);
    }

    public void ClosePanel()
    {
        if (_isGenerating)
            return;

        if (_panelRoot.activeSelf)
            _panelRoot.SetActive(false);
    }

    void OnClickClothing1()
    {
        TryCompositeWithClothing(_clothingSprite1);
    }

    void OnClickClothing2()
    {
        TryCompositeWithClothing(_clothingSprite2);
    }

    void OnClickClothing3()
    {
        TryCompositeWithClothing(_clothingSprite3);
    }

    void TryCompositeWithClothing(Sprite clothingSprite)
    {
        if (_isGenerating)
            return;

        if (_geminiCore == null)
        {
            Debug.LogError("PolarBearOutfitPanelController: GeminiCore 未設定。");
            return;
        }

        if (clothingSprite == null)
        {
            Debug.LogError("PolarBearOutfitPanelController: 衣服 Sprite 未設定。");
            return;
        }

        Sprite baseSprite = GetCurrentPolarBearSprite();
        if (baseSprite == null)
        {
            Debug.LogError("PolarBearOutfitPanelController: 北極熊圖片未設定。");
            return;
        }

        Texture2D baseTexture = ImageUtility.SpriteToTexture(baseSprite);
        Texture2D clothingTexture = ImageUtility.SpriteToTexture(clothingSprite);
        if (baseTexture == null || clothingTexture == null)
            return;

        SetGeneratingState(true);

        string instruction = string.IsNullOrWhiteSpace(_compositeInstruction)
            ? "Put the clothing item from the second image onto the polar bear in the first image."
            : _compositeInstruction.Trim();

        _geminiCore.CompositeImages(
            instruction,
            baseTexture,
            clothingTexture,
            OnCompositeSucceeded,
            OnCompositeFailed
        );
    }

    void OnCompositeSucceeded(Texture2D generatedTexture)
    {
        if (generatedTexture == null)
        {
            OnCompositeFailed("Gemini 回傳空圖片。");
            return;
        }

        if (_falRembgCore == null)
        {
            OnCompositeFailed("FalRembgCore 未設定。");
            return;
        }

        string imageDataUri = ImageUtility.TextureToDataUri(generatedTexture);
        if (string.IsNullOrWhiteSpace(imageDataUri))
        {
            OnCompositeFailed("生成圖片轉 Data URI 失敗。");
            return;
        }

        _falRembgCore.RemoveBackground(
            imageDataUri,
            OnRembgSucceeded,
            OnRembgFailed,
            _cropToBbox
        );
    }

    void OnRembgSucceeded(Texture2D rembgTexture)
    {
        if (rembgTexture == null)
        {
            OnRembgFailed("FalAI 回傳空圖片。");
            return;
        }

        Sprite rembgSprite = ImageUtility.TextureToSprite(rembgTexture);
        if (rembgSprite == null)
        {
            OnRembgFailed("去背圖片轉 Sprite 失敗。");
            return;
        }

        ApplyGeneratedSprite(rembgSprite);
        SetGeneratingState(false);
    }

    void OnRembgFailed(string errorMessage)
    {
        Debug.LogError($"PolarBearOutfitPanelController: FalAI 去背失敗。{errorMessage}");
        SetGeneratingState(false);
    }

    void OnCompositeFailed(string errorMessage)
    {
        Debug.LogError($"PolarBearOutfitPanelController: 合成失敗。{errorMessage}");
        SetGeneratingState(false);
    }

    void ApplyGeneratedSprite(Sprite sprite)
    {
        if (_polarBearImage == null)
            return;

        _polarBearImage.sprite = sprite;
        _polarBearImage.preserveAspect = true;
    }

    Sprite GetCurrentPolarBearSprite()
    {
        if (_polarBearImage != null && _polarBearImage.sprite != null)
            return _polarBearImage.sprite;
        
        return null;
    }

    void SetGeneratingState(bool generating)
    {
        _isGenerating = generating;
        SetGeneratingMaskVisible(generating);
        SetButtonsInteractable(!generating);
    }

    void SetGeneratingMaskVisible(bool isVisible)
    {
        if (_generatingMaskObject == null)
            return;
        if (_generatingMaskObject.activeSelf == isVisible)
            return;

        _generatingMaskObject.SetActive(isVisible);
    }

    void SetButtonsInteractable(bool isInteractable)
    {
        if (_closeButton != null)
            _closeButton.interactable = isInteractable;
        if (_clothingButton1 != null)
            _clothingButton1.interactable = isInteractable;
        if (_clothingButton2 != null)
            _clothingButton2.interactable = isInteractable;
        if (_clothingButton3 != null)
            _clothingButton3.interactable = isInteractable;
    }

    void BindButtons()
    {
        if (_closeButton != null)
            _closeButton.onClick.AddListener(ClosePanel);
        if (_clothingButton1 != null)
            _clothingButton1.onClick.AddListener(OnClickClothing1);
        if (_clothingButton2 != null)
            _clothingButton2.onClick.AddListener(OnClickClothing2);
        if (_clothingButton3 != null)
            _clothingButton3.onClick.AddListener(OnClickClothing3);
    }

    void UnbindButtons()
    {
        if (_closeButton != null)
            _closeButton.onClick.RemoveListener(ClosePanel);
        if (_clothingButton1 != null)
            _clothingButton1.onClick.RemoveListener(OnClickClothing1);
        if (_clothingButton2 != null)
            _clothingButton2.onClick.RemoveListener(OnClickClothing2);
        if (_clothingButton3 != null)
            _clothingButton3.onClick.RemoveListener(OnClickClothing3);
    }
}
