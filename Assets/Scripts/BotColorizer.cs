using UnityEngine;

/// <summary>
/// Randomizes bot body colors on spawn to make them visually distinct.
/// Finds SkinnedMeshRenderer components and applies random colors to body materials.
/// </summary>
public class BotColorizer : MonoBehaviour
{
    [Header("Color Palette")]
    [SerializeField] private Color[] _bodyColors = new Color[]
    {
        new Color(0.2f, 0.6f, 1.0f),    // Blue
        new Color(1.0f, 0.3f, 0.3f),    // Red
        new Color(0.3f, 1.0f, 0.4f),    // Green
        new Color(1.0f, 0.8f, 0.2f),    // Yellow
        new Color(1.0f, 0.5f, 0.0f),    // Orange
        new Color(0.6f, 0.2f, 1.0f),    // Purple
        new Color(1.0f, 0.4f, 0.7f),    // Pink
        new Color(0.2f, 0.9f, 0.9f),    // Cyan
        new Color(0.9f, 0.5f, 0.2f),    // Brown
        new Color(0.5f, 0.5f, 0.5f)     // Gray
    };

    [Header("Material Names to Colorize")]
    [Tooltip("Names of materials that should be colorized (e.g., 'NewPlayerMain', 'Body')")]
    [SerializeField] private string[] _materialNamePatterns = new string[]
    {
        "NewPlayerMain",
        "PlayerMain",
        "Body",
        "Shirt",
        "Torso"
    };

    private void Start()
    {
        // Only colorize if this is a bot
        AIRacer aiRacer = GetComponent<AIRacer>();
        if (aiRacer != null && aiRacer.IsBotControlled)
        {
            ApplyRandomColor();
        }
    }

    public void ApplyRandomColor()
    {
        // Pick a random color from the palette
        Color randomColor = _bodyColors[Random.Range(0, _bodyColors.Length)];
        
        // Find all SkinnedMeshRenderers in children (where character model usually is)
        SkinnedMeshRenderer[] renderers = GetComponentsInChildren<SkinnedMeshRenderer>();
        
        bool colorApplied = false;
        
        foreach (var renderer in renderers)
        {
            // Check each material on this renderer
            Material[] materials = renderer.materials;
            
            for (int i = 0; i < materials.Length; i++)
            {
                // Check if this material name matches our patterns
                foreach (string pattern in _materialNamePatterns)
                {
                    if (materials[i].name.Contains(pattern))
                    {
                        // Create a unique material instance for this bot (don't share materials)
                        Material uniqueMaterial = new Material(materials[i]);
                        uniqueMaterial.color = randomColor;
                        materials[i] = uniqueMaterial;
                        colorApplied = true;
                        
                        Debug.Log($"[BotColorizer] Applied color {randomColor} to material {materials[i].name}");
                    }
                }
            }
            
            // Apply the modified materials array back to the renderer
            if (colorApplied)
            {
                renderer.materials = materials;
            }
        }
        
        // Also check regular MeshRenderers (in case character uses those)
        if (!colorApplied)
        {
            MeshRenderer[] meshRenderers = GetComponentsInChildren<MeshRenderer>();
            
            foreach (var renderer in meshRenderers)
            {
                Material[] materials = renderer.materials;
                
                for (int i = 0; i < materials.Length; i++)
                {
                    foreach (string pattern in _materialNamePatterns)
                    {
                        if (materials[i].name.Contains(pattern))
                        {
                            Material uniqueMaterial = new Material(materials[i]);
                            uniqueMaterial.color = randomColor;
                            materials[i] = uniqueMaterial;
                            colorApplied = true;
                            
                            Debug.Log($"[BotColorizer] Applied color {randomColor} to material {materials[i].name}");
                        }
                    }
                }
                
                if (colorApplied)
                {
                    renderer.materials = materials;
                }
            }
        }
        
        if (!colorApplied)
        {
            Debug.LogWarning($"[BotColorizer] No matching materials found on {gameObject.name}. Check material name patterns.");
        }
    }

    /// <summary>
    /// Call this to apply a specific color instead of random
    /// </summary>
    public void ApplyColor(Color color)
    {
        SkinnedMeshRenderer[] renderers = GetComponentsInChildren<SkinnedMeshRenderer>();
        
        foreach (var renderer in renderers)
        {
            Material[] materials = renderer.materials;
            
            for (int i = 0; i < materials.Length; i++)
            {
                foreach (string pattern in _materialNamePatterns)
                {
                    if (materials[i].name.Contains(pattern))
                    {
                        Material uniqueMaterial = new Material(materials[i]);
                        uniqueMaterial.color = color;
                        materials[i] = uniqueMaterial;
                    }
                }
            }
            
            renderer.materials = materials;
        }
    }
}
