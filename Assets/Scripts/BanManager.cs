using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// Enforces host bans. On the host it refuses banned devices when they try to join and removes
// banned players from the session. On a banned client it shows a notice and returns to the main menu.
// Created automatically at startup, so it needs no scene setup.
public class BanManager : MonoBehaviour, INetworkRunnerCallbacks
{
    private const int MainMenuSceneIndex = 0;
    private const float DisconnectDelay = 1f;
    private const float NoticeDuration = 6f;

    public static BanManager Instance { get; private set; }

    private NetworkRunner _runner;
    private float _nextRunnerSearch;
    private bool _leaving;
    private GameObject _noticeBox;
    private TextMeshProUGUI _noticeText;
    private float _noticeHideTime;

    public NetworkRunner Runner => _runner;
    public bool IsHost => _runner != null && _runner.IsRunning && _runner.IsServer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (Instance != null)
            return;

        var go = new GameObject("BanManager");
        DontDestroyOnLoad(go);
        go.AddComponent<BanManager>();
        go.AddComponent<BanPanelUI>();
    }

    private void Awake()
    {
        Instance = this;
        BuildNotice();
    }

    private void Update()
    {
        // Registers with each new session's runner.
        if (_runner == null && Time.unscaledTime >= _nextRunnerSearch)
        {
            _nextRunnerSearch = Time.unscaledTime + 0.25f;
            _runner = FindFirstObjectByType<NetworkRunner>();
            if (_runner != null)
                _runner.AddCallbacks(this);
        }

        if (_noticeBox != null && _noticeBox.activeSelf && Time.unscaledTime >= _noticeHideTime)
            _noticeBox.SetActive(false);
    }

    // --- Host side ---

    public string GetPlayerName(PlayerRef player)
    {
        Player p = Player.FindByPlayerRef(player);
        if (p != null)
            return p.DisplayName.ToString();
        return $"Player {player.PlayerId}";
    }

    // Bans the player's device and removes them from the session. The host cannot ban itself.
    public void Ban(PlayerRef player)
    {
        if (!IsHost || player == _runner.LocalPlayer)
            return;

        string playerName = GetPlayerName(player);
        string deviceId = BanList.DeviceIdFromToken(_runner.GetPlayerConnectionToken(player));

        if (string.IsNullOrEmpty(deviceId))
            Debug.LogWarning($"BanManager: {playerName} has no device ID, so they can only be removed, not blocked from rejoining.");
        else
            BanList.Ban(deviceId, playerName);

        Player target = Player.FindByPlayerRef(player);
        if (target != null)
            target.NotifyBanned();

        StartCoroutine(DisconnectAfterDelay(_runner, player));
        ShowNotice($"{playerName} has been banned.");
    }

    // The delay gives the ban notice time to reach the player before they are disconnected.
    private IEnumerator DisconnectAfterDelay(NetworkRunner runner, PlayerRef player)
    {
        yield return new WaitForSecondsRealtime(DisconnectDelay);

        if (runner == null || !runner.IsRunning)
            yield break;

        foreach (var active in runner.ActivePlayers)
        {
            if (active == player)
            {
                runner.Disconnect(player);
                yield break;
            }
        }
    }

    void INetworkRunnerCallbacks.OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
    {
        if (BanList.IsBanned(BanList.DeviceIdFromToken(token)))
            request.Refuse();
    }

    // --- Client side ---

    public void OnBannedByHost()
    {
        ShowNotice("You have been banned from this session by the host.");
        ReturnToMainMenu();
    }

    public void ShowJoinRefused()
    {
        ShowNotice("You are banned from this session.");
    }

    private async void ReturnToMainMenu()
    {
        if (_leaving)
            return;
        _leaving = true;

        // Waits a frame so the runner is not shut down from inside a network callback.
        await Task.Yield();

        GameplayInputBlock.Blocked = false;
        if (LoadingScreen.Instance != null)
            LoadingScreen.Instance.Show();

        if (_runner != null && _runner.IsRunning)
            await _runner.Shutdown();

        var launcher = FindFirstObjectByType<GameLauncher>();
        if (launcher != null)
            Destroy(launcher.gameObject);

        MenuState.SkipTitle = true;
        SceneManager.LoadScene(MainMenuSceneIndex);

        while (SceneManager.GetActiveScene().buildIndex != MainMenuSceneIndex)
            await Task.Yield();
        await Task.Yield();
        await Task.Yield();

        if (LoadingScreen.Instance != null)
            LoadingScreen.Instance.Hide();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        _leaving = false;
    }

    // --- Notice text ---

    public void ShowNotice(string message)
    {
        if (_noticeBox == null)
            return;

        _noticeText.text = message;
        _noticeBox.SetActive(true);
        _noticeHideTime = Time.unscaledTime + NoticeDuration;
    }

    private void BuildNotice()
    {
        Canvas canvas = UiFactory.CreateOverlayCanvas(transform, "BanNoticeCanvas", 600);

        var box = UiFactory.CreatePanel(canvas.transform, "NoticeBox", new Color(0f, 0f, 0f, 0.85f));
        box.raycastTarget = false;
        RectTransform boxRect = box.rectTransform;
        boxRect.anchorMin = boxRect.anchorMax = boxRect.pivot = new Vector2(0.5f, 1f);
        boxRect.anchoredPosition = new Vector2(0f, -160f);
        boxRect.sizeDelta = new Vector2(1100f, 100f);
        _noticeBox = box.gameObject;

        _noticeText = UiFactory.CreateText(box.transform, "", 40, TextAlignmentOptions.Center);
        _noticeText.raycastTarget = false;
        UiFactory.Stretch(_noticeText.rectTransform);

        _noticeBox.SetActive(false);
    }

    void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
    void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    void INetworkRunnerCallbacks.OnInput(NetworkRunner runner, NetworkInput input) { }
    void INetworkRunnerCallbacks.OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    void INetworkRunnerCallbacks.OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner) { }
    void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    void INetworkRunnerCallbacks.OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    void INetworkRunnerCallbacks.OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    void INetworkRunnerCallbacks.OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    void INetworkRunnerCallbacks.OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    void INetworkRunnerCallbacks.OnSceneLoadDone(NetworkRunner runner) { }
    void INetworkRunnerCallbacks.OnSceneLoadStart(NetworkRunner runner) { }
    void INetworkRunnerCallbacks.OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    void INetworkRunnerCallbacks.OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    void INetworkRunnerCallbacks.OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }
    void INetworkRunnerCallbacks.OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
}
