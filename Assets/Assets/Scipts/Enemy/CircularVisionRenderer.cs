using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class CircularVisionRenderer : MonoBehaviour
{
    [Header("Vision Setting")]
    public float viewDistance = 5f;
    [Range(10, 360)] public int rayCount = 100;
    public LayerMask obstacleMask; //Basically always walls
    public Transform target;

    [Header("Fade Setting")] //Mainly for visual purposes
    public float fadeSpeed = 5f;
    public Color visibleColor = new Color(1f, 1f, 0f, 0.3f);
    public Color hiddenColor = new Color(1f, 1f, 0f, 0f);
    public Color alertColor = new Color(1f, 0f, 0f, 0.7f);

    //Mesh rendering section
    private Mesh mesh;
    private Vector3[] vertices;
    private Vector2[] uv;
    private int[] triangles;
    private MeshRenderer meshRenderer;
    private Material dynamicMaterial;
    private Color currentColor;

    public bool isVisible = true;
    public bool playerInSight { get; private set; } = false;

    public Blackboard blackboard;
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
        blackboard = GetComponentInParent<Blackboard>();

    }

    void Update()
    {
        GenerateCircle();
        UpdateFade();
    }

    //Includes both the logic for rendering the circle as well as the player detection
    void GenerateCircle()
    {
        float angleStep = 360f / rayCount;

        List<Vector3> points = new List<Vector3> { Vector3.zero }; 
        List<Vector2> uvs = new List<Vector2> { new Vector2(0.5f, 0.5f) };

        playerInSight = false;

        //Player detection section
        for (int i = 0; i <= rayCount; i++)
        {
            float angle = angleStep * i;
            Vector3 dir = Quaternion.Euler(0, 0, angle) * Vector3.up;

            RaycastHit2D hit = Physics2D.Raycast(transform.position, dir, viewDistance, obstacleMask);
            Vector3 end = hit.collider ?
                transform.InverseTransformPoint(hit.point) :
                transform.InverseTransformDirection(dir) * viewDistance;

            points.Add(end);

            Vector2 uvCoord = new Vector2((Mathf.Cos(Mathf.Deg2Rad * angle) + 1f) * 0.5f, (Mathf.Sin(Mathf.Deg2Rad * angle) + 1f) * 0.5f);
            uvs.Add(uvCoord);

            if (target)
            {
                float distToTarget = Vector3.Distance(transform.position, target.position);
                if (distToTarget < viewDistance)
                {
                    Vector3 dirToTarget = (target.position - transform.position).normalized;
                    RaycastHit2D sightCheck = Physics2D.Raycast(transform.position, dirToTarget, distToTarget, obstacleMask);
                    if (sightCheck.collider == null || sightCheck.transform == target)
                    {
                        playerInSight = true;
                    }
                }
            }
        }
        if (playerInSight && blackboard != null)
        {
            blackboard.Set("circlePlayerSeen", true);
        }
        else if (blackboard != null)
        {
            blackboard.Set("circlePlayerSeen", false);
        }

        //Drawing the circle
        vertices = points.ToArray();
        uv = uvs.ToArray();
        triangles = new int[rayCount * 3];

        int triIndex = 0;
        for (int i = 1; i <= rayCount; i++)
        {
            triangles[triIndex++] = 0;
            triangles[triIndex++] = i;
            triangles[triIndex++] = i + 1;
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    void UpdateFade()
    {
        Color targetColor;

        if (!isVisible)
            targetColor = hiddenColor;
        else if (playerInSight)
            targetColor = alertColor;
        else
            targetColor = visibleColor;

        currentColor = Color.Lerp(currentColor, targetColor, Time.deltaTime * fadeSpeed);
        dynamicMaterial.color = currentColor;
    }
}