using UnityEngine;

// Applies cosmetics to a Y-Bot model. Pure visuals, no networking. Used by both
// PlayerCustomisation (networked players) and the shop's preview mannequin.
public class CosmeticVisuals : MonoBehaviour
{
    [Header("Body colour")]
    [SerializeField] private Renderer _bodyRenderer;
    [SerializeField] private string _colourProperty = "_BaseColor";   // "_Color" on built-in shaders

    [Header("Bones")]
    [SerializeField] private Transform _headBone;       // mixamorig:Head
    [SerializeField] private Transform _leftFootBone;   // mixamorig:LeftFoot
    [SerializeField] private Transform _rightFootBone;  // mixamorig:RightFoot

    private MaterialPropertyBlock _propBlock;
    private GameObject _head, _leftShoe, _rightShoe;

    // Set when a Rainbow colour item is applied; Update() then cycles the body colour.
    // Lives here (not in PlayerCustomisation) so the shop preview mannequin animates too.
    private bool _rainbow;
    private float _rainbowSpeed;

    public void ApplyHead(CosmeticItem item)
    {
        Clear(ref _head);
        if (item != null)
            _head = Attach(item.Prefab, _headBone, item.Fit);
    }

    public void ApplyFeet(CosmeticItem item)
    {
        Clear(ref _leftShoe);
        Clear(ref _rightShoe);
        if (item == null) return;

        _leftShoe = Attach(item.Prefab, _leftFootBone, item.Fit);
        _rightShoe = Attach(item.SecondaryPrefab, _rightFootBone, item.SecondaryFit);
    }

    public void ApplyColour(CosmeticItem item)
    {
        _rainbow = item != null && item.Rainbow;
        _rainbowSpeed = item != null ? item.RainbowSpeed : 0f;

        if (!_rainbow)
            SetColour(item != null ? item.BodyColour : Color.white);
    }

    private void Update()
    {
        if (_rainbow)
            SetColour(Color.HSVToRGB(Mathf.Repeat(Time.time * _rainbowSpeed, 1f), 1f, 1f));
    }

    public void SetColour(Color colour)
    {
        if (_bodyRenderer == null) return;
        _propBlock ??= new MaterialPropertyBlock();

        _bodyRenderer.GetPropertyBlock(_propBlock);
        _propBlock.SetColor(_colourProperty, colour);
        _bodyRenderer.SetPropertyBlock(_propBlock);
    }

    private static GameObject Attach(GameObject prefab, Transform bone, AttachmentFit fit)
    {
        if (prefab == null || bone == null) return null;

        GameObject instance = Instantiate(prefab, bone);
        instance.transform.localPosition = fit.LocalPosition;
        instance.transform.localRotation = Quaternion.Euler(fit.LocalEuler);
        instance.transform.localScale = Vector3.one * fit.Scale;

        // Match the model's layer, so the shop preview camera (which only renders the
        // ShopPreview layer) can see accessories attached to the mannequin.
        SetLayerRecursively(instance, bone.gameObject.layer);
        return instance;
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    private static void Clear(ref GameObject go)
    {
        if (go != null) Destroy(go);
        go = null;
    }
}
