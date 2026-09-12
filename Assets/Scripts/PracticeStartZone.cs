using UnityEngine;

// The start-line interactable. Put this on the start-line object (between the flagposts),
// on the "Interactable" layer, with a collider for the spherecast to hit. When the player
// faces it and presses interact, it snaps them to a consistent start pose and readies the dash.
//
// The start pose is defined by an optional _startPoint Transform (drag a positioned empty
// GameObject here). If none is set, the zone's own transform is used.
public class PracticeStartZone : MonoBehaviour, IInteractable
{
    [Tooltip("Where the player is placed to begin the dash. If empty, uses this object's transform.")]
    [SerializeField] private Transform _startPoint;

    public string GetPrompt()
    {
        return "Press E to start practice dash";
    }

    public void Interact(Player player)
    {
        var practice = player.GetComponent<PracticeController>();
        if (practice == null)
            return;

        Transform point = _startPoint != null ? _startPoint : transform;
        practice.ReadyUp(point.position, point.forward);
    }
}