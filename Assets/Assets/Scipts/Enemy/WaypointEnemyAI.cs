using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using static GeneralEnemyData;

public class WaypointEnemyAI : MonoBehaviour, IHearingReceiver
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

    #region SoundReceiver
    private Vector3 lastHeardPosition = Vector3.positiveInfinity;

    void OnEnable() => SoundDetectionManager.RegisterListener(this);
    void OnDisable() => SoundDetectionManager.UnregisterListener(this);

    public void OnHearSound(SoundEvent e, float effectiveVolume)
    {
        if (!blackboard.Get<bool>("canHear")) return;

        //Calculate importance based on type and volume
        float importance = effectiveVolume;
        if (e.type == SoundType.Footstep) importance *= 1.5f;

        //Only react if sound is important enough
        if (importance > confidenceThreshold)
        {
            blackboard.Set("heardSound", true);
            blackboard.Set("heardPosition", e.sourcePosition);
            blackboard.Set("heardType", e.type.ToString());
            blackboard.Set("heardVolume", effectiveVolume);
            blackboard.Set("heardFrequency", e.frequency);
            blackboard.Set("heardIsDirect", e.isDirect);

            //If sound is muffled, move more cautiously
            float speedModifier = e.isDirect ? 1f : 0.7f;
            blackboard.Set("investigationSpeed", moveSpeed * speedModifier);
            Debug.Log(gameObject.name + " heard something....");
        }
    }

    public Vector3 GetPosition() => transform.position;
    #endregion

    [SerializeField] private GameObject suspiciousPrefab;
    [SerializeField] private GameObject alertPrefab;

    private GameObject currentIcon;
    private Coroutine iconRoutine;

    private float suspiciousTimer = 0f;
    public float suspiciousCooldown = 2f; // seconds

    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
        blackboard = GetComponent<Blackboard>() ?? gameObject.AddComponent<Blackboard>();
        enemyData = GameObject.FindGameObjectWithTag("EnemyDataController").GetComponent<GeneralEnemyData>();

        blackboard.Set("pathIndex", 0);
        blackboard.Set("canHear", enemyData.canHearSound);
        SoundDetectionManager.occlusionMask = LayerMask.GetMask("Wall");
    }

    void Update()
    {
        if (!player) return;

        viewCone.isVisible = (enemyData.currentAwareness == AwarenessMode.ViewCone);
        circularVision.isVisible = (enemyData.currentAwareness == AwarenessMode.CircularRadius);
        blackboard.Set("canHear", enemyData.canHearSound);

        bool isChasing = UpdateChasingState();
        blackboard.Set("chasingPlayer", isChasing);

        bool heardSound = ProcessHearing(isChasing);
        ProcessMovement(isChasing, heardSound);
        UpdateIconDisplay(isChasing, heardSound);

        if (suspiciousTimer > 0f)
        {
            suspiciousTimer -= Time.deltaTime;
            blackboard.Set("suspicious", true);
        }
        else
        {
            blackboard.Set("suspicious", false);
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
        bool isSuspicious = false;
        bool isAlerted = false;

        bool alreadyChasing = blackboard.Has("chasingPlayer") && blackboard.Get<bool>("chasingPlayer");
        int currentSampleCount = alreadyChasing ? alertSampleCount : normalSampleCount;

        var frontSamples = PoissonDiscSampler.Generate(transform.up, viewAngle, viewDistance, currentSampleCount);
        if (ProcessSamples(frontSamples, 1.0f, out isSuspicious, out isAlerted)) return true;

        int backSampleCount = Mathf.FloorToInt(currentSampleCount * 0.5f);
        var backSamples = PoissonDiscSampler.Generate(-transform.up, backViewAngle, backViewDistance, backSampleCount);
        if (ProcessSamples(backSamples, 0.5f, out isSuspicious, out isAlerted)) return true;

        blackboard.Set("suspicious", false);
        return false;
    }

    bool ProcessSamples(List<PoissonDiscSampler.Sample> samples, float maxConfidence, out bool suspicious, out bool confirmed)
    {
        suspicious = false;
        confirmed = false;

        foreach (var s in samples)
        {
            float confidence = Mathf.Min(s.confidence, maxConfidence);
            if (confidence < confidenceThreshold * 0.5f) continue;

            Vector3 worldSample = transform.position + s.direction * s.radius;
            RaycastHit2D hit = Physics2D.Raycast(transform.position, s.direction, s.radius, obstacleMask | playerMask);

            if (hit.collider != null && hit.transform == player)
            {
                if (confidence >= confidenceThreshold)
                {
                    confirmed = true;
                    blackboard.Set("suspicious", false);
                    return true;// Fully confirmed sighting
                }
                else
                {
                    suspicious = true;
                    blackboard.Set("suspicious", true);
                    suspiciousTimer = suspiciousCooldown;
                    return false;
                }
            }
        }

        blackboard.Set("suspicious", false);
        return false;
    }

    void ClearPathOrbs()
    {
        foreach (var orb in pathOrbs)
            if (orb) Destroy(orb);
        pathOrbs.Clear();
    }
    bool UpdateChasingState()
    {
        switch (enemyData.currentAwareness)
        {
            case AwarenessMode.OmniScient:
                return true;
            case AwarenessMode.ViewCone:
                return blackboard.Get<bool>("viewConePlayerSeen");
            case AwarenessMode.PoissonDisc:
                return CheckPlayerInPoissonDisc();
            case AwarenessMode.CircularRadius:
                return blackboard.Get<bool>("circlePlayerSeen");
            default:
                return false;
        }
    }


    bool ProcessHearing(bool isChasing)
    {
        bool heardSound = enemyData.canHearSound && blackboard.Get<bool>("heardSound");

        if (heardSound && !isChasing)
        {
            Vector3 soundPos = blackboard.Get<Vector3>("heardPosition");
            blackboard.Set("suspicious", true);
            suspiciousTimer = suspiciousCooldown;

            repathTimer += Time.deltaTime;
            if (repathTimer >= repathInterval)
            {
                path = GridAStar.Instance.FindPath(transform.position, soundPos);
                blackboard.Set("path", path);
                blackboard.Set("pathIndex", 0);
                repathTimer = 0f;
                ClearPathOrbs();
            }

            FollowPath();

            int pathIdx = blackboard.Get<int>("pathIndex");
            if (path != null && pathIdx >= path.Count)
            {
                blackboard.Set("heardSound", false);
                path = null;
                ClearPathOrbs();
            }
        }

        return heardSound;
    }

    void ProcessMovement(bool isChasing, bool heardSound)
    {
        if (isChasing)
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
        else if (!heardSound)
        {
            Patrol();
        }
    }

    void UpdateIconDisplay(bool isChasing, bool heardSound)
    {
        bool showSuspicious = (blackboard.Get<bool>("suspicious") || heardSound) && !isChasing;

        if (isChasing)
        {
            TryShowIcon(alertPrefab);
        }
        else if (showSuspicious && suspiciousTimer > 0)
        {
            TryShowIcon(suspiciousPrefab);
        }
        else
        {
            HideIcon();
        }
    }

    void TryShowIcon(GameObject prefab)
    {
        if (currentIcon != null && currentIcon.name.StartsWith(prefab.name))
            return;

        HideIcon();
        if (prefab == null) return;

        currentIcon = Instantiate(prefab, transform);
        currentIcon.name = prefab.name + "(Clone)";
        currentIcon.transform.localPosition = Vector3.up * 1.5f;

        var renderer = currentIcon.GetComponent<SpriteRenderer>();
        if (renderer != null) renderer.sortingOrder = 100;
    }

    void HideIcon()
    {
        if (currentIcon != null)
        {
            Destroy(currentIcon);
            currentIcon = null;
        }
    }

}
