using Fusion;
using UnityEngine;

// Drives the race lifecycle: Lobby -> Countdown -> Racing -> Results -> Podium -> Lobby.
//
// Self-driving model: the host (StateAuthority) owns Phase and advances it every tick
// via timers and condition checks. The only externally-triggered transition is the
// host choosing to start the race from the Lobby (RequestStartRace), which also
// selects which race map to load.
//
// Lobby and race events are SEPARATE Unity scenes. Entering the race loads the chosen
// race map; returning to Podium->Lobby reloads the default lobby scene. Scene loading
// is host-driven and asynchronous, so the countdown timer only starts once the race
// scene has finished loading.
//
// The opening flyover is a LOCAL presentation beat (not frame-synced): each client plays
// it when the race scene finishes loading, then the authoritative countdown is shown.
// Only the countdown itself is networked/frame-synced, because it gates movement.
//
// All clients read Phase and react locally through the ChangeDetector in Render().

// Real race coordinator. Drives Lobby -> Countdown -> Racing -> Results -> Podium -> Lobby.
// Uses the shared RaceLogic + per-player race state (Player.IsRacing/HasFinished/FinishTick).
//
// This version wires REAL finish detection (position-based, host-authoritative) into the
// Racing phase, testable on a single scene. Scene loading (Race_Event) is deferred; the
// LoadScene calls remain commented until we add multi-scene support.
public class RaceManager : NetworkBehaviour
{
    public enum RacePhase
    {
        Lobby,
        Countdown,
        Racing,
        Results,
        Podium
    }

    [SerializeField] private float _countdownDuration = 3f;
    [SerializeField] private float _resultsDuration = 5f;
    [SerializeField] private float _podiumDuration = 8f;

    // Finish line position along Z (per-track later; serialized for single-scene testing).
    [SerializeField] private float _finishZ = 233f;

    [Header("Start line")]
    [Tooltip("Fixed spawn spots players are teleported to the moment Countdown begins, so everyone starts equidistant from the finish line and facing the correct direction -- movement here can't turn (2-pedal input only), so facing has to be set for them. Assign one Transform per expected player slot, up to 8; participants are matched to slots by join order. Unassigned/extra slots are simply skipped.")]
    [SerializeField] private Transform[] _startLineSpots = new Transform[8];

    //Teleport positions for podium in a scene
    [Header("Podium")]
    [SerializeField] private Transform _podium1;      // 1st place spot
    [SerializeField] private Transform _podium2;      // 2nd place spot
    [SerializeField] private Transform _podium3;      // 3rd place spot
    [SerializeField] private Transform _spectatorArea; // where everyone else gathers

    [Networked] public RacePhase Phase { get; private set; }
    [Networked] private TickTimer _phaseTimer { get; set; }
    [Networked] public int RaceStartTick { get; private set; }

    // --- Read-only accessors for local presentation (RaceHud etc.) ---
    // Seconds left in the current phase timer (Countdown/Results/Podium); 0 once expired
    // or when there's no phase timer running (e.g. during Racing/Lobby).
    public float PhaseTimeRemaining => _phaseTimer.RemainingTime(Runner) ?? 0f;
    public float CountdownDuration => _countdownDuration;
    public float FinishZ => _finishZ;

    private bool _startRequested;
    private ChangeDetector _changeDetector;

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        if (HasStateAuthority)
            Phase = RacePhase.Lobby;
    }

    // Called on the host when the event is started (e.g. from event setup UI).
    public void RequestStartRace()
    {
        if (HasStateAuthority)
            _startRequested = true;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        switch (Phase)
        {
            case RacePhase.Lobby:
                if (_startRequested)
                {
                    _startRequested = false;
                    EnterCountdown();
                }
                break;

            case RacePhase.Countdown:
                if (_phaseTimer.Expired(Runner))
                    EnterRacing();
                break;

            case RacePhase.Racing:
                CheckFinishes();
                if (RaceLogic.AllFinished(Runner))
                    EnterResults();
                break;

            case RacePhase.Results:
                if (_phaseTimer.Expired(Runner))
                    EnterPodium();
                break;

            case RacePhase.Podium:
                if (_phaseTimer.Expired(Runner))
                    EnterLobby();
                break;
        }
    }

    // --- Transitions (host-only) ---

    private void EnterCountdown()
    {
        Phase = RacePhase.Countdown;
        _phaseTimer = TickTimer.CreateFromSeconds(Runner, _countdownDuration);

        // Enrol every player in the session as a participant and line them up at the
        // start, in join order, so everyone begins the same distance from the finish
        // line and facing the correct direction.
        int index = 0;
        foreach (var pref in Runner.ActivePlayers)
        {
            if (Runner.TryGetPlayerObject(pref, out var obj) &&
                obj.TryGetComponent<Player>(out var p))
            {
                p.StartRacing();

                if (_startLineSpots != null && index < _startLineSpots.Length && _startLineSpots[index] != null)
                {
                    Transform spot = _startLineSpots[index];
                    TeleportPlayer(p, spot.position, spot.forward);
                    // Teleport only updates the visual transform.rotation -- two-key racing
                    // moves along a separate _forward field on Player, so it has to be told
                    // about the new facing directly too (same fix PracticeController uses).
                    p.SetForward(spot.forward);
                }

                index++;
            }
        }

        // TODO (scene loading later): load Race_Event -- start line spots above will need
        // to live in that scene too once this moves off the single testing scene.
    }

    private void EnterRacing()
    {
        Phase = RacePhase.Racing;
        RaceStartTick = Runner.Tick;
    }

    private void EnterResults()
    {
        Phase = RacePhase.Results;
        _phaseTimer = TickTimer.CreateFromSeconds(Runner, _resultsDuration);

        // Winner/standings available via RaceLogic for the Results UI to read.
        var winner = RaceLogic.Winner(Runner);
        Debug.Log(winner != null
            ? $"Race finished. Winner: {winner.Object.InputAuthority}"
            : "Race finished. No winner recorded.");
    }

    private void EnterPodium()
    {
        Phase = RacePhase.Podium;
        _phaseTimer = TickTimer.CreateFromSeconds(Runner, _podiumDuration);

        var ranked = RaceLogic.RankParticipants(Runner);

        for (int i = 0; i < ranked.Count; i++)
        {
            Player p = ranked[i];

            // Choose podium spot + celebration by placement.
            Transform spot;
            Player.CelebrationState celebration;

            if (i == 0) { spot = _podium1; celebration = Player.CelebrationState.First; }
            else if (i == 1) { spot = _podium2; celebration = Player.CelebrationState.Second; }
            else if (i == 2) { spot = _podium3; celebration = Player.CelebrationState.Third; }
            else { spot = _spectatorArea; celebration = Player.CelebrationState.Clapping; }

            // Teleport to the spot (with a small spread for spectators so they don't stack).
            if (spot != null)
            {
                Vector3 pos = spot.position;
                if (i >= 3)   // spread spectators around the area a little
                    pos += new Vector3((i - 3) * 1.5f, 0f, 0f);
                TeleportPlayer(p, pos, spot.forward);
            }

            p.SetCelebration(celebration);
        }
    }

    // Teleport helper (uses the player's NetworkCharacterController).
    private void TeleportPlayer(Player p, Vector3 position, Vector3 forward)
    {
        var cc = p.GetComponent<NetworkCharacterController>();
        if (cc != null)
            cc.Teleport(position, Quaternion.LookRotation(forward));
    }

    private void EnterLobby()
    {
        Phase = RacePhase.Lobby;

        int index = 0;
        // Clear race state on all players for the next race.
        foreach (var pref in Runner.ActivePlayers)
        {
            if (Runner.TryGetPlayerObject(pref, out var obj) &&
                obj.TryGetComponent<Player>(out var p))
            {
                p.ResetRace();
                p.SetCelebration(Player.CelebrationState.None);   // back to normal

                // Teleport back to a ground-level lobby position so they don't fall off the podium.
                var cc = p.GetComponent<NetworkCharacterController>();
                if (cc != null)
                {
                    Vector3 lobbyPos = new Vector3(index * 3f, 1.1f, 0f);   // spread along X, on the ground
                    cc.Teleport(lobbyPos, Quaternion.identity);
                }
                index++;
            }
        }

        // TODO (scene loading later): return to the lobby scene.
    }

    // --- Finish detection: host-authoritative, position-based (per participant) ---
    private void CheckFinishes()
    {
        var participants = RaceLogic.GetParticipants(Runner);
        foreach (var p in participants)
        {
            if (!p.HasFinished && p.transform.position.z >= _finishZ)
            {
                Debug.Log("  -> RecordFinish!");
                p.RecordFinish(Runner.Tick);
            }
        }
    }

    // --- Local reactions on every client ---
    public override void Render()
    {
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            switch (change)
            {
                case nameof(Phase):
                    OnPhaseChanged(Phase);
                    break;
            }
        }
    }

    private void OnPhaseChanged(RacePhase newPhase)
    {
        Debug.Log($"[RaceManager] Phase -> {newPhase}");
        // TODO: local UI per phase (countdown overlay, racing HUD, results, podium).
    }

    private void Update()
    {
        if (HasStateAuthority
            && UnityEngine.InputSystem.Keyboard.current != null
            && UnityEngine.InputSystem.Keyboard.current.oKey.wasPressedThisFrame)
        {
            RequestStartRace();
        }
    }
}
