using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

// Username label shown above a character.
// Uses Player.DisplayName when a Player component is present, otherwise the name set with SetName (e.g. bots).
public class PlayerNameTag : MonoBehaviour
{
    [SerializeField] private float _heightAboveRoot = 1.15f;
    [SerializeField] private float _fontSize = 3f;
    [SerializeField] private Color _localPlayerColour = new Color(0.08f, 0.2f, 0.55f);
    [SerializeField] private Color _otherColour = new Color(0.1f, 0.1f, 0.1f);
    [SerializeField] private Color _outlineColour = Color.white;
    [Tooltip("Hide the label beyond this distance from the camera. 0 = always visible.")]
    [SerializeField] private float _maxVisibleDistance = 0f;

    private Player _player;
    private string _manualName;
    private TextMeshPro _text;
    private Transform _labelTransform;

    private void Awake()
    {
        _player = GetComponent<Player>();
        CreateLabel();
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += FaceCamera;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= FaceCamera;
    }

    public void SetName(string displayName)
    {
        _manualName = displayName;
    }

    private void CreateLabel()
    {
        var go = new GameObject("NameTag");

        // TextMeshPro swaps the Transform for a RectTransform, so the transform is read after adding it.
        _text = go.AddComponent<TextMeshPro>();
        _labelTransform = go.transform;
        _labelTransform.SetParent(transform, false);
        _labelTransform.localPosition = Vector3.up * _heightAboveRoot;

        _text.fontSize = _fontSize;
        _text.fontStyle = FontStyles.Bold;
        _text.alignment = TextAlignmentOptions.Center;
        _text.textWrappingMode = TextWrappingModes.NoWrap;
        _text.richText = false;
        _text.outlineWidth = 0.25f;
        _text.outlineColor = _outlineColour;
        _text.text = "";
    }

    private void LateUpdate()
    {
        if (_text == null) return;

        _text.text = CurrentName();
        _text.color = IsLocalPlayer() ? _localPlayerColour : _otherColour;
    }

    // Runs just before each camera renders, so the label faces whichever camera is drawing it.
    private void FaceCamera(ScriptableRenderContext context, Camera cam)
    {
        if (_labelTransform == null || cam == null) return;

        _labelTransform.rotation = cam.transform.rotation;

        if (_maxVisibleDistance > 0f)
        {
            float dist = Vector3.Distance(cam.transform.position, _labelTransform.position);
            _text.enabled = dist <= _maxVisibleDistance;
        }
    }

    private string CurrentName()
    {
        if (_player != null && _player.Object != null && _player.Object.IsValid)
            return _player.DisplayName.ToString();
        return _manualName ?? "";
    }

    private bool IsLocalPlayer() =>
        _player != null && _player.Object != null && _player.Object.IsValid && _player.Object.HasInputAuthority;
}
