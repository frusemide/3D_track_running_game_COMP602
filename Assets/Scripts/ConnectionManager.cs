using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ConnectionManager : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] private NetworkPrefabRef lobbyAvatarPrefab;
    [SerializeField] private int lobbySceneIndex = 1; // build index of lobby scene

    private NetworkRunner _runner;

    // ---- called by UI buttons ----
    public async void HostGame() => await StartSession(GameMode.Host);
    public async void JoinGame() => await StartSession(GameMode.Client);

    public void OnConnectedToServer(NetworkRunner runner)
    {
        throw new NotImplementedException();
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        throw new NotImplementedException();
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
    {
        throw new NotImplementedException();
    }

    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
    {
        throw new NotImplementedException();
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        throw new NotImplementedException();
    }

    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
    {
        throw new NotImplementedException();
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        throw new NotImplementedException();
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
    {
        throw new NotImplementedException();
    }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
        throw new NotImplementedException();
    }

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
        throw new NotImplementedException();
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        throw new NotImplementedException();
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        throw new NotImplementedException();
    }

    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
    {
        throw new NotImplementedException();
    }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data)
    {
        throw new NotImplementedException();
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        throw new NotImplementedException();
    }

    public void OnSceneLoadStart(NetworkRunner runner)
    {
        throw new NotImplementedException();
    }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        throw new NotImplementedException();
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        throw new NotImplementedException();
    }

    private async System.Threading.Tasks.Task StartSession(GameMode mode)
    {
        if (_runner != null) return; // guard against double-clicks

        _runner = gameObject.AddComponent<NetworkRunner>();
        _runner.ProvideInput = true;
        _runner.AddCallbacks(this);

        var sceneInfo = new NetworkSceneInfo();
        sceneInfo.AddSceneRef(SceneRef.FromIndex(lobbySceneIndex), LoadSceneMode.Single);

        var result = await _runner.StartGame(new StartGameArgs
        {
            GameMode = mode,
            SessionName = "SprintLobby",
            Scene = sceneInfo,
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>(),
            PlayerCount = 8
        });

        if (!result.Ok)
        {
            Debug.LogError($"StartGame failed: {result.ShutdownReason}");
            Destroy(_runner);
            _runner = null;
        }
    }
}
