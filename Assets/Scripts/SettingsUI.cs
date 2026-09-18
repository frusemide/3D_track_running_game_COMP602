using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class SettingsUI : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject mainButtons;

    [Header("Buttons")]
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button closeSettingsButton;
    [SerializeField] private Button pedalAButton;
    [SerializeField] private Button pedalBButton;

    [Header("Text")]
    [SerializeField] private TMP_Text pedalAText;
    [SerializeField] private TMP_Text pedalBText;

    private bool rebindingPedalA;
    private bool rebindingPedalB;

    private void Start()
    {
        settingsPanel.SetActive(false);

        settingsButton.onClick.AddListener(OpenSettings);
        closeSettingsButton.onClick.AddListener(CloseSettings);
        pedalAButton.onClick.AddListener(StartRebindPedalA);
        pedalBButton.onClick.AddListener(StartRebindPedalB);

        UpdateKeybindText();
    }

    private void Update()
    {
        if (!rebindingPedalA && !rebindingPedalB)
            return;

        if (Keyboard.current == null)
            return;

        foreach (KeyControl key in Keyboard.current.allKeys)
        {
            if (!key.wasPressedThisFrame)
                continue;

            Key pressedKey = key.keyCode;

            if (rebindingPedalA)
            {
                KeybindManager.PedalA = pressedKey;
                rebindingPedalA = false;
                pedalAText.text = "Pedal A: " + pressedKey;
            }
            else if (rebindingPedalB)
            {
                KeybindManager.PedalB = pressedKey;
                rebindingPedalB = false;
                pedalBText.text = "Pedal B: " + pressedKey;
            }

            GameplayInputBlock.Blocked = false;
            return;
        }
    }

    public void OpenSettings()
    {
        mainButtons.SetActive(false);
        settingsPanel.SetActive(true);

        GameplayInputBlock.Blocked = true;

        UpdateKeybindText();
    }

    public void CloseSettings()
    {
        rebindingPedalA = false;
        rebindingPedalB = false;

        settingsPanel.SetActive(false);
        mainButtons.SetActive(true);

        GameplayInputBlock.Blocked = false;
    }

    private void StartRebindPedalA()
    {
        Debug.Log("PEDAL A BUTTON CLICKED");

        rebindingPedalA = true;
        rebindingPedalB = false;

        pedalAText.text = "Press a key...";
        GameplayInputBlock.Blocked = true;
    }

    private void StartRebindPedalB()
    {
        Debug.Log("PEDAL B BUTTON CLICKED");

        rebindingPedalA = false;
        rebindingPedalB = true;

        pedalBText.text = "Press a key...";
        GameplayInputBlock.Blocked = true;
    }

    private void UpdateKeybindText()
    {
        pedalAText.text = "Pedal A: " + KeybindManager.PedalA;
        pedalBText.text = "Pedal B: " + KeybindManager.PedalB;
    }
}