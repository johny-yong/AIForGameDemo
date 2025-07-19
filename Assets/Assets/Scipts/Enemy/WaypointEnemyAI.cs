using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static GeneralEnemyData;

public class WaypointEnemyAI : MonoBehaviour
{
    [Header("Patrol Settings")]
    public Transform[] waypoints;
    public float moveSpeed = 3f;
    public float reachDistance = 0.1f;

    [Header("Player Detection")]
    public Transform player;
    public float detectionRange = 5f;
    public float repathInterval = 0.5f;

    private List<Vector3> path = new List<Vector3>();
    private float repathTimer = 0f;

    [Header("Vision Cone Settings")]
    public float viewAngle = 90f;
    public float viewDistance = 5f;
    public int rayCount = 30;
    public LayerMask obstacleMask;
    public LayerMask playerMask;
    public ViewConeRenderer viewCone;

    [Header("Vision Circle Settings")]
    public CircularVisionRenderer circularVision;

    [Header("Poisson Sampling")]
    public int normalSampleCount = 50;
    public int alertSampleCount = 150;
    public float backViewDistance = 5f;
    public float backViewAngle = 180f;
    public GeneralEnemyData enemyData;

    [Range(0f, 1f)]
    public float confidenceThreshold = 0.3f;

    private Blackboard blackboard;

    public GameObject pathOrbPrefab;
    private List<GameObject> pathOrbs = new List<GameObject>();

    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
        blackboard = GetComponent<Blackboard>();
        if (blackboard == null) blackboard = gameObject.AddComponent<Blackboard>();
        enemyData = GameObject.FindGameObjectWithTag("EnemyDataController").GetComponent<GeneralEnemyData>();

        blackboard.Set("pathIndex", 0);
    }

    void Update()
    {
        if (!player) return;

        viewCone.isVisible = (enemyData.currentAwareness == AwarenessMode.ViewCone);
        circularVision.isVisible = (enemyData.currentAwareness == AwarenessMode.CircularRadius);

        bool isChasing = false;
        switch (enemyData.currentAwareness)
        {
            case AwarenessMode.OmniScient:
                isChasing = true;
                break;
            case AwarenessMode.ViewCone:
                isChasing = blackboard.Has("viewConePlayerSeen") && blackboard.Get<bool>("viewConePlayerSeen");
                break;
            case AwarenessMode.PoissonDisc:
                isChasing = CheckPlayerInPoissonDisc();
                break;
            case AwarenessMode.CircularRadius:
                isChasing = blackboard.Has("circlePlayerSeen") && blackboard.Get<bool>("circlePlayerSeen");
                break;
        }

        blackboard.Set("chasingPlayer", isChasing);

        if (blackboard.Get<bool>("chasingPlayer"))
        {
            repathTimer += Time.deltaTime;

            if (repathTimer >= repathInterval)
            {
                path = GridAStar.Instance.FindPath(transform.position, player.position);
                blackboard.Set("path", path);
                blackboard.Set("playerPosition", player.position);
                blackboard.Set("pathIndex", 0);
                repathTimer = 0f;
                ClearPathOrbs();

                if (enemyData.showDottedPath && path != null)
                {
                    foreach (Vector3 pos in path)
                    {
                        GameObject orb = Instantiate(pathOrbPrefab, pos, Quaternion.identity);
                        orb.transform.localScale *= 3f;
                        orb.GetComponent<SpriteRenderer>().color = Color.black;
                        pathOrbs.Add(orb);
                    }
                }
            }

            FollowPath();
            int pathIdx = blackboard.Get<int>("pathIndex");
            if (path != null && !CheckPlayerInPoissonDisc() && pathIdx >= path.Count)
            {
                blackboard.Set("chasingPlayer", false);
                path = null;
                ClearPathOrbs();
            }
        }
        else
        {
            Patrol();
        }
    }

    void Patrol()
    {
        if (waypoints.Length == 0) return;

        int currentWaypointIndex = blackboard.Has("waypointIndex") ? blackboard.Get<int>("waypointIndex") : 0;
        Transform target = waypoints[currentWaypointIndex];

        int pathIdx = blackboard.Get<int>("pathIndex");

        if (path == null || path.Count == 0 || pathIdx >= path.Count)
        {
            path = GridAStar.Instance.FindPath(transform.position, target.position);
            blackboard.Set("path", path);
            blackboard.Set("pathIndex", 0);
        }

        FollowPath();

        if (path != null && path.Count > 0 && Vector3.Distance(transform.position, path[^1]) <= reachDistance)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
            blackboard.Set("waypointIndex", currentWaypointIndex);
            path = null;
            ClearPathOrbs();
        }
    }

    void FollowPath()
    {
        path = blackboard.Get<List<Vector3>>("path");
        int pathIndex = blackboard.Get<int>("pathIndex");

        if (path == null || path.Count == 0 || pathIndex >= path.Count) return;

        Vector3 target = path[pathIndex];
        MoveTowards(target);

        if (Vector3.Distance(transform.position, target) <= reachDistance)
        {
            blackboard.Set("pathIndex", pathIndex + 1);
        }
    }

    void MoveTowards(Vector3 target)
    {
        Vector3 current = transform.position;
        target.z = current.z;

        Vector3 direction = (target - current).normalized;

        if (direction != Vector3.zero)
        {
            float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            Quaternion targetRotation = Quaternion.Euler(0, 0, targetAngle);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, 360f * Time.deltaTime);
        }

        transform.position = current + direction * moveSpeed * Time.deltaTime;
    }

    bool CheckPlayerInPoissonDisc()
    {
        bool isSuspicious = blackboard.Has("suspicious") && blackboard.Get<bool>("suspicious");
        bool isChasing = blackboard.Has("chasingPlayer") && blackboard.Get<bool>("chasingPlayer");

        int currentSampleCount = isSuspicious || isChasing ? alertSampleCount : normalSampleCount;

        var frontSamples = PoissonDiscSampler.Generate(transform.up, viewAngle, viewDistance, currentSampleCount);
        if (CheckSamplesForPlayer(frontSamples, 1.0f)) return true;

        int backSampleCount = Mathf.FloorToInt(currentSampleCount * 0.5f);
        var backSamples = PoissonDiscSampler.Generate(-transform.up, backViewAngle, backViewDistance, backSampleCount);
        if (CheckSamplesForPlayer(backSamples, 0.5f)) return true;

        return false;
    }

    bool CheckSamplesForPlayer(List<PoissonDiscSampler.Sample> samples, float maxConfidence)
    {
        foreach (var s in samples)
        {
            float confidence = Mathf.Min(s.confidence, maxConfidence);
            if (confidence < confidenceThreshold) continue;

            Vector3 worldSample = transform.position + s.direction * s.radius;
            RaycastHit2D hit = Physics2D.Raycast(transform.position, s.direction, s.radius, obstacleMask | playerMask);

            if (hit.collider != null && hit.transform == player)
            {
                Debug.Log($"Player hit at confidence {confidence}");
                return true;
            }
        }
        return false;
    }

    void ClearPathOrbs()
    {
        foreach (var orb in pathOrbs)
            if (orb) Destroy(orb);
        pathOrbs.Clear();
    }
}
