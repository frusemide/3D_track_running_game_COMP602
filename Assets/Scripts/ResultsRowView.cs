using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One row in the Results panel (RaceHud spawns one per participant). Purely a view --
// RaceHud computes placement/name/time and calls Setup; this just pushes those values
// onto the row's widgets and swaps the row background between its normal and
// "this is you" highlighted state (Rectangle 19 / Rectangle 19-1 in the Figma export).
public class ResultsRowView : MonoBehaviour
{
    [SerializeField] private Image _background;
    [SerializeField] private Sprite _normalBackground;
    [SerializeField] private Sprite _localPlayerBackground;

    [SerializeField] private Image _placementBadge;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _timeText;

    public void Setup(Sprite placementBadge, string playerName, string timeLabel, bool isLocalPlayer)
    {
        if (_placementBadge != null)
            _placementBadge.sprite = placementBadge;

        if (_nameText != null)
            _nameText.text = playerName;

        if (_timeText != null)
            _timeText.text = timeLabel;

        if (_background != null)
            _background.sprite = isLocalPlayer ? _localPlayerBackground : _normalBackground;
    }
}
