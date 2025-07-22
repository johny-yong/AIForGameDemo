using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SoundEmitter : MonoBehaviour
{
    public float soundRadius = 5f;
    public float showDuration = 1f;
    private float timer = 0f;
    private bool showGizmo = false;

    public SoundType soundType = SoundType.Footstep;
    public float baseVolume = 1f;

    public void EmitSound()
    {
        timer = showDuration;
        showGizmo = true;

        SoundEvent sound = new SoundEvent
        {
            sourcePosition = transform.position,
            baseVolume = baseVolume,
            type = soundType,
            emitter = gameObject
        };

        SoundDetectionManager.EmitSound(sound);
    }


    void Update()
    {
        if (showGizmo)
        {
            timer -= Time.deltaTime;
            if (timer <= 0f)
                showGizmo = false;
        }
    }

    void OnDrawGizmos()
    {
        if (showGizmo)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.DrawSphere(transform.position, soundRadius);
        }
    }
}

