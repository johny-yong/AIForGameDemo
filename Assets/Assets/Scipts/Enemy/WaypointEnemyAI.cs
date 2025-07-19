using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static GeneralEnemyData;

//AI Logic
//Uses Waypoint for idling (Need ownself set the position of the waypoints per AI)
//Uses A* for shortest distance to move between way points as well as the player chasing
//Currently when the player is touched, get back to previous (next) waypoint


//Maybe need to add a persistancy logic behind the enemy AI?
//To do: Try carry some data from project 3 into here...

//Currently while blackboard is implemented into the system already, but the usage not there yet
//To be updated later on
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

    [Header("Waypoints")]
    private int currentWaypointIndex = 0;
    private bool chasingPlayer = false;
    private List<Vector3> path = new List<Vector3>();
    private int pathIndex = 0;
    private float repathTimer = 0f;

    [Header("Vision Cone Settings")]
    public float viewAngle = 90f;
    public float viewDistance = 5f;
    public int rayCount = 30;
    public LayerMask obstacleMask;
    public LayerMask playerMask;

    public ViewConeRenderer viewCone;

    [Header("Poisson Sampling")]
    public int normalSampleCount = 50;
    public int alertSampleCount = 150;
    private bool suspicious = false; // Could be set based on hearing or other cues
    public float backViewDistance = 5f;
    public float backViewAngle = 180f;

    public GeneralEnemyData enemyData;

    //The actual one
    [Range(0f, 1f)] 
    public float confidenceThreshold = 0.3f;

    private Blackboard blackboard;

    public GameObject pathOrbPrefab;
    private List<GameObject> pathOrbs = new List<GameObject>();

    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
        //Automatically adding the blackboard
        blackboard = GetComponent<Blackboard>();
        if (blackboard == null) { 
            blackboard = gameObject.AddComponent<Blackboard>();
        }
        enemyData = GameObject.FindGameObjectWithTag("EnemyDataController").GetComponent<GeneralEnemyData>();
}

void Update()
    {
        if (!player) return;
        viewCone.isVisible = (enemyData.currentAwareness == AwarenessMode.ViewCone);

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        switch (enemyData.currentAwareness)
        {
            case AwarenessMode.OmniScient:
                chasingPlayer = true;
                break;

            case AwarenessMode.ViewCone:
                chasingPlayer = viewCone.playerInSight;
                break;

            case AwarenessMode.PoissonDisc:
                chasingPlayer = CheckPlayerInPoissonDisc();
                break;
        }

        if (chasingPlayer)
        {
            repathTimer += Time.deltaTime;

            if (repathTimer >= repathInterval)
            {
                path = GridAStar.Instance.FindPath(transform.position, player.position);
                ClearPathOrbs();
                if (enemyData.showDottedPath)
                {
                    foreach (Vector3 pos in path)
                    {
                        GameObject orb = Instantiate(pathOrbPrefab, pos, Quaternion.identity);
                        orb.transform.localScale = new Vector3(orb.transform.localScale.x * 3f, orb.transform.localScale.y * 3f, orb.transform.localScale.z * 3f);
                        orb.GetComponent<SpriteRenderer>().color = Color.black;
                        pathOrbs.Add(orb);
                    }
                }

                pathIndex = 0;
                repathTimer = 0f;

                if (path == null)
                {
                    Debug.LogWarning("No path found to player!");
                    ClearPathOrbs();         
                }
                else
                {
                    Debug.Log("Path found with " + path.Count + " steps.");
                }

            }

            FollowPath();
            if (!CheckPlayerInPoissonDisc() && (pathIndex >= path.Count))
            {
                chasingPlayer = false;
                ClearPathOrbs();
                path = null;
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

        Transform target = waypoints[currentWaypointIndex];
        //GridAStar.Instance.ResetHighlightedToWhite();

        // If path is null, empty, or we�ve reached the end, recalculate
        if (path == null || path.Count == 0 || pathIndex >= path.Count)
        {
            path = GridAStar.Instance.FindPath(transform.position, target.position);
            pathIndex = 0;

            if (path == null || path.Count == 0)
                return; // No path = can't move
        }

        FollowPath();

        if (path != null && path.Count > 0 && Vector3.Distance(transform.position, path[path.Count - 1]) <= reachDistance)
        {
            Debug.Log("Reached waypoint " + currentWaypointIndex);
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
            path = null;
            ClearPathOrbs();
        }
    }

    bool CanSeePlayer()
    {
        if (player == null) return false;

        Vector3 dirToPlayer = (player.position - transform.position).normalized;
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        //Angular forward of the AI
        float angleToPlayer = Vector3.Angle(transform.up, dirToPlayer);
        if (angleToPlayer > viewAngle * 0.5f || distanceToPlayer > viewDistance)
            return false;

        //Using raycast
        RaycastHit2D hit = Physics2D.Raycast(transform.position, dirToPlayer, viewDistance, obstacleMask);
        if (hit.collider != null && hit.collider.transform != player)
            return false;

        return true;
    }



    void FollowPath()
    {
        if (path == null || path.Count == 0 || pathIndex >= path.Count)
            return;

        Vector3 target = path[pathIndex];
        MoveTowards(target);

        if (Vector3.Distance(transform.position, target) <= reachDistance)
            pathIndex++;
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

            float rotationSpeed = 360f;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        transform.position = current + direction * moveSpeed * Time.deltaTime;
    }
    bool CheckPlayerInPoissonDisc()
    {
        if (!player) return false;

        int currentSampleCount = suspicious || chasingPlayer ? alertSampleCount : normalSampleCount;

        //Front cone
        var frontSamples = PoissonDiscSampler.Generate(transform.up, viewAngle, viewDistance, currentSampleCount);
        if (CheckSamplesForPlayer(frontSamples, 1.0f))
            return true;

        //Back cone
        int backSampleCount = Mathf.FloorToInt(currentSampleCount * 0.5f);

        var backSamples = PoissonDiscSampler.Generate(-transform.up, backViewAngle, backViewDistance, backSampleCount);
        if (CheckSamplesForPlayer(backSamples, 0.5f))
            return true;

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
        {
            if (orb) Destroy(orb);
        }
        pathOrbs.Clear();
    }

    private void OnDrawGizmosSelected()
    {
        //if (waypoints == null || waypoints.Length < 2) return;

        //Gizmos.color = Color.yellow;
        //for (int i = 0; i < waypoints.Length; i++)
        //{
        //    if (waypoints[i] != null)
        //    {
        //        Gizmos.DrawSphere(waypoints[i].position, 0.15f);
        //        int next = (i + 1) % waypoints.Length;
        //        if (waypoints[next] != null)
        //            Gizmos.DrawLine(waypoints[i].position, waypoints[next].position);
        //    }
        //}

        #region Cone shaped FOV
        if (player != null)
        {
            Gizmos.color = Color.red;
            Vector3 origin = transform.position;
            Vector3 forward = transform.up;

            float halfAngle = viewAngle / 2f;
            Quaternion leftRayRot = Quaternion.Euler(0, 0, -halfAngle);
            Quaternion rightRayRot = Quaternion.Euler(0, 0, halfAngle);

            Vector3 leftRayDir = leftRayRot * forward;
            Vector3 rightRayDir = rightRayRot * forward;

            Gizmos.DrawLine(origin, origin + leftRayDir * viewDistance);
            Gizmos.DrawLine(origin, origin + rightRayDir * viewDistance);

            // Draw fan rays for debug
            for (int i = 0; i <= rayCount; i++)
            {
                float t = (float)i / rayCount;
                float angle = Mathf.Lerp(-halfAngle, halfAngle, t);
                Vector3 dir = Quaternion.Euler(0, 0, angle) * forward;

                RaycastHit2D hit = Physics2D.Raycast(origin, dir, viewDistance, obstacleMask);
                Vector3 end = hit.collider ? hit.point : origin + dir * viewDistance;

                Gizmos.DrawLine(origin, end);
            }
        }
        #endregion
    }
}
