using UnityEngine;

public class TitleSkip : MonoBehaviour
{
    [SerializeField] private GameObject _titleCanvas;

    private void Start()
    {
        if (MenuState.SkipTitle)
        {
            MenuState.SkipTitle = false;
            if (_titleCanvas != null)
                _titleCanvas.SetActive(false);
        }
    }
}
