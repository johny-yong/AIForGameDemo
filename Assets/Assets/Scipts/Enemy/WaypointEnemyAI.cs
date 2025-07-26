using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;
using UnityEngine.XR;
using static GeneralEnemyData;

public class WaypointEnemyAI : MonoBehaviour, IHearingReceiver
{
    [Header("Patrol Settings")]
    public Transform[] waypoints;
    public float moveSpeed = 3f;
    public float reachDistance = 0.1f;
    public Color waypointColor = Color.black;

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
    public LayerMask healthPackMask;
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

    [Header("Health Pack Sharing")]
    public float healthPackMemoryTimeout = 15f; // How long to remember health pack locations

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
        if (e.type == SoundType.Footstep)
            importance *= 1.5f;
        else if (e.type == SoundType.GunShot)
            importance *= 1.2f;

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

        // Initialize health pack memory storage
        if (!blackboard.Has("knownHealthPacks"))
        {
            blackboard.Set("knownHealthPacks", new Dictionary<GameObject, float>());
        }

    }

    void Update()
    {
        if (!player) return;

        viewCone.isVisible = (enemyData.currentAwareness == AwarenessMode.ViewCone);
        circularVision.isVisible = (enemyData.currentAwareness == AwarenessMode.CircularRadius);
        viewCone.useGaussian = enemyData.useGaussianRandomness;
        viewCone.showRayLines = enemyData.showRayLines;
        blackboard.Set("canHear", enemyData.canHearSound);


        //Always detect health packs and enemies regardless of current state
        bool isChasing = false;
        bool heardSound =false;
        DetectAndShareObjects();
        if (blackboard.Has("getLatestPlayerPosition") && blackboard.Get<bool>("getLatestPlayerPosition"))
        {
            isChasing = true;
            ProcessMovement(isChasing, heardSound);
            UpdateIconDisplay(isChasing, heardSound);
        }
        else
        {
            isChasing = UpdateChasingState();
            blackboard.Set("chasingPlayer", isChasing);

            heardSound = ProcessHearing(isChasing);

            //Chasing Healthpack logic
            bool seekingHealthPack = blackboard.Has("seekingHealthPack") && blackboard.Get<bool>("seekingHealthPack");
            if (seekingHealthPack && HandleHealthPackMovement()) return;// Skip normal behavior while seeking health pack

            ProcessMovement(isChasing, heardSound);
            UpdateIconDisplay(isChasing, heardSound);
        }

        if (suspiciousTimer > 0f)
        {
            suspiciousTimer -= Time.deltaTime;
            blackboard.Set("suspicious", true);
        }
        else
        {
            blackboard.Set("suspicious", false);
        }

        // Clean up expired health pack memories
        CleanupExpiredHealthPackMemories();
    }

    void DetectAndShareObjects()
    {
        var (detectedEnemy, detectedHealthPack) = GetDetectedObjects();

        // Always store detected health pack
        if (detectedHealthPack != null)
        {
            foreach (var healthpack in detectedHealthPack)
            {
                StoreHealthPackInMemory(healthpack);
            }
        }

        // Share intel with detected enemy
        if (detectedEnemy != null)
        {
            foreach (var enemy in detectedEnemy)
            {
                Blackboard enemyBlackboard = enemy.GetComponent<Blackboard>();
                if (enemyBlackboard != null)
                {
                    ShareHealthPackIntel(enemyBlackboard);
                    SharePlayerIntel(enemyBlackboard);
                }
            }
        }
    }


    bool HandleHealthPackMovement()
    {
        GameObject targetHealthPack = blackboard.Get<GameObject>("targetHealthPack");

        // Check if health pack still exists
        if (targetHealthPack == null)
        {
            // Health pack was destroyed/picked up by someone else, stop seeking
            blackboard.Set("seekingHealthPack", false);
            path = null;
            ClearPathOrbs();

            // Remove from memory if it was destroyed
            RemoveHealthPackFromMemory(targetHealthPack);
            return false; // Return to normal behavior
        }

        // Handle pathfinding to health pack
        repathTimer += Time.deltaTime;

        if (repathTimer >= repathInterval)
        {
            path = GridAStar.Instance.FindPath(transform.position, targetHealthPack.transform.position);
            blackboard.Set("path", path);
            blackboard.Set("pathIndex", 0);
            repathTimer = 0f;
            ClearPathOrbs();

            // Visualize health pack path with green orbs
            if (enemyData.showDottedPath && path != null)
            {
                foreach (Vector3 pos in path)
                {
                    GameObject orb = Instantiate(pathOrbPrefab, pos, Quaternion.identity);
                    orb.transform.localScale *= 3f;
                    orb.GetComponent<SpriteRenderer>().color = Color.green; // Green for health pack path
                    pathOrbs.Add(orb);
                }
            }
        }

        FollowPath();

        // Check if we reached the health pack area
        if (Vector3.Distance(transform.position, targetHealthPack.transform.position) <= reachDistance * 2f)
        {
            // Health pack should be automatically picked up by the HealthPack script when we get close
            blackboard.Set("seekingHealthPack", false);
            path = null;
            ClearPathOrbs();

            // Remove from memory since we picked it up
            RemoveHealthPackFromMemory(targetHealthPack);
            return false; // Return to normal behavior after reaching health pack
        }

        return true; // Continue seeking health pack
    }

    //HealthPack or Chase Player
    void HandleBehaviorSimplified()
    {
        EnemyStats enemyStats = GetComponent<EnemyStats>();
        bool needsHealth = enemyStats.currentHealth <= enemyStats.maxHealth * 0.5f; // only search for healthpack once below 50%

        // If we need health, try to find a health pack from memory
        if (needsHealth)
        {
            GameObject targetHealthPack = FindBestHealthPack(null); // Check memory only
            if (targetHealthPack != null)
            {
                blackboard.Set("targetHealthPack", targetHealthPack);
                blackboard.Set("seekingHealthPack", true);
                return;
            }
        }

        blackboard.Set("seekingHealthPack", false);

        // Determine chasing behavior based on direct sight or recent intel
        if (blackboard.Get<bool>("chasingPlayer"))
        {
            blackboard.Set("lastKnownPlayerPosition", player.position);
            blackboard.Set("lastKnownPlayerTime", Time.time);
        }
    }

    void SharePlayerIntel(Blackboard enemyBlackboard)
    {
        // Determine who has the most recent intel
        float thisLastSeen = blackboard.Has("lastKnownPlayerTime") ? blackboard.Get<float>("lastKnownPlayerTime") : -1f;
        float otherLastSeen = enemyBlackboard.Has("lastKnownPlayerTime") ? enemyBlackboard.Get<float>("lastKnownPlayerTime") : -1f;

        // Share intel if either enemy is chasing and has more recent information
        if (thisLastSeen > otherLastSeen)
        {
            // This enemy shares intel to other enemy
            enemyBlackboard.Set("lastKnownPlayerPosition", blackboard.Get<Vector3>("lastKnownPlayerPosition"));
            enemyBlackboard.Set("lastKnownPlayerTime", thisLastSeen);
            enemyBlackboard.Set("getLatestPlayerPosition", true);
            Debug.Log($"{name} shared player intel with {enemyBlackboard.gameObject.name}");
        }
        else if (otherLastSeen > thisLastSeen)
        {
            // Other enemy shares intel to this enemy
            blackboard.Set("lastKnownPlayerPosition", enemyBlackboard.Get<Vector3>("lastKnownPlayerPosition"));
            blackboard.Set("lastKnownPlayerTime", otherLastSeen);
            blackboard.Set("getLatestPlayerPosition", true);
            Debug.Log($"{enemyBlackboard.gameObject.name} shared player intel with {name}");
        }
    }

    void ShareHealthPackIntel(Blackboard enemyBlackboard)
    {
        // Get both enemies' health pack memories
        var thisHealthPacks = blackboard.Get<Dictionary<GameObject, float>>("knownHealthPacks");

        if (!enemyBlackboard.Has("knownHealthPacks"))
        {
            enemyBlackboard.Set("knownHealthPacks", new Dictionary<GameObject, float>());
        }
        var otherHealthPacks = enemyBlackboard.Get<Dictionary<GameObject, float>>("knownHealthPacks");

        // Share this enemy's health pack knowledge with the other enemy
        foreach (var kvp in thisHealthPacks)
        {
            GameObject healthPack = kvp.Key;
            float timeDiscovered = kvp.Value;

            // Only share if the health pack still exists and the other enemy doesn't know about it
            // or if we discovered it more recently
            if (healthPack != null &&
                (!otherHealthPacks.ContainsKey(healthPack) || otherHealthPacks[healthPack] < timeDiscovered))
            {
                otherHealthPacks[healthPack] = timeDiscovered;
                Debug.Log($"{name} shared health pack location with {enemyBlackboard.gameObject.name}");
            }
        }

        // Share the other enemy's health pack knowledge with this enemy
        foreach (var kvp in otherHealthPacks)
        {
            GameObject healthPack = kvp.Key;
            float timeDiscovered = kvp.Value;

            // Only accept if the health pack still exists and we don't know about it
            // or if they discovered it more recently
            if (healthPack != null &&
                (!thisHealthPacks.ContainsKey(healthPack) || thisHealthPacks[healthPack] < timeDiscovered))
            {
                thisHealthPacks[healthPack] = timeDiscovered;
                Debug.Log($"{enemyBlackboard.gameObject.name} shared health pack location with {name}");
            }
        }
    }

    void StoreHealthPackInMemory(GameObject healthPack)
    {
        if (healthPack == null) return;

        var knownHealthPacks = blackboard.Get<Dictionary<GameObject, float>>("knownHealthPacks");
        if (!knownHealthPacks.ContainsKey(healthPack))
        {
            knownHealthPacks[healthPack] = Time.time;
            Debug.Log($"{name} discovered health pack at {healthPack.transform.position}");
        }
    }

    void RemoveHealthPackFromMemory(GameObject healthPack)
    {
        if (healthPack == null) return;

        var knownHealthPacks = blackboard.Get<Dictionary<GameObject, float>>("knownHealthPacks");
        if (knownHealthPacks.ContainsKey(healthPack))
        {
            knownHealthPacks.Remove(healthPack);
            Debug.Log($"{name} removed health pack from memory");
        }
    }

    void CleanupExpiredHealthPackMemories()
    {
        var knownHealthPacks = blackboard.Get<Dictionary<GameObject, float>>("knownHealthPacks");
        var healthPacksToRemove = new List<GameObject>();

        foreach (var kvp in knownHealthPacks)
        {
            GameObject healthPack = kvp.Key;
            float timeDiscovered = kvp.Value;

            // Remove if health pack no longer exists or memory has expired
            if (healthPack == null || Time.time - timeDiscovered > healthPackMemoryTimeout)
            {
                healthPacksToRemove.Add(healthPack);
            }
        }

        foreach (var healthPack in healthPacksToRemove)
        {
            knownHealthPacks.Remove(healthPack);
        }
    }

    GameObject FindBestHealthPack(GameObject directlyDetectedHealthPack)
    {
        // If we can see a health pack directly, prioritize it
        if (directlyDetectedHealthPack != null)
        {
            return directlyDetectedHealthPack;
        }

        // Otherwise, look through our memory for the closest valid health pack
        var knownHealthPacks = blackboard.Get<Dictionary<GameObject, float>>("knownHealthPacks");
        GameObject closestHealthPack = null;
        float closestDistance = float.MaxValue;

        foreach (var kvp in knownHealthPacks)
        {
            GameObject healthPack = kvp.Key;

            // Skip if health pack no longer exists
            if (healthPack == null) continue;

            float distance = Vector3.Distance(transform.position, healthPack.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestHealthPack = healthPack;
            }
        }

        return closestHealthPack;
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

    //Even more efficient: detect both enemies and health packs in one pass
    (List<GameObject> detectedEnemy, List<GameObject> detectedHealthPack) GetDetectedObjects()
    {
        bool isSuspicious = blackboard.Has("suspicious") && blackboard.Get<bool>("suspicious");
        bool isChasing = blackboard.Has("chasingPlayer") && blackboard.Get<bool>("chasingPlayer");
        int currentSampleCount = isSuspicious || isChasing ? alertSampleCount : normalSampleCount;

        var frontSamples = PoissonDiscSampler.Generate(transform.up, viewAngle, viewDistance, currentSampleCount);
        var(frontEnemy, frontHealthPack) = CheckSamplesForBothObjects(frontSamples, 1.0f);

        if (frontEnemy != null || frontHealthPack != null)
        {
            return (frontEnemy, frontHealthPack);
        }

        int backSampleCount = Mathf.FloorToInt(currentSampleCount * 0.5f);
        var backSamples = PoissonDiscSampler.Generate(-transform.up, backViewAngle, backViewDistance, backSampleCount);
        var (backEnemy, backHealthPack) = CheckSamplesForBothObjects(backSamples, 0.5f);

        return (backEnemy, backHealthPack);
    }

    //Check for both enemies and health packs in a single loop
    (List<GameObject> enemy, List<GameObject> healthPack) CheckSamplesForBothObjects(List<PoissonDiscSampler.Sample> samples, float maxConfidence)
    {
        List<GameObject> detectedEnemy = new List<GameObject>();
        List<GameObject> detectedHealthPack = new List<GameObject>();

        foreach (var s in samples)
        {
            Vector3 worldSample = transform.position + s.direction * s.radius;

            // Check for enemies
            RaycastHit2D[] enemyHits = Physics2D.RaycastAll(transform.position, s.direction, s.radius, enemyMask);
            foreach (var enemyHit in enemyHits)
            {
                if (enemyHit.transform.gameObject == gameObject)
                    continue;

                if (enemyHit.collider != null && enemyHit.transform.CompareTag("Enemy"))
                {
                    RaycastHit2D wallRayCheck = Physics2D.Raycast(transform.position, s.direction, enemyHit.distance);
                    if (wallRayCheck.collider.tag == "Wall")
                        continue;
                    if (!detectedEnemy.Contains(enemyHit.transform.gameObject))
                        detectedEnemy.Add(enemyHit.transform.gameObject);
                }
            }

            // Check for health packs
            RaycastHit2D[] healthHits = Physics2D.RaycastAll(transform.position, s.direction, s.radius, obstacleMask | healthPackMask);
            foreach (var health in healthHits)
            {
                if (health.collider != null && health.transform.CompareTag("HealthPack"))
                {
                    if (!detectedHealthPack.Contains(health.transform.gameObject))
                        detectedHealthPack.Add(health.transform.gameObject);
                }
            }
        }

        return (detectedEnemy, detectedHealthPack);
    }


void ClearPathOrbs()
    {
        foreach (var orb in pathOrbs)
            if (orb) Destroy(orb);
        pathOrbs.Clear();
    }
    bool UpdateChasingState()
    {
        HandleBehaviorSimplified();   //Choose Health Pack or Chase Player
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
            else if (path == null)
            {
                blackboard.Set("heardSound", false);
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
                if (blackboard.Get<bool>("getLatestPlayerPosition"))
                    path = GridAStar.Instance.FindPath(transform.position, blackboard.Get<Vector3>("lastKnownPlayerPosition"));
                else
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
                blackboard.Set("getLatestPlayerPosition", false);
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

    void OnDrawGizmos()
    {
        if (waypoints == null || waypoints.Length < 2) return;

        Gizmos.color = waypointColor;
        for (int i = 0; i < waypoints.Length - 1; i++)
        {
            Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
        }
    }

    //bool SharingIntelBetweenEnemies(bool canSeePlayerDirectly, GameObject detectedEnemy)
    //{
    //    // Always check if we can see another enemy and share intel
    //    if (detectedEnemy != null)
    //    {
    //        Blackboard enemyBlackboard = detectedEnemy.GetComponent<Blackboard>();
    //        if (enemyBlackboard != null)
    //        {
    //            bool thisEnemyChasing = blackboard.Has("chasingPlayer") && blackboard.Get<bool>("chasingPlayer");
    //            bool otherEnemyChasing = enemyBlackboard.Has("chasingPlayer") && enemyBlackboard.Get<bool>("chasingPlayer");

    //            // Share player intel
    //            SharePlayerIntel(enemyBlackboard, thisEnemyChasing, otherEnemyChasing);

    //            // Share health pack intel
    //            ShareHealthPackIntel(enemyBlackboard);
    //        }
    //    }

    //    // Determine if we should be chasing
    //    // If we can see the player directly, update last known position
    //    if (canSeePlayerDirectly)
    //    {
    //        blackboard.Set("lastKnownPlayerPosition", player.position);
    //        blackboard.Set("lastKnownPlayerTime", Time.time);
    //        return true; // We are chasing if we can see the player directly
    //    }
    //    // Can't see player directly - check if we should chase based on recent intel
    //    if (blackboard.Has("lastKnownPlayerTime"))
    //    {
    //        float lastSeenTime = blackboard.Get<float>("lastKnownPlayerTime");
    //        float timeSinceLastSeen = Time.time - lastSeenTime;
    //        return timeSinceLastSeen < lastKnownPositionTimeout;
    //    }

    //    return false; // No recent intel, don't chase
    //}
}
