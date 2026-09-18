using UnityEngine;
using UnityEngine.UI;

// Full-screen "Race is about to start" notice shown to every player right before the
// scene transition begins. Lives on its own Canvas in Gathering_Lobby (NOT RaceHUDCanvas,
// since that canvas is moving into Race_Event1). Triggered via RaceManager's start RPC so
// every client sees it at the same moment, then auto-hides after _displayDuration.
public class RaceStartNoticeBanner : MonoBehaviour
{
    [SerializeField] private GameObject _root;        // panel root to toggle
    [SerializeField] private Image _bannerImage;       // the Image component the sprite is drawn on
    [SerializeField] private Sprite _bannerSprite;      // <- the banner graphic itself, drag it in here
    [SerializeField] private float _displayDuration = 2.5f;

    private float _hideAtTime = -1f;

    private void Awake()
    {
        if (_root != null)
            _root.SetActive(false);
    }

    public void Show()
    {
        Show(_displayDuration);
    }

    public void Show(float duration)
    {
        if (_root != null)
            _root.SetActive(true);

        if (_bannerImage != null)
        {
            if (_bannerSprite != null)
                _bannerImage.sprite = _bannerSprite;
            _bannerImage.SetNativeSize();
        }

        _hideAtTime = Time.time + duration;
    }

    private void Update()
    {
        if (_hideAtTime >= 0f && Time.time >= _hideAtTime)
        {
            _hideAtTime = -1f;
            if (_root != null)
                _root.SetActive(false);
        }
    }
}
