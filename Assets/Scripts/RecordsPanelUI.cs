using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Racer records screen. Goes on the main menu's Records button and opens when it is clicked.
[RequireComponent(typeof(Button))]
public class RecordsPanelUI : MonoBehaviour
{
    private const float PanelWidth = 640f;
    private const float PanelHeight = 600f;
    private const float Padding = 40f;
    private const float RowHeight = 56f;

    private static readonly Color PanelColour = new Color(0.08f, 0.08f, 0.1f, 0.95f);
    private static readonly Color LabelColour = new Color(0.75f, 0.75f, 0.75f);
    private static readonly Color ButtonColour = new Color(0.75f, 0.15f, 0.15f);

    private GameObject _root;
    private TextMeshProUGUI _nameText;
    private TextMeshProUGUI _racesText;
    private TextMeshProUGUI _winsText;
    private TextMeshProUGUI _lossesText;
    private TextMeshProUGUI _winRateText;
    private TextMeshProUGUI _bestTimeText;

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(Open);
    }

    private void Open()
    {
        if (_root == null)
            Build();

        Refresh();
        _root.SetActive(true);
    }

    private void Close()
    {
        if (_root != null)
            _root.SetActive(false);
    }

    private void Refresh()
    {
        string playerName = PlayerNameStore.HasName ? PlayerNameStore.Name : "No name set";

        _nameText.text = playerName;
        _racesText.text = RacerRecords.Races.ToString();
        _winsText.text = RacerRecords.Wins.ToString();
        _lossesText.text = RacerRecords.Losses.ToString();
        _winRateText.text = RacerRecords.Races > 0 ? $"{Mathf.RoundToInt(RacerRecords.WinRate * 100f)}%" : "-";
        _bestTimeText.text = RacerRecords.FormatTime(RacerRecords.BestTime);
    }

    private void Build()
    {
        Canvas canvas = UiFactory.CreateOverlayCanvas(null, "RecordsCanvas", 100);
        _root = canvas.gameObject;

        // Blocks clicks on the menu behind the panel.
        var shade = UiFactory.CreatePanel(canvas.transform, "Shade", new Color(0f, 0f, 0f, 0.5f));
        UiFactory.Stretch(shade.rectTransform);

        var panel = UiFactory.CreatePanel(canvas.transform, "RecordsPanel", PanelColour);
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);

        TextMeshProUGUI title = UiFactory.CreateText(panel.transform, "Racer Records", 44, TextAlignmentOptions.Left);
        UiFactory.PlaceTopLeft(title.rectTransform, Padding, Padding, PanelWidth - Padding * 2f, 56f);

        float y = Padding + 80f;
        _nameText = AddRow(panel.transform, "Racer", ref y);
        _racesText = AddRow(panel.transform, "Races", ref y);
        _winsText = AddRow(panel.transform, "Wins", ref y);
        _lossesText = AddRow(panel.transform, "Losses", ref y);
        _winRateText = AddRow(panel.transform, "Win rate", ref y);
        _bestTimeText = AddRow(panel.transform, "Best time", ref y);

        var close = UiFactory.CreateButton(panel.transform, "Close", ButtonColour, Close);
        UiFactory.PlaceTopLeft((RectTransform)close.transform, (PanelWidth - 200f) / 2f, PanelHeight - Padding - 56f, 200f, 56f);
    }

    private TextMeshProUGUI AddRow(Transform parent, string label, ref float y)
    {
        float width = PanelWidth - Padding * 2f;

        TextMeshProUGUI labelText = UiFactory.CreateText(parent, label, 30, TextAlignmentOptions.Left);
        labelText.color = LabelColour;
        UiFactory.PlaceTopLeft(labelText.rectTransform, Padding, y, width / 2f, RowHeight);

        TextMeshProUGUI valueText = UiFactory.CreateText(parent, "", 30, TextAlignmentOptions.Right);
        UiFactory.PlaceTopLeft(valueText.rectTransform, Padding + width / 2f, y, width / 2f, RowHeight);

        y += RowHeight;
        return valueText;
    }
}
