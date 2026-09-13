using Fusion;
using UnityEngine;

// Player movement + input, organised around a resolved MovementMode.
//
// Two-key racing is host-authoritative: the host validates pedal alternation, owns Speed
// and the StumbleState, and writes them as networked values. Every client reads those and
// reacts locally (fall animation via the ChangeDetector). Speed builds with rapid
// alternation up to a ceiling and decays when alternation slows (Sprinter-like). A stumble
// (same pedal twice, or a pedal press during recovery) triggers a controlled decelerating
// glide to a stop, then a recovery lockout.
public class Player : NetworkBehaviour
{
    private enum MovementMode
    {
        Locked,
        FreeMovement,
        TwoKeyRace
    }

    public enum CelebrationState : byte
    {
        None,      // not celebrating
        First,     // 1st place victory
        Second,    // 2nd place victory
        Third,     // 3rd place victory
        Clapping   // everyone else
    }

    // Host-authoritative running sub-state, networked so all clients see the same thing.
    public enum StumbleState : byte
    {
        Running,     // Normal: pedals build speed
        Sliding,     // Stumbled: gliding to a stop, decelerating
        Recovering   // Stopped: locked out until the recovery timer expires
    }

    // --- Free movement (lobby) ---
    private const float MoveSpeed = 5f;

    // --- Two-key racing tunables (serialized so you can dial in the feel) ---
    [Header("Two-key racing")]
    [SerializeField] private float _topSpeed = 12f;            // speed ceiling
    [SerializeField] private float _accelPerAlternation = 2.2f; // speed added per valid alternation
    [SerializeField] private float _recoveryLockout = 1.25f;    // seconds locked after coming to rest
    [SerializeField] private float _speedDecayPerSecond = 6f; // bleed speed when not alternating fast enough
    [SerializeField] private float _slideDecelRate = 1.5f; //fraction of slide speed lost per second
    [SerializeField] private float _turnSpeed = 12f;   // how quickly the character turns to face input direction
    [SerializeField] private float _sprintSpeed = 10f;   // held-shift movement speed

    private NetworkCharacterController _cc;
    private Vector3 _forward = Vector3.forward;
    private RaceManager _raceManager;
    private Animator _animator;
    private PracticeController _practice;
    private ChangeDetector _changeDetector;

    // Per-player networked practice flag (lobby two-key at practice starts).
    [Networked] public NetworkBool InPracticeMode { get; set; }
    // --- Host-owned racing state (networked) ---
    [Networked] public float Speed { get; set; }
    [Networked] public StumbleState RunState { get; set; }
    // 0 = none yet, 1 = pedal A, 2 = pedal B. Which pedal was last accepted, for alternation.
    [Networked] private byte _lastPedal { get; set; }
    // Recovery lockout timer after a stumble.
    [Networked] private TickTimer _recoverTimer { get; set; }
   // --- Race participation (Option 3: per-player race state) ---
    [Networked] public NetworkBool IsRacing { get; set; }     // in the current race?
    [Networked] public NetworkBool HasFinished { get; set; }  // crossed the finish line?
    [Networked] public int FinishTick { get; set; }           // tick when finished (for ranking/time)// --- Race participation (Option 3: per-player race state) ---
    [Networked] public CelebrationState Celebration { get; set; }  // choosing celebration animation to play

    private void Awake()
    {
        _cc = GetComponent<NetworkCharacterController>();
    }

    public override void Spawned()
    {
        _raceManager = FindFirstObjectByType<RaceManager>();
        _practice = GetComponent<PracticeController>();
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        _animator = GetComponentInChildren<Animator>();
        _cc.maxSpeed = 100f;

        //Camera only set for local player
        if (Object.HasInputAuthority)
        {
            var cam = FindFirstObjectByType<ThirdPersonCamera>();
            if (cam != null)
                cam.SetTarget(transform);
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!GetInput(out NetworkInputData data))
            return;

        switch (ResolveMode())
        {
            case MovementMode.FreeMovement:
                HandleFreeMovement(data);
                break;

            case MovementMode.TwoKeyRace:
                HandleTwoKeyRace(data);
                break;

            case MovementMode.Locked:
                break;
        }
    }

    private MovementMode ResolveMode()
    {
        if (_raceManager == null)
            return InPracticeMode ? MovementMode.TwoKeyRace : MovementMode.FreeMovement;

        switch (_raceManager.Phase)
        {
            case RaceManager.RacePhase.Countdown:
                return MovementMode.Locked;

            case RaceManager.RacePhase.Racing:
                return MovementMode.TwoKeyRace;

            case RaceManager.RacePhase.Lobby:
                // Lock during practice countdown; two-key during practice dash; free otherwise.
                if (_practice != null && _practice.State == PracticeController.PracticeState.Countdown)
                    return MovementMode.Locked;
                return InPracticeMode ? MovementMode.TwoKeyRace : MovementMode.FreeMovement;

            case RaceManager.RacePhase.Results:
                // Keep racing players in two-key so they coast to a stop (decel), rather
                // than freezing. HandleTwoKeyRace ignores pedals once HasFinished.
                return IsRacing ? MovementMode.TwoKeyRace : MovementMode.Locked;

            case RaceManager.RacePhase.Podium:
            default:
                return MovementMode.Locked;
        }
    }

    // --- Free movement (lobby WASD) ---
    private void HandleFreeMovement(NetworkInputData data)
    {
        bool moving = data.Direction.sqrMagnitude > 0;
        bool sprinting = data.Buttons.IsSet(NetworkInputData.Sprint);

        if (moving)
        {
            Vector3 targetDir = data.Direction.normalized;
            Quaternion targetRot = Quaternion.LookRotation(targetDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, _turnSpeed * Runner.DeltaTime);

            _forward = transform.forward;

            float speed = sprinting ? _sprintSpeed : MoveSpeed;
            _cc.Velocity = speed * transform.forward;
            Speed = speed;                       // drive the animation blend
        }
        else
        {
            _cc.Velocity = Vector3.zero;
            Speed = 0f;                          // idle
        }

        _cc.Move(_cc.Velocity * Runner.DeltaTime);
    }

    // --- Two-key racing (host-authoritative) ---
    private void HandleTwoKeyRace(NetworkInputData data)
    {
        float dt = Runner.DeltaTime;

        bool pedalA = data.Buttons.IsSet(NetworkInputData.RunPedalA);
        bool pedalB = data.Buttons.IsSet(NetworkInputData.RunPedalB);

        switch (RunState)
        {
            case StumbleState.Running:
                // Passive decay every tick: stop hammering and you bleed speed.
                Speed = Mathf.Max(0f, Speed - _speedDecayPerSecond * dt);

                // Only accept pedals if still racing (not yet finished). Finished players coast.
                if (!HasFinished && (pedalA || pedalB))
                {
                    byte pressed = pedalA ? (byte)1 : (byte)2;

                    // Pressing BOTH pedals in one tick is treated as a fault (a "mash").
                    if (pedalA && pedalB)
                    {
                        BeginStumble();
                    }
                    else if (_lastPedal != 0 && pressed == _lastPedal)
                    {
                        // Same pedal twice in a row = failed alternation = stumble.
                        BeginStumble();
                    }
                    else
                    {
                        // Valid alternation: accelerate, with diminishing returns near top speed.
                        float headroom = 1f - (Speed / _topSpeed);   // 1 at rest, 0 at top speed
                        Speed = Mathf.Min(_topSpeed, Speed + _accelPerAlternation * headroom);
                        _lastPedal = pressed;
                    }
                }

                if (RunState == StumbleState.Running)
                {
                    _cc.Velocity = Speed * _forward;
                    _cc.Move(_cc.Velocity * dt);
                }
                break;

            case StumbleState.Sliding:
                // Proportional glide: bleeds fast at high speed, eases to a soft stop.
                Speed = Mathf.Max(0f, Speed - Speed * _slideDecelRate * dt);

                if (Speed > 0.5f)
                {
                    _cc.Velocity = Speed * _forward;
                    _cc.Move(_cc.Velocity * dt);
                }
                else
                {
                    // Snap to rest and begin recovery (proportional decay never hits exactly 0).
                    Speed = 0f;
                    _cc.Velocity = Vector3.zero;
                    RunState = StumbleState.Recovering;
                    _recoverTimer = TickTimer.CreateFromSeconds(Runner, _recoveryLockout);
                }
                break;

            case StumbleState.Recovering:
                // Locked out; ignore pedals until the timer expires, then resume running.
                if (_recoverTimer.ExpiredOrNotRunning(Runner))
                {
                    RunState = StumbleState.Running;
                    _lastPedal = 0;   // fresh start; first pedal after recovery is always valid
                }
                break;
        }
    }

    // Enter the stumble: freeze acceleration and start the decelerating glide from
    // whatever speed we had (that speed determines slide distance).
    private void BeginStumble()
    {
        RunState = StumbleState.Sliding;
        Speed = Speed * 1.5f;   //launch the slide a bit faster than run speed for more distance
        _lastPedal = 0;
    }

    // Called by a coordinator when a race begins, to enrol this player.
    public void StartRacing()
    {
        if (!HasStateAuthority) return;
        IsRacing = true;
        HasFinished = false;
        FinishTick = 0;
    }

    public void SetCelebration(CelebrationState state)
    {
        if (!HasStateAuthority) return;
        Celebration = state;
    }

    // Called (host-side) when this player crosses the finish line.
    public void RecordFinish(int tick)
    {
        if (!HasStateAuthority) return;
        if (!IsRacing || HasFinished) return;   // only finish once, only if racing
        HasFinished = true;
        FinishTick = tick;
    }

    // Called by a coordinator to clear race state (race over / left / reset).
    public void ResetRace()
    {
        if (!HasStateAuthority) return;
        IsRacing = false;
        HasFinished = false;
        FinishTick = 0;
    }

    public void SetForward(Vector3 forward)
    {
        _forward = forward.normalized;
    }


    // --- Local reactions on every client ---
    public override void Render()
    {
        // Drive the run-cycle blend from the replicated Speed, on every client for every player
        if (_animator != null)
            _animator.SetFloat("Speed", Speed);

        foreach (var change in _changeDetector.DetectChanges(this))
        {
            switch (change)
            {
                case nameof(RunState):
                    OnRunStateChanged(RunState);
                    break;

                case nameof(Celebration):
                    OnCelebrationChanged(Celebration);
                    break;
            }
        }
    }

    // Runs on ALL clients when the host-owned RunState changes. Presentation only.
    private void OnRunStateChanged(StumbleState state)
    {
        if (_animator == null)
            return;

        switch (state)
        {
            case StumbleState.Sliding:
                _animator.SetTrigger("Fall");
                break;

            case StumbleState.Recovering:
                _animator.SetTrigger("GetUp");
                break;

            case StumbleState.Running:
                // Returns to Locomotion automatically when GetUp finishes (exit-time transition).
                break;
        }
    }

    private void OnCelebrationChanged(CelebrationState state)
    {
        if (_animator == null)
            return;

        switch (state)
        {
            case CelebrationState.First:
                _animator.SetTrigger("Victory1");
                break;
            case CelebrationState.Second:
                _animator.SetTrigger("Victory2");
                break;
            case CelebrationState.Third:
                _animator.SetTrigger("Victory3");
                break;
            case CelebrationState.Clapping:
                _animator.SetTrigger("Clap");
                break;
            case CelebrationState.None:
                // back to normal locomotion (e.g. when returning to lobby)
                _animator.Play("Locomotion", 0);
                break;
        }
    }

    public void ResetRacingState()
    {
        Debug.Log($"ResetRacingState called. RunState was {RunState}, animator null? {_animator == null}");
        RunState = StumbleState.Running;
        Speed = 0f;
        _lastPedal = 0;
        _cc.Velocity = Vector3.zero;

        if (_animator != null)
            _animator.Play("Locomotion", 0);
    }
}