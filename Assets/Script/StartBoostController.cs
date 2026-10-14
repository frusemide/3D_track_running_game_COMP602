
using System.Collections;
using UnityEngine;
using TMPro;

public class StartBoostController : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text countdownText;
    public TMP_Text boostMessageText;

    [Header("Countdown")]
    public float countdownDuration = 3f;

    [Header("Input")]
    public KeyCode accelerationKey = KeyCode.W;

    [Header("Start Timing Windows")]
    [Tooltip("Time before GO that gives a perfect start.")]
    public float perfectWindow = 0.35f;

    [Tooltip("Time before GO that gives a good start.")]
    public float goodWindow = 0.8f;

    [Header("Boost Settings")]
    public Rigidbody carRigidbody;
    public float perfectBoostForce = 18f;
    public float goodBoostForce = 8f;
    public float perfectBoostDuration = 3f;
    public float goodBoostDuration = 2f;

    private float raceStartTime;
    private float boostEndTime;
    private float boostForce;

    private bool raceStarted;
    private bool raceFinished;
    private bool startAttempted;
    private bool boostActive;

    private void Start()
    {
        if (carRigidbody == null)
        {
            carRigidbody = GetComponent<Rigidbody>();
        }

        if (boostMessageText != null)
        {
            boostMessageText.text = "";
        }

        if (countdownText != null)
        {
            countdownText.text = "";
        }

        StartCoroutine(StartCountdown());
    }

    private IEnumerator StartCountdown()
    {
        raceStarted = false;
        raceFinished = false;
        startAttempted = false;
        boostActive = false;

        // Schedule the start time.
        raceStartTime = Time.time + countdownDuration;

        while (Time.time < raceStartTime)
        {
            float remaining = raceStartTime - Time.time;

            if (countdownText != null)
            {
                countdownText.text =
                    Mathf.CeilToInt(remaining).ToString();
            }

            yield return null;
        }

        raceStarted = true;

        if (countdownText != null)
        {
            countdownText.text = "GO!";
        }

        // Start the existing race timer.
        if (RaceManager.Instance != null)
        {
            RaceManager.Instance.StartRace();
        }

        if (!startAttempted && boostMessageText != null)
        {
            boostMessageText.text = "NORMAL START";
        }

        yield return new WaitForSeconds(1f);

        if (countdownText != null)
        {
            countdownText.text = "";
        }
    }

    private void Update()
    {
        if (raceFinished || startAttempted)
        {
            return;
        }

        if (Input.GetKeyDown(accelerationKey))
        {
            EvaluateStart();
        }
    }

    private void EvaluateStart()
    {
        startAttempted = true;

        float timeDifference = Time.time - raceStartTime;

        // Input before GO.
        if (timeDifference < 0f)
        {
            float earlyBy = -timeDifference;

            if (earlyBy <= perfectWindow)
            {
                ActivateBoost(
                    perfectBoostForce,
                    perfectBoostDuration,
                    "PERFECT START!"
                );
            }
            else if (earlyBy <= goodWindow)
            {
                ActivateBoost(
                    goodBoostForce,
                    goodBoostDuration,
                    "GOOD START!"
                );
            }
            else
            {
                ShowMessage("TOO EARLY!");
            }
        }
        // Input just after GO.
        else if (timeDifference <= 0.2f)
        {
            ActivateBoost(
                goodBoostForce,
                goodBoostDuration,
                "GOOD START!"
            );
        }
        else
        {
            ShowMessage("TOO LATE!");
        }
    }

    private void ActivateBoost(
        float force,
        float duration,
        string message)
    {
        if (carRigidbody == null)
        {
            Debug.LogError(
                "StartBoostController requires a Rigidbody."
            );

            ShowMessage("BOOST UNAVAILABLE");
            return;
        }

        boostForce = force;
        boostEndTime = raceStartTime + duration;
        boostActive = true;

        ShowMessage(message);
        Debug.Log(message);
    }

    private void FixedUpdate()
    {
        if (!boostActive || carRigidbody == null)
        {
            return;
        }

        // Do not apply the boost before the race starts.
        if (Time.time < raceStartTime)
        {
            return;
        }

        if (Time.time >= boostEndTime)
        {
            boostActive = false;
            boostForce = 0f;

            ShowMessage("BOOST FINISHED");
            return;
        }

        carRigidbody.AddForce(
            transform.forward * boostForce,
            ForceMode.Acceleration
        );
    }

    public void FinishRace()
    {
        raceFinished = true;
        boostActive = false;
        boostForce = 0f;
    }

    private void ShowMessage(string message)
    {
        if (boostMessageText != null)
        {
            boostMessageText.text = message;
        }
    }
}
