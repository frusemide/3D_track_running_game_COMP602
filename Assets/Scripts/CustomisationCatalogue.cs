using UnityEngine;

// Central definition of the cosmetic options available on Y-Bot: a small preset colour
// palette for body tint, and one rigid head-accessory slot parented to the head bone.
//
// Deliberately scoped to rigid, single-bone-mount items only -- no deforming/skinned
// clothing swaps. Full modular clothing (torso/legs/feet swaps) would need runtime
// bone-rebinding since the PolyMate body-part packs carry their own skeletons, which is
// too costly for the current time budget. This is a documented scoping decision --
// revisit modular clothing later, once rigid accessories are implemented and stable.
//
// Indices are what get networked (compact int/byte), so don't reorder or remove existing
// entries once players can own them -- append new entries to the end instead.
public static class CustomisationCatalogue
{
    // --- Body colour ---
    public static readonly Color[] BodyColours =
    {
        Color.white,
        new Color(0.85f, 0.10f, 0.10f), // red
        new Color(0.10f, 0.35f, 0.85f), // blue
        new Color(0.15f, 0.75f, 0.25f), // green
        new Color(0.95f, 0.85f, 0.10f), // yellow
        new Color(0.15f, 0.15f, 0.15f), // black
    };

    public static Color GetColour(int index)
    {
        if (index < 0 || index >= BodyColours.Length)
            index = 0; // fall back to the first preset rather than throwing on bad networked data
        return BodyColours[index];
    }

    // --- Head accessory ---
    // One slot, rigid meshes only. Matches the Urban Man PolyMate pack's Accessories set
    // (CapV1 / GlassesV1 / SunglassesV1), parented straight to mixamorig:Head.
    // PlayerCustomisation holds a per-item position/rotation/scale fit (Inspector
    // fields), since the Urban Man accessories weren't authored against Y-Bot's
    // proportions and each needs its own nudge once parented to the head bone.
    public enum HeadAccessory : byte
    {
        None = 0,
        Cap = 1,
        Glasses = 2,
        Sunglasses = 3
    }
}
