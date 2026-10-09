using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Editor tool: turns a skinned shoe PAIR (e.g. from npc_casual_set_00, where both shoes are one
// mesh skinned to a full humanoid skeleton) into two rigid prefabs -- left and right -- that
// CosmeticVisuals can attach straight to Y Bot's foot bones.
//
//   Tools > Dash > Rigid Shoe Maker
//
// What it does:
//  1. Spawns the shoe prefab and the character model temporarily, in their default (rest) poses.
//  2. Bakes the chosen LOD's skinned mesh into a plain mesh.
//  3. Splits the triangles into left and right by which side of the body they're on.
//  4. Re-expresses each shoe relative to its foot bone, using the CHARACTER's foot orientation
//     and (optionally) scaled to the character's foot length -- so on Y Bot the CosmeticItem's
//     Fit can start at position 0, rotation 0, scale 1 and only needs small tweaks.
//  5. Saves the meshes plus <name>_L and <name>_R prefabs (Mesh Filter + Mesh Renderer only).
//
// Editor-only (lives in an Editor folder), so it's never included in builds. It doesn't
// modify the source pack. Re-running with the same name overwrites the previous output
// in place, so CosmeticItems that reference the prefabs keep working.
public class RigidShoeMaker : EditorWindow
{
    private const string DefaultCharacterPath = "Assets/CharacterAnimations/Y Bot.fbx";

    private GameObject _shoePrefab;
    private GameObject _characterModel;
    private int _lod;
    private bool _matchFootLength = true;
    private Material _materialOverride;
    private string _outputFolder = "Assets/Cosmetics/Feet/Generated";
    private string _outputName = "";

    [MenuItem("Tools/Dash/Rigid Shoe Maker")]
    private static void OpenWindow()
    {
        var window = GetWindow<RigidShoeMaker>("Rigid Shoe Maker");
        window.minSize = new Vector2(400, 300);
    }

    private void OnEnable()
    {
        if (_characterModel == null)
            _characterModel = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultCharacterPath);
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Converts a skinned shoe pair into rigid left/right prefabs for the character's foot bones.\n" +
            "Use the outputs as a CosmeticItem's Prefab (_L) and Secondary Prefab (_R), starting with " +
            "Fit position 0, rotation 0, scale 1.", MessageType.Info);

        _shoePrefab = (GameObject)EditorGUILayout.ObjectField(
            new GUIContent("Shoe Prefab", "A prefab or model from the shoe pack, e.g. npc_csl_shoe_01_00_01."),
            _shoePrefab, typeof(GameObject), false);

        _characterModel = (GameObject)EditorGUILayout.ObjectField(
            new GUIContent("Character Model", "The model the shoes will be worn on (Y Bot)."),
            _characterModel, typeof(GameObject), false);

        _lod = EditorGUILayout.IntSlider(
            new GUIContent("LOD", "Which detail level to use. 0 = most detailed, higher = fewer triangles."),
            _lod, 0, 4);

        _matchFootLength = EditorGUILayout.Toggle(
            new GUIContent("Match Foot Length", "Scale the shoes so they suit the character's foot length."),
            _matchFootLength);

        _materialOverride = (Material)EditorGUILayout.ObjectField(
            new GUIContent("Material Override", "Optional. E.g. the URP material from the pack's MaterialsUPR folder. " +
                                                 "Blank = keep the shoe prefab's own material(s)."),
            _materialOverride, typeof(Material), false);

        _outputFolder = EditorGUILayout.TextField(new GUIContent("Output Folder"), _outputFolder);
        _outputName = EditorGUILayout.TextField(
            new GUIContent("Output Name", "Blank = use the shoe prefab's name. E.g. Shoe01_Red."), _outputName);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(_shoePrefab == null || _characterModel == null))
        {
            if (GUILayout.Button("Make Rigid Shoes", GUILayout.Height(32)))
                Make();
        }
    }

    // ------------------------------------------------------------------------------------------

    private void Make()
    {
        string folder = (_outputFolder ?? "").Replace('\\', '/').TrimEnd('/');
        if (!folder.StartsWith("Assets"))
        {
            EditorUtility.DisplayDialog("Rigid Shoe Maker", "Output Folder must be inside Assets/.", "OK");
            return;
        }

        string baseName = string.IsNullOrWhiteSpace(_outputName) ? _shoePrefab.name : _outputName.Trim();

        GameObject shoe = null;
        GameObject character = null;
        Mesh baked = null;

        try
        {
            // Temporary copies in their default (rest) poses. DontSave keeps them out of the scene file.
            shoe = Instantiate(_shoePrefab);
            character = Instantiate(_characterModel);
            shoe.hideFlags = HideFlags.HideAndDontSave;
            character.hideFlags = HideFlags.HideAndDontSave;
            shoe.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            character.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            SkinnedMeshRenderer smr = FindShoeRenderer(shoe, _lod);
            if (smr == null)
            {
                EditorUtility.DisplayDialog("Rigid Shoe Maker",
                    "No Skinned Mesh Renderer found in the shoe prefab. If the shoe is already rigid, " +
                    "it doesn't need this tool.", "OK");
                return;
            }

            Transform srcLeft = FindBone(shoe.transform, "LeftFoot");
            Transform srcRight = FindBone(shoe.transform, "RightFoot");
            Transform dstLeft = FindBone(character.transform, "LeftFoot");
            Transform dstRight = FindBone(character.transform, "RightFoot");

            if (srcLeft == null || srcRight == null || dstLeft == null || dstRight == null)
            {
                EditorUtility.DisplayDialog("Rigid Shoe Maker",
                    "Couldn't find LeftFoot/RightFoot bones on both the shoe prefab and the character model.",
                    "OK");
                return;
            }

            // Bake the skinned mesh as it currently looks (rest pose). With useScale = true the
            // result is in the renderer's local space including its scale, so only position and
            // rotation are needed to get to world space.
            baked = new Mesh();
            smr.BakeMesh(baked, true);
            Matrix4x4 bakedToWorld = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
            Quaternion bakedRotation = smr.transform.rotation;

            Vector3[] worldVerts = baked.vertices;
            for (int i = 0; i < worldVerts.Length; i++)
                worldVerts[i] = bakedToWorld.MultiplyPoint3x4(worldVerts[i]);

            float midX = (srcLeft.position.x + srcRight.position.x) * 0.5f;
            bool leftIsBelowMid = srcLeft.position.x < midX;

            float leftScale = _matchFootLength ? FootScale(shoe.transform, character.transform, "Left") : 1f;
            float rightScale = _matchFootLength ? FootScale(shoe.transform, character.transform, "Right") : 1f;

            Mesh leftMesh = BuildSide(baked, worldVerts, bakedRotation, true, midX, leftIsBelowMid,
                                      srcLeft.position, dstLeft.rotation, leftScale);
            Mesh rightMesh = BuildSide(baked, worldVerts, bakedRotation, false, midX, leftIsBelowMid,
                                       srcRight.position, dstRight.rotation, rightScale);

            Material[] materials = BuildMaterials(smr, baked.subMeshCount);

            EnsureFolder(folder);
            var created = new List<string>();

            if (leftMesh != null)
            {
                Mesh saved = SaveMesh(leftMesh, $"{folder}/{baseName}_L_Mesh.asset", baseName + "_L");
                created.Add(SavePrefab(saved, materials, $"{folder}/{baseName}_L.prefab", baseName + "_L"));
            }

            if (rightMesh != null)
            {
                Mesh saved = SaveMesh(rightMesh, $"{folder}/{baseName}_R_Mesh.asset", baseName + "_R");
                created.Add(SavePrefab(saved, materials, $"{folder}/{baseName}_R.prefab", baseName + "_R"));
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (created.Count == 0)
            {
                EditorUtility.DisplayDialog("Rigid Shoe Maker", "No triangles found for either foot.", "OK");
                return;
            }

            var firstPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(created[0]);
            Selection.activeObject = firstPrefab;
            EditorGUIUtility.PingObject(firstPrefab);

            string note = created.Count < 2
                ? "\n\nOnly one shoe was found -- the source may contain a single shoe. You can use the same " +
                  "prefab for both Prefab and Secondary Prefab."
                : "";

            EditorUtility.DisplayDialog("Rigid Shoe Maker",
                $"Used mesh '{smr.name}'.\nFoot scale: left {leftScale:0.###}, right {rightScale:0.###}.\n\n" +
                "Created:\n" + string.Join("\n", created) + note, "OK");
        }
        finally
        {
            if (shoe != null) DestroyImmediate(shoe);
            if (character != null) DestroyImmediate(character);
            if (baked != null) DestroyImmediate(baked);
        }
    }

    // Picks the shoe mesh for the requested LOD. Skips "b_" meshes (not the shoe itself in this pack).
    private static SkinnedMeshRenderer FindShoeRenderer(GameObject shoe, int lod)
    {
        SkinnedMeshRenderer[] all = shoe.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        string suffix = "_lod" + lod;

        foreach (var smr in all)
            if (!smr.name.StartsWith("b_") && smr.name.EndsWith(suffix))
                return smr;

        foreach (var smr in all)
            if (!smr.name.StartsWith("b_"))
            {
                Debug.LogWarning($"[RigidShoeMaker] No '{suffix}' mesh found -- using '{smr.name}' instead.");
                return smr;
            }

        return all.Length > 0 ? all[0] : null;
    }

    // Matches "LeftFoot" as well as prefixed names like "mixamorig:LeftFoot".
    private static Transform FindBone(Transform root, string boneName)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == boneName || t.name.EndsWith(":" + boneName))
                return t;
        return null;
    }

    // Ratio of the character's foot length (foot bone -> toe bone) to the source rig's.
    private static float FootScale(Transform shoeRoot, Transform characterRoot, string side)
    {
        Transform srcFoot = FindBone(shoeRoot, side + "Foot");
        Transform srcToe = FindBone(shoeRoot, side + "ToeBase");
        Transform dstFoot = FindBone(characterRoot, side + "Foot");
        Transform dstToe = FindBone(characterRoot, side + "ToeBase");

        if (srcFoot == null || srcToe == null || dstFoot == null || dstToe == null)
            return 1f;

        float srcLength = Vector3.Distance(srcFoot.position, srcToe.position);
        float dstLength = Vector3.Distance(dstFoot.position, dstToe.position);
        return srcLength > 0.0001f ? dstLength / srcLength : 1f;
    }

    // Copies one side's triangles into a new mesh, with vertices expressed relative to the foot:
    // origin at the source foot bone, axes in the character's foot-bone space, scaled to fit.
    private static Mesh BuildSide(Mesh baked, Vector3[] worldVerts, Quaternion bakedRotation, bool wantLeft,
                                  float midX, bool leftIsBelowMid, Vector3 sourceFootPosition,
                                  Quaternion characterFootRotation, float scale)
    {
        Quaternion toFoot = Quaternion.Inverse(characterFootRotation);
        Quaternion normalRotation = toFoot * bakedRotation;

        Vector3[] normals = baked.normals;
        Vector4[] tangents = baked.tangents;
        Vector2[] uv0 = baked.uv;
        Vector2[] uv1 = baked.uv2;
        Color[] colors = baked.colors;

        bool hasNormals = normals.Length == worldVerts.Length;
        bool hasTangents = tangents.Length == worldVerts.Length;
        bool hasUv0 = uv0.Length == worldVerts.Length;
        bool hasUv1 = uv1.Length == worldVerts.Length;
        bool hasColors = colors.Length == worldVerts.Length;

        var remap = new Dictionary<int, int>();
        var verts = new List<Vector3>();
        var outNormals = new List<Vector3>();
        var outTangents = new List<Vector4>();
        var outUv0 = new List<Vector2>();
        var outUv1 = new List<Vector2>();
        var outColors = new List<Color>();
        var subMeshTriangles = new List<List<int>>();
        int triangleCount = 0;

        int Remap(int index)
        {
            if (remap.TryGetValue(index, out int newIndex))
                return newIndex;

            newIndex = verts.Count;
            remap[index] = newIndex;

            verts.Add(toFoot * (worldVerts[index] - sourceFootPosition) * scale);
            if (hasNormals) outNormals.Add(normalRotation * normals[index]);
            if (hasTangents)
            {
                Vector4 t = tangents[index];
                Vector3 d = normalRotation * new Vector3(t.x, t.y, t.z);
                outTangents.Add(new Vector4(d.x, d.y, d.z, t.w));
            }
            if (hasUv0) outUv0.Add(uv0[index]);
            if (hasUv1) outUv1.Add(uv1[index]);
            if (hasColors) outColors.Add(colors[index]);
            return newIndex;
        }

        for (int s = 0; s < baked.subMeshCount; s++)
        {
            int[] tris = baked.GetTriangles(s);
            var list = new List<int>();

            for (int i = 0; i < tris.Length; i += 3)
            {
                int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                float centroidX = (worldVerts[a].x + worldVerts[b].x + worldVerts[c].x) / 3f;
                bool isLeft = (centroidX < midX) == leftIsBelowMid;
                if (isLeft != wantLeft)
                    continue;

                list.Add(Remap(a));
                list.Add(Remap(b));
                list.Add(Remap(c));
                triangleCount++;
            }

            subMeshTriangles.Add(list);
        }

        if (triangleCount == 0)
            return null;

        var mesh = new Mesh
        {
            indexFormat = verts.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
        };
        mesh.SetVertices(verts);
        if (hasNormals) mesh.SetNormals(outNormals);
        if (hasTangents) mesh.SetTangents(outTangents);
        if (hasUv0) mesh.SetUVs(0, outUv0);
        if (hasUv1) mesh.SetUVs(1, outUv1);
        if (hasColors) mesh.SetColors(outColors);

        mesh.subMeshCount = subMeshTriangles.Count;
        for (int s = 0; s < subMeshTriangles.Count; s++)
            mesh.SetTriangles(subMeshTriangles[s], s);

        if (!hasNormals) mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private Material[] BuildMaterials(SkinnedMeshRenderer smr, int subMeshCount)
    {
        if (_materialOverride == null)
            return smr.sharedMaterials;

        var materials = new Material[Mathf.Max(1, subMeshCount)];
        for (int i = 0; i < materials.Length; i++)
            materials[i] = _materialOverride;
        return materials;
    }

    // Overwrites an existing mesh asset in place (keeps its GUID) so prefab references survive re-runs.
    private static Mesh SaveMesh(Mesh mesh, string path, string name)
    {
        mesh.name = name;
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(mesh, existing);
            existing.name = name;
            EditorUtility.SetDirty(existing);
            DestroyImmediate(mesh);
            return existing;
        }

        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    // Saving over an existing prefab path keeps its GUID, so CosmeticItems keep their reference.
    private static string SavePrefab(Mesh mesh, Material[] materials, string path, string name)
    {
        var go = new GameObject(name);
        try
        {
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = materials;
            PrefabUtility.SaveAsPrefabAsset(go, path);
        }
        finally
        {
            DestroyImmediate(go);
        }
        return path;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        if (!string.IsNullOrEmpty(parent))
            EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
