using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bullet : MonoBehaviour
{
    [HideInInspector] public Vector3 dir = Vector3.zero;
    public float speed = 5f;

    void Update()
    {
        transform.position += dir * speed * Time.deltaTime;
        GetComponent<SoundEmitter>().EmitSound();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.layer == LayerMask.NameToLayer("Wall"))
            Destroy(gameObject);
    }
}

