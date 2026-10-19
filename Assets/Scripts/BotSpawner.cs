using Fusion;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages spawning of AI bots to fill empty race slots.
/// Spawns bots on the host, up to 8 total racers (including real players).
/// </summary>
public class BotSpawner : NetworkBehaviour
{
    [Header("Bot Settings")]
    [SerializeField] private NetworkPrefabRef _botPlayerPrefab;  // Your Player prefab (must be NetworkPrefabRef)
    [SerializeField] private int _maxRacers = 8;           // Total racers (players + bots)
    [SerializeField] private bool _fillWithBots = true;    // Auto-fill with bots?
    
    [Header("Bot Variety")]
    [Tooltip("Distribution of bot difficulties: 0=Easy, 1=Medium, 2=Hard, 3=Expert")]
    [SerializeField] private int[] _botDifficultyDistribution = new int[] { 0, 1, 1, 2, 2, 3 };

    private List<Player> _spawnedBots = new List<Player>();
    private RaceManager _raceManager;

    public override void Spawned()
    {
        _raceManager = FindFirstObjectByType<RaceManager>();
    }

    /// <summary>
    /// Call this before starting a race to spawn bots if needed
    /// </summary>
    public void SpawnBotsForRace()
    {
        if (!HasStateAuthority || !_fillWithBots)
            return;

        // Count real players
        int realPlayerCount = CountRealPlayers();
        
        // Calculate how many bots we need
        int botsNeeded = Mathf.Max(0, _maxRacers - realPlayerCount);
        
        Debug.Log($"Real players: {realPlayerCount}, Spawning {botsNeeded} bots (Max: {_maxRacers})");

        if (botsNeeded > 0)
        {
            SpawnBots(botsNeeded);
        }
    }

    /// <summary>
    /// Remove all bots from the session
    /// </summary>
    public void DespawnAllBots()
    {
        if (!HasStateAuthority)
            return;

        foreach (var bot in _spawnedBots)
        {
            if (bot != null && bot.Object != null)
            {
                Runner.Despawn(bot.Object);
            }
        }
        
        _spawnedBots.Clear();
        Debug.Log("All bots despawned");
    }

    private int CountRealPlayers()
    {
        int count = 0;
        
        foreach (var playerRef in Runner.ActivePlayers)
        {
            if (Runner.TryGetPlayerObject(playerRef, out var playerObj))
            {
                // Check if it's a real player (not a bot)
                if (!playerObj.TryGetComponent<AIRacer>(out var ai) || !ai.IsBotControlled)
                {
                    count++;
                }
            }
        }
        
        return count;
    }

    private void SpawnBots(int count)
    {
        if (_botPlayerPrefab.IsValid == false)
        {
            Debug.LogError("Bot Player Prefab is not assigned or invalid!");
            return;
        }

        for (int i = 0; i < count; i++)
        {
            SpawnSingleBot(i);
        }
    }

    private void SpawnSingleBot(int index)
    {
        // Random spawn position in lobby (will be repositioned at race start)
        Vector3 spawnPos = new Vector3(
            Random.Range(-5f, 5f),
            1f,
            Random.Range(-5f, 5f)
        );

        // Spawn the bot player
        NetworkObject botObject = Runner.Spawn(
            _botPlayerPrefab,
            spawnPos,
            Quaternion.identity,
            PlayerRef.None  // No player input authority (bot controlled)
        );

        if (botObject != null && botObject.TryGetComponent<Player>(out var player))
        {
            // Add AI component if it doesn't exist
            AIRacer aiComponent = botObject.GetComponent<AIRacer>();
            if (aiComponent == null)
            {
                aiComponent = botObject.gameObject.AddComponent<AIRacer>();
            }

            // Pick a difficulty from the distribution
            int difficultyIndex = _botDifficultyDistribution[Random.Range(0, _botDifficultyDistribution.Length)];
            aiComponent.InitializeBot(difficultyIndex);

            // Apply random color using PlayerCustomisation
            // We need to delay this slightly so PlayerCustomisation.Spawned() runs first
            PlayerCustomisation customisation = botObject.GetComponent<PlayerCustomisation>();
            if (customisation != null)
            {
                // Pick a random color index (1-5, excluding white at index 0)
                int randomColorIndex = Random.Range(1, CustomisationCatalogue.BodyColours.Length);
                
                // Delay color application to next frame so Spawned() runs first
                StartCoroutine(ApplyBotColorDelayed(customisation, randomColorIndex, index));
            }
            else
            {
                Debug.LogWarning($"Bot #{index + 1} has no PlayerCustomisation component!");
            }

            _spawnedBots.Add(player);
            
            Debug.Log($"Spawned bot #{index + 1} - Difficulty: {difficultyIndex}");
        }
        else
        {
            Debug.LogError("Failed to spawn bot or get Player component!");
        }
    }

    /// <summary>
    /// Activate all bots to start racing
    /// </summary>
    public void ActivateBots()
    {
        foreach (var bot in _spawnedBots)
        {
            if (bot != null && bot.TryGetComponent<AIRacer>(out var ai))
            {
                ai.SetActive(true);
            }
        }
    }

    /// <summary>
    /// Deactivate all bots (after race ends)
    /// </summary>
    public void DeactivateBots()
    {
        foreach (var bot in _spawnedBots)
        {
            if (bot != null && bot.TryGetComponent<AIRacer>(out var ai))
            {
                ai.SetActive(false);
            }
        }
    }

    public int BotCount => _spawnedBots.Count;
    public int MaxRacers => _maxRacers;

    // Coroutine to apply bot color after a short delay (allows PlayerCustomisation.Spawned() to run first)
    private System.Collections.IEnumerator ApplyBotColorDelayed(PlayerCustomisation customisation, int colorIndex, int botIndex)
    {
        // Wait one frame for Spawned() to complete
        yield return null;
        
        // Now apply the color
        customisation.SetBodyColour(colorIndex);
        Debug.Log($"Bot #{botIndex + 1} assigned color index {colorIndex}");
    }
}
