using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class QuestNotificationUI : MonoBehaviour
{
    // References to the notification panel and text
    [SerializeField] private GameObject notificationPanel;
    [SerializeField] private TMP_Text notificationTitle;
    [SerializeField] private TMP_Text notificationDescription;

    // Reference to the system controlling the race
    [SerializeField] private RaceManager raceManager;

    [Header("Notification Settings")]
    [SerializeField] private float notificationDuration = 4f;

    // Remember the previous race phase
    private RaceManager.RacePhase previousPhase;
    private bool phaseInitialized;

    // Daily quest progress
    private bool dailyQuestCompleted;
    private string lastCompletedDate = "";

    // Weekly quest progress
    private int weeklyRaceCount;
    private string savedWeek = "";
    private bool weeklyQuestCompleted;

    // Store quests completed during this race
    private readonly List<string> completedQuests = new List<string>();

    // Time when the notification should disappear
    private float notificationEndTime;

    void Start()
    {
        // Hide the notification when the game starts
        notificationPanel.SetActive(false);

        // Load saved daily quest progress
        lastCompletedDate = PlayerPrefs.GetString(
            "DailyQuestCompletedDate", ""
        );

        string today = DateTime.Now.ToString("yyyy-MM-dd");
        dailyQuestCompleted = lastCompletedDate == today;

        // Load saved weekly quest progress
        savedWeek = PlayerPrefs.GetString("WeeklyQuestWeek", "");
        weeklyRaceCount = PlayerPrefs.GetInt("WeeklyQuestRaceCount", 0);

        // Reset weekly progress if a new week started
        string currentWeek = GetCurrentWeek();

        if (savedWeek != currentWeek)
        {
            savedWeek = currentWeek;
            weeklyRaceCount = 0;

            PlayerPrefs.SetString("WeeklyQuestWeek", savedWeek);
            PlayerPrefs.SetInt("WeeklyQuestRaceCount", weeklyRaceCount);
            PlayerPrefs.Save();
        }

        weeklyQuestCompleted = weeklyRaceCount >= 3;
    }

    void Update()
    {
        // Check the notification timer first
        // This also works when the race is not running
        if (notificationPanel.activeSelf &&
            Time.time >= notificationEndTime)
        {
            notificationPanel.SetActive(false);
        }

        // Wait until the race manager is ready
        if (raceManager == null || raceManager.Runner == null)
            return;

        RaceManager.RacePhase currentPhase = raceManager.Phase;

        // Remember the initial race phase
        if (!phaseInitialized)
        {
            previousPhase = currentPhase;
            phaseInitialized = true;
            return;
        }

        // Check quests when the race enters Results
        if (currentPhase == RaceManager.RacePhase.Results &&
            previousPhase != RaceManager.RacePhase.Results)
        {
            // Clear notifications from the previous race
            completedQuests.Clear();

            // Check both quests
            CheckDailyQuest();
            CheckWeeklyQuest();

            // Display all newly completed quests together
            ShowCompletedQuests();
        }

        // Hide the notification when Results ends
        if (currentPhase != RaceManager.RacePhase.Results &&
            previousPhase == RaceManager.RacePhase.Results)
        {
            notificationPanel.SetActive(false);
            completedQuests.Clear();
        }

        // Remember the current phase
        previousPhase = currentPhase;
    }

    private void ShowCompletedQuests()
    {
        // Show nothing if no quests were completed
        if (completedQuests.Count == 0)
        {
            notificationPanel.SetActive(false);
            return;
        }

        // Change the heading based on the number of quests
        notificationTitle.text = completedQuests.Count == 1
            ? "QUEST COMPLETED!"
            : "QUESTS COMPLETED!";

        // Put each completed quest on its own line
        notificationDescription.text =
            string.Join("\n", completedQuests);

        // Show the notification
        notificationPanel.SetActive(true);

        // Set when the notification should disappear
        notificationEndTime = Time.time + notificationDuration;
    }

    private Player FindLocalPlayer()
    {
        // Find the player controlled by this client
        foreach (Player player in FindObjectsByType<Player>(
            FindObjectsSortMode.None))
        {
            if (player.Object != null &&
                player.Object.HasInputAuthority)
            {
                return player;
            }
        }

        // No local player was found
        return null;
    }

    private void CheckDailyQuest()
    {
        Player localPlayer = FindLocalPlayer();

        // Only count a race that our player finished
        if (localPlayer == null || !localPlayer.HasFinished)
            return;

        string today = DateTime.Now.ToString("yyyy-MM-dd");

        // Reset daily progress if the date changed
        if (lastCompletedDate != today)
            dailyQuestCompleted = false;

        // Complete the daily quest once per day
        if (!dailyQuestCompleted)
        {
            dailyQuestCompleted = true;
            lastCompletedDate = today;

            // Save daily quest completion
            PlayerPrefs.SetString("DailyQuestCompletedDate", today);
            PlayerPrefs.Save();

            // Add the daily quest to this race's notification
            completedQuests.Add("Daily Quest: Finish a Race");
        }
    }

    private string GetCurrentWeek()
    {
        // Get today's date
        DateTime today = DateTime.Today;

        // Find how many days have passed since Monday
        int daysSinceMonday =
            ((int)today.DayOfWeek + 6) % 7;

        // Get the Monday of the current week
        DateTime monday = today.AddDays(-daysSinceMonday);

        return monday.ToString("yyyy-MM-dd");
    }

    private void CheckWeeklyQuest()
    {
        Player localPlayer = FindLocalPlayer();

        // Only count a race that our player finished
        if (localPlayer == null || !localPlayer.HasFinished)
            return;

        string currentWeek = GetCurrentWeek();

        // Reset progress if a new week started
        if (savedWeek != currentWeek)
        {
            savedWeek = currentWeek;
            weeklyRaceCount = 0;
            weeklyQuestCompleted = false;
        }

        // Stop counting after the weekly quest is completed
        if (weeklyQuestCompleted)
            return;

        // Count this completed race
        weeklyRaceCount++;

        // Save weekly progress
        PlayerPrefs.SetString("WeeklyQuestWeek", savedWeek);
        PlayerPrefs.SetInt("WeeklyQuestRaceCount", weeklyRaceCount);
        PlayerPrefs.Save();

        Debug.Log("Weekly quest progress: " + weeklyRaceCount + "/3");

        // Complete the weekly quest after 3 races
        if (weeklyRaceCount >= 3)
        {
            weeklyQuestCompleted = true;

            // Add the weekly quest to this race's notification
            completedQuests.Add("Weekly Quest: Complete 3 Races");
        }
    }
}