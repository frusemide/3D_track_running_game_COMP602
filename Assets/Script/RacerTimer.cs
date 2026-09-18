using UnityEngine;
using TMPro;

public class RaceTimer : MonoBehaviour
{
    [Header("Timer UI")]
    public TMP_Text timerText;

    private float currentTime;
    private bool raceRunning;

    public float CurrentTime => currentTime;
    public bool RaceRunning => raceRunning;

    private void Start()
    {
        currentTime = 0f;
        raceRunning = false;

        UpdateTimerDisplay();
    }

    private void Update()
    {
        if (!raceRunning)
            return;

        currentTime += Time.deltaTime;

        UpdateTimerDisplay();
    }

    public void StartTimer()
    {
        currentTime = 0f;
        raceRunning = true;

        UpdateTimerDisplay();
    }

    public float StopTimer()
    {
        raceRunning = false;

        UpdateTimerDisplay();

        return currentTime;
    }

    public void ResetTimer()
    {
        currentTime = 0f;
        raceRunning = false;

        UpdateTimerDisplay();
    }

    private void UpdateTimerDisplay()
    {
        if (timerText == null)
            return;

        timerText.text = FormatTime(currentTime);
    }

    public static string FormatTime(float time)
    {
        int minutes = Mathf.FloorToInt(time / 60f);
        float seconds = time % 60f;

        return string.Format("{0:00}:{1:00.000}", minutes, seconds);
    }
}
