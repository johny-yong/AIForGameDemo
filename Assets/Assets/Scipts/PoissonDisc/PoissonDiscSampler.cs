using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class PoissonDiscSampler
{
    public struct Sample
    {
        public Vector3 direction;     // Unit direction vector
        public float radius;          
        public float confidence;      //Clamped between 0-1
    }

    //Generate Poisson-disc samples
    public static List<Sample> Generate(Vector3 forward, float viewAngle, float viewRadius, int sampleCount)
    {
        List<Sample> samples = new List<Sample>();

        for (int i = 0; i < sampleCount; i++)
        {
            float radius = Mathf.Sqrt(Random.value) * viewRadius;

            //Sample angle within [-viewAngle/2, +viewAngle/2]
            float angle = Random.Range(-viewAngle / 2f, viewAngle / 2f);
            Vector3 dir = Quaternion.Euler(0, 0, angle) * forward;

            //Confidence computation
            float angleFactor = 1f - Mathf.Abs(angle) / (viewAngle * 0.5f); //1 as center
            float radiusFactor = 1f - radius / viewRadius;               //1 = close
            float confidence = Mathf.Min(angleFactor, radiusFactor);    

            samples.Add(new Sample
            {
                direction = dir.normalized,
                radius = radius,
                confidence = confidence
            });
        }

        return samples;
    }
}
