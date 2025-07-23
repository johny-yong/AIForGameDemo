using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GizmosWayPoint : MonoBehaviour
{
    public float gizmoRadius = 0.3f;
    public Color gizmoColor = Color.red;

    void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        Gizmos.DrawSphere(transform.position, gizmoRadius);
    }
}
