using System.Collections.Generic;
using System.Text;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// Host-only player management panel, toggled with the B key. Lists players in the session with a
// Ban button each, and previously banned players with an Unban button each.
public class BanPanelUI : MonoBehaviour
{
    private const float PanelWidth = 640f;
    private const float PanelHeight = 640f;
    private const float Padding = 30f;
    private const float RowHeight = 48f;
    private const float ButtonWidth = 130f;
    private const int MaxBannedRows = 5;
    private const float RefreshInterval = 0.5f;

    private static readonly Color PanelColour = new Color(0.08f, 0.08f, 0.1f, 0.92f);
    private static readonly Color BanColour = new Color(0.75f, 0.15f, 0.15f);
    private static readonly Color UnbanColour = new Color(0.25f, 0.35f, 0.5f);
    private static readonly Color CloseColour = new Color(0.3f, 0.3f, 0.3f);
    private static readonly Color HeadingColour = new Color(1f, 0.8f, 0.3f);

    private GameObject _root;
    private RectTransform _content;
    private readonly List<GameObject> _rows = new List<GameObject>();
    private string _shownState = "";
    private float _nextRefresh;
    private bool _isOpen;

    private void Update()
    {
        bool isHost = BanManager.Instance != null && BanManager.Instance.IsHost;

        if (_isOpen && !isHost)
            Close(false);

        var keyboard = Keyboard.current;
        if (isHost && keyboard != null && keyboard.bKey.wasPressedThisFrame)
        {
            if (_isOpen)
                Close(true);
            else if (!GameplayInputBlock.Blocked)
                Open();
        }

        if (_isOpen && Time.unscaledTime >= _nextRefresh)
        {
            _nextRefresh = Time.unscaledTime + RefreshInterval;
            Refresh();
        }
    }

    private void Open()
    {
        if (_root == null)
            Build();

        _root.SetActive(true);
        _isOpen = true;
        _shownState = "";
        Refresh();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        GameplayInputBlock.Blocked = true;
    }

    private void Close() => Close(true);

    // The cursor is only re-locked when returning to gameplay, not when the session has ended.
    private void Close(bool relockCursor)
    {
        if (_root != null)
            _root.SetActive(false);
        _isOpen = false;
        GameplayInputBlock.Blocked = false;

        if (relockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void Build()
    {
        Canvas canvas = UiFactory.CreateOverlayCanvas(transform, "BanPanelCanvas", 500);
        _root = canvas.gameObject;

        var panel = UiFactory.CreatePanel(canvas.transform, "BanPanel", PanelColour);
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);

        TextMeshProUGUI title = UiFactory.CreateText(panel.transform, "Player Management", 36, TextAlignmentOptions.Left);
        UiFactory.PlaceTopLeft(title.rectTransform, Padding, Padding, PanelWidth - Padding * 2f, 50f);

        TextMeshProUGUI hint = UiFactory.CreateText(panel.transform, "Host only. Press B to close.", 22, TextAlignmentOptions.Left);
        hint.color = new Color(0.75f, 0.75f, 0.75f);
        UiFactory.PlaceTopLeft(hint.rectTransform, Padding, Padding + 48f, PanelWidth - Padding * 2f, 30f);

        var close = UiFactory.CreateButton(panel.transform, "Close", CloseColour, Close);
        UiFactory.PlaceTopLeft((RectTransform)close.transform, PanelWidth - Padding - ButtonWidth, Padding, ButtonWidth, 44f);

        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(panel.transform, false);
        _content = (RectTransform)content.transform;
        UiFactory.PlaceTopLeft(_content, Padding, Padding + 100f, PanelWidth - Padding * 2f, PanelHeight - Padding * 2f - 100f);
    }

    // Rebuilds the rows only when the player or ban list has changed, so clicks are not lost.
    private void Refresh()
    {
        var manager = BanManager.Instance;
        if (manager == null || manager.Runner == null)
            return;

        NetworkRunner runner = manager.Runner;

        var players = new List<PlayerRef>();
        foreach (var p in runner.ActivePlayers)
        {
            if (p != runner.LocalPlayer)
                players.Add(p);
        }

        var state = new StringBuilder();
        foreach (var p in players)
            state.Append(p.PlayerId).Append(':').Append(manager.GetPlayerName(p)).Append(';');
        state.Append('|');
        foreach (var entry in BanList.Entries)
            state.Append(entry.DeviceId).Append(';');

        if (state.ToString() == _shownState)
            return;
        _shownState = state.ToString();

        foreach (var row in _rows)
            Destroy(row);
        _rows.Clear();

        float width = _content.rect.width;
        float y = 0f;

        AddHeading("Players in session", ref y, width);
        if (players.Count == 0)
            AddMessage("No other players have joined.", ref y, width);
        foreach (var p in players)
        {
            PlayerRef target = p;
            AddRow(manager.GetPlayerName(p), "Ban", BanColour, () => manager.Ban(target), ref y, width);
        }

        y += 16f;
        AddHeading("Banned players", ref y, width);
        var banned = BanList.Entries;
        if (banned.Count == 0)
            AddMessage("Nobody is banned.", ref y, width);
        for (int i = 0; i < banned.Count && i < MaxBannedRows; i++)
        {
            string deviceId = banned[i].DeviceId;
            AddRow(banned[i].Name, "Unban", UnbanColour, () => BanList.Unban(deviceId), ref y, width);
        }
        if (banned.Count > MaxBannedRows)
            AddMessage($"and {banned.Count - MaxBannedRows} more", ref y, width);
    }

    private void AddHeading(string text, ref float y, float width)
    {
        TextMeshProUGUI heading = UiFactory.CreateText(_content, text, 26, TextAlignmentOptions.Left);
        heading.color = HeadingColour;
        UiFactory.PlaceTopLeft(heading.rectTransform, 0f, y, width, 36f);
        _rows.Add(heading.gameObject);
        y += 40f;
    }

    private void AddMessage(string text, ref float y, float width)
    {
        TextMeshProUGUI message = UiFactory.CreateText(_content, text, 24, TextAlignmentOptions.Left);
        message.color = new Color(0.75f, 0.75f, 0.75f);
        UiFactory.PlaceTopLeft(message.rectTransform, 0f, y, width, RowHeight);
        _rows.Add(message.gameObject);
        y += RowHeight;
    }

    private void AddRow(string playerName, string buttonLabel, Color buttonColour, UnityEngine.Events.UnityAction onClick, ref float y, float width)
    {
        TextMeshProUGUI nameText = UiFactory.CreateText(_content, playerName, 26, TextAlignmentOptions.Left);
        UiFactory.PlaceTopLeft(nameText.rectTransform, 0f, y, width - ButtonWidth - 20f, RowHeight - 6f);
        _rows.Add(nameText.gameObject);

        var button = UiFactory.CreateButton(_content, buttonLabel, buttonColour, onClick);
        UiFactory.PlaceTopLeft((RectTransform)button.transform, width - ButtonWidth, y, ButtonWidth, RowHeight - 6f);
        _rows.Add(button.gameObject);

        y += RowHeight;
    }
}
