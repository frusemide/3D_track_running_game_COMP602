using UnityEngine;

// Tab order in the shop matches this order: Head, Colour, Feet.
public enum CosmeticSlot { Head = 0, Colour = 1, Feet = 2 }

[System.Serializable]
public struct AttachmentFit
{
    public Vector3 LocalPosition;
    public Vector3 LocalEuler;
    public float Scale;

    public static AttachmentFit Default => new AttachmentFit
    {
        LocalPosition = Vector3.zero,
        LocalEuler = Vector3.zero,
        Scale = 1f
    };
}

// Display-only for now. Later, the host can read these to modify movement
// (e.g. JumpDistance once hazards exist, Boost -> start-timing bonus,
// Recovery -> shorter stumble lockout). Higher is always better.
[System.Serializable]
public struct ItemStats
{
    public int TopSpeed;
    public int Acceleration;
    public int Boost;
    public int JumpDistance;
    [Tooltip("Higher = gets up faster after a stumble.")]
    public int Recovery;

    public static ItemStats operator +(ItemStats a, ItemStats b) => new ItemStats
    {
        TopSpeed = a.TopSpeed + b.TopSpeed,
        Acceleration = a.Acceleration + b.Acceleration,
        Boost = a.Boost + b.Boost,
        JumpDistance = a.JumpDistance + b.JumpDistance,
        Recovery = a.Recovery + b.Recovery
    };
}

[CreateAssetMenu(menuName = "Dash/Cosmetic Item", fileName = "NewCosmeticItem")]
public class CosmeticItem : ScriptableObject
{
    [Header("Shop display")]
    public string DisplayName;
    public Sprite Icon;                 // unselected state (white cell, red outline + red icon)
    public Sprite SelectedIcon;         // selected state (red cell, white icon)
    public CosmeticSlot Slot;
    [Tooltip("Everything starts unlocked for now. When the wallet exists, ownership replaces this.")]
    public bool UnlockedByDefault = true;

    [Header("Head: Prefab only.  Feet: Prefab = LEFT shoe, SecondaryPrefab = RIGHT shoe.")]
    public GameObject Prefab;
    public AttachmentFit Fit = AttachmentFit.Default;
    public GameObject SecondaryPrefab;
    public AttachmentFit SecondaryFit = AttachmentFit.Default;

    [Header("Colour items only")]
    public Color BodyColour = Color.white;
    [Tooltip("Cycles through all colours instead of using Body Colour.")]
    public bool Rainbow;
    [Tooltip("Full colour cycles per second when Rainbow is ticked.")]
    public float RainbowSpeed = 0.25f;

    [Header("Stats (display only for now)")]
    public ItemStats Stats;
}
