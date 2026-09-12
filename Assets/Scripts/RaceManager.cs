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
public class RaceManager : NetworkBehaviour
{
    public enum RacePhase
    {
        Lobby,      // Default map: gathering, shop, practice tracks (all local sub-states)
        Countdown,  // Race map loaded; flyover (local) then synced countdown, movement locked
        Racing,     // Running; finish ticks recorded as players cross
        Results,    // Times ranked, progress saved
        Podium      // Top 3 shown, then loop back to Lobby
    }

    // --- Tunable durations (seconds). Serialized so you can adjust in the Inspector. ---
    [SerializeField] private float _countdownDuration = 3f;
    [SerializeField] private float _resultsDuration = 5f;
    [SerializeField] private float _podiumDuration = 8f;

    // Build-index of the default lobby scene. Race map indices are chosen by the host
    // per race (see RequestStartRace). Set these to match your Build Settings order.
    [SerializeField] private int _lobbySceneIndex = 0;

    // --- Networked state: replicated from host to every client ---
    [Networked] public RacePhase Phase { get; private set; }

    // Which race map this event uses; set by the host when starting. Lets any client
    // know the course, and drives the scene load.
    [Networked] public int RaceSceneIndex { get; private set; }

    // Generic phase timer, reused by Countdown / Results / Podium.
    // TickTimer is tick-based, so it stays consistent across all peers.
    [Networked] private TickTimer _phaseTimer { get; set; }

    // True once the countdown timer has actually been started (i.e. race scene finished
    // loading). Prevents the countdown counting down while the scene is still loading.
    [Networked] private NetworkBool _countdownStarted { get; set; }

    // Set by the host when the race begins; race time = finish tick - RaceStartTick.
    [Networked] public int RaceStartTick { get; private set; }

    // Host-only intent: captured from the lobby UI, consumed by the state machine.
    private bool _startRequested;
    private int _requestedSceneIndex;

    private ChangeDetector _changeDetector;

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

        if (HasStateAuthority)
            Phase = RacePhase.Lobby;
    }

    private void Update()
    {
        // TEMP debug: press O (host only) to start a race on the current scene.
        if (HasStateAuthority
            && UnityEngine.InputSystem.Keyboard.current != null
            && UnityEngine.InputSystem.Keyboard.current.oKey.wasPressedThisFrame)
        {
            // Uses the current scene as the "race map" for now, since IsRaceSceneLoaded
            // is stubbed true and you're not testing real scene-swapping yet.
            RequestStartRace(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }
    }


    // Called on the host when the lobby UI starts the race, passing the chosen course's
    // scene build index. The transition still happens inside the state machine next tick.
    public void RequestStartRace(int raceSceneIndex)
    {
        if (HasStateAuthority)
        {
            _startRequested = true;
            _requestedSceneIndex = raceSceneIndex;
        }
    }

    public override void FixedUpdateNetwork()
    {
        // Only the host advances phases. Clients just read the replicated Phase.
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
                // Wait until the race scene has loaded before starting the count.
                if (!_countdownStarted)
                {
                    if (IsRaceSceneLoaded())
                    {
                        _phaseTimer = TickTimer.CreateFromSeconds(Runner, _countdownDuration);
                        _countdownStarted = true;
                    }
                }
                else if (_phaseTimer.Expired(Runner))
                {
                    EnterRacing();
                }
                break;

            case RacePhase.Racing:
                if (AllPlayersFinished())
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

    // --- Transition methods (host-only) ---

    private void EnterCountdown()
    {
        Phase = RacePhase.Countdown;
        RaceSceneIndex = _requestedSceneIndex;
        _countdownStarted = false;           // count starts only after the scene loads
        _phaseTimer = default;

       // LoadRaceScene(RaceSceneIndex);    //TEMP disabled: single-scene testing

        // TODO: place players at the start line side by side once the scene is loaded.
        //       Movement lockout needs no push: player input checks Phase == Racing.
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

        // TODO: compute final standings from finish ticks; trigger local save on clients.
    }

    private void EnterPodium()
    {
        Phase = RacePhase.Podium;
        _phaseTimer = TickTimer.CreateFromSeconds(Runner, _podiumDuration);

        // TODO: hand top 3 (by finish tick) to the podium presentation.
    }

    private void EnterLobby()
    {
        Phase = RacePhase.Lobby;
        _countdownStarted = false;

        // LoadLobbyScene();

        // TODO: reset per-player race state (finish ticks, progress, stumble, ready flags).
    }

    // --- Scene loading (host-driven via Fusion's scene manager) ---

    private void LoadRaceScene(int sceneIndex)
    {
        // Host tells Fusion to load the race map for all peers. Fusion replicates the
        // scene transition; clients follow automatically.
        Runner.LoadScene(SceneRef.FromIndex(sceneIndex));
    }

    private void LoadLobbyScene()
    {
        Runner.LoadScene(SceneRef.FromIndex(_lobbySceneIndex));
    }

    // True once the active scene on the host matches the requested race map.
    private bool IsRaceSceneLoaded()
    {
        // TODO: confirm against Fusion's scene state. A simple check is whether the
        //       runner's current scene matches RaceSceneIndex, e.g. comparing
        //       Runner.SceneManager's active scene to SceneRef.FromIndex(RaceSceneIndex).
        //       Stubbed true for now so the countdown proceeds during early testing.
        return true;
    }

    // --- Racing -> Results condition ---
    private bool AllPlayersFinished()
    {
        // TODO: return true once every present, connected player has a finish tick
        //       (or has left). Stubbed false so Racing doesn't auto-advance yet.
        return false;
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

    // Runs on ALL clients when Phase changes. Local presentation only — no authority.
    private void OnPhaseChanged(RacePhase newPhase)
    {
        Debug.Log($"[RaceManager] Phase -> {newPhase}");

        // TODO: local view switching per phase:
        //   Lobby     -> lobby UI (shop/practice available locally)
        //   Countdown -> play local flyover, then per-player camera + countdown overlay
        //   Racing    -> racing HUD (or standings if this player already finished)
        //   Results   -> results screen
        //   Podium    -> podium screen
    }
}
