using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

// Persistent launcher: starts the Fusion session from the main menu and loads the
// gathering lobby scene. Survives the menu -> lobby transition via DontDestroyOnLoad.
//
// Uses the shared LoadingScreen (a separate persistent object) to cover transitions, so
// the launcher can be destroyed on return-to-menu without taking the loading screen with it.
public class GameLauncher : MonoBehaviour
{
    [SerializeField] private int _lobbySceneIndex = 1;   // Gathering_Lobby build index
    [SerializeField] private string _sessionName = "TestRoom";

    private NetworkRunner _runner;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    public void StartHost() => StartGame(GameMode.Host);
    public void StartClient() => StartGame(GameMode.Client);

    private async void StartGame(GameMode mode)
    {
        if (_runner != null)
            return;

        if (LoadingScreen.Instance != null)
            LoadingScreen.Instance.Show();

        _runner = gameObject.AddComponent<NetworkRunner>();
        _runner.ProvideInput = true;

        var scene = SceneRef.FromIndex(_lobbySceneIndex);
        var sceneInfo = new NetworkSceneInfo();
        if (scene.IsValid)
            sceneInfo.AddSceneRef(scene, LoadSceneMode.Single);

        var result = await _runner.StartGame(new StartGameArgs()
        {
            GameMode = mode,
            SessionName = _sessionName,
            Scene = scene,
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
        });

        if (!result.Ok)
        {
            Debug.LogError($"StartGame failed: {result.ShutdownReason}");
            Destroy(_runner);
            _runner = null;
            if (LoadingScreen.Instance != null)
                LoadingScreen.Instance.Hide();
            return;
        }

        // Wait until the lobby is the active scene and has rendered, then uncover.
        while (SceneManager.GetActiveScene().buildIndex != _lobbySceneIndex)
            await System.Threading.Tasks.Task.Yield();
            await System.Threading.Tasks.Task.Yield();
            await System.Threading.Tasks.Task.Yield();

        if (LoadingScreen.Instance != null)
            LoadingScreen.Instance.Hide();
    }
}
