using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public static class SoundDetectionManager
{
    public static List<IHearingReceiver> listeners = new List<IHearingReceiver>();
    public static LayerMask occlusionMask;
    public static void EmitSound(SoundEvent sound)
    {
        float concreteVal = GameObject.Find("EventSystem").GetComponent<SoundVisualizer>().WallConcreteness;


        SoundVisualizer.Instance.DrawMultipleRipples(
            sound.sourcePosition,
            sound.baseVolume * 2f,
            GetColorForSoundType(sound.type),
            3, //Ripple count
            0.15f //Delay between ripples
        );

        foreach (var listener in listeners)
        {
            Vector3 dir = (listener.GetPosition() - sound.sourcePosition).normalized;
            float maxRange = sound.baseVolume * 2f;
            float distToListener = Vector3.Distance(listener.GetPosition(), sound.sourcePosition);

            if (distToListener > maxRange) continue;

            RaycastHit2D hit = Physics2D.Raycast(sound.sourcePosition, dir, distToListener, occlusionMask);

            float effectiveRange = maxRange;

            if (hit.collider != null)
            {
                // reduce the sound strength when it passes through the wall based of its concreteness
                float blockedPortion = (maxRange - hit.distance);
                effectiveRange = hit.distance + blockedPortion * concreteVal;
            }

            if (distToListener > effectiveRange) continue;

            float falloff = 1f - (distToListener / effectiveRange);
            float effectiveVolume = sound.baseVolume * falloff;

            listener.OnHearSound(sound, effectiveVolume);
        }


    }

    private static Color GetColorForSoundType(SoundType type)
    {
        return type switch
        {
            SoundType.Footstep => new Color(1f, 1f, 1f, 0.1f),
            SoundType.Ambient => new Color(0.5f, 0.8f, 1f, 0.3f),
            _ => Color.white
        };
    }
    public static void RegisterListener(IHearingReceiver receiver)
    {
        if (!listeners.Contains(receiver)) listeners.Add(receiver);
    }

    public static void UnregisterListener(IHearingReceiver receiver)
    {
        if (listeners.Contains(receiver)) listeners.Remove(receiver);
    }
}
