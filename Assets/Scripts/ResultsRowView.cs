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

    [Header("Row colour scheme")]
    [Tooltip("Name/time text colour on your own row (red background -> white text). Placement badge colour comes from whichever sprite RaceHud passes in, not from here.")]
    [SerializeField] private Color _localPlayerTextColor = Color.white;
    [Tooltip("Name/time text colour on opponent rows (white background -> red text).")]
    [SerializeField] private Color _opponentTextColor = Color.red;

    public void Setup(Sprite placementBadge, string playerName, string timeLabel, bool isLocalPlayer)
    {
        if (_placementBadge != null)
        {
            _placementBadge.sprite = placementBadge;
            // Resize to the badge sprite's own dimensions -- 1st through 8th can each be a
            // different size, so a single fixed box would stretch/squash whichever doesn't
            // match its original RectTransform size.
            _placementBadge.SetNativeSize();
        }

        if (_nameText != null)
            _nameText.text = playerName;

        if (_timeText != null)
            _timeText.text = timeLabel;

        if (_background != null)
            _background.sprite = isLocalPlayer ? _localPlayerBackground : _normalBackground;

        Color textColor = isLocalPlayer ? _localPlayerTextColor : _opponentTextColor;
        if (_nameText != null)
            _nameText.color = textColor;
        if (_timeText != null)
            _timeText.color = textColor;
    }
}
