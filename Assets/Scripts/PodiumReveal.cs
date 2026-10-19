using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Podium reveal shown on every client when the race reaches the Podium phase. The camera visits
// 3rd, 2nd and 1st place in turn and shows each racer's name, then pulls back to a wide shot of
// the podium. The normal follow camera returns when the podium phase ends.
// Created automatically at startup, so it needs no scene setup.
public class PodiumReveal : MonoBehaviour
{
    private const float SearchInterval = 0.5f;
    private const float SettleDelay = 0.4f;     // lets the podium teleport reach every client
    private const float MoveTime = 0.7f;
    private const float HoldTime = 1.1f;
    private const float FocusHeight = 0.5f;     // face height above a racer's root
    private const float CloseDistance = 3.2f;
    private const float CloseHeight = 0.3f;
    private const float WideDistance = 9f;
    private const float WideHeight = 3f;

    private static readonly string[] PlaceLabels = { "1st place", "2nd place", "3rd place" };
    private static readonly Color PlaceColour = new Color(1f, 0.8f, 0.3f);

    private RaceManager _raceManager;
    private float _nextSearch;
    private bool _phaseKnown;
    private RaceManager.RacePhase _lastPhase;

    private Coroutine _sequence;
    private ThirdPersonCamera _followCamera;
    private GameObject _overlay;
    private TextMeshProUGUI _placeText;
    private TextMeshProUGUI _nameText;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        var go = new GameObject("PodiumReveal");
        DontDestroyOnLoad(go);
        go.AddComponent<PodiumReveal>();
    }

    private void Awake()
    {
        BuildOverlay();
    }

    private void Update()
    {
        if (_raceManager == null)
        {
            if (_sequence != null || _overlay.activeSelf)
                StopSequence();

            if (Time.unscaledTime < _nextSearch)
                return;
            _nextSearch = Time.unscaledTime + SearchInterval;

            _raceManager = FindFirstObjectByType<RaceManager>();
            _phaseKnown = false;
            return;
        }

        if (_raceManager.Object == null || !_raceManager.Object.IsValid || _raceManager.Runner == null)
            return;

        RaceManager.RacePhase phase = _raceManager.Phase;

        // A player who joins part way through the podium does not get the reveal.
        if (!_phaseKnown)
        {
            _lastPhase = phase;
            _phaseKnown = true;
            return;
        }

        if (phase == _lastPhase)
            return;
        _lastPhase = phase;

        if (phase == RaceManager.RacePhase.Podium)
            StartSequence();
        else
            StopSequence();
    }

    private void StartSequence()
    {
        StopSequence();
        _sequence = StartCoroutine(PlaySequence());
    }

    private void StopSequence()
    {
        if (_sequence != null)
        {
            StopCoroutine(_sequence);
            _sequence = null;
        }

        _overlay.SetActive(false);

        if (_followCamera != null)
            _followCamera.enabled = true;
        _followCamera = null;
    }

    private IEnumerator PlaySequence()
    {
        yield return new WaitForSeconds(SettleDelay);

        Camera cam = Camera.main;
        if (cam == null)
            yield break;

        _followCamera = cam.GetComponent<ThirdPersonCamera>();
        if (_followCamera != null)
            _followCamera.enabled = false;

        List<Player> ranked = RaceLogic.RankParticipants(_raceManager.Runner);
        int podiumCount = Mathf.Min(PlaceLabels.Length, ranked.Count);

        // 3rd, then 2nd, then 1st.
        for (int i = podiumCount - 1; i >= 0; i--)
        {
            Player racer = ranked[i];
            if (racer == null)
                continue;

            Vector3 focus = racer.transform.position + Vector3.up * FocusHeight;
            Vector3 cameraPosition = focus + racer.transform.forward * CloseDistance + Vector3.up * CloseHeight;

            ShowText(null, null);
            yield return MoveCamera(cam.transform, cameraPosition, focus);

            ShowText(PlaceLabels[i], racer.DisplayName.ToString());
            yield return new WaitForSeconds(HoldTime);
        }

        if (podiumCount == 0 || ranked[0] == null)
        {
            ShowText(null, null);
            yield break;
        }

        Vector3 centre = Vector3.zero;
        int counted = 0;
        for (int i = 0; i < podiumCount; i++)
        {
            if (ranked[i] == null)
                continue;
            centre += ranked[i].transform.position;
            counted++;
        }
        centre = centre / counted + Vector3.up * FocusHeight;

        Vector3 widePosition = centre + ranked[0].transform.forward * WideDistance + Vector3.up * WideHeight;

        ShowText(null, null);
        yield return MoveCamera(cam.transform, widePosition, centre);
        ShowText("Winner", ranked[0].DisplayName.ToString());

        // The camera holds the wide shot until the podium phase ends.
        _sequence = null;
    }

    private IEnumerator MoveCamera(Transform cam, Vector3 targetPosition, Vector3 lookAt)
    {
        Vector3 startPosition = cam.position;
        Quaternion startRotation = cam.rotation;
        Quaternion endRotation = Quaternion.LookRotation(lookAt - targetPosition);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / MoveTime;
            float eased = Mathf.SmoothStep(0f, 1f, t);
            cam.position = Vector3.Lerp(startPosition, targetPosition, eased);
            cam.rotation = Quaternion.Slerp(startRotation, endRotation, eased);
            yield return null;
        }
    }

    // Passing null hides the caption.
    private void ShowText(string place, string racerName)
    {
        bool visible = !string.IsNullOrEmpty(place);
        _overlay.SetActive(visible);
        if (!visible)
            return;

        _placeText.text = place;
        _nameText.text = racerName;
    }

    private void BuildOverlay()
    {
        Canvas canvas = UiFactory.CreateOverlayCanvas(transform, "PodiumRevealCanvas", 300);
        _overlay = canvas.gameObject;

        var strip = UiFactory.CreatePanel(canvas.transform, "Caption", new Color(0f, 0f, 0f, 0.6f));
        strip.raycastTarget = false;
        RectTransform stripRect = strip.rectTransform;
        stripRect.anchorMin = stripRect.anchorMax = stripRect.pivot = new Vector2(0.5f, 0f);
        stripRect.anchoredPosition = new Vector2(0f, 140f);
        stripRect.sizeDelta = new Vector2(900f, 170f);

        _placeText = UiFactory.CreateText(strip.transform, "", 44, TextAlignmentOptions.Center);
        _placeText.color = PlaceColour;
        _placeText.raycastTarget = false;
        UiFactory.PlaceTopLeft(_placeText.rectTransform, 0f, 15f, 900f, 60f);

        _nameText = UiFactory.CreateText(strip.transform, "", 64, TextAlignmentOptions.Center);
        _nameText.fontStyle = FontStyles.Bold;
        _nameText.raycastTarget = false;
        UiFactory.PlaceTopLeft(_nameText.rectTransform, 0f, 75f, 900f, 80f);

        _overlay.SetActive(false);
    }
}
