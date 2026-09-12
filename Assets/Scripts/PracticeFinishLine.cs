using UnityEngine;

// The finish-line trigger. Put this on the finish-line object (between the flagposts) with
// a trigger collider spanning the gap. When a player's collider crosses it, their dash
// timer stops. Unlike the start zone, this needs no interaction key -- crossing is enough.
//
// The trigger collider must have "Is Trigger" ticked. The player needs a collider (the
// CharacterController counts) for OnTriggerEnter to fire.
public class PracticeFinishLine : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"Finish trigger entered by: {other.name}");
        var player = other.GetComponentInParent<Player>();
        if (player == null)
            return;

        var practice = player.GetComponent<PracticeController>();
        if (practice != null)
            practice.Finish();
    }
}
