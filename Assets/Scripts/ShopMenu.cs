using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Lobby cosmetics shop. Opened by a ShopKiosk (E) or the pause menu's Shop button.
// Local UI only: clicks update the preview instantly and send a request to the host,
// whose networked update then changes the real character for everyone.
public class ShopMenu : MonoBehaviour
{
    // Each tab is one baked image exported from Figma, with its own unselected
    // (red outline) and selected (filled red) sprite.
    [System.Serializable]
    private class Tab
    {
        public Image Image;
        public Sprite NormalSprite;
        public Sprite SelectedSprite;
        [Tooltip("How many cells this tab shows, including the 'None' cell on Head/Feet and any '?' cells.")]
        [Min(1)] public int CellCount = 6;
    }

    [Header("Data")]
    [SerializeField] private CosmeticDatabase _database;

    [Header("Layout")]
    [SerializeField] private GameObject _shopRoot;
    [SerializeField] private Transform _itemGrid;
    [SerializeField] private ShopItemSlot _slotPrefab;
    [SerializeField] private Sprite _noneIcon;           // "remove item" cell for head/feet, unselected
    [SerializeField] private Sprite _noneSelectedIcon;   // "remove item" cell, selected

    [Header("Tabs: element 0 = Head, 1 = Colour, 2 = Feet")]
    [SerializeField] private Tab[] _tabs;

    [Header("Preview & stats")]
    [SerializeField] private CosmeticVisuals _previewModel;
    [SerializeField] private Camera _previewCamera;
    [SerializeField] private ShopStatsPanel _statsPanel;

    public bool IsOpen { get; private set; }

    private readonly List<ShopItemSlot> _cells = new List<ShopItemSlot>();
    private int[] _cellItemIndex;           // which item index each cell shows (-1 = None, -2 = placeholder)
    private PlayerCustomisation _target;
    private CosmeticSlot _currentTab = CosmeticSlot.Head;
    private bool _closeRequested;

    // Local copy of the loadout, updated instantly on click so the preview doesn't
    // wait for the host's networked update to come back.
    private int _head = -1, _colour = 0, _feet = -1;

    private const int DefaultCellCount = 6;

    private void Start()
    {
        // Create enough cells for the largest tab; RefreshGrid hides the extras per tab.
        int maxCells = 1;
        foreach (Tab tab in _tabs)
            maxCells = Mathf.Max(maxCells, CellCountFor(tab));

        for (int i = 0; i < maxCells; i++)
            _cells.Add(Instantiate(_slotPrefab, _itemGrid));
        _cellItemIndex = new int[maxCells];

        _shopRoot.SetActive(false);
        if (_previewCamera != null) _previewCamera.enabled = false;
    }

    // --- Open / close ---

    public void Open() => Open(FindLocalCustomisation());   // from the pause menu

    public void Open(PlayerCustomisation target)             // from a ShopKiosk
    {
        if (target == null || IsOpen) return;

        // No gear changes mid-race (the host would reject them anyway).
        Player player = target.GetComponent<Player>();
        if (player != null && player.IsRacing) return;

        _target = target;
        _head = target.HeadIndex;
        _colour = target.ColourIndex;
        _feet = target.FeetIndex;

        IsOpen = true;
        _shopRoot.SetActive(true);
        if (_previewCamera != null) _previewCamera.enabled = true;   // only render while open

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        GameplayInputBlock.Blocked = true;

        SelectTab((int)_currentTab);
        RefreshPreviewAndStats();
    }

    public void Close()   // wired to CloseButton
    {
        if (!IsOpen) return;

        IsOpen = false;
        ClearStatsPreview();
        _shopRoot.SetActive(false);
        if (_previewCamera != null) _previewCamera.enabled = false;
        _target = null;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        GameplayInputBlock.Blocked = false;
    }

    private void Update()
    {
        if (IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            _closeRequested = true;
    }

    // Close after every Update has run, so the same Escape press doesn't also open
    // PauseMenu (which opens whenever input isn't blocked).
    private void LateUpdate()
    {
        if (!_closeRequested) return;
        _closeRequested = false;
        Close();
    }

    // --- Tabs ---

    public void SelectTab(int tab)   // wired to tab buttons: 0 Head, 1 Colour, 2 Feet
    {
        _currentTab = (CosmeticSlot)tab;

        for (int i = 0; i < _tabs.Length; i++)
        {
            if (_tabs[i].Image == null) continue;
            _tabs[i].Image.sprite = i == tab ? _tabs[i].SelectedSprite : _tabs[i].NormalSprite;
        }

        ClearStatsPreview();
        RefreshGrid();
    }

    // --- Grid ---

    private void RefreshGrid()
    {
        CosmeticItem[] items = _database.GetItems(_currentTab) ?? System.Array.Empty<CosmeticItem>();

        // Show only this tab's number of cells. Hidden cells take no space in the grid.
        int tabIndex = (int)_currentTab;
        int cellCount = tabIndex < _tabs.Length ? CellCountFor(_tabs[tabIndex]) : DefaultCellCount;
        int visible = Mathf.Min(cellCount, _cells.Count);

        for (int i = 0; i < _cells.Count; i++)
        {
            _cells[i].gameObject.SetActive(i < visible);
            _cellItemIndex[i] = -2;   // hidden cells never count as selected
        }

        int needed = items.Length + (_currentTab != CosmeticSlot.Colour ? 1 : 0);
        if (needed > visible)
            Debug.LogWarning($"[ShopMenu] {_currentTab} tab has {needed} entries but only {visible} cells -- raise its Cell Count on ShopMenu's Tabs list.");

        int cell = 0;

        // Head and feet start with a "None" cell so items can be taken off.
        if (_currentTab != CosmeticSlot.Colour)
        {
            _cells[cell].Show(_noneIcon, _noneSelectedIcon, () => Select(-1),
                              () => PreviewStats(-1), ClearStatsPreview);
            _cellItemIndex[cell] = -1;
            cell++;
        }

        for (int i = 0; i < items.Length && cell < visible; i++, cell++)
        {
            CosmeticItem item = items[i];

            if (item == null || !item.UnlockedByDefault)
            {
                _cells[cell].ShowPlaceholder();   // locked items show as "?"
                _cellItemIndex[cell] = -2;
                continue;
            }

            int index = i;   // copy for the lambda
            _cells[cell].Show(item.Icon, item.SelectedIcon, () => Select(index),
                              () => PreviewStats(index), ClearStatsPreview);
            _cellItemIndex[cell] = i;
        }

        // Fill the rest of this tab's cells with "?" for future items.
        for (; cell < visible; cell++)
        {
            _cells[cell].ShowPlaceholder();
            _cellItemIndex[cell] = -2;
        }

        UpdateHighlights();
    }

    // Unset counts (e.g. tab elements created before CellCount existed) fall back to 6.
    private static int CellCountFor(Tab tab) =>
        tab != null && tab.CellCount > 0 ? tab.CellCount : DefaultCellCount;

    private void UpdateHighlights()
    {
        int equipped = GetEquipped(_currentTab);
        for (int i = 0; i < _cells.Count; i++)
            _cells[i].SetSelected(_cellItemIndex[i] != -2 && _cellItemIndex[i] == equipped);
    }

    private void Select(int index)
    {
        switch (_currentTab)
        {
            case CosmeticSlot.Head: _head = index; break;
            case CosmeticSlot.Colour: _colour = index; break;
            case CosmeticSlot.Feet: _feet = index; break;
        }

        if (_target != null)
            _target.RequestItem(_currentTab, index);   // saves to the profile + asks the host

        UpdateHighlights();
        RefreshPreviewAndStats();
    }

    private int GetEquipped(CosmeticSlot slot) => slot switch
    {
        CosmeticSlot.Head => _head,
        CosmeticSlot.Colour => _colour,
        CosmeticSlot.Feet => _feet,
        _ => -1
    };

    // --- Preview + stats ---

    private void RefreshPreviewAndStats()
    {
        CosmeticItem head = _database.Get(CosmeticSlot.Head, _head);
        CosmeticItem colour = _database.Get(CosmeticSlot.Colour, _colour);
        CosmeticItem feet = _database.Get(CosmeticSlot.Feet, _feet);

        if (_previewModel != null)
        {
            _previewModel.ApplyHead(head);
            _previewModel.ApplyColour(colour);
            _previewModel.ApplyFeet(feet);
        }

        if (_statsPanel != null)
            _statsPanel.Show(head, colour, feet);
    }

    // Hover preview: the current loadout with the hovered item swapped into this tab's slot.
    private void PreviewStats(int index)
    {
        if (_statsPanel == null) return;

        int head = _head, colour = _colour, feet = _feet;
        switch (_currentTab)
        {
            case CosmeticSlot.Head: head = index; break;
            case CosmeticSlot.Colour: colour = index; break;
            case CosmeticSlot.Feet: feet = index; break;
        }

        _statsPanel.Preview(
            _database.Get(CosmeticSlot.Head, head),
            _database.Get(CosmeticSlot.Colour, colour),
            _database.Get(CosmeticSlot.Feet, feet));
    }

    private void ClearStatsPreview()
    {
        if (_statsPanel != null)
            _statsPanel.ClearPreview();
    }

    private static PlayerCustomisation FindLocalCustomisation()
    {
        foreach (var pc in FindObjectsByType<PlayerCustomisation>(FindObjectsSortMode.None))
            if (pc.Object != null && pc.HasInputAuthority)
                return pc;
        return null;
    }
}
