using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//AI Logic
//Uses Waypoint for idling (Need ownself set the position of the waypoints per AI)
//Uses A* for shortest distance to move between way points as well as the player chasing
//Currently when the player is touched, get back to previous (next) waypoint

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

    public ViewConeRenderer viewCone;

    private Blackboard blackboard;
    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
        //Automatically adding the blackboard
        blackboard = GetComponent<Blackboard>();
        if (blackboard == null) { 
            blackboard = gameObject.AddComponent<Blackboard>();
        }
    }

    void Update()
    {
        if (!player) return;

        viewCone.isVisible = true;


        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        chasingPlayer = viewCone.playerInSight;

        if (chasingPlayer)
        {
            repathTimer += Time.deltaTime;

            if (repathTimer >= repathInterval)
            {
                path = GridAStar.Instance.FindPath(transform.position, player.position);
                pathIndex = 0;
                repathTimer = 0f;

                if (path == null)
                    Debug.LogWarning("No path found to player!");
                else
                    Debug.Log("Path found with " + path.Count + " steps.");
            }

            FollowPath();
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

        // If path is null, empty, or we’ve reached the end, recalculate
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
