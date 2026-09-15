using UnityEngine;

public class FinishLine : MonoBehaviour
{
    [Header("Player Settings")]
    public string playerTag = "Player";

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag))
            return;

        if (RaceManager.Instance == null)
        {
            Debug.LogError("RaceManager could not be found.");
            return;
        }

        RaceManager.Instance.FinishRace();
    }
}
