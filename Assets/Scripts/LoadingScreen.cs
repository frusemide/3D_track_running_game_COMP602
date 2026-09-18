using UnityEngine;

// Independent, persistent loading screen shared by all scene transitions (menu->lobby,
// lobby->menu, and later lobby->race). Lives on its own DontDestroyOnLoad object so it
// survives even when the GameLauncher is destroyed on return-to-menu.
//
// Simple singleton access: LoadingScreen.Instance.Show() / .Hide() from anywhere.
//
// Setup: put this on a persistent Canvas object (Screen Space - Overlay, high sort order)
// with a full-screen opaque panel and "Loading..." text as children. It starts hidden.
public class LoadingScreen : MonoBehaviour
{
    public static LoadingScreen Instance { get; private set; }

    [SerializeField] private GameObject _panel;   // the full-screen covering panel

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.Log("LoadingScreen: duplicate, destroying self");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Debug.Log("LoadingScreen: became Instance");

        if (_panel != null)
            _panel.SetActive(false);
    }

    public void Show()
    {
        Debug.Log($"LoadingScreen.Show called. panel null? {_panel == null}");
        if (_panel != null)
            _panel.SetActive(true);
    }

    public void Hide()
    {
        if (_panel != null)
            _panel.SetActive(false);
    }
}
