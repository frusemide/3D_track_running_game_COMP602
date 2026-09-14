using Fusion;
using UnityEngine;

// Networked cosmetic loadout for a player: a preset body colour + one rigid head
// accessory (none/cap/glasses/sunglasses), parented to Y-Bot's head bone.
//
// Host-authoritative like everything else networked on Player -- only the
// StateAuthority may write the [Networked] fields; every client (including the owner)
// reacts through the ChangeDetector and applies the visual locally. Same pattern
// Player.cs uses for StumbleState/Celebration.
//
// Scope: rigid accessories only, on purpose -- see CustomisationCatalogue. Stat
// modifiers and a shop/selection UI are later, separate steps; this component only
// handles applying the loadout to the model.
public class PlayerCustomisation : NetworkBehaviour
{
    // Per-item fit: the Urban Man accessories weren't authored against Y-Bot's
    // proportions, so each one needs its own nudge/scale once parented to the head
    // bone. Exposed per-slot (rather than baked into prefab variants) so you can dial
    // each one in from the Inspector without creating separate prefab assets.
    [System.Serializable]
    public struct AccessoryFit
    {
        public Vector3 LocalPosition;
        public Vector3 LocalEuler;
        public float Scale;

        public static AccessoryFit Default => new AccessoryFit
        {
            LocalPosition = new Vector3(0.003f, 0.125f, 0f),
            LocalEuler = Vector3.zero,
            Scale = 1.3f
        };
    }

    [Header("Body")]
    [Tooltip("The renderer to recolour. Uses a MaterialPropertyBlock so it doesn't create a material instance per player.")]
    [SerializeField] private Renderer _bodyRenderer;
    [Tooltip("Shader colour property to drive. Built-in/Standard shaders usually want \"_Color\"; URP/Lit shaders usually want \"_BaseColor\" -- check which your Y-Bot material uses.")]
    [SerializeField] private string _colourProperty = "_BaseColor";

    [Header("Head accessory")]
    [Tooltip("Bone to parent the accessory under. Should be Y-Bot's mixamorig:Head.")]
    [SerializeField] private Transform _headBone;
    [SerializeField] private GameObject _capPrefab;
    [SerializeField] private GameObject _glassesPrefab;
    [SerializeField] private GameObject _sunglassesPrefab;

    // Starting values are the last hand-tuned offset/scale that worked for a cap parented
    // to the head bone -- reused as the default for all three so equipping anything is
    // immediately reasonable, not floating off in space. Glasses/sunglasses will likely
    // need their own tweak (lower + closer to the face than a cap sits) -- that's exactly
    // what having three separate fits here is for.
    [Header("Per-item fit (position/rotation/scale relative to the head bone)")]
    [SerializeField] private AccessoryFit _capFit = AccessoryFit.Default;
    [SerializeField] private AccessoryFit _glassesFit = AccessoryFit.Default;
    [SerializeField] private AccessoryFit _sunglassesFit = AccessoryFit.Default;

    [Networked] private int BodyColourIndex { get; set; }
    [Networked] private CustomisationCatalogue.HeadAccessory Accessory { get; set; }

    private MaterialPropertyBlock _propBlock;
    private GameObject _currentAccessoryInstance;
    private ChangeDetector _changeDetector;

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        _propBlock = new MaterialPropertyBlock();

        // Apply whatever the networked state already holds -- covers late joiners
        // seeing players who'd already equipped something before they connected.
        ApplyColour(BodyColourIndex);
        ApplyAccessory(Accessory);
    }

    // --- Host-only setters. Not RPC-wrapped yet -- nothing except the host drives
    // selection during this stage (a debug key, or direct calls while testing). Add an
    // InputAuthority-facing RPC once a client-side shop/selection UI exists (Step 5). ---

    public void SetBodyColour(int index)
    {
        if (!HasStateAuthority) return;
        BodyColourIndex = Mathf.Clamp(index, 0, CustomisationCatalogue.BodyColours.Length - 1);
    }

    public void SetAccessory(CustomisationCatalogue.HeadAccessory accessory)
    {
        if (!HasStateAuthority) return;
        Accessory = accessory;
    }

    // --- React to networked changes on every client ---
    public override void Render()
    {
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            switch (change)
            {
                case nameof(BodyColourIndex):
                    ApplyColour(BodyColourIndex);
                    break;
                case nameof(Accessory):
                    ApplyAccessory(Accessory);
                    break;
            }
        }
    }

    // --- TEMP debug controls, so you can test fits in Play mode without a shop UI yet
    // (Step 5). Only the local player's own instance reacts to its own keypresses --
    // same HasInputAuthority gating Player.cs/ThirdPersonCamera use elsewhere. Uses the
    // new Input System's Keyboard, matching RaceManager's existing "O" debug key rather
    // than the legacy Input class. Remove once a real shop UI calls SetAccessory /
    // SetBodyColour instead.
    private void Update()
    {
        if (!HasInputAuthority) return;

        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null) return;

        if (kb.digit1Key.wasPressedThisFrame) SetAccessory(CustomisationCatalogue.HeadAccessory.None);
        if (kb.digit2Key.wasPressedThisFrame) SetAccessory(CustomisationCatalogue.HeadAccessory.Cap);
        if (kb.digit3Key.wasPressedThisFrame) SetAccessory(CustomisationCatalogue.HeadAccessory.Glasses);
        if (kb.digit4Key.wasPressedThisFrame) SetAccessory(CustomisationCatalogue.HeadAccessory.Sunglasses);

        if (kb.digit5Key.wasPressedThisFrame)
            SetBodyColour((BodyColourIndex + 1) % CustomisationCatalogue.BodyColours.Length);
    }

    // --- Local visual application ---

    private void ApplyColour(int index)
    {
        if (_bodyRenderer == null) return;

        _bodyRenderer.GetPropertyBlock(_propBlock);
        _propBlock.SetColor(_colourProperty, CustomisationCatalogue.GetColour(index));
        _bodyRenderer.SetPropertyBlock(_propBlock);
    }

    private void ApplyAccessory(CustomisationCatalogue.HeadAccessory accessory)
    {
        if (_currentAccessoryInstance != null)
        {
            Destroy(_currentAccessoryInstance);
            _currentAccessoryInstance = null;
        }

        if (accessory == CustomisationCatalogue.HeadAccessory.None || _headBone == null)
            return;

        (GameObject prefab, AccessoryFit fit) = accessory switch
        {
            CustomisationCatalogue.HeadAccessory.Cap => (_capPrefab, _capFit),
            CustomisationCatalogue.HeadAccessory.Glasses => (_glassesPrefab, _glassesFit),
            CustomisationCatalogue.HeadAccessory.Sunglasses => (_sunglassesPrefab, _sunglassesFit),
            _ => (null, default)
        };

        if (prefab == null) return;

        _currentAccessoryInstance = Instantiate(prefab, _headBone);
        _currentAccessoryInstance.transform.localPosition = fit.LocalPosition;
        _currentAccessoryInstance.transform.localRotation = Quaternion.Euler(fit.LocalEuler);
        _currentAccessoryInstance.transform.localScale = Vector3.one * fit.Scale;
    }
}
