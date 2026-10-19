using UnityEngine;

// The shop's item lists. A list INDEX is what gets networked, so only ever append
// new items to the end -- never reorder or remove existing entries.
[CreateAssetMenu(menuName = "Dash/Cosmetic Database", fileName = "CosmeticDatabase")]
public class CosmeticDatabase : ScriptableObject
{
    public CosmeticItem[] HeadItems;
    public CosmeticItem[] Colours;
    public CosmeticItem[] FeetItems;

    public CosmeticItem[] GetItems(CosmeticSlot slot) => slot switch
    {
        CosmeticSlot.Head => HeadItems,
        CosmeticSlot.Colour => Colours,
        CosmeticSlot.Feet => FeetItems,
        _ => System.Array.Empty<CosmeticItem>()
    };

    // Returns null for -1 ("nothing equipped") or any out-of-range index.
    public CosmeticItem Get(CosmeticSlot slot, int index)
    {
        CosmeticItem[] items = GetItems(slot);
        return items != null && index >= 0 && index < items.Length ? items[index] : null;
    }
}
