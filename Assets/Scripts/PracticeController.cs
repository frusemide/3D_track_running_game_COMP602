using Fusion;
using UnityEngine;

// Per-player practice dash flow (local): idle -> countdown -> dashing -> finished.
// Lives on the Player prefab alongside Player and PlayerInteractor.
//
// Flow:
//   - PracticeStartZone (an IInteractable) calls ReadyUp(startPoint) when the player
//     interacts. The player is snapped to a consistent start position/facing.
//   - Countdown runs (movement locked); at the end the timer starts and two-key sprint
//     is enabled (via the player's InPracticeMode).
//   - PracticeFinishLine calls Finish() when the player crosses it; the timer stops and
//     the time is shown.
//
// This is LOCAL practice: per-player, not host-authoritative. It's the simpler cousin of
// the real race's finish/timing, and the tick-based timing here transfers to the race.
[RequireComponent(typeof(Player))]
public class PracticeController : NetworkBehaviour
{
    public enum PracticeState : byte
    {
        Idle,        // not practicing
        Countdown,   // readied up, counting down, movement locked
        Dashing,     // timer running, two-key sprint enabled
        Finished     // crossed the line, time shown
    }

    [SerializeField] private float _countdownDuration = 3f;
    [SerializeField] private float _finishZ = 233f;   // Z position of the finish line

    private Player _player;
    private NetworkCharacterController _cc;

    [Networked] public PracticeState State { get; set; }
    [Networked] private TickTimer _countdownTimer { get; set; }
    [Networked] private int _startTick { get; set; }
    [Networked] public float LastTime { get; set; }   // most recent completed dash time (seconds)

    private void Awake()
    {
        _player = GetComponent<Player>();
        _cc = GetComponent<NetworkCharacterController>();
    }

    // Called by PracticeStartZone when the player interacts at the start line.
    // startPosition / startForward define the consistent start pose for every dash.
    public void ReadyUp(Vector3 startPosition, Vector3 startForward)
    {
        Debug.Log($"ReadyUp called. StateAuth={HasStateAuthority}, State={State}");

        if (!HasStateAuthority)
        {
            Debug.Log("ReadyUp blocked: no state authority");
            return;
        }
        if (State != PracticeState.Idle && State != PracticeState.Finished)
        {
            Debug.Log($"ReadyUp blocked: already in state {State}");
            return;
        }

        Quaternion startRot = Quaternion.LookRotation(startForward);
        _cc.Teleport(startPosition, startRot);
        Debug.Log($"Teleported to {startPosition}");
        _player.SetForward(startForward);   // align racing direction with the start facing

        State = PracticeState.Countdown;
        _countdownTimer = TickTimer.CreateFromSeconds(Runner, _countdownDuration);
        _player.InPracticeMode = false;
    }

    // Called by PracticeFinishLine when the player crosses it.
    public void Finish()
    {
        if (!HasStateAuthority)
            return;
        if (State != PracticeState.Dashing)
            return;

        LastTime = (Runner.Tick - _startTick) * Runner.DeltaTime;
        State = PracticeState.Finished;
        _player.InPracticeMode = false;
        _player.ResetRacingState();
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        if (State == PracticeState.Countdown && _countdownTimer.Expired(Runner))
        {
            State = PracticeState.Dashing;
            _startTick = Runner.Tick;
            _player.InPracticeMode = true;
        }

        // Position-based finish detection: crossed the finish Z while dashing.
        if (State == PracticeState.Dashing && transform.position.z >= _finishZ)
        {
            Finish();
        }
    }

    // Live elapsed time while dashing, for the on-screen display.
    public float CurrentElapsed()
    {
        if (State == PracticeState.Dashing)
            return (Runner.Tick - _startTick) * Runner.DeltaTime;
        return LastTime;
    }

    // TEMP on-screen display of countdown / timer / result. Local player only.
    private void OnGUI()
    {
        if (Object == null || !Object.HasInputAuthority)
            return;

        string text = null;
        switch (State)
        {
            case PracticeState.Countdown:
                float remaining = _countdownTimer.RemainingTime(Runner) ?? 0f;
                text = $"Get ready: {Mathf.CeilToInt(remaining)}";
                break;
            case PracticeState.Dashing:
                text = $"Time: {CurrentElapsed():F2}s";
                break;
            case PracticeState.Finished:
                text = $"Finished! {LastTime:F2}s";
                break;
        }

        if (text != null)
        {
            var style = new GUIStyle(GUI.skin.box) { fontSize = 22 };
            GUI.Box(new Rect(Screen.width / 2f - 120, 20, 240, 50), text, style);
        }
    }
}
