using Fusion;
using UnityEngine;

public class HighScoreManager : MonoBehaviour
{
    [SerializeField]
    private GameObject highScorePopup;

    private float previousHighScore;

    private RaceManager raceManager;
    private RaceManager.RacePhase previousPhase;

    private bool phaseInitialized;
    private bool scoreChecked;

    void Start()
    {
        previousHighScore = PlayerPrefs.GetFloat("HighScore", 999f);

        highScorePopup.SetActive(false);
    }

    void Update()
    {
        // Find the RaceManager in the current scene.
        if (raceManager == null)
        {
            raceManager = FindFirstObjectByType<RaceManager>();
            return;
        }

        // A NetworkBehaviour cannot safely use its networked
        // properties until Fusion has spawned it.
        if (raceManager.Object == null)
        {
            return;
        }

        // Remember the phase we started on.
        if (phaseInitialized == false)
        {
            previousPhase = raceManager.Phase;
            phaseInitialized = true;
            return;
        }

        // Nothing changed, so there is nothing to check.
        if (raceManager.Phase == previousPhase)
        {
            return;
        }

        // A new race has started.
        if (raceManager.Phase == RaceManager.RacePhase.Racing
            || raceManager.Phase == RaceManager.RacePhase.Podium
            || raceManager.Phase == RaceManager.RacePhase.Lobby)
        {
            scoreChecked = false;
            highScorePopup.SetActive(false);
        }

        // The race has just reached the results phase.
        if (raceManager.Phase == RaceManager.RacePhase.Results
            && scoreChecked == false)
        {
            CheckHighScore();
        }

        previousPhase = raceManager.Phase;
    }

    private void CheckHighScore()
    {
        NetworkRunner runner = raceManager.Runner;

        if (runner == null)
        {
            return;
        }

        PlayerRef localPlayer = runner.LocalPlayer;

        if (runner.TryGetPlayerObject(localPlayer, out var playerObject))
        {
            if (playerObject.TryGetComponent<Player>(out var player))
            {
                if (player.HasFinished)
                {
                    scoreChecked = true;

                    int raceTicks = player.FinishTick - raceManager.RaceStartTick;
                    float finishTime = raceTicks * runner.DeltaTime;

                    if (finishTime < previousHighScore)
                    {
                        previousHighScore = finishTime;

                        PlayerPrefs.SetFloat("HighScore", finishTime);
                        PlayerPrefs.Save();

                        highScorePopup.SetActive(true);

                        Debug.Log($"NEW HIGH SCORE! {finishTime:F2}s");
                    }
                }
            }
        }
    }
}