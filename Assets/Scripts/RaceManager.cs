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
        Positioning,
        Countdown,
        Racing,
        Results,
        Podium
    }

    [SerializeField] private float _countdownDuration = 3f;
    [SerializeField] private float _positioningDuration = 3f; // time to get bearings before the countdown starts
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
    [SerializeField] private RaceStartNoticeBanner _startNoticeBanner; // image notice that race is about to start
    [SerializeField] private float _startNoticeDuration = 2.5f; // duration start notice is shown

    [Header("Weather")]
    [Tooltip("Pick Clear or Rain at random for each race. Untick to always use Fixed Weather.")]
    [SerializeField] private bool _randomWeather = true;
    [SerializeField] private WeatherType _fixedWeather = WeatherType.Clear;
    [Tooltip("Optional: rain particles / wet-look objects, switched on for every client while it's raining.")]
    [SerializeField] private GameObject _rainEffects;

    [Header("Bots")]
    [Tooltip("Fills empty race slots with AI bots. Found automatically in the scene if left empty.")]
    [SerializeField] private BotSpawner _botSpawner;

    [Networked] public RacePhase Phase { get; private set; }
    [Networked] private TickTimer _phaseTimer { get; set; }
    [Networked] public int RaceStartTick { get; private set; }
    [Networked] private TickTimer _startNoticeTimer { get; set; }
    // Host picks this when a race starts; Player reads it every tick (top speed + slip patches).
    [Networked] public WeatherType Weather { get; private set; }
    // Host picks this when a race starts, so every client plays the same race music track.
    [Networked] public int RaceMusicIndex { get; private set; }

    // --- Read-only accessors for local presentation (RaceHud etc.) ---
    // Seconds left in the current phase timer (Countdown/Results/Podium); 0 once expired
    // or when there's no phase timer running (e.g. during Racing/Lobby).
    public float PhaseTimeRemaining => _phaseTimer.RemainingTime(Runner) ?? 0f;
    public float CountdownDuration => _countdownDuration;
    public float FinishZ => _finishZ;
    private ChangeDetector _changeDetector;


    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        if (_botSpawner == null)
            _botSpawner = FindFirstObjectByType<BotSpawner>();
        if (HasStateAuthority)
            Phase = RacePhase.Lobby;
    }

    // Called on the host when the event is started (e.g. from the console UI).
    public void RequestStartRace()
    {
        if (!HasStateAuthority) return;
        _startNoticeTimer = TickTimer.CreateFromSeconds(Runner, _startNoticeDuration);
        RPC_NotifyStarting(_startNoticeDuration);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyStarting(float duration)
    {
        var banner = FindFirstObjectByType<RaceStartNoticeBanner>();
        if (banner != null)
            banner.Show(duration);

        // Sound for the "race about to start" banner, on every client.
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayRaceStartingSound();
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        switch (Phase)
        {
            case RacePhase.Lobby:
                if (_startNoticeTimer.IsRunning && _startNoticeTimer.Expired(Runner))
                {
                    EnterPositioning();
                }
                break;

            case RacePhase.Positioning:
                if (_phaseTimer.Expired(Runner))
                    EnterCountdown();
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

    private void EnterPositioning()
    {
        Phase = RacePhase.Positioning;
        _phaseTimer = TickTimer.CreateFromSeconds(Runner, _positioningDuration);

        // Weather is decided before the countdown so players see it while lining up.
        Weather = _randomWeather ? (WeatherType)UnityEngine.Random.Range(0, 2) : _fixedWeather;   // 0 = Clear, 1 = Rain
        Debug.Log($"[RaceManager] Weather for this race: {Weather}");

        PickRaceMusic();

        // Fill the empty slots with AI bots. Spawning happens straight away on the host,
        // so the bots are ready to be lined up below with everyone else.
        if (_botSpawner != null)
        {
            _botSpawner.DespawnAllBots();   // clear any leftovers from a previous race
            _botSpawner.SpawnBotsForRace();
        }

        // Build the start roster: real players first (in join order), then bots.
        var racers = new System.Collections.Generic.List<Player>();
        foreach (var pref in Runner.ActivePlayers)
        {
            if (Runner.TryGetPlayerObject(pref, out var obj) &&
                obj.TryGetComponent<Player>(out var realPlayer))
            {
                racers.Add(realPlayer);
            }
        }
        if (_botSpawner != null)
        {
            foreach (var bot in _botSpawner.SpawnedBots)
            {
                if (bot != null && bot.Object != null && bot.Object.IsValid)
                    racers.Add(bot);
            }
        }

        // Enrol every racer as a participant and line them up at the start, so everyone
        // begins the same distance from the finish line and facing the correct direction.
        int index = 0;
        foreach (var p in racers)
        {
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

                // Clear leftover movement state (lobby sprint speed, stumbles, pedal
                // history) so nobody carries momentum into the race start.
                if (p.TryGetComponent<PracticeController>(out var practice))
                    practice.CancelPractice();
                p.ResetRacingState();

                index++;
            }
        }

        // TODO (scene loading later): load Race_Event -- start line spots above will need
        // to live in that scene too once this moves off the single testing scene.
    }

    private void EnterCountdown()
    {
        Phase = RacePhase.Countdown;
        _phaseTimer = TickTimer.CreateFromSeconds(Runner, _countdownDuration);
    }

    private void EnterRacing()
    {
        Phase = RacePhase.Racing;
        RaceStartTick = Runner.Tick;

        // Bots stand still through Positioning/Countdown and start pedalling on "Go".
        if (_botSpawner != null)
            _botSpawner.ActivateBots();
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
        _startNoticeTimer = TickTimer.None;
        Weather = WeatherType.Clear;   // the lobby is always dry

        // Bots only exist for the race; remove them before everyone returns to the lobby.
        if (_botSpawner != null)
        {
            _botSpawner.DeactivateBots();
            _botSpawner.DespawnAllBots();
        }

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

        int finishedSoFar = 0;
        var crossedThisTick = new System.Collections.Generic.List<Player>();
        foreach (var p in participants)
        {
            if (p.HasFinished)
                finishedSoFar++;
            else if (p.transform.position.z >= _finishZ)
                crossedThisTick.Add(p);
        }

        // Racers crossing on the same tick are tied, so their finishing order is drawn at random.
        for (int i = crossedThisTick.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (crossedThisTick[i], crossedThisTick[j]) = (crossedThisTick[j], crossedThisTick[i]);
        }

        foreach (var p in crossedThisTick)
        {
            finishedSoFar++;
            p.RecordFinish(Runner.Tick, finishedSoFar);
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

        // Checked every frame rather than via the ChangeDetector so late joiners also see rain.
        bool raining = Weather == WeatherType.Rain;
        if (_rainEffects != null && _rainEffects.activeSelf != raining)
            _rainEffects.SetActive(raining);

        // Background music follows the phase. Checked every frame (like the rain) so it also
        // starts for the host on spawn and for late joiners; SetMusic only acts on a change.
        if (AudioManager.Instance != null)
        {
            AudioManager.MusicTrack track = MusicForPhase(Phase);

            // Race music fades out as soon as YOU finish, even while others are still racing.
            if (track == AudioManager.MusicTrack.Race && LocalPlayerHasFinished())
                track = AudioManager.MusicTrack.None;

            AudioManager.Instance.SetMusic(track, RaceMusicIndex);
        }
    }

    // True once this machine's own player has crossed the finish line.
    private bool LocalPlayerHasFinished()
    {
        if (Runner == null)
            return false;

        return Runner.TryGetPlayerObject(Runner.LocalPlayer, out var obj)
            && obj.TryGetComponent<Player>(out var local)
            && local.HasFinished;
    }

    // Host-only: choose this race's music track at random, avoiding the one that played last race.
    private void PickRaceMusic()
    {
        int count = AudioManager.Instance != null ? AudioManager.Instance.RaceMusicTrackCount : 0;
        if (count <= 1)
        {
            RaceMusicIndex = 0;
            return;
        }

        int next = UnityEngine.Random.Range(0, count - 1);
        if (next >= RaceMusicIndex)
            next++;   // skip last race's track
        RaceMusicIndex = next;
        Debug.Log($"[RaceManager] Race music track: {RaceMusicIndex}");
    }

    // Which music track plays in each phase.
    // Race music: from being moved to the start line, through the countdown, until you finish.
    // Results and podium are silent; lobby music fades back in on returning to the lobby.
    private static AudioManager.MusicTrack MusicForPhase(RacePhase phase)
    {
        switch (phase)
        {
            case RacePhase.Lobby:
                return AudioManager.MusicTrack.Lobby;

            case RacePhase.Positioning:
            case RacePhase.Countdown:
            case RacePhase.Racing:
                return AudioManager.MusicTrack.Race;

            case RacePhase.Results:
            case RacePhase.Podium:
            default:
                return AudioManager.MusicTrack.None;
        }
    }

    private void OnPhaseChanged(RacePhase newPhase)
    {
        Debug.Log($"[RaceManager] Phase -> {newPhase}");
        // TODO: local UI per phase (countdown overlay, racing HUD, results, podium).

        // Race audio. Runs locally on every client, since Phase is networked.
        // (Per-player sounds -- running, falling, finishing, victory -- live in Player.)
        AudioManager audio = AudioManager.Instance;
        if (audio == null)
            return;

        switch (newPhase)
        {
            case RacePhase.Positioning:
                // Players were just teleported to the start line; don't carry lobby footsteps over.
                audio.StopRunningSound();
                break;

            case RacePhase.Countdown:
                // Ready / Set / GO! sounds are played by RaceHud, in sync with the banner.
                break;

            case RacePhase.Racing:
                // Crowd cheers from "GO!" until everyone is back in the lobby.
                audio.StartCrowdSound();
                break;

            case RacePhase.Results:
                // Results screen appears (RaceHud shows it from Results onwards).
                audio.PlayResultsSound();
                break;

            case RacePhase.Podium:
                // Crowd keeps going; the podium victory sound is played by Player.
                break;

            case RacePhase.Lobby:
                audio.StopCrowdSound();
                audio.StopRunningSound();
                break;
        }
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
