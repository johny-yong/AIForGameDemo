using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static WaypointEnemyAI;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ViewConeRenderer : MonoBehaviour
{
    [Header("Vision Settings")]
    public float viewAngle = 90f;
    public float viewDistance = 5f;
    [Range(3, 100)] public int rayCount = 50;
    public LayerMask obstacleMask;
    public Transform target;

    [Header("Fade Settings")]
    public float fadeSpeed = 5f;
    public Color visibleColor = new Color(1f, 1f, 0f, 0.3f);
    public Color hiddenColor = new Color(1f, 1f, 0f, 0f);
    public Color alertColor = new Color(1f, 0f, 0f, 0.7f); // Red with same alpha


    [Header("Shader Settings")]
    private Mesh mesh;
    private Vector3[] vertices;
    private Vector2[] uv;
    private int[] triangles;
    private MeshRenderer meshRenderer;
    private Material dynamicMaterial;
    private Color currentColor;

    public bool isVisible = true;
    public bool playerInSight { get; private set; } = false;

    void Awake()
    {
        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;
        meshRenderer = GetComponent<MeshRenderer>();

        dynamicMaterial = new Material(Shader.Find("Sprites/Default")); 
        meshRenderer.material = dynamicMaterial;
        currentColor = hiddenColor;
        dynamicMaterial.color = currentColor;

        target = GameObject.FindGameObjectWithTag("Player").transform;
        viewDistance = gameObject.GetComponentInParent<WaypointEnemyAI>().viewDistance;
    }

    void LateUpdate()
    {
        GenerateCone();
        UpdateFade();
    }

    void GenerateCone()
    {
        float halfAngle = viewAngle * 0.5f;
        float angleStep = viewAngle / (rayCount - 1);

        //Setting the origin here
        List<Vector3> points = new List<Vector3> { Vector3.zero }; 
        
        //Taking center UV
        List<Vector2> uvs = new List<Vector2> { new Vector2(0.5f, 0.5f) }; 
        

        playerInSight = false;

        for (int i = 0; i < rayCount; i++)
        {
            float angle = -halfAngle + (angleStep * i);
            //Directly infront of the enemy
            Vector3 dir = Quaternion.Euler(0, 0, angle) * transform.up;

            RaycastHit2D hit = Physics2D.Raycast(transform.position, dir, viewDistance, obstacleMask);

            Vector3 end;
            if (hit.collider)
            {
                end = transform.InverseTransformPoint(hit.point);
            }
            else
            {
                end = transform.InverseTransformDirection(dir) * viewDistance;
            }

            points.Add(end);

            float uvAngle = angle / viewAngle; //UV Angle
            uvs.Add(new Vector2(uvAngle, 1f));

            //Player detected
            if (target)
            {
                float distToTarget = Vector3.Distance(transform.position, target.position);
                Vector3 dirToTarget = (target.position - transform.position).normalized;
                float angleToTarget = Vector3.Angle(transform.up, dirToTarget);

                if (distToTarget < viewDistance && angleToTarget < halfAngle)
                {
                    RaycastHit2D sightCheck = Physics2D.Raycast(
                        transform.position,
                        dirToTarget,
                        distToTarget,
                        obstacleMask);

                    if (sightCheck.collider == null || sightCheck.transform == target)
                    {
                        playerInSight = true;
                    }
                }
            }
        }

        //Mesh vertices data
        vertices = points.ToArray();
        uv = uvs.ToArray();

        triangles = new int[(rayCount - 1) * 3];
        int triIndex = 0;

        for (int i = 1; i < rayCount; i++)
        {
            triangles[triIndex++] = 0;
            triangles[triIndex++] = i;
            triangles[triIndex++] = i + 1;
        }

        //Assigning mesh data
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    //Toggled when player is detected here
    void UpdateFade()
    {
        Color targetColor;

        if (!isVisible)
        {
            targetColor = hiddenColor;
        }
        else if (playerInSight)
        {
            targetColor = alertColor;
        }
        else
        {
            targetColor = visibleColor;
        }

        currentColor = Color.Lerp(currentColor, targetColor, Time.deltaTime * fadeSpeed);
        dynamicMaterial.color = currentColor;
    }

}