using TMPro;
using UnityEngine;

// Main menu name input. Saves the name as it is typed.
public class NameEntryUI : MonoBehaviour
{
    [SerializeField] private TMP_InputField _nameInput;

    private void Start()
    {
        if (_nameInput == null)
        {
            Debug.LogWarning("NameEntryUI: no TMP_InputField assigned.");
            return;
        }

        _nameInput.characterLimit = PlayerNameStore.MaxLength;
        _nameInput.text = PlayerNameStore.Name;
        _nameInput.onValueChanged.AddListener(OnNameChanged);
    }

    private void OnDestroy()
    {
        if (_nameInput != null)
            _nameInput.onValueChanged.RemoveListener(OnNameChanged);
    }

    private void OnNameChanged(string value)
    {
        PlayerNameStore.Name = value;
    }
}
