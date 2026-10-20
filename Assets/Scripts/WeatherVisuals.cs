using System.Collections.Generic;
using UnityEngine;

// Changes the look of the race to match how many puddles (slip patches) are on the track:
// skybox, sunlight and rain particle objects. RaceManager calls SetLevel() every frame with
// the current puddle count (or -1 in the lobby), so every player sees the same weather.
//
// Level = number of puddles. With the default 4 levels:
//   0 = Clear      (scene skybox, no rain)
//   1 = Overcast   (overcast skybox, no rain)
//   2 = Light Rain (overcast skybox, light rain)
//   3 = Storm      (stormy skybox, heavy rain)
// More puddles than levels uses the last level.
//
// Setup: add to an empty GameObject (e.g. "Weather"), fill in each level's Skybox and Enable
// Objects (your rain particle systems), and optionally drag your Directional Light into Sun.
// Put rain particle systems under one parent and drag it into Rain Anchor so the rain
// follows the camera instead of having to cover the whole track.
public class WeatherVisuals : MonoBehaviour
{
    [System.Serializable]
    public class WeatherLevel
    {
        [Tooltip("Just a label for the Inspector.")]
        public string Name;

        [Tooltip("Skybox material for this level. Leave empty to keep the scene's own skybox.")]
        public Material Skybox;

        [Tooltip("Objects switched ON at this level (rain particle systems, lightning, wet ground, etc). Anything listed in another level is switched off.")]
        public GameObject[] EnableObjects;

        [Tooltip("Sun brightness compared with the scene's normal sun. 1 = normal, 0.5 = half as bright.")]
        [Range(0f, 2f)]
        public float SunIntensity = 1f;
    }

    [SerializeField] private WeatherLevel[] _levels =
    {
        new WeatherLevel { Name = "0 puddles - Clear", SunIntensity = 1f },
        new WeatherLevel { Name = "1 puddle - Overcast", SunIntensity = 0.7f },
        new WeatherLevel { Name = "2 puddles - Light Rain", SunIntensity = 0.55f },
        new WeatherLevel { Name = "3 puddles - Storm", SunIntensity = 0.35f },
    };

    [Header("Sun")]
    [Tooltip("Your Directional Light. Leave empty to use the scene's Sun Source (Lighting window).")]
    [SerializeField] private Light _sun;

    [Header("Rain follows the camera")]
    [Tooltip("Parent of your rain particle systems. Kept above and ahead of the camera so a small rain area always surrounds the player.")]
    [SerializeField] private Transform _rainAnchor;
    [Tooltip("Where the rain sits relative to the camera: Y = height above, Z = distance ahead.")]
    [SerializeField] private Vector3 _rainAnchorOffset = new Vector3(0f, 12f, 8f);

    private Material _sceneSkybox;
    private float _sceneSunIntensity = 1f;
    private int _currentLevel = int.MinValue;

    private void Awake()
    {
        _sceneSkybox = RenderSettings.skybox;

        if (_sun == null)
            _sun = RenderSettings.sun;
        if (_sun != null)
            _sceneSunIntensity = _sun.intensity;

        SetLevel(-1);   // start in the scene's own look, with all rain off
    }

    /// <summary>
    /// Switch to the look for this many puddles. -1 = the scene's own look (lobby).
    /// Safe to call every frame: only does work when the level changes.
    /// </summary>
    public void SetLevel(int level)
    {
        if (level == _currentLevel)
            return;
        _currentLevel = level;

        WeatherLevel chosen = null;
        if (level >= 0 && _levels != null && _levels.Length > 0)
            chosen = _levels[Mathf.Min(level, _levels.Length - 1)];

        // Objects: switch on this level's, switch off every other level's.
        var enable = new HashSet<GameObject>();
        if (chosen != null && chosen.EnableObjects != null)
        {
            foreach (GameObject go in chosen.EnableObjects)
                if (go != null)
                    enable.Add(go);
        }

        if (_levels != null)
        {
            foreach (WeatherLevel l in _levels)
            {
                if (l.EnableObjects == null)
                    continue;
                foreach (GameObject go in l.EnableObjects)
                {
                    if (go != null && go.activeSelf != enable.Contains(go))
                        go.SetActive(enable.Contains(go));
                }
            }
        }

        // Skybox (falls back to the scene's own).
        Material skybox = chosen != null && chosen.Skybox != null ? chosen.Skybox : _sceneSkybox;
        if (RenderSettings.skybox != skybox)
        {
            RenderSettings.skybox = skybox;
            DynamicGI.UpdateEnvironment();   // refresh ambient light/reflections from the new sky
        }

        // Sun brightness.
        if (_sun != null)
            _sun.intensity = _sceneSunIntensity * (chosen != null ? chosen.SunIntensity : 1f);

        Debug.Log($"[WeatherVisuals] Weather look: {(chosen != null ? chosen.Name : "scene default (lobby)")}");
    }

    private void LateUpdate()
    {
        if (_rainAnchor == null)
            return;

        Camera cam = Camera.main;
        if (cam == null)
            return;

        // Flatten the camera's forward so the rain stays level whatever the camera pitch.
        Vector3 forward = cam.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;
        forward.Normalize();

        _rainAnchor.position = cam.transform.position
                             + Vector3.up * _rainAnchorOffset.y
                             + forward * _rainAnchorOffset.z;
    }
}
