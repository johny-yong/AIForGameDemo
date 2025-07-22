using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class SoundDetectionManager
{
    public static List<IHearingReceiver> listeners = new List<IHearingReceiver>();
    public static LayerMask occlusionMask;

    public static void EmitSound(SoundEvent sound)
    {

        SoundVisualizer.Instance.DrawMultipleRipples(
            sound.sourcePosition,
            sound.baseVolume * 5f,
            GetColorForSoundType(sound.type),
            3, //Ripple count
            0.15f //Delay between ripples
        );


        //Then process hearing logic
        foreach (var listener in listeners)
        {
            Vector3 dir = (listener.GetPosition() - sound.sourcePosition).normalized;
            float maxRange = sound.baseVolume * 5f;
            float dist = Vector3.Distance(listener.GetPosition(), sound.sourcePosition);

            if (dist > maxRange) continue;

            RaycastHit2D hit = Physics2D.Raycast(sound.sourcePosition, dir, dist, occlusionMask);
            float muffling = hit.collider != null ? 0.5f : 1f;
            float effectiveVolume = sound.baseVolume * muffling * (1 - dist / maxRange);

            if (effectiveVolume >= 0.1f)
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
