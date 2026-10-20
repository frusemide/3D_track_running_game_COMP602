using Fusion;
using UnityEngine;

/// <summary>
/// AI racer that simulates pedal inputs for bot players.
/// Each bot has different skill levels affecting their speed and stumble frequency.
/// </summary>
public class AIRacer : NetworkBehaviour
{
    [System.Serializable]
    public class BotProfile
    {
        public string name;
        [Range(0f, 1f)] public float skill;           // 0 = terrible, 1 = perfect
        [Range(0.1f, 0.5f)] public float reactionTime; // How fast they alternate pedals
        [Range(0f, 0.5f)] public float errorRate;      // Chance to press same pedal twice
    }

    [Header("Slip patches")]
    [Tooltip("Bots at this difficulty or higher never trip on slip patches: they slow their pedalling to a safe pace while on one and make no pedal mistakes there. 0 = Easy, 1 = Medium, 2 = Hard, 3 = Expert. Set above 3 to turn off.")]
    [SerializeField] private int _slipProofFromDifficulty = 3;

    [Header("Bot Difficulty Presets")]
    [SerializeField] private BotProfile[] _botProfiles = new BotProfile[]
    {
        // Competitive but beatable - stumbles are now rare (about 3/8 bots fall per race)
        new BotProfile { name = "Easy Bot", skill = 0.65f, reactionTime = 0.14f, errorRate = 0.005f },     // 8-9 ticks (6.7 presses/sec), ~1 fall per race
        new BotProfile { name = "Medium Bot", skill = 0.77f, reactionTime = 0.12f, errorRate = 0.003f },   // 7 ticks (8.5 presses/sec), rare falls
        new BotProfile { name = "Hard Bot", skill = 0.88f, reactionTime = 0.11f, errorRate = 0.002f },     // 6-7 ticks (9 presses/sec), very rare falls
        new BotProfile { name = "Expert Bot", skill = 0.95f, reactionTime = 0.10f, errorRate = 0.001f }    // 6 ticks (10 presses/sec), almost never falls
    };

    private Player _player;
    private BotProfile _currentProfile;
    private float _nextPedalTime;
    private bool _lastPedalWasA = false;
    private bool _isActive = false;
    private int _ticksSinceLastPress = 0;
    private int _ticksPerPress = 0; // How many ticks between presses
    private int _difficultyIndex = -1;
    private int _lastPressTick = -1;   // tick of this bot's last pedal press (-1 = none yet / just recovered)

    [Networked] private NetworkBool _isBotControlled { get; set; }

    private bool _isInitialized = false;
    private bool _isBotLocal = false;  // Local flag to avoid network access issues

    public void InitializeBot(int difficultyIndex = -1)
    {
        _player = GetComponent<Player>();
        
        // Random difficulty if not specified
        if (difficultyIndex < 0 || difficultyIndex >= _botProfiles.Length)
        {
            difficultyIndex = Random.Range(0, _botProfiles.Length);
        }

        _currentProfile = _botProfiles[difficultyIndex];
        _difficultyIndex = difficultyIndex;
        _isInitialized = true;
        _isBotLocal = true;
        
        // Calculate ticks per press based on reaction time
        // Assuming 60 ticks per second, 0.35s ideal = ~21 ticks
        float tickRate = 60f;
        _ticksPerPress = Mathf.RoundToInt(_currentProfile.reactionTime * tickRate);
        _ticksSinceLastPress = _ticksPerPress; // Start ready to press
        
        Debug.Log($"Bot initialized: {_currentProfile.name} (Ticks per press: {_ticksPerPress}, Error Rate: {_currentProfile.errorRate * 100}%)");
    }

    public override void Spawned()
    {
        base.Spawned();
        
        // Set as bot-controlled after spawning
        if (HasStateAuthority && _isBotLocal)
        {
            _isBotControlled = true;
        }
        
        // Initialize if not already done
        if (!_isInitialized)
        {
            InitializeBot();
        }
    }

    public void SetActive(bool active)
    {
        _isActive = active;
        if (!active)
        {
            _lastPedalWasA = false;
            _ticksSinceLastPress = 0;
        }
        else
        {
            // Start immediately when race begins!
            _ticksSinceLastPress = _ticksPerPress; // Ready to press now
        }
    }

    public override void FixedUpdateNetwork()
    {
        // Bots are handled by Player script calling GetBotInput()
        // This method is here for compatibility but doesn't do anything
    }

    public NetworkInputData GetBotInput()
    {
        var data = new NetworkInputData();

        // Don't provide input if bot finished the race!
        if (!_isActive || _player == null || !_player.IsRacing || _player.HasFinished)
        {
            // Bot has finished - stop pressing pedals
            return data;
        }

        // Don't press pedals during recovery
        if (_player.RunState == Player.StumbleState.Recovering)
        {
            _ticksSinceLastPress = 0;
            _lastPressTick = -1;   // first pedal after recovering is always safe
            return data;
        }

        // Increment tick counter
        _ticksSinceLastPress++;

        // Careful bots: on a slip patch, wait until the gap since the last pedal is safe.
        bool carefulOnPatch = _difficultyIndex >= _slipProofFromDifficulty
                              && SlipPatch.IsOnAnyPatch(_player.transform.position);
        // Use the Player's Runner: this component is added at runtime after the bot spawned,
        // so its own Runner/Object are never set up (they're null).
        NetworkRunner runner = _player.Runner;
        if (carefulOnPatch && _lastPressTick >= 0 && runner != null)
        {
            // One extra tick of margin so rounding can never land exactly on the limit.
            float safeGap = _player.SlipPatchSafeStepInterval() + runner.DeltaTime;
            float sinceLastPress = ((int)runner.Tick - _lastPressTick) * runner.DeltaTime;
            if (sinceLastPress < safeGap)
                return data;   // hold this pedal for now
        }

        // Check if it's time to press the next pedal
        if (_ticksSinceLastPress >= _ticksPerPress)
        {
            // Careful bots never fumble a pedal while on a patch.
            bool shouldMakeError = !carefulOnPatch && Random.value < _currentProfile.errorRate;
            
            if (shouldMakeError)
            {
                // Error: same pedal twice
                if (_lastPedalWasA)
                    data.Buttons.Set(NetworkInputData.RunPedalA, true);
                else
                    data.Buttons.Set(NetworkInputData.RunPedalB, true);
            }
            else
            {
                // Correct alternation
                _lastPedalWasA = !_lastPedalWasA;
                
                if (_lastPedalWasA)
                    data.Buttons.Set(NetworkInputData.RunPedalA, true);
                else
                    data.Buttons.Set(NetworkInputData.RunPedalB, true);
            }

            _lastPressTick = runner != null ? (int)runner.Tick : -1;

            // Reset tick counter with small variation
            int variation = Mathf.RoundToInt(Mathf.Lerp(2f, 1f, _currentProfile.skill));
            _ticksSinceLastPress = Random.Range(-variation, variation + 1);
        }

        return data;
    }

    // This method will be called by the networking system to provide input for this bot
    public void ProvideBotInput(ref NetworkInputData input)
    {
        if (!_isBotControlled || !_isActive || _player == null)
            return;

        if (!_player.IsRacing || _player.HasFinished)
            return;

        if (_player.RunState == Player.StumbleState.Recovering)
            return;

        // Only provide input when it's time to press a pedal
        if (Time.time >= _nextPedalTime)
        {
            bool shouldMakeError = Random.value < _currentProfile.errorRate;
            
            if (shouldMakeError)
            {
                // Error: same pedal twice
                if (_lastPedalWasA)
                    input.Buttons.Set(NetworkInputData.RunPedalA, true);
                else
                    input.Buttons.Set(NetworkInputData.RunPedalB, true);
            }
            else
            {
                // Correct alternation
                _lastPedalWasA = !_lastPedalWasA;
                
                if (_lastPedalWasA)
                    input.Buttons.Set(NetworkInputData.RunPedalA, true);
                else
                    input.Buttons.Set(NetworkInputData.RunPedalB, true);
            }

            // Schedule next press
            float nextDelay = _currentProfile.reactionTime;
            nextDelay += Random.Range(-0.05f, 0.05f);
            nextDelay *= Mathf.Lerp(1.2f, 0.9f, _currentProfile.skill);
            _nextPedalTime = Time.time + nextDelay;
        }
    }

    public bool IsBotControlled => _isBotLocal || (_isBotControlled && Object != null && Object.IsValid);
    public bool IsActive => _isActive;
    public string BotName => _currentProfile?.name ?? "Bot";
    public float BotSkill => _currentProfile?.skill ?? 0.5f;
}
