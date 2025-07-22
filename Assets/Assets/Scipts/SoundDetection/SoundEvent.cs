using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public struct SoundEvent
{
    public Vector3 sourcePosition;
    public float baseVolume;
    public SoundType type;
    public GameObject emitter;
    public float frequency; //(0-1 where 0=bass, 1=treble)
    public bool isDirect;   //Whether this is direct or reflected sound
}