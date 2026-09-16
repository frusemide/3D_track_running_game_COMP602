using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

// Builds the podium and reveals 3rd, 2nd and 1st place before a wide shot of the results.
public class PodiumSequence : MonoBehaviour
{
    [Header("Runner")]
    public GameObject runnerPrefab;
    public float runnerHeightOffset = 0f;
    public float focusHeight = 1.2f;

    [Header("UI")]
    public TextMeshProUGUI placeText;
    public TextMeshProUGUI nameText;
    public GameObject resultsPanel;
    public TextMeshProUGUI resultsText;

    [Header("Timing")]
    public float moveTime = 1.5f;
    public float holdTime = 2f;

    [Header("Podium Layout")]
    public float blockWidth = 2f;
    public float firstHeight = 1.5f;
    public float secondHeight = 1f;
    public float thirdHeight = 0.6f;

    Camera cam;
    readonly Vector3[] focusPoints = new Vector3[3];

    void Start()
    {
        cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("PodiumSequence: no camera tagged MainCamera in the scene.");
            return;
        }

        if (resultsPanel != null) resultsPanel.SetActive(false);
        SetText("", "");

        List<string> names = RaceResults.GetTopThree();
        BuildPodium(names);

        cam.transform.position = new Vector3(0f, 6f, -14f);
        cam.transform.LookAt(new Vector3(0f, firstHeight, 0f));

        StartCoroutine(PlaySequence(names));
    }

    void BuildPodium(List<string> names)
    {
        float[] xPos = { 0f, -blockWidth, blockWidth };
        float[] heights = { firstHeight, secondHeight, thirdHeight };
        Color[] colours =
        {
            new Color(1f, 0.84f, 0f),
            new Color(0.75f, 0.75f, 0.75f),
            new Color(0.8f, 0.5f, 0.2f)
        };

        for (int i = 0; i < 3; i++)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = "Podium Block " + (i + 1);
            block.transform.position = new Vector3(xPos[i], heights[i] / 2f, 0f);
            block.transform.localScale = new Vector3(blockWidth, heights[i], blockWidth);
            block.GetComponent<Renderer>().material.color = colours[i];

            Vector3 standPoint = new Vector3(xPos[i], heights[i], 0f);
            GameObject runner = SpawnRunner();
            runner.name = "Runner " + (i + 1) + " - " + names[i];
            runner.transform.position = standPoint + Vector3.up * runnerHeightOffset;
            runner.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            focusPoints[i] = standPoint + Vector3.up * focusHeight;
        }
    }

    GameObject SpawnRunner()
    {
        if (runnerPrefab == null)
        {
            GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            GameObject root = new GameObject("CapsuleRunner");
            capsule.transform.SetParent(root.transform);
            capsule.transform.localPosition = Vector3.up;
            return root;
        }

        GameObject holder = new GameObject("RunnerHolder");
        holder.SetActive(false);
        GameObject runner = Instantiate(runnerPrefab, holder.transform);
        RemoveGameplayComponents(runner);
        runner.transform.SetParent(null);
        Destroy(holder);
        return runner;
    }

    // Gameplay and network components are removed so each runner stays still on the podium.
    void RemoveGameplayComponents(GameObject runner)
    {
        for (int pass = 0; pass < 5; pass++)
        {
            MonoBehaviour[] scripts = runner.GetComponentsInChildren<MonoBehaviour>(true);
            if (scripts.Length == 0) break;
            for (int i = scripts.Length - 1; i >= 0; i--)
            {
                if (scripts[i] != null && !IsRequiredByOthers(scripts[i]))
                    DestroyImmediate(scripts[i]);
            }
        }

        foreach (Rigidbody body in runner.GetComponentsInChildren<Rigidbody>(true))
            body.isKinematic = true;
    }

    bool IsRequiredByOthers(Component target)
    {
        foreach (Component other in target.GetComponents<Component>())
        {
            if (other == null || other == target) continue;
            object[] attributes = other.GetType().GetCustomAttributes(typeof(RequireComponent), true);
            foreach (RequireComponent req in attributes)
            {
                if (Requires(req.m_Type0, target) || Requires(req.m_Type1, target) || Requires(req.m_Type2, target))
                    return true;
            }
        }
        return false;
    }

    bool Requires(Type requiredType, Component target)
    {
        return requiredType != null && requiredType.IsAssignableFrom(target.GetType());
    }

    IEnumerator PlaySequence(List<string> names)
    {
        string[] labels = { "Here comes 1st place!", "Here comes 2nd place!", "Here comes 3rd place!" };

        for (int i = 2; i >= 0; i--)
        {
            SetText("", "");
            Vector3 camPos = focusPoints[i] + new Vector3(0f, 0.3f, -4f);
            yield return MoveCamera(camPos, focusPoints[i]);
            SetText(labels[i], names[i]);
            yield return new WaitForSeconds(holdTime);
        }

        SetText("", "");
        yield return MoveCamera(new Vector3(0f, 4f, -10f), new Vector3(0f, firstHeight, 0f));

        if (resultsPanel != null) resultsPanel.SetActive(true);
        if (resultsText != null)
            resultsText.text = "1st   " + names[0] + "\n2nd   " + names[1] + "\n3rd   " + names[2];
    }

    IEnumerator MoveCamera(Vector3 targetPos, Vector3 lookAt)
    {
        Vector3 startPos = cam.transform.position;
        Quaternion startRot = cam.transform.rotation;
        Quaternion endRot = Quaternion.LookRotation(lookAt - targetPos);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / moveTime;
            float s = Mathf.SmoothStep(0f, 1f, t);
            cam.transform.position = Vector3.Lerp(startPos, targetPos, s);
            cam.transform.rotation = Quaternion.Slerp(startRot, endRot, s);
            yield return null;
        }
    }

    void SetText(string place, string playerName)
    {
        if (placeText != null) placeText.text = place;
        if (nameText != null) nameText.text = playerName;
    }
}
