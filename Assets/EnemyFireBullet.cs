using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyFireBullet : MonoBehaviour
{
    Blackboard blackboard;
    [SerializeField] private GameObject bullet;
    [SerializeField] private float bulletSpeed = 0f;
    [SerializeField] private float cooldown = 0f;
    float cooldownTimer = 0f;
    // Start is called before the first frame update
    void Start()
    {
        blackboard = GetComponent<Blackboard>();
    }

    // Update is called once per frame
    void Update()
    {
        cooldownTimer -= Time.deltaTime;
        if (blackboard.Get<bool>("chasingPlayer") == true)
        {
            float dist = (transform.position - blackboard.Get<Vector3>("playerPosition")).magnitude;
            if (dist < GetComponent<WaypointEnemyAI>().viewDistance * 0.5f && cooldownTimer <= 0f)
            {
                GameObject obj = Instantiate(bullet, transform.position, Quaternion.identity);
                obj.GetComponent<Bullet>().dir = transform.up;
                obj.GetComponent<Bullet>().speed = bulletSpeed;
                obj.GetComponent<Bullet>().canHearSound = GetComponent<WaypointEnemyAI>().enemyData.canHearSound;
                obj.tag = "Enemy";
                cooldownTimer = cooldown;
            }

        }
    }
}
