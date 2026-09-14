using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Local, presentation-only race HUD -- reads already-networked state from RaceManager /
// RaceLogic / Player and drives the Figma-designed UI. Not a NetworkBehaviour: nothing
// here writes networked state, it only reads it (same as ThirdPersonCamera).
//
// Put this on a Canvas in the race scene (Screen Space - Overlay). Covers five pieces
// from the mockups:
//  - Countdown banner: Ready -> Set -> GO!, driven by RaceManager.Phase/PhaseTimeRemaining.
//  - Progress bar: one pin per participant, positioned by Z-progress toward the finish
//    line, with a crown on whoever currently leads.
//  - Timer: counts up during Racing, freezes at YOUR OWN finish time once you finish.
//  - Personal Finish! banner: shown the moment your own HasFinished flips true, with
//    your placement at that moment. Disappears once the race as a whole moves past
//    Racing (so it doesn't linger through Results).
//  - Results panel: the full standings list, shown once RaceManager.Phase reaches
//    Results/Podium (built once per race, not every frame).
public class RaceHud : MonoBehaviour
{
    [System.Serializable]
    private struct ProgressPin
    {
        public GameObject Root;
        public RectTransform Rect;
        public Image Icon;
        public GameObject Crown;
    }

    [Header("Countdown banner (Ready / Set / GO!)")]
    [SerializeField] private GameObject _countdownBannerRoot;
    [SerializeField] private Image _countdownBannerImage;
    [SerializeField] private Sprite _readySprite;
    [SerializeField] private Sprite _setSprite;
    [SerializeField] private Sprite _goSprite;
    [Tooltip("Countdown shows \"Ready\" while more than this many seconds remain, then switches to \"Set\".")]
    [SerializeField] private float _readyThresholdSeconds = 1.5f;
    [Tooltip("How long \"GO!\" stays on screen after Racing begins.")]
    [SerializeField] private float _goDisplaySeconds = 1f;

    [Header("Timer")]
    [SerializeField] private TMP_Text _timerText;

    [Header("Progress bar")]
    [Tooltip("Anchored positions of these two mark the bar's start/end -- pins are Lerp'd between them.")]
    [SerializeField] private RectTransform _trackStart;
    [SerializeField] private RectTransform _trackEnd;
    [Tooltip("Up to 8 pre-placed pin slots (project supports up to 8 players/session). Unused slots are hidden.")]
    [SerializeField] private ProgressPin[] _pins = new ProgressPin[8];
    [SerializeField] private Sprite _selfPinSprite;
    [SerializeField] private Sprite _opponentPinSprite;

    [Header("Personal finish banner")]
    [SerializeField] private GameObject _finishBannerRoot;
    [SerializeField] private Image _placementBadge;
    [Tooltip("Index 0 = 1st (\"Group 17\" in your RaceUI export), 1 = 2nd, 2 = 3rd, 3 = 4th. 5th+ reuses the last sprite.")]
    [SerializeField] private Sprite[] _placementSprites = new Sprite[4];
    [Tooltip("Shown only when your placement is 1st.")]
    [SerializeField] private GameObject _crownIcon;

    [Header("Results panel")]
    [SerializeField] private GameObject _resultsRoot;
    [SerializeField] private Transform _resultsListContainer;
    [SerializeField] private ResultsRowView _resultsRowPrefab;

    [Header("Key legend")]
    [Tooltip("Static hint bar (Bottom bar / Menu key legend). Left active for the whole race scene -- toggle it yourself if you want it hidden during specific phases.")]
    [SerializeField] private GameObject _keyLegendRoot;

    private RaceManager _raceManager;
    private Player _localPlayer;
    private readonly List<ResultsRowView> _spawnedRows = new List<ResultsRowView>();
    private bool _resultsBuilt;

    private void Awake()
    {
        _raceManager = FindFirstObjectByType<RaceManager>();

        if (_keyLegendRoot != null)
            _keyLegendRoot.SetActive(true);
    }

    private void Update()
    {
        if (_raceManager == null)
        {
            _raceManager = FindFirstObjectByType<RaceManager>();
            if (_raceManager == null) return;
        }

        if (_raceManager.Runner == null) return; // not spawned/connected yet

        if (_localPlayer == null)
            _localPlayer = FindLocalPlayer();

        UpdateCountdownBanner();
        UpdateTimer();
        UpdateProgressBar();
        UpdateFinishBanner();
        UpdateResultsPanel();
    }

    private static Player FindLocalPlayer()
    {
        foreach (var p in FindObjectsByType<Player>(FindObjectsSortMode.None))
        {
            if (p.Object != null && p.Object.HasInputAuthority)
                return p;
        }
        return null;
    }

    // --- Countdown banner ---

    private void UpdateCountdownBanner()
    {
        if (_countdownBannerRoot == null) return;

        switch (_raceManager.Phase)
        {
            case RaceManager.RacePhase.Countdown:
                _countdownBannerRoot.SetActive(true);
                if (_countdownBannerImage != null)
                {
                    _countdownBannerImage.sprite = _raceManager.PhaseTimeRemaining > _readyThresholdSeconds
                        ? _readySprite
                        : _setSprite;
                }
                break;

            case RaceManager.RacePhase.Racing:
                float sinceStart = TickSeconds(_raceManager.Runner.Tick);
                bool showGo = sinceStart <= _goDisplaySeconds;
                _countdownBannerRoot.SetActive(showGo);
                if (showGo && _countdownBannerImage != null)
                    _countdownBannerImage.sprite = _goSprite;
                break;

            default:
                _countdownBannerRoot.SetActive(false);
                break;
        }
    }

    // --- Timer ---

    private void UpdateTimer()
    {
        if (_timerText == null) return;

        float elapsed = 0f;

        if (_localPlayer != null && _localPlayer.HasFinished)
            elapsed = TickSeconds(_localPlayer.FinishTick);
        else if (_raceManager.Phase == RaceManager.RacePhase.Racing)
            elapsed = TickSeconds(_raceManager.Runner.Tick);

        _timerText.text = FormatTime(elapsed);
    }

    private float TickSeconds(int tick) =>
        Mathf.Max(0f, (tick - _raceManager.RaceStartTick) * _raceManager.Runner.DeltaTime);

    private static string FormatTime(float seconds)
    {
        int minutes = Mathf.FloorToInt(seconds / 60f);
        float remainder = seconds - minutes * 60f;
        return $"{minutes}:{remainder:00.000}";
    }

    // --- Progress bar ---

    private void UpdateProgressBar()
    {
        if (_trackStart == null || _trackEnd == null) return;

        var participants = RaceLogic.GetParticipants(_raceManager.Runner);

        // Whoever's furthest along right now gets the crown -- recomputed every frame,
        // so it can move mid-race, not just once someone finishes.
        Player leader = null;
        float bestProgress = -1f;
        foreach (var p in participants)
        {
            float progress = Mathf.Clamp01(p.transform.position.z / _raceManager.FinishZ);
            if (progress > bestProgress)
            {
                bestProgress = progress;
                leader = p;
            }
        }

        for (int i = 0; i < _pins.Length; i++)
        {
            var pin = _pins[i];
            if (pin.Root == null) continue;

            if (i >= participants.Count)
            {
                pin.Root.SetActive(false);
                continue;
            }

            pin.Root.SetActive(true);
            Player p = participants[i];

            float t = Mathf.Clamp01(p.transform.position.z / _raceManager.FinishZ);
            if (pin.Rect != null)
                pin.Rect.anchoredPosition = Vector2.Lerp(_trackStart.anchoredPosition, _trackEnd.anchoredPosition, t);

            bool isSelf = p.Object != null && p.Object.HasInputAuthority;
            if (pin.Icon != null)
                pin.Icon.sprite = isSelf ? _selfPinSprite : _opponentPinSprite;

            if (pin.Crown != null)
                pin.Crown.SetActive(p == leader);
        }
    }

    // --- Personal finish banner ---

    private void UpdateFinishBanner()
    {
        if (_finishBannerRoot == null) return;

        bool show = _localPlayer != null
            && _localPlayer.HasFinished
            && _raceManager.Phase == RaceManager.RacePhase.Racing;

        _finishBannerRoot.SetActive(show);
        if (!show) return;

        int placement = RaceLogic.RankParticipants(_raceManager.Runner).IndexOf(_localPlayer); // 0-based
        Sprite badge = PlacementSprite(placement);

        if (_placementBadge != null)
            _placementBadge.sprite = badge;

        if (_crownIcon != null)
            _crownIcon.SetActive(placement == 0);
    }

    private Sprite PlacementSprite(int zeroBasedPlacement)
    {
        if (_placementSprites.Length == 0) return null;
        int index = Mathf.Clamp(zeroBasedPlacement, 0, _placementSprites.Length - 1);
        return _placementSprites[index];
    }

    // --- Results panel ---

    private void UpdateResultsPanel()
    {
        if (_resultsRoot == null) return;

        bool show = _raceManager.Phase == RaceManager.RacePhase.Results
                 || _raceManager.Phase == RaceManager.RacePhase.Podium;

        _resultsRoot.SetActive(show);

        if (!show)
        {
            _resultsBuilt = false; // rebuild fresh next time we enter Results
            return;
        }

        if (_resultsBuilt || _resultsRowPrefab == null || _resultsListContainer == null)
            return;
        _resultsBuilt = true;

        foreach (var row in _spawnedRows)
            if (row != null) Destroy(row.gameObject);
        _spawnedRows.Clear();

        // Stable per-seat numbering (join order), independent of finish order -- so
        // "Player 2" doesn't change identity race to race just because standings shift.
        // No real username system exists yet; this matches the mockup's placeholder
        // "Player N" labelling until one does.
        var joinOrder = RaceLogic.GetParticipants(_raceManager.Runner);
        var ranked = RaceLogic.RankParticipants(_raceManager.Runner);

        for (int i = 0; i < ranked.Count; i++)
        {
            Player p = ranked[i];
            bool isSelf = p == _localPlayer;

            string name = isSelf ? "You" : $"Player {joinOrder.IndexOf(p) + 1}";
            string time = p.HasFinished ? FormatTime(TickSeconds(p.FinishTick)) : "--:--.---";

            var row = Instantiate(_resultsRowPrefab, _resultsListContainer);
            row.Setup(PlacementSprite(i), name, time, isSelf);
            _spawnedRows.Add(row);
        }
    }
}
