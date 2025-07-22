using System.Collections;
using UnityEngine;

public class SoundVisualizer : MonoBehaviour
{
    public static SoundVisualizer Instance;
    public GameObject circlePrefab;
    public float baseDuration = 1f;

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

        StartCoroutine(GrowAndFade(circle, radius, baseDuration * durationMultiplier, color));
    }

    IEnumerator GrowAndFade(GameObject obj, float targetRadius, float duration, Color baseColor)
    {
        Vector3 fixedPosition = obj.transform.position;
        float elapsed = 0f;
        SpriteRenderer sr = obj.GetComponent<SpriteRenderer>();

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
            sr.color = new Color(fadedColor.r, fadedColor.g, fadedColor.b, fadedColor.a * alpha);

            elapsed += Time.deltaTime;
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


}
