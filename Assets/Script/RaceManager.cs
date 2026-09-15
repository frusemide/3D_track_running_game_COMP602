using UnityEngine;

public class RaceManager : MonoBehaviour
{
    public static RaceManager Instance;

    [Header("Race Components")]
    public RaceTimer raceTimer;
    public LeaderboardManager leaderboardManager;
    public LeaderboardUI leaderboardUI;

    [Header("Race Settings")]
    public bool startRaceAutomatically = true;

    private bool raceStarted;
    private bool raceFinished;

    public bool RaceStarted => raceStarted;
    public bool RaceFinished => raceFinished;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (startRaceAutomatically)
        {
            StartRace();
        }
    }

    public void StartRace()
    {
        if (raceTimer == null)
        {
            Debug.LogError("RaceTimer has not been assigned to RaceManager.");
            return;
        }

        raceStarted = true;
        raceFinished = false;

        raceTimer.StartTimer();

        if (leaderboardUI != null)
        {
            leaderboardUI.HideLeaderboard();
        }

        Debug.Log("Race started!");
    }

    public void FinishRace()
    {
        if (!raceStarted || raceFinished)
            return;

        raceFinished = true;
        raceStarted = false;

        float finalTime = raceTimer.StopTimer();

        Debug.Log("Race finished!");
        Debug.Log("Final time: " + RaceTimer.FormatTime(finalTime));

        if (leaderboardManager != null)
        {
            leaderboardManager.AddTime(finalTime);
        }
    }

    public void RestartRace()
    {
        if (raceTimer != null)
        {
            raceTimer.ResetTimer();
        }

        if (leaderboardUI != null)
        {
            leaderboardUI.HideLeaderboard();
        }

        StartRace();
    }
}
