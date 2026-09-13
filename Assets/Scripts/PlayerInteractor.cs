using UnityEngine;
using UnityEngine.InputSystem;

// Local interaction detector. Spherecasts in front of the player each frame to find the
// nearest IInteractable, shows its prompt, and triggers it on the interact key.
//
// Local-only: runs for the local player's own character (checked via the Player's input
// authority). Detection and input are local; what each interactable DOES may be local or
// networked, but that's the interactable's concern, not this script's.
//
// Attach to the Player prefab alongside the Player script.
[RequireComponent(typeof(Player))]
public class PlayerInteractor : MonoBehaviour
{
    [Header("Spherecast")]
    [SerializeField] private float _castDistance = 2.5f;   // how far ahead to detect
    [SerializeField] private float _castRadius = 0.5f;     // aim forgiveness
    [SerializeField] private float _originHeight = 1.0f;   // cast from ~chest height
    [SerializeField] private LayerMask _interactableMask;  // only hit interactable objects

    [Header("Input")]
    

    private Player _player;
    private IInteractable _current;   // the interactable currently in focus, if any

    private void Awake()
    {
        _player = GetComponent<Player>();
    }

    private void Update()
    {
        if (_player == null) return;
        if (_player.Object == null) return;

        // Don't detect or trigger interactions while a menu is open.
        if (GameplayInputBlock.Blocked)
            return;

        DetectInteractable();

        if (_current != null
            && Keyboard.current != null
            && Keyboard.current.eKey.wasPressedThisFrame)
        {
            _current.Interact(_player);
        }
    }

    private void DetectInteractable()
    {
        Vector3 origin = transform.position + Vector3.up * _originHeight;
        Vector3 direction = transform.forward;

        Debug.DrawRay(origin, direction * _castDistance, Color.red);

        if (Physics.SphereCast(origin, _castRadius, direction, out RaycastHit hit,
                               _castDistance, _interactableMask))
        {
            var interactable = hit.collider.GetComponentInParent<IInteractable>();
            _current = interactable;
        }
        else
        {
            // Cast ran but hit nothing on the mask.
            _current = null;
        }
    }

    // The current prompt to display, or null if nothing is in focus.
    // The UI reads this to show/hide the interaction prompt.
    public string CurrentPrompt => _current?.GetPrompt();

    // TEMP: simple on-screen prompt. Replace with proper UI later.
    private void OnGUI()
    {
        if (_player.Object == null || !_player.Object.HasInputAuthority)
            return;

        string prompt = CurrentPrompt;
        if (!string.IsNullOrEmpty(prompt))
        {
            var style = new GUIStyle(GUI.skin.box) { fontSize = 18 };
            GUI.Box(new Rect(Screen.width / 2f - 150, Screen.height - 80, 300, 40), prompt, style);
        }
    }
}
