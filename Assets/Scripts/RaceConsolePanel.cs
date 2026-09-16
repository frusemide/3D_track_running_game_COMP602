using System.Text;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Confirm/abort UI shown when a player interacts with the 3D race-start console in
// Gathering_Lobby. Anyone can open it to see who's joined, but only the host sees the
// Start/Abort controls -- everyone else sees a "host only" notice instead.
public class RaceConsolePanel : MonoBehaviour
{
    [SerializeField] private GameObject _root;              // panel root to toggle
    [SerializeField] private TMP_Text _playerListText;       // joined players list
    [SerializeField] private GameObject _hostControls;       // parent of Start/Abort buttons
    [SerializeField] private GameObject _hostOnlyNotice;     // "Only the host can start the race" label
    [SerializeField] private Button _startButton;
    [SerializeField] private Button _abortButton;
    [SerializeField] private RaceManager _raceManager;

    private NetworkRunner _runner;
    private bool _isOpen;

    private void Awake()
    {
        if (_root != null)
            _root.SetActive(false);

        if (_startButton != null)
            _startButton.onClick.AddListener(OnStartClicked);
        if (_abortButton != null)
            _abortButton.onClick.AddListener(OnAbortClicked);
    }

    private void Update()
    {
        if (_isOpen
            && Keyboard.current != null
            && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            OnAbortClicked();   // Escape cancels, same as pressing Abort
        }
    }

    // Called by RaceConsole when a player interacts with the console object.
    public void Open(NetworkRunner runner)
    {
        _runner = runner;
        bool isHost = _runner != null && _runner.IsServer;

        if (_hostControls != null)
            _hostControls.SetActive(isHost);
        if (_hostOnlyNotice != null)
            _hostOnlyNotice.SetActive(!isHost);

        RefreshPlayerList();

        if (_root != null)
            _root.SetActive(true);

        _isOpen = true;

        // Free the cursor so the player can click the panel's buttons.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Reuse the existing menu-open block so movement/pedals pause like any other menu.
        GameplayInputBlock.Blocked = true;
    }

    public void Close()
    {
        if (_root != null)
            _root.SetActive(false);

        // Explicitly hide these too, in case they aren't nested under _root in the Hierarchy.
        if (_hostControls != null)
            _hostControls.SetActive(false);
        if (_hostOnlyNotice != null)
            _hostOnlyNotice.SetActive(false);

        _isOpen = false;

        // Re-lock for gameplay camera control.
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        GameplayInputBlock.Blocked = false;
    }

    private void RefreshPlayerList()
    {
        if (_playerListText == null || _runner == null)
            return;

        var sb = new StringBuilder();
        int count = 0;
        foreach (var p in _runner.ActivePlayers)
        {
            count++;
            sb.AppendLine($"Player {count}{(p == _runner.LocalPlayer ? "  (you)" : "")}");
        }

        _playerListText.text = sb.Length > 0 ? sb.ToString() : "No players joined yet.";
    }

    private void OnStartClicked()
    {
        if (_raceManager != null)
            _raceManager.RequestStartRace();

        Close();
    }

    private void OnAbortClicked()
    {
        Close();
    }
}
