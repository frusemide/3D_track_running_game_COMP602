using System.Collections.Generic;
using UnityEngine;

// Moves the track's slip patches to random spots for each race.
//
// RaceManager picks a networked seed when a race starts and calls ApplyLayout() on every
// machine. The same seed always produces the same layout, so every player gets identical
// patches -- this matters because Player checks SlipPatch.IsOnAnyPatch every tick on both
// the host and the predicting client, so they must agree on where the patches are.
//
// Seed 0 = the layout exactly as placed in the scene (used in the lobby).
//
// Setup: add this to any GameObject in the scene (e.g. an empty "Hazards" object), drag your
// slip patches into Patches (or leave it empty to use every SlipPatch in the scene), and set
// Min Z / Max Z to the stretch of track patches may appear on. Select the object to see that
// stretch drawn in the Scene view.
public class SlipPatchLayout : MonoBehaviour
{
    [Header("Patches")]
    [Tooltip("The slip patches to move around. Leave empty to use every SlipPatch in the scene, including disabled ones. Add more patches (e.g. duplicate SlipPatch1) to allow more per race.")]
    [SerializeField] private List<SlipPatch> _patches = new List<SlipPatch>();

    [Header("Where patches can appear (world Z along the track)")]
    [Tooltip("Closest a patch can be to the start.")]
    [SerializeField] private float _minZ = 30f;
    [Tooltip("Furthest a patch can be along the track. Keep it before the finish line.")]
    [SerializeField] private float _maxZ = 180f;
    [Tooltip("Minimum distance between two patches along the track.")]
    [Min(0f)]
    [SerializeField] private float _minGap = 15f;
    [Tooltip("Also shift each patch sideways by up to this much. 0 keeps it where it was placed across the track (the current patch already covers every lane).")]
    [Min(0f)]
    [SerializeField] private float _maxSideOffset = 0f;

    [Header("How many patches per race")]
    [Tooltip("Fewest patches a race can have. The patch count also sets the weather (see WeatherVisuals / RaceManager).")]
    [Min(0)]
    [SerializeField] private int _minPatches = 0;
    [Tooltip("Most patches a race can have. Capped by how many patches are in the list, so add enough copies to the scene.")]
    [Min(0)]
    [SerializeField] private int _maxPatches = 3;

    private readonly List<Vector3> _originalPositions = new List<Vector3>();
    private readonly List<bool> _originalActive = new List<bool>();
    private bool _initialised;
    private bool _hasApplied;
    private int _appliedSeed;

    /// <summary>How many patches are currently switched on (after the last ApplyLayout).</summary>
    public int ActivePatchCount { get; private set; }

    private void Awake() => Initialise();

    private void Initialise()
    {
        if (_initialised)
            return;
        _initialised = true;

        if (_patches.Count == 0)
        {
            _patches.AddRange(FindObjectsByType<SlipPatch>(FindObjectsInactive.Include, FindObjectsSortMode.None));

            // Same order on every machine, so the same seed moves the same patches.
            _patches.Sort((a, b) =>
            {
                int byName = string.CompareOrdinal(a.name, b.name);
                return byName != 0 ? byName : a.transform.position.z.CompareTo(b.transform.position.z);
            });
        }

        _patches.RemoveAll(p => p == null);

        foreach (SlipPatch patch in _patches)
        {
            _originalPositions.Add(patch.transform.position);
            _originalActive.Add(patch.gameObject.activeSelf);
        }
    }

    /// <summary>
    /// How many patches this seed will place. The host uses it to decide the weather before
    /// the layout is applied; every machine gets the same answer from the same seed.
    /// </summary>
    public int GetPatchCount(int seed)
    {
        Initialise();
        if (seed == 0)
            return 0;

        var rng = new System.Random(seed);
        return PickCount(rng);
    }

    // First draw from the seed's random sequence, so GetPatchCount and ApplyLayout agree.
    private int PickCount(System.Random rng)
    {
        int count = rng.Next(_minPatches, Mathf.Max(_minPatches, _maxPatches) + 1);
        return Mathf.Clamp(count, 0, _patches.Count);
    }

    /// <summary>
    /// Lay out the patches for this seed. Safe to call every frame: it only does work when
    /// the seed changes. Seed 0 restores the scene layout.
    /// </summary>
    public void ApplyLayout(int seed)
    {
        Initialise();

        if (_hasApplied && seed == _appliedSeed)
            return;

        _hasApplied = true;
        _appliedSeed = seed;

        if (seed == 0)
        {
            RestoreSceneLayout();
            return;
        }

        var rng = new System.Random(seed);
        int count = PickCount(rng);
        ActivePatchCount = count;

        // Shuffle which patches are used, so different-looking patches get a turn.
        var order = new List<int>();
        for (int i = 0; i < _patches.Count; i++)
            order.Add(i);
        for (int i = order.Count - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }

        var usedZ = new List<float>();
        for (int n = 0; n < order.Count; n++)
        {
            int i = order[n];
            SlipPatch patch = _patches[i];
            bool active = n < count;

            if (active)
            {
                Vector3 position = _originalPositions[i];
                position.z = PickZ(rng, usedZ);
                if (_maxSideOffset > 0f)
                    position.x += Mathf.Lerp(-_maxSideOffset, _maxSideOffset, (float)rng.NextDouble());

                usedZ.Add(position.z);
                patch.transform.position = position;
            }

            patch.gameObject.SetActive(active);
        }

        Debug.Log($"[SlipPatchLayout] {count} slip patch(es) placed at Z: {string.Join(", ", usedZ.ConvertAll(z => z.ToString("0")))}");
    }

    private void RestoreSceneLayout()
    {
        int active = 0;
        for (int i = 0; i < _patches.Count; i++)
        {
            _patches[i].transform.position = _originalPositions[i];
            _patches[i].gameObject.SetActive(_originalActive[i]);
            if (_originalActive[i])
                active++;
        }
        ActivePatchCount = active;
    }

    // A random Z between Min Z and Max Z, at least Min Gap from the patches already placed.
    private float PickZ(System.Random rng, List<float> usedZ)
    {
        float z = _minZ;
        for (int attempt = 0; attempt < 30; attempt++)
        {
            z = Mathf.Lerp(_minZ, _maxZ, (float)rng.NextDouble());

            bool clear = true;
            foreach (float used in usedZ)
            {
                if (Mathf.Abs(used - z) < _minGap)
                {
                    clear = false;
                    break;
                }
            }

            if (clear)
                return z;
        }

        return z;   // stretch too short for the gap: accept some overlap rather than fail
    }

    // Shows the stretch of track patches can appear on when this object is selected.
    private void OnDrawGizmosSelected()
    {
        float x = transform.position.x;
        float y = transform.position.y;
        const float halfWidth = 20f;

        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.9f);
        Gizmos.DrawLine(new Vector3(x - halfWidth, y, _minZ), new Vector3(x + halfWidth, y, _minZ));
        Gizmos.DrawLine(new Vector3(x - halfWidth, y, _maxZ), new Vector3(x + halfWidth, y, _maxZ));
        Gizmos.DrawLine(new Vector3(x, y, _minZ), new Vector3(x, y, _maxZ));
    }
}
