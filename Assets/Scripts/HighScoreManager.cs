using Fusion;
using UnityEngine;

public class HighScoreManager : MonoBehaviour
{
    [SerializeField]
    private GameObject highScorePopup;

    private float previousHighScore;
    private PracticeController practiceController;
    private PracticeController.PracticeState previousState;

    void Start()
    {
        previousHighScore = PlayerPrefs.GetFloat("HighScore", 999f);
        highScorePopup.SetActive(false);
    }

    void Update()
    {
        // Find the local player's PracticeController
        if (practiceController == null)
        {
            NetworkRunner runner = FindFirstObjectByType<NetworkRunner>();

            if (runner == null || runner.LocalPlayer == PlayerRef.None)
                return;

            if (runner.TryGetPlayerObject(runner.LocalPlayer, out var playerObject))
            {
                practiceController = playerObject.GetComponent<PracticeController>();

                if (practiceController != null)
                {
                    previousState = practiceController.State;
                }
            }

            return;
        }

        // Only react when the practice state changes
        if (practiceController.State != previousState)
        {
            // New race starting - hide the old popup
            if (practiceController.State == PracticeController.PracticeState.Countdown)
            {
                highScorePopup.SetActive(false);
            }

            // Race has just finished
            if (practiceController.State == PracticeController.PracticeState.Finished)
            {
                CheckHighScore();
            }

            previousState = practiceController.State;
        }
    }

    private void CheckHighScore()
    {
        float finishTime = practiceController.LastTime;

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