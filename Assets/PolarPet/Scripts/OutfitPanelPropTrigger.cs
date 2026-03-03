using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class OutfitPanelPropTrigger : MonoBehaviour
{
    [SerializeField] PolarBearOutfitPanelController _outfitPanelController;

    void OnMouseDown()
    {
        if (!isActiveAndEnabled)
            return;

        if (_outfitPanelController == null)
        {
            Debug.LogWarning("OutfitPanelPropTrigger: Outfit 面板控制器未設定。");
            return;
        }

        _outfitPanelController.OpenPanel();
    }
}
