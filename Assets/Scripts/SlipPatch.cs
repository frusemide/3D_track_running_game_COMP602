using System.Collections.Generic;
using UnityEngine;

// A slippery hazard on the track (puddle, ice, mud). Add to a GameObject with a
// BoxCollider sized to the patch and tick "Is Trigger" so players run through it.
//
// Player asks IsOnAnyPatch(position) every tick instead of using OnTriggerEnter,
// because Fusion re-simulates ticks on clients and trigger callbacks don't re-run.
// A plain position check gives the same answer on the host and the predicting client.
[RequireComponent(typeof(BoxCollider))]
public class SlipPatch : MonoBehaviour
{
    private static readonly List<SlipPatch> _active = new List<SlipPatch>();
    private BoxCollider _box;

    private void Awake() => _box = GetComponent<BoxCollider>();
    private void OnEnable() => _active.Add(this);
    private void OnDisable() => _active.Remove(this);

    public static bool IsOnAnyPatch(Vector3 position)
    {
        foreach (SlipPatch patch in _active)
        {
            Bounds b = patch._box.bounds;
            // Ignore height: only the footprint on the track matters.
            if (position.x >= b.min.x && position.x <= b.max.x &&
                position.z >= b.min.z && position.z <= b.max.z)
                return true;
        }
        return false;
    }
}
