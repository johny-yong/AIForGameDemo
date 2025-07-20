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
    public LayerMask enemyMask;
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

    [Header("Last Known Position")]
    public float lastKnownPositionTimeout = 10f; // How long to remember last known position

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
        bool canSeePlayerDirectly = false;

        switch (enemyData.currentAwareness)
        {
            case AwarenessMode.OmniScient:
                isChasing = true;
                break;
            case AwarenessMode.ViewCone:
                isChasing = blackboard.Has("viewConePlayerSeen") && blackboard.Get<bool>("viewConePlayerSeen");
                break;
            case AwarenessMode.PoissonDisc:
                canSeePlayerDirectly = CheckPlayerInPoissonDisc();  //Direct Line of Sight of player
                isChasing = SharingIntelBetweenEnemies(canSeePlayerDirectly);  //Sharing intel with other enemies 
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
                Vector3 targetPosition = player.position;

                // If we can't see player directly but have last known position, use that
                if (!canSeePlayerDirectly && blackboard.Has("lastKnownPlayerPosition"))
                {
                    targetPosition = blackboard.Get<Vector3>("lastKnownPlayerPosition");
                }


                path = GridAStar.Instance.FindPath(transform.position, targetPosition);
                blackboard.Set("path", path);
                blackboard.Set("playerPosition", targetPosition);
                blackboard.Set("pathIndex", 0);
                repathTimer = 0f;
                ClearPathOrbs();

                if (enemyData.showDottedPath && path != null)
                {
                    foreach (Vector3 pos in path)
                    {
                        GameObject orb = Instantiate(pathOrbPrefab, pos, Quaternion.identity);
                        orb.transform.localScale *= 3f;
                        orb.GetComponent<SpriteRenderer>().color = Color.red; // Red for last known position
                        pathOrbs.Add(orb);
                    }
                }
            }

            FollowPath();
            int pathIdx = blackboard.Get<int>("pathIndex");

            //Timer for the enemy to return to patrol if reached the last known position
            if (path != null && pathIdx >= path.Count)
            {
                if (blackboard.Has("lastKnownPlayerTime"))
                {
                    float lastSeenTime = blackboard.Get<float>("lastKnownPlayerTime");
                    if (Time.time - lastSeenTime >= lastKnownPositionTimeout)
                    {
                        blackboard.Set("chasingPlayer", false);
                        path = null;
                        ClearPathOrbs();
                    }
                }
                else
                {
                    blackboard.Set("chasingPlayer", false);
                    path = null;
                    ClearPathOrbs();
                }
            }
        }
        else
        {
            Patrol();
        }
    }

    bool SharingIntelBetweenEnemies(bool canSeePlayerDirectly) {

        // Always check if we can see another enemy and share intel
        GameObject detectedEnemy = GetDetectedEnemy();
        if (detectedEnemy != null)
        {
            Blackboard enemyBlackboard = detectedEnemy.GetComponent<Blackboard>();
            if (enemyBlackboard != null)
            {
                bool thisEnemyChasing = blackboard.Has("chasingPlayer") && blackboard.Get<bool>("chasingPlayer");
                bool otherEnemyChasing = enemyBlackboard.Has("chasingPlayer") && enemyBlackboard.Get<bool>("chasingPlayer");

                // Determine who has the most recent intel
                float thisLastSeen = blackboard.Has("lastKnownPlayerTime") ? blackboard.Get<float>("lastKnownPlayerTime") : -1f;
                float otherLastSeen = enemyBlackboard.Has("lastKnownPlayerTime") ? enemyBlackboard.Get<float>("lastKnownPlayerTime") : -1f;

                // Share intel if either enemy is chasing and has more recent information
                if (thisEnemyChasing && thisLastSeen > otherLastSeen && thisLastSeen > 0f)
                {
                    // This enemy shares intel to other enemy
                    enemyBlackboard.Set("lastKnownPlayerPosition", blackboard.Get<Vector3>("lastKnownPlayerPosition"));
                    enemyBlackboard.Set("lastKnownPlayerTime", thisLastSeen);
                    Debug.Log($"{name} shared intel with {detectedEnemy.name}");
                }
                else if (otherEnemyChasing && otherLastSeen > thisLastSeen && otherLastSeen > 0f)
                {
                    // Other enemy shares intel to this enemy
                    blackboard.Set("lastKnownPlayerPosition", enemyBlackboard.Get<Vector3>("lastKnownPlayerPosition"));
                    blackboard.Set("lastKnownPlayerTime", otherLastSeen);
                    Debug.Log($"{detectedEnemy.name} shared intel with {name}");
                }
            }
        }

        // Determine if we should be chasing
        // If we can see the player directly, update last known position
        if (canSeePlayerDirectly)
        {
            blackboard.Set("lastKnownPlayerPosition", player.position);
            blackboard.Set("lastKnownPlayerTime", Time.time);
            return true; // We are chasing if we can see the player directly
        }
        // Can't see player directly - check if we should chase based on recent intel
        if (blackboard.Has("lastKnownPlayerTime"))
        {
            float lastSeenTime = blackboard.Get<float>("lastKnownPlayerTime");
            float timeSinceLastSeen = Time.time - lastSeenTime;
            return timeSinceLastSeen < lastKnownPositionTimeout;
        }

        return false; // No recent intel, don't chase

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
                //Debug.Log($"Player hit at confidence {confidence}");
                return true;
            }
        }
        return false;
    }

    //Function to detect enemies
    GameObject GetDetectedEnemy()
    {
        bool isSuspicious = blackboard.Has("suspicious") && blackboard.Get<bool>("suspicious");
        bool isChasing = blackboard.Has("chasingPlayer") && blackboard.Get<bool>("chasingPlayer");
        int currentSampleCount = isSuspicious || isChasing ? alertSampleCount : normalSampleCount;

        var frontSamples = PoissonDiscSampler.Generate(transform.up, viewAngle, viewDistance, currentSampleCount);
        GameObject detectedEnemy = CheckSamplesForEnemyObject(frontSamples, 1.0f);
        if (detectedEnemy != null)
        {
            //Debug.Log($"{name} detected enemy {detectedEnemy.name} in FRONT Poisson disc sampling with {frontSamples.Count} samples");
            return detectedEnemy;
        }

        int backSampleCount = Mathf.FloorToInt(currentSampleCount * 0.5f);
        var backSamples = PoissonDiscSampler.Generate(-transform.up, backViewAngle, backViewDistance, backSampleCount);
        detectedEnemy = CheckSamplesForEnemyObject(backSamples, 0.5f);
        if (detectedEnemy != null)
        {
            //Debug.Log($"{name} detected enemy {detectedEnemy.name} in BACK Poisson disc sampling with {backSamples.Count} samples");
            return detectedEnemy;

        }

        return null;
    }

    //Function to check Poisson Disk samples for enemy objects
    GameObject CheckSamplesForEnemyObject(List<PoissonDiscSampler.Sample> samples, float maxConfidence)
    {
        foreach (var s in samples)
        {
            Vector3 worldSample = transform.position + s.direction * s.radius;
            RaycastHit2D hit = Physics2D.Raycast(transform.position, s.direction, s.radius, obstacleMask | enemyMask);

            if (hit.collider != null && hit.transform.CompareTag("Enemy"))
            {
                //Debug.Log($"Detected Enemy");
                return hit.transform.gameObject;
            }
        }
        return null;
    }


    void ClearPathOrbs()
    {
        foreach (var orb in pathOrbs)
            if (orb) Destroy(orb);
        pathOrbs.Clear();
    }
}
