using Fusion;
using UnityEngine;

// Networked cosmetic loadout: one head item, one pair of shoes, one body colour.
// Values are indices into the CosmeticDatabase (-1 = nothing, for head/feet).
//
// Host-authoritative: clients ask with RPC_RequestItem, the host validates and writes
// the [Networked] values, and every client applies visuals through CosmeticVisuals
// when the ChangeDetector sees them change.
public class PlayerCustomisation : NetworkBehaviour
{
    [SerializeField] private CosmeticDatabase _database;
    [SerializeField] private CosmeticVisuals _visuals;

    [Networked] public int HeadIndex { get; private set; }
    [Networked] public int ColourIndex { get; private set; }
    [Networked] public int FeetIndex { get; private set; }

    // What's currently applied to the model, so visuals update whenever the networked
    // values differ. (Comparing directly, rather than via ChangeDetector, can't miss a
    // value that changes and changes back between checks -- e.g. during spawn.)
    private int _appliedHead = int.MinValue;
    private int _appliedFeet = int.MinValue;
    private int _appliedColour = int.MinValue;
    private bool _savedLoadoutRequested;
    private Player _player;   // local player: has the saved loadout been sent this spawn?

    public override void Spawned()
    {
        _player = GetComponent<Player>();

        if (HasStateAuthority)
        {
            HeadIndex = -1;
            FeetIndex = -1;
            ColourIndex = 0;
        }
        // Visuals are applied in Render() -- including for late joiners.
    }

    // --- Host-side ---

    public void SetItem(CosmeticSlot slot, int index)
    {
        if (!HasStateAuthority) return;
        if (_player != null && _player.IsRacing) return;   // loadout (and so race stats) locked mid-race
        if (!IsValidChoice(slot, index)) return;

        switch (slot)
        {
            case CosmeticSlot.Head: HeadIndex = index; break;
            case CosmeticSlot.Feet: FeetIndex = index; break;
            case CosmeticSlot.Colour: ColourIndex = index; break;
        }
    }

    // The host only accepts real, unlocked items. When the wallet exists, swap the
    // unlock check for an ownership check here -- the UI never gets the final say.
    private bool IsValidChoice(CosmeticSlot slot, int index)
    {
        if (index == -1)
            return slot != CosmeticSlot.Colour;   // head/feet can be emptied; colour can't

        CosmeticItem item = _database.Get(slot, index);
        return item != null && item.UnlockedByDefault;
    }

    // Sum of the equipped items' stat modifiers, used by Player to adjust race values.
    // Built from the networked loadout, so every machine computes the same result.
    public ItemStats GetStatModifiers()
    {
        ItemStats total = default;
        if (_database == null) return total;

        CosmeticItem head = _database.Get(CosmeticSlot.Head, HeadIndex);
        CosmeticItem colour = _database.Get(CosmeticSlot.Colour, ColourIndex);
        CosmeticItem feet = _database.Get(CosmeticSlot.Feet, FeetIndex);

        if (head != null) total += head.Stats;
        if (colour != null) total += colour.Stats;
        if (feet != null) total += feet.Stats;
        return total;
    }

    // --- Client -> host requests ---

    // Called by the shop for the local player: remember the choice in the save
    // profile, then ask the host to apply it.
    public void RequestItem(CosmeticSlot slot, int index)
    {
        CosmeticsData saved = SaveSystem.Profile.Cosmetics;
        switch (slot)
        {
            case CosmeticSlot.Head: saved.Head = index; break;
            case CosmeticSlot.Colour: saved.Colour = index; break;
            case CosmeticSlot.Feet: saved.Feet = index; break;
        }
        SaveSystem.Save();

        RPC_RequestItem((int)slot, index);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_RequestItem(int slot, int index)
    {
        SetItem((CosmeticSlot)slot, index);
    }

    // --- React to networked changes on every client ---

    public override void Render()
    {
        // Once per spawn, the local player asks the host to re-apply their saved loadout.
        // The host validates it like any other request, so a saved item that no longer
        // exists (or is locked) is simply ignored and the default stays.
        if (!_savedLoadoutRequested && HasInputAuthority)
        {
            _savedLoadoutRequested = true;

            CosmeticsData saved = SaveSystem.Profile.Cosmetics;
            RPC_RequestItem((int)CosmeticSlot.Head, saved.Head);
            RPC_RequestItem((int)CosmeticSlot.Colour, saved.Colour);
            RPC_RequestItem((int)CosmeticSlot.Feet, saved.Feet);
        }

        if (HeadIndex != _appliedHead)
        {
            _appliedHead = HeadIndex;
            _visuals.ApplyHead(_database.Get(CosmeticSlot.Head, HeadIndex));
        }

        if (FeetIndex != _appliedFeet)
        {
            _appliedFeet = FeetIndex;
            _visuals.ApplyFeet(_database.Get(CosmeticSlot.Feet, FeetIndex));
        }

        // A Rainbow colour item is just another colour index; CosmeticVisuals animates it.
        if (ColourIndex != _appliedColour)
        {
            _appliedColour = ColourIndex;
            _visuals.ApplyColour(_database.Get(CosmeticSlot.Colour, ColourIndex));
        }
    }
}