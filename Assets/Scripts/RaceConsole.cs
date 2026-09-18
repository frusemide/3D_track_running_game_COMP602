using Fusion;
using UnityEngine;

// Sits on the 3D console/panel object in Gathering_Lobby. Interacting opens the
// RaceConsolePanel UI (host sees Start/Abort, everyone else sees a host-only notice).
public class RaceConsole : MonoBehaviour, IInteractable
{
    [SerializeField] private RaceConsolePanel _panel;
    [SerializeField] private string _prompt = "Press 'E' to open race console";

    public string GetPrompt()
    {
        return _prompt;
    }

    public void Interact(Player player)
    {
        var runner = FindFirstObjectByType<NetworkRunner>();
        if (_panel != null && runner != null)
            _panel.Open(runner);
    }
}
