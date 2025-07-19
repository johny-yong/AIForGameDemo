using System.Collections.Generic;
using UnityEngine;

public class PoissonDiscRenderer : MonoBehaviour
{
    [Header("Vision Settings")]
    public float viewAngle = 90f;
    public float viewDistance = 5f;
    public float backViewDistance = 5f;
    public float backViewAngle = 180f;
    public int sampleCount = 100;
    public LayerMask obstacleMask;

    [Header("Confidence Display")]
    [Range(0f, 1f)] public float confidenceThreshold = 0.3f;
    public GameObject redDotPrefab;
    public Gradient confidenceColor;
    public float dotScale = 0.1f;


    [Header("Confidence Zones")]
    public bool useZoneColors = true;
    public float[] confidenceSteps = new float[] { 0.8f, 0.5f, 0.2f }; 
    
    //Each color means different clarity
    //Red means obvious, high rate of recognising the player (Around >= 0.8)
    //Yellow means a bit obvious, normal rate (Between 0.5 to 0.79)
    //Green means a bit safe for the player, enemy rarely can recognise the player (Between 0.2 to 0.49)
    //Black means enemy almost can't recognise the area at that point (< 0.2)

    public Color[] zoneColors = new Color[] { Color.red, Color.yellow, Color.green };
    
    private List<GameObject> dotPool = new List<GameObject>();
    private int lastActiveCount = 0;

    void Update()
    {
        WaypointEnemyAI enemy = GetComponentInParent<WaypointEnemyAI>();
        if (enemy == null || enemy.enemyData.currentAwareness != GeneralEnemyData.AwarenessMode.PoissonDisc)
        {
            HideAllDots();
            return;
        }

        ShowSampleDots();
    }

    void ShowSampleDots()
    {
        int activeCount = 0;

        // === FORWARD CONE ===
        var forwardSamples = PoissonDiscSampler.Generate(transform.up, viewAngle, viewDistance, sampleCount);
        foreach (var s in forwardSamples)
        {
            if (s.confidence < confidenceThreshold) continue;
            activeCount = PlaceDot(s, activeCount);
        }

        // === BACK CONE ===
        int backSampleCount = Mathf.FloorToInt(sampleCount * 0.5f);

        Vector3 backDir = -transform.up;
        var backSamples = PoissonDiscSampler.Generate(backDir, backViewAngle, backViewDistance, backSampleCount);

        for (int i = 0; i < backSamples.Count; i++)
        {
            var s = backSamples[i];
            if (s.confidence < confidenceThreshold) continue;

            s.confidence = Mathf.Min(s.confidence, 0.5f);
            backSamples[i] = s;
            activeCount = PlaceDot(s, activeCount);
        }

        // === HIDE UNUSED DOTS ===
        for (int i = activeCount; i < lastActiveCount; i++)
            dotPool[i].SetActive(false);

        lastActiveCount = activeCount;
    }

    GameObject GetDotFromPool(int index)
    {
        if (index < dotPool.Count)
            return dotPool[index];

        GameObject newDot = Instantiate(redDotPrefab, transform.position, Quaternion.identity);
        newDot.transform.SetParent(transform); // optional: keep under this object
        dotPool.Add(newDot);
        return newDot;
    }

    void HideAllDots()
    {
        foreach (var dot in dotPool)
            dot.SetActive(false);
        lastActiveCount = 0;
    }

    int PlaceDot(PoissonDiscSampler.Sample s, int activeCount)
    {
        Vector3 worldSample = transform.position + s.direction * s.radius;
        RaycastHit2D hit = Physics2D.Raycast(transform.position, s.direction, s.radius, obstacleMask);
        Vector3 endPoint = hit.collider ? hit.point : worldSample;

        GameObject dot = GetDotFromPool(activeCount);
        dot.transform.position = endPoint;
        dot.transform.localScale = Vector3.one * dotScale;

        SpriteRenderer sr = dot.GetComponent<SpriteRenderer>();
        if (sr)
        {
            if (useZoneColors)
            {
                for (int z = 0; z < confidenceSteps.Length; z++)
                {
                    if (s.confidence >= confidenceSteps[z])
                    {
                        sr.color = (zoneColors.Length > z) ? zoneColors[z] : Color.white;
                        break;
                    }
                }
            }
            else if (confidenceColor != null)
            {
                sr.color = confidenceColor.Evaluate(s.confidence);
            }
        }

        dot.SetActive(true);
        return activeCount + 1;
    }


}
