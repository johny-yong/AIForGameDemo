using System.Collections.Generic;
using UnityEngine;

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
    public Color alertColor = new Color(1f, 0f, 0f, 0.7f);

    private Mesh mesh;
    private MeshRenderer meshRenderer;
    private Material dynamicMaterial;
    private Color currentColor;

    public bool isVisible = true;
    public bool playerInSight { get; private set; } = false;
    public Blackboard blackboard;

    [Header("Gaussian Settings")]
    public float stdDev = 10.3f;
    public bool useGaussian = true;


    [Header("Head Turn Settings")]
    public float headTurnAmplitude = 30f;     //±30 degrees
    public float headTurnSpeed = 0.5f;        
    public float headTurnGaussianRange = 10f; //Max randomness
    public float headTurnJitterInterval = 2f; //Refresh interval (seconds)

    private float headTurnGaussianOffset = 0f;
    private float lastHeadTurnJitterTime = -999f;

    public bool lockHeadWhenAlerted = true;
    private bool headTurnLocked = false;
    private float lockedHeadAngle = 0f;

    //For building the texture
    private Vector3[] vertices;
    private Vector2[] uv;
    private int[] triangles;

    [Header("Raycast Line Settings")]
    public bool showRayLines = true;
    public Material rayLineMaterial;
    public float rayLineWidth = 0.02f;


    private List<LineRenderer> rayLines = new List<LineRenderer>();

    void Awake()
    {
        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;

        meshRenderer = GetComponent<MeshRenderer>();
        dynamicMaterial = new Material(Shader.Find("Sprites/Default"));
        meshRenderer.material = dynamicMaterial;
        currentColor = hiddenColor;
        dynamicMaterial.color = currentColor;
        viewDistance = gameObject.GetComponentInParent<WaypointEnemyAI>().viewDistance;
        viewAngle = gameObject.GetComponentInParent<WaypointEnemyAI>().viewAngle;

        target = GameObject.FindGameObjectWithTag("Player")?.transform;
        blackboard = GetComponentInParent<Blackboard>();

        float newOffset = GaussianRandom(0f, headTurnGaussianRange);
        headTurnGaussianOffset = Mathf.Lerp(headTurnGaussianOffset, newOffset, 0.5f);
        lastHeadTurnJitterTime = Time.time;
    }

    void Update()
    {
        if (!showRayLines)
        {
            DisableAllRayLines();
        }

        UpdateHeadTurnJitter();
        GenerateCone();
        UpdateFade();
    }
    void GenerateCone()
    {
        float halfAngle = viewAngle * 0.5f;
        float headTurnOffset = (lockHeadWhenAlerted && headTurnLocked)
            ? lockedHeadAngle
            : Mathf.Sin(Time.time * headTurnSpeed * 2f * Mathf.PI) * headTurnAmplitude + headTurnGaussianOffset;

        List<(float angle, Vector3 endPoint, Vector2 uv)> sortedRays = new List<(float, Vector3, Vector2)>();
        playerInSight = false;
        if (blackboard != null)
        {
            blackboard.Set("viewConePlayerSeen", false);
        }
        Vector3 worldPos = transform.position;

        for (int i = 0; i < rayCount; i++)
        {
            float t = i / (float)(rayCount - 1);
            float uniformAngle = Mathf.Lerp(-halfAngle, halfAngle, t);
            float g = Mathf.Clamp(FastGaussian(), -3f, 3f);
            float gaussianAngle = Mathf.Lerp(-halfAngle, halfAngle, (g + 3f) / 6f);
            float angle = useGaussian
                ? Mathf.Lerp(uniformAngle, gaussianAngle, 0.7f) + headTurnOffset
                : uniformAngle + headTurnOffset;

            Vector3 dir = Quaternion.Euler(0, 0, angle) * transform.up;
            RaycastHit2D hit = Physics2D.Raycast(worldPos, dir, viewDistance, obstacleMask);
            Vector3 endPoint;

            if (hit.collider)
            {
                endPoint = transform.InverseTransformPoint(hit.point);

                if (!playerInSight && target != null)
                {
                    bool hitPlayer = (hit.collider.gameObject == target.gameObject);

                    if (!hitPlayer)
                    {
                        hitPlayer = (hit.collider.transform == target) ||
                                   (hit.collider.transform.IsChildOf(target));
                    }

                    if (hitPlayer)
                    {
                        playerInSight = true;
                        if (blackboard != null)
                        {
                            blackboard.Set("viewConePlayerSeen", true);
                        }
                        if (lockHeadWhenAlerted)
                        {
                            headTurnLocked = true;
                            lockedHeadAngle = angle - headTurnOffset;
                        }

                        Debug.Log($"Player detected! Distance: {hit.distance}");
                    }
                }

                if (showRayLines)
                {
                    EnsureRayLineCount(rayCount);
                    LineRenderer lr = rayLines[i];
                    lr.enabled = true;
                    lr.SetPosition(0, worldPos);
                    lr.SetPosition(1, hit.point);
                }
            }
            else
            {
                endPoint = transform.InverseTransformDirection(dir) * viewDistance;
                if (showRayLines)
                {
                    EnsureRayLineCount(rayCount);
                    LineRenderer lr = rayLines[i];
                    lr.enabled = true;
                    lr.SetPosition(0, worldPos);
                    lr.SetPosition(1, hit.collider ? hit.point : (worldPos + dir * viewDistance));
                }

            }

            float uvAngle = (angle - headTurnOffset + halfAngle) / viewAngle;
            sortedRays.Add((angle, endPoint, new Vector2(uvAngle, 1f)));
        }

        //Sort rays by angle
        sortedRays.Sort((a, b) => a.angle.CompareTo(b.angle));

        List<Vector3> vertices = new List<Vector3> { Vector3.zero };
        List<Vector2> uvs = new List<Vector2> { new Vector2(0.5f, 0f) };

        foreach (var ray in sortedRays)
        {
            vertices.Add(ray.endPoint);
            uvs.Add(ray.uv);
        }

        int[] triangles = new int[(vertices.Count - 1) * 3];
        for (int i = 0, triIndex = 0; i < vertices.Count - 1; i++, triIndex += 3)
        {
            triangles[triIndex] = 0;
            triangles[triIndex + 1] = i + 1;
            triangles[triIndex + 2] = (i + 2) % vertices.Count;
        }

        mesh.Clear();
        mesh.vertices = vertices.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        meshRenderer.transform.localScale = Vector3.one;
    }

    void UpdateFade()
    {
        Color targetColor = hiddenColor;

        if (isVisible)
            targetColor = playerInSight ? alertColor : visibleColor;

        currentColor = Color.Lerp(currentColor, targetColor, Time.deltaTime * fadeSpeed);
        dynamicMaterial.color = currentColor;
    }

    float GaussianRandom(float mean, float stdDev)
    {
        float u1 = 1.0f - Random.value;
        float u2 = 1.0f - Random.value;
        float randStdNormal = Mathf.Sqrt(-2.0f * Mathf.Log(u1)) * Mathf.Sin(2.0f * Mathf.PI * u2);
        return mean + stdDev * randStdNormal;
    }
    void EnsureRayLineCount(int count)
    {
        while (rayLines.Count < count)
        {
            GameObject lineObj = new GameObject("RayLine");
            lineObj.transform.parent = transform;

            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            lr.material = rayLineMaterial;
            lr.startWidth = rayLineWidth;
            lr.endWidth = rayLineWidth;
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.sortingOrder = 99;

            rayLines.Add(lr);
        }

        //Disable extra lines
        for (int i = 0; i < rayLines.Count; i++)
        {
            rayLines[i].enabled = i < count;
        }
    }

    float FastGaussian()
    {
        float u1 = Random.value;
        float u2 = Random.value;
        float randStdNormal = Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2);
        return randStdNormal; // mean 0, stddev 1
    }


    void UpdateHeadTurnJitter()
    {
        if (Time.time - lastHeadTurnJitterTime >= headTurnJitterInterval)
        {
            headTurnGaussianOffset = GaussianRandom(0f, headTurnGaussianRange);
            lastHeadTurnJitterTime = Time.time;
        }
    }

    void DisableAllRayLines()
    {
        foreach (var lr in rayLines)
        {
            lr.enabled = false;
            lr.SetPosition(0, transform.position);
            lr.SetPosition(1, transform.position);
        }
    }

}
