using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gives AI bots a random body colour, and optionally a random head item and shoes, from the
/// CosmeticDatabase so they're visually distinct.
///
/// Works through PlayerCustomisation, so the choices are stored as networked item indices:
/// every client sees the same loadout, and CosmeticVisuals does the actual recolouring and
/// attaching (including animating a Rainbow item). Runs on the host only.
///
/// Note: head items and shoes carry stats (and shoes give weather protection), so bots
/// wearing them race with those bonuses, exactly like players. Tick Stat Free Items Only
/// if accessories should be for looks only.
///
/// BotSpawner adds this component at runtime if the bot prefab doesn't already have it.
/// Add it to the Player prefab yourself if you want to change the settings below.
/// </summary>
public class BotColorizer : MonoBehaviour
{
    [Header("Colour choice")]
    [Tooltip("Tick to stop bots getting the first colour in the database (the default player colour). Unticked, the default colour is one of the random options.")]
    [SerializeField] private bool _skipDefaultColour = false;

    [Tooltip("Allow bots to get Rainbow colour items.")]
    [SerializeField] private bool _allowRainbow = true;

    [Header("Accessories")]
    [Tooltip("Chance (0 to 1) that a bot wears a random head item. 0 = never, 1 = always.")]
    [Range(0f, 1f)]
    [SerializeField] private float _headItemChance = 0.5f;

    [Tooltip("Chance (0 to 1) that a bot wears random shoes. 0 = never, 1 = always.")]
    [Range(0f, 1f)]
    [SerializeField] private float _shoesChance = 0.5f;

    [Tooltip("Only give bots head items and shoes with no stat bonuses, so accessories don't change how fast they race.")]
    [SerializeField] private bool _statFreeItemsOnly = false;

    /// <summary>
    /// Picks a random colour from the CosmeticDatabase and applies it to this bot.
    /// Colours in coloursInUse are avoided until every colour has been used once.
    /// Must be called on the host, before the bot starts racing (the loadout locks mid-race).
    /// Returns the chosen colour index, or -1 if no colour could be applied.
    /// </summary>
    public int ApplyRandomColour(ICollection<int> coloursInUse = null)
    {
        PlayerCustomisation customisation = GetComponent<PlayerCustomisation>();
        if (customisation == null)
        {
            Debug.LogWarning($"[BotColorizer] {gameObject.name} has no PlayerCustomisation component.");
            return -1;
        }

        if (!customisation.HasStateAuthority)
            return -1;   // only the host changes loadouts; clients receive the result

        List<int> candidates = GetCandidateColours(customisation.Database);
        if (candidates.Count == 0)
        {
            Debug.LogWarning("[BotColorizer] No usable colours in the Cosmetic Database. Check the Colours list and Unlocked By Default.");
            return -1;
        }

        // Prefer a colour no other bot has yet; once they're all taken, allow repeats.
        List<int> pool = candidates;
        if (coloursInUse != null)
        {
            List<int> unused = candidates.FindAll(i => !coloursInUse.Contains(i));
            if (unused.Count > 0)
                pool = unused;
        }

        int colourIndex = pool[Random.Range(0, pool.Count)];
        customisation.SetItem(CosmeticSlot.Colour, colourIndex);

        CosmeticItem item = customisation.Database.Colours[colourIndex];
        Debug.Log($"[BotColorizer] {gameObject.name} assigned colour {colourIndex} ({item.DisplayName})");
        return colourIndex;
    }

    /// <summary>
    /// Randomly gives this bot a head item and/or shoes, using Head Item Chance and Shoes Chance.
    /// Must be called on the host, before the bot starts racing (the loadout locks mid-race).
    /// </summary>
    public void ApplyRandomAccessories()
    {
        PlayerCustomisation customisation = GetComponent<PlayerCustomisation>();
        if (customisation == null || !customisation.HasStateAuthority)
            return;

        TryEquipRandom(customisation, CosmeticSlot.Head, _headItemChance);
        TryEquipRandom(customisation, CosmeticSlot.Feet, _shoesChance);
    }

    private void TryEquipRandom(PlayerCustomisation customisation, CosmeticSlot slot, float chance)
    {
        if (Random.value >= chance)
            return;   // this bot goes without (stays at -1, nothing equipped)

        CosmeticDatabase database = customisation.Database;
        CosmeticItem[] items = database != null ? database.GetItems(slot) : null;
        if (items == null)
            return;

        var candidates = new List<int>();
        for (int i = 0; i < items.Length; i++)
        {
            CosmeticItem item = items[i];
            if (item == null || !item.UnlockedByDefault) continue;   // PlayerCustomisation rejects these
            if (_statFreeItemsOnly && HasStats(item)) continue;
            candidates.Add(i);
        }

        if (candidates.Count == 0)
            return;

        int index = candidates[Random.Range(0, candidates.Count)];
        customisation.SetItem(slot, index);
        Debug.Log($"[BotColorizer] {gameObject.name} equipped {slot}: {items[index].DisplayName}");
    }

    private static bool HasStats(CosmeticItem item)
    {
        ItemStats s = item.Stats;
        return s.TopSpeed != 0 || s.Acceleration != 0 || s.Boost != 0 || s.Grip != 0 || s.Recovery != 0
            || item.SuitedWeather != WeatherType.Clear;
    }

    // Colour indices a bot is allowed to use. Matches what PlayerCustomisation accepts:
    // only real items that are unlocked by default.
    private List<int> GetCandidateColours(CosmeticDatabase database)
    {
        var candidates = new List<int>();
        if (database == null || database.Colours == null)
            return candidates;

        for (int i = 0; i < database.Colours.Length; i++)
        {
            CosmeticItem item = database.Colours[i];
            if (item == null || !item.UnlockedByDefault) continue;
            if (_skipDefaultColour && i == 0) continue;
            if (item.Rainbow && !_allowRainbow) continue;
            candidates.Add(i);
        }

        return candidates;
    }
}
