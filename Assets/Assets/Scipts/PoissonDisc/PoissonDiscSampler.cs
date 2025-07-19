using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//Reference: https://devmag.org.za/2009/05/03/poisson-disk-sampling/ 
//Warned as dangerous but somehow managed to enter
public static class PoissonDiscSampler
{
    public struct Sample
    {
        public Vector3 direction; //This one is ray direction
        public float radius;          
        public float confidence;      //Higher = more likely to identify
    }

    //Main driver for generating the poisson disc
    public static List<Sample> Generate(Vector3 forward, float viewAngle, float viewRadius, int sampleCount = 100, float minDistance = 0.5f)
    {
        List<Sample> samples = new List<Sample>();
        List<Vector2> acceptedPoints = new List<Vector2>();

        int maxAttemptsPerPoint = 30;
        float halfAngle = viewAngle * 0.5f;

        // Initial random point in the cone
        for (int i = 0; i < maxAttemptsPerPoint; i++)
        {
            Vector2 first = RandomPointInDisc(forward, halfAngle, viewRadius);
            acceptedPoints.Add(first);
            samples.Add(ToSample(first, forward, viewRadius));
            break;
        }

        Queue<Vector2> active = new Queue<Vector2>(acceptedPoints);

        while (active.Count > 0)
        {
            Vector2 center = active.Dequeue();

            for (int i = 0; i < maxAttemptsPerPoint; i++)
            {
                Vector2 candidate = GenerateAround(center, minDistance, viewRadius, forward, halfAngle);
                if (candidate == Vector2.zero) continue;

                if (!HasCloseNeighbor(candidate, acceptedPoints, minDistance))
                {
                    acceptedPoints.Add(candidate);
                    active.Enqueue(candidate);
                    samples.Add(ToSample(candidate, forward, viewRadius));
                }
            }

            if (samples.Count >= sampleCount)
                break;
        }

        return samples;
    }

    //Just making random point in cone
    private static Vector2 RandomPointInDisc(Vector3 forward, float halfAngle, float radius)
    {
        float angle = Random.Range(-halfAngle, halfAngle);
        float r = Mathf.Sqrt(Random.value) * radius;
        Vector2 dir = Quaternion.Euler(0, 0, angle) * forward;
        return dir.normalized * r;
    }

    //From the random point, generate points around that point
    //Candidates will later be needed to check if the position is valid, inside the disc or not
    private static Vector2 GenerateAround(Vector2 center, float minDist, float maxRadius, Vector3 forward, float halfAngle)
    {
        float r = Random.Range(minDist, 2f * minDist);
        float theta = Random.Range(0f, 2f * Mathf.PI);
        Vector2 offset = new Vector2(Mathf.Cos(theta), Mathf.Sin(theta)) * r;
        Vector2 candidate = center + offset;

        float distFromOrigin = candidate.magnitude;
        float angleFromForward = Vector2.Angle(forward, candidate.normalized);

        if (distFromOrigin <= maxRadius && angleFromForward <= halfAngle)
            return candidate;

        return Vector2.zero;
    }

    //Checker for between the points for offset
    private static bool HasCloseNeighbor(Vector2 point, List<Vector2> points, float minDist)
    {
        foreach (var other in points)
        {
            if (Vector2.Distance(point, other) < minDist)
                return true;
        }
        return false;
    }

    //Translating the data into actual sample
    private static Sample ToSample(Vector2 pos, Vector3 forward, float viewRadius)
    {
        Vector3 dir = new Vector3(pos.x, pos.y, 0f).normalized;
        float radius = pos.magnitude;

        //here is the main calculation for the calculation of the confidence level
        float angleFactor = 1f - Vector2.Angle(forward, dir) / 90f; //Affected by angle
        float radiusFactor = 1f - radius / viewRadius; //Affect by distance from the origin
        float confidence = Mathf.Min(angleFactor, radiusFactor);

        return new Sample
        {
            direction = dir,
            radius = radius,
            confidence = confidence
        };
    }
}
