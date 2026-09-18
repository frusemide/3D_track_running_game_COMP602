using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// In-lobby spawn + input handler. Lives in the Gathering_Lobby scene. Does NOT start the
// session (that's GameLauncher's job) -- it finds the runner the launcher created and
// registers its callbacks so it receives OnPlayerJoined / OnInput etc.
public class BasicSpawner : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] private NetworkPrefabRef _playerPrefab;
    private Dictionary<PlayerRef, NetworkObject> _spawnedCharacters = new Dictionary<PlayerRef, NetworkObject>();

    // Default bindings: Left arrow = pedal A, Right arrow = pedal B. Remappable later.
    private Key _pedalAKey => KeybindManager.PedalA;
    private Key _pedalBKey => KeybindManager.PedalB;
    private bool _pedalA;
    private bool _pedalB;

    private NetworkRunner _runner;

    private void Start()
    {
        GameplayInputBlock.Blocked = false;   // ensure a fresh lobby starts unblocked

        // The launcher created the runner in the menu scene and it persisted here.
        // Find it and register ourselves as a callback handler so we receive
        // OnPlayerJoined / OnInput / etc. in this scene.
        _runner = FindFirstObjectByType<NetworkRunner>();
        if (_runner != null)
        {
            _runner.AddCallbacks(this);
        }
        else
        {
            Debug.LogError("BasicSpawner: no NetworkRunner found. Did you start from the menu?");
        }
    }

    void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer)
        {
            Vector3 spawnPosition = new Vector3((player.RawEncoded % runner.Config.Simulation.PlayerCount) * 3, 1.1f, 0);
            NetworkObject networkPlayerObject = runner.Spawn(_playerPrefab, spawnPosition, Quaternion.identity, player);
            runner.SetPlayerObject(player, networkPlayerObject);
            _spawnedCharacters.Add(player, networkPlayerObject);
        }
    }

    void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (_spawnedCharacters.TryGetValue(player, out NetworkObject networkObject))
        {
            runner.Despawn(networkObject);
            _spawnedCharacters.Remove(player);
        }
    }

    private void Update()
    {
        if (GameplayInputBlock.Blocked)
            return;   // don't sample pedals while a menu is open

        if (Keyboard.current != null)
        {
            _pedalA = _pedalA | Keyboard.current[_pedalAKey].wasPressedThisFrame;
            _pedalB = _pedalB | Keyboard.current[_pedalBKey].wasPressedThisFrame;
        }
    }

    void INetworkRunnerCallbacks.OnInput(NetworkRunner runner, NetworkInput input)
    {
        var data = new NetworkInputData();
        var keyboard = Keyboard.current;

        // If a menu is open locally, send empty input (no movement, no pedals).
        if (GameplayInputBlock.Blocked)
        {
            input.Set(data);   // empty input
            return;
        }

        if (keyboard != null)
        {
            Vector2 moveInput = Vector2.zero;
            if (keyboard.wKey.isPressed) moveInput.y += 1f;
            if (keyboard.sKey.isPressed) moveInput.y -= 1f;
            if (keyboard.aKey.isPressed) moveInput.x -= 1f;
            if (keyboard.dKey.isPressed) moveInput.x += 1f;

            Camera cam = Camera.main;
            if (cam != null && moveInput != Vector2.zero)
            {
                Vector3 camForward = cam.transform.forward;
                Vector3 camRight = cam.transform.right;

                camForward.y = 0f;
                camRight.y = 0f;
                camForward.Normalize();
                camRight.Normalize();

                data.Direction = camForward * moveInput.y + camRight * moveInput.x;
            }
        }

        data.Buttons.Set(NetworkInputData.Sprint,
            keyboard != null && keyboard.leftShiftKey.isPressed);
        data.Buttons.Set(NetworkInputData.RunPedalA, _pedalA);
        data.Buttons.Set(NetworkInputData.RunPedalB, _pedalB);
        _pedalA = false;
        _pedalB = false;

        input.Set(data);
    }

    void INetworkRunnerCallbacks.OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    void INetworkRunnerCallbacks.OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner) { }
    void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    void INetworkRunnerCallbacks.OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
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
