using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

//Basic controls
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5f;
    public GameObject bullet;
    private Rigidbody2D rb;                      
    private Vector2 movement;                    
    private SoundEmitter emitter;
    private GeneralEnemyData enemyData;
    // Start is called before the first frame update
    void Start()
    {
        enemyData = GameObject.FindGameObjectWithTag("EnemyDataController").GetComponent<GeneralEnemyData>();
        rb = GetComponent<Rigidbody2D>();
        emitter = GetComponent<SoundEmitter>();
    }

    // Update is called once per frame
    void Update()
    {
        movement.x = Input.GetAxisRaw("Horizontal");
        movement.y = Input.GetAxisRaw("Vertical");
        movement = movement.normalized;

        if (Input.GetMouseButtonDown(0))
        {
            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mouseWorldPos.z = 0f;

            Vector3 direction = (mouseWorldPos - transform.position).normalized;

            GameObject obj = Instantiate(bullet, transform.position, Quaternion.identity);
            obj.GetComponent<Bullet>().dir = direction;
            obj.tag = "Player";
        }

        if (movement.sqrMagnitude > 0.001f)
        {
            if (enemyData.canHearSound)
            {
                emitter.EmitSound();
            }
        }
    }

    //When using rigidbody, using FixedUpdate is better
    void FixedUpdate()
    {
        rb.MovePosition(rb.position + movement * moveSpeed * Time.fixedDeltaTime);
    }
}
