using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Bullet : MonoBehaviour
{
    [HideInInspector] public Vector3 dir = Vector3.zero;
    [HideInInspector] public bool canHearSound = false;
    public float speed = 5f;
    

    private void Start()
    {
        GetComponent<SoundEmitter>().baseVolume = 2f;
    }

    void Update()
    {
        transform.position += dir * speed * Time.deltaTime;
        if (canHearSound)
        {
            GetComponent<SoundEmitter>().EmitSound();
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.layer == LayerMask.NameToLayer("Wall"))
            Destroy(gameObject);
        else if (!collision.gameObject.CompareTag(gameObject.tag))
        {
            if (collision.tag == "Enemy")
            {
                collision.GetComponent<EnemyStats>().Heal(-10);
                if (collision.GetComponent<EnemyStats>().currentHealth <= 0)
                    Destroy(collision.gameObject);
            }
            else
            {
                collision.GetComponent<PlayerStats>().Heal(-10);
                if (collision.GetComponent<PlayerStats>().currentHealth <= 0)
                    SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
            Destroy(gameObject);
        }
    }
}

