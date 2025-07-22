using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IHearingReceiver
{
    void OnHearSound(SoundEvent e, float effectiveVolume);
    Vector3 GetPosition();
}
