using Fusion;
using UnityEngine;

// Input replicated from each client to the host every tick.
//
// The two run "pedals" (A/B) are abstract: each client maps its own keys (Left/Right
// arrows by default, remappable later) onto these two signals in BasicSpawner. The host
// only ever sees pedal A / pedal B, so alternation validation is identical regardless of
// anyone's key bindings.
public struct NetworkInputData : INetworkInput
{
    // Existing button bits (mouse etc.) kept for compatibility with earlier tutorial code.
    public const byte MouseButton0 = 1;
    public const byte MouseButton1 = 2;

    // Run pedals for two-key racing. These are the fresh presses THIS tick (edge, not held),
    // so the host counts each alternation once.
    public const byte RunPedalA = 4;
    public const byte RunPedalB = 8;

    public NetworkButtons Buttons;

    // Free-movement direction (lobby WASD).
    public Vector3 Direction;
    public const byte Sprint = 16;   // next free bit after RunPedalB (8)
}
