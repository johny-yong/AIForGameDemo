using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollowPlayer : MonoBehaviour
{
    public float zoomInVal = 7;
    public float zoomOutVal = 22;

    private bool followPlayer = false;
    private GameObject player;
    // Start is called before the first frame update
    void Start()
    {
        GameObject[] playerItem = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject item in playerItem)
        {
            if (item.name == "Player")
            {
                player = item;
                break;
            }
        }
        followPlayer = true;
    }

    // Update is called once per frame
    void Update()
    {
        if (player == null)
            return;
        if (followPlayer)
        {
            transform.position = new Vector3(player.transform.position.x, player.transform.position.y, -10f);
            GetComponent<Camera>().orthographicSize = zoomInVal;
        }
        else
        {
            transform.position = new Vector3(22f, 14f, -10f);
            GetComponent<Camera>().orthographicSize = zoomOutVal;
        }

        if (Input.GetKeyDown(KeyCode.F9))
            followPlayer = !followPlayer;
    }
}
