
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public class SprintLeaderboardUI : MonoBehaviour
{
    [Header("Leaderboard Display")]
    public string mapId = "Track01";
    public TMP_Text leaderboardText;
    public TMP_Text personalBestText;

    private void Start()
    {
        RefreshLeaderboard();
    }

    public void SetMap(string newMapId)
    {
        if (string.IsNullOrWhiteSpace(newMapId))
            return;

        mapId = newMapId;
        RefreshLeaderboard();
    }

    public void RefreshLeaderboard()
    {
        if (SprintLeaderboardManager.Instance == null)
        {
            Debug.LogWarning("SprintLeaderboardManager is missing.");
            return;
        }

        SprintLeaderboardManager manager =
            SprintLeaderboardManager.Instance;

        if (leaderboardText != null)
        {
            List<SprintLeaderboardEntry> entries =
                manager.GetLeaderboard(mapId);

            StringBuilder output = new StringBuilder();

            output.AppendLine($"SPRINT LEADERBOARD - {mapId}");
            output.AppendLine("RANK   PLAYER             TIME");
            output.AppendLine("--------------------------------");

            if (entries.Count == 0)
            {
                output.AppendLine("No sprint times recorded yet.");
            }
            else
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    SprintLeaderboardEntry entry = entries[i];
                    bool isCurrentPlayer =
                        entry.playerId == manager.CurrentPlayerId;

                    string playerName = entry.playerName;

                    if (isCurrentPlayer)
                    {
                        output.Append("<color=#FFD700>");
                        playerName += " (YOU)";
                    }

                    output.AppendLine(
                        $"{i + 1,2}.   {playerName,-18} " +
                        $"{SprintLeaderboardManager.FormatTime(entry.timeSeconds)}" +
                        (isCurrentPlayer ? "</color>" : ""));
                }
            }

            leaderboardText.text = output.ToString();
        }

        if (personalBestText != null)
        {
            SprintLeaderboardEntry best =
                manager.GetPersonalBest(mapId);

            if (best == null)
            {
                personalBestText.text = "Your personal best: --:--.---";
            }
            else
            {
                int position = manager.GetPosition(mapId);

                personalBestText.text =
                    $"Your personal best: " +
                    $"{SprintLeaderboardManager.FormatTime(best.timeSeconds)}" +
                    $"\nYour position: #{position}";
            }
        }
    }
}
