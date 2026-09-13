using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Fusion;

// Gathering lobby pause/overlay menu. Toggled with Escape. Lives in the lobby scene.
//
// In multiplayer, this doesn't truly "pause" (other players keep playing) -- it's an
// overlay of options. Resume and Return to Main Menu are functional; Event Setup, Shop,
// and Settings are scaffolded stubs to fill in when those systems are built.
//
// Return to Main Menu shuts down the Fusion session cleanly, destroys the persistent
// launcher (clean slate), and loads the menu -- using the shared LoadingScreen to cover it.
public class PauseMenu : MonoBehaviour
{
    [SerializeField] private GameObject _menuPanel;      // the overlay panel (starts hidden)
    [SerializeField] private int _mainMenuSceneIndex = 0;

    private bool _isOpen;

    private void Start()
    {
        if (_menuPanel != null)
            _menuPanel.SetActive(false);
        _isOpen = false;
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Debug.Log("Escape pressed");
            if (_isOpen) Resume();
            else Open();
        }
    }

    // --- Open / close ---
    private void Open()
    {
        _isOpen = true;
        if (_menuPanel != null)
            _menuPanel.SetActive(true);

        // Free the cursor so the player can click menu buttons.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        GameplayInputBlock.Blocked = true;   // block gameplay input while menu is open
    }

    // Wired to the Resume button (and Escape while open).
    public void Resume()
    {
        _isOpen = false;
        if (_menuPanel != null)
            _menuPanel.SetActive(false);

        // Re-lock for gameplay.
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        GameplayInputBlock.Blocked = false;   // restore gameplay input
    }

    // --- Scaffolded stubs (fill in when these systems exist) ---

    // Wired to the Event Setup button.
    public void OpenEventSetup()
    {
        // TODO: open the race event setup UI (host chooses course, starts the race).
        Debug.Log("Event Setup: not yet implemented");
    }

    // Wired to the Shop button.
    public void OpenShop()
    {
        // TODO: open the equipment/cosmetics shop UI.
        Debug.Log("Shop: not yet implemented");
    }

    // Wired to the Settings button.
    public void OpenSettings()
    {
        // TODO: open the settings UI (audio, display, controls) -- shared with main menu.
        Debug.Log("Settings: not yet implemented");
    }

    // --- Return to main menu (functional) ---

    // Wired to the Return to Main Menu button.
    public void ReturnToMainMenu()
    {
        ReturnToMainMenuAsync();
    }

    private async void ReturnToMainMenuAsync()
    {
        // Cover the transition.
        if (LoadingScreen.Instance != null)
            LoadingScreen.Instance.Show();

        // Shut down the Fusion session cleanly.
        var runner = FindFirstObjectByType<NetworkRunner>();
        if (runner != null)
            await runner.Shutdown();

        // Destroy the persistent launcher for a clean slate (fresh runner on next host).
        var launcher = FindFirstObjectByType<GameLauncher>();
        if (launcher != null)
            Destroy(launcher.gameObject);

        // Load the main menu (plain Unity load -- Fusion no longer manages scenes).
        MenuState.SkipTitle = true;
        SceneManager.LoadScene(_mainMenuSceneIndex);

        // Wait until the menu is active and rendered, then uncover.
        while (SceneManager.GetActiveScene().buildIndex != _mainMenuSceneIndex)
            await System.Threading.Tasks.Task.Yield();
        await System.Threading.Tasks.Task.Yield();
        await System.Threading.Tasks.Task.Yield();

        if (LoadingScreen.Instance != null)
            LoadingScreen.Instance.Hide();
    }
}
