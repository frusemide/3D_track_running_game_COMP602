using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// One cell of the shop grid. Each cell is a single baked image exported from Figma,
// with separate sprites for the unselected and selected states. Locked items and
// unused cells show the "?" placeholder sprite. Also reports mouse hover, which the
// shop uses to preview an item's stats before it's equipped.
public class ShopItemSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Button _button;
    [SerializeField] private Image _image;               // the whole cell graphic
    [SerializeField] private Sprite _placeholderSprite;  // the "?" cell

    private Sprite _normalSprite;
    private Sprite _selectedSprite;
    private Action _onHoverEnter;
    private Action _onHoverExit;

    public void Show(Sprite normal, Sprite selected, Action onClick,
                     Action onHoverEnter = null, Action onHoverExit = null)
    {
        _onHoverEnter = onHoverEnter;
        _onHoverExit = onHoverExit;

        _normalSprite = normal;
        _selectedSprite = selected != null ? selected : normal;   // fall back if selected art is missing

        _button.interactable = true;
        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => onClick());
        SetSelected(false);
    }

    public void ShowPlaceholder()
    {
        _onHoverEnter = null;   // "?" cells don't preview anything
        _onHoverExit = null;

        _normalSprite = _placeholderSprite;
        _selectedSprite = _placeholderSprite;

        _button.interactable = false;
        _button.onClick.RemoveAllListeners();
        SetSelected(false);
    }

    public void OnPointerEnter(PointerEventData eventData) => _onHoverEnter?.Invoke();
    public void OnPointerExit(PointerEventData eventData) => _onHoverExit?.Invoke();

    public void SetSelected(bool selected)
    {
        _image.sprite = selected ? _selectedSprite : _normalSprite;
    }
}
