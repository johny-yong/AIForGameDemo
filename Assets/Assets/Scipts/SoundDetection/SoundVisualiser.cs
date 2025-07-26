using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public class SoundVisualizer : MonoBehaviour
{
    public static SoundVisualizer Instance;
    public GameObject circlePrefab;
    public float baseDuration = 1f;
    public float WallConcreteness = 0.5f; //soundproof to hollow (0 - 1)

    public TextMeshProUGUI wallConcretenessValueText;
    private void Start()
    {
        wallConcretenessValueText.text = "Wall Concreteness: " + WallConcreteness.ToString();
    }
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    public void DrawSoundCircle(Vector3 position, float radius, Color color, float durationMultiplier = 1f)
    {
        if (circlePrefab == null)
        {
            Debug.LogError("Circle prefab is not assigned!");
            return;
        }

        // Spawn at world position with no parent and scale 0
        GameObject circle = Instantiate(circlePrefab, new Vector3(position.x, position.y, 0f), Quaternion.identity);
        circle.transform.localScale = Vector3.zero;
        circle.GetComponent<MeshRenderer>().material = new Material(Shader.Find("Sprites/Default"));

        StartCoroutine(GrowAndFade(circle, radius, baseDuration * durationMultiplier, color));
    }

    IEnumerator GrowAndFade(GameObject obj, float targetRadius, float duration, Color baseColor)
    {
        Vector3 fixedPosition = obj.transform.position;
        float elapsed = 0f;

        Color fadedColor = baseColor;
        fadedColor.a = Mathf.Clamp01(baseColor.a * 0.5f); // Ripple opacity

        float scaleFactor = targetRadius * 2f;

        while (elapsed < duration)
        {
            obj.transform.position = fixedPosition;

            float t = elapsed / duration;

            //Ripple Effect
            float rippleGrowth = Mathf.SmoothStep(0f, 1f, t);
            obj.transform.localScale = Vector3.one * (rippleGrowth * scaleFactor);

            float alpha = 1f - t; //Linear fade out
            Color color = new Color(fadedColor.r, fadedColor.g, fadedColor.b, fadedColor.a * alpha);

            elapsed += Time.deltaTime;

            DrawSoundWave(fixedPosition, obj.transform.localScale.x, 360, LayerMask.GetMask("Wall"), ref obj, color);
            yield return null;
        }


        Destroy(obj);
    }

    public void DrawMultipleRipples(Vector3 position, float radius, Color color, int count = 3, float delay = 0.2f)
    {
        StartCoroutine(SpawnRipples(position, radius, color, count, delay));
    }

    IEnumerator SpawnRipples(Vector3 position, float radius, Color color, int count, float delay)
    {
        for (int i = 0; i < count; i++)
        {
            DrawSoundCircle(position, radius, color);
            yield return new WaitForSeconds(delay);
        }
    }

    void DrawSoundWave(Vector2 origin, float maxRadius, int resolution, LayerMask obstacleMask, ref GameObject obj, Color color)
    {
        if (obj == null || !obj.TryGetComponent(out MeshFilter filter))
        {
            Debug.LogWarning("MeshFilter missing or obj not assigned.");
            return;
        }

        if (!obj.TryGetComponent(out MeshRenderer renderer))
        {
            Debug.LogWarning("MeshRenderer missing or obj not assigned.");
            return;
        }

        List<Vector3> points = new List<Vector3> { Vector3.zero };
        List<Vector2> uvs = new List<Vector2> { new Vector2(0.5f, 0.5f) };

        for (int i = 0; i <= resolution; i++)
        {
            float angle = (360f / resolution) * i;
            Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

            RaycastHit2D hit = Physics2D.Raycast(origin, dir, maxRadius, obstacleMask);
            float finalRadius = hit.collider != null ? (hit.distance + (maxRadius - hit.distance) * WallConcreteness) : maxRadius;

            Vector2 pointWorld = origin + dir * finalRadius;
            points.Add(obj.transform.InverseTransformPoint(pointWorld)); // local space

            Vector2 uvCoord = new Vector2((Mathf.Cos(Mathf.Deg2Rad * angle) + 1f) * 0.5f, (Mathf.Sin(Mathf.Deg2Rad * angle) + 1f) * 0.5f);
            uvs.Add(uvCoord);
        }

        int[] triangles = new int[resolution * 3];
        int triIndex = 0;

        for (int i = 1; i < resolution; i++)
        {
            triangles[triIndex++] = 0;
            triangles[triIndex++] = i;
            triangles[triIndex++] = i + 1;
        }

        // Close the fan
        triangles[triIndex++] = 0;
        triangles[triIndex++] = resolution;
        triangles[triIndex++] = 1;

        Mesh mesh = new Mesh
        {
            vertices = points.ToArray(),
            triangles = triangles,
            uv = uvs.ToArray(),
        };

        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        renderer.material.color = color;
        filter.mesh = mesh;
    }

    public void OnSliderValueChanged(float value)
    {
        WallConcreteness = value;
        wallConcretenessValueText.text = "Wall Concreteness: " + WallConcreteness.ToString();
    }
}
