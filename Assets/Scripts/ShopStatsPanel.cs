using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Display-only stat readout for the shop: base stats + everything equipped.
// Hovering an item previews how it would change each stat:
//   increase -> the bar extends in the Increase colour, number shows e.g. "6 (+1)"
//   decrease -> the part you'd lose shows in the Decrease colour, number shows e.g. "4 (-1)"
// Nothing here affects gameplay yet -- when mechanics are built, the host will read the
// same ItemStats from the equipped items.
public class ShopStatsPanel : MonoBehaviour
{
    [System.Serializable]
    private class StatRow
    {
        [Tooltip("Current value. Image Type = Filled, Horizontal. Drawn on top of Preview Fill.")]
        public Image Fill;
        [Tooltip("Hover preview layer. Same setup as Fill, but ABOVE it in the Hierarchy so it draws behind.")]
        public Image PreviewFill;
        public TMP_Text Value;
    }

    [Header("Values")]
    [SerializeField] private ItemStats _baseStats = new ItemStats
    {
        TopSpeed = 5, Acceleration = 5, Boost = 5, JumpDistance = 5, Recovery = 5
    };
    [SerializeField, Min(1)] private int _maxStat = 10;

    [Header("Rows")]
    [SerializeField] private StatRow _topSpeed;
    [SerializeField] private StatRow _acceleration;
    [SerializeField] private StatRow _boost;
    [SerializeField] private StatRow _jumpDistance;
    [SerializeField] private StatRow _recovery;

    [Header("Hover preview colours")]
    [SerializeField] private Color _increaseFillColour = new Color32(0x5C, 0xCB, 0x6A, 0xFF);   // green tail on the bar
    [SerializeField] private Color _decreaseFillColour = new Color32(0x9E, 0x9E, 0x9E, 0xFF);   // grey "lost" part of the bar
    [Tooltip("Text sits on the red panel, so avoid red here.")]
    [SerializeField] private Color _increaseTextColour = new Color32(0x9B, 0xFF, 0xA0, 0xFF);
    [Tooltip("Text sits on the red panel, so avoid red here.")]
    [SerializeField] private Color _decreaseTextColour = new Color32(0xFF, 0xE0, 0x66, 0xFF);

    private ItemStats _current;

    // Equipped loadout changed: show its totals and drop any hover preview.
    public void Show(params CosmeticItem[] equipped)
    {
        _current = Total(equipped);
        ClearPreview();
    }

    // Hovering an item: compare the loadout-with-that-item against what's equipped now.
    public void Preview(params CosmeticItem[] loadout)
    {
        ItemStats preview = Total(loadout);
        SetRow(_topSpeed, _current.TopSpeed, preview.TopSpeed);
        SetRow(_acceleration, _current.Acceleration, preview.Acceleration);
        SetRow(_boost, _current.Boost, preview.Boost);
        SetRow(_jumpDistance, _current.JumpDistance, preview.JumpDistance);
        SetRow(_recovery, _current.Recovery, preview.Recovery);
    }

    public void ClearPreview()
    {
        SetRow(_topSpeed, _current.TopSpeed, _current.TopSpeed);
        SetRow(_acceleration, _current.Acceleration, _current.Acceleration);
        SetRow(_boost, _current.Boost, _current.Boost);
        SetRow(_jumpDistance, _current.JumpDistance, _current.JumpDistance);
        SetRow(_recovery, _current.Recovery, _current.Recovery);
    }

    private ItemStats Total(CosmeticItem[] items)
    {
        ItemStats total = _baseStats;
        if (items != null)
            foreach (CosmeticItem item in items)
                if (item != null) total += item.Stats;
        return total;
    }

    private void SetRow(StatRow row, int current, int preview)
    {
        if (row == null) return;

        float currentFraction = Fraction(current);
        float previewFraction = Fraction(preview);
        int delta = preview - current;

        // Fill shows the lower of the two; PreviewFill (behind it) shows the higher, so the
        // difference is visible: a green tail for a gain, a grey segment for a loss.
        if (row.Fill != null)
            row.Fill.fillAmount = Mathf.Min(currentFraction, previewFraction);

        if (row.PreviewFill != null)
        {
            row.PreviewFill.enabled = delta != 0;
            row.PreviewFill.fillAmount = Mathf.Max(currentFraction, previewFraction);
            row.PreviewFill.color = delta > 0 ? _increaseFillColour : _decreaseFillColour;
        }

        if (row.Value != null)
        {
            if (delta == 0)
            {
                row.Value.text = current.ToString();
            }
            else
            {
                Color colour = delta > 0 ? _increaseTextColour : _decreaseTextColour;
                string sign = delta > 0 ? "+" : "-";
                row.Value.text = $"{preview} <color=#{ColorUtility.ToHtmlStringRGB(colour)}>({sign}{Mathf.Abs(delta)})</color>";
            }
        }
    }

    private float Fraction(int value) => Mathf.Clamp(value, 0, _maxStat) / (float)_maxStat;
}
