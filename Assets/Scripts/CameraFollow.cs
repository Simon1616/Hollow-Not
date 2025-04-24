using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    Vector3 offset;
    GameObject Player;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Player = GameObject.FindGameObjectWithTag("Player");
        offset = transform.position - Player.transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        transform.position = Player.transform.position + offset;
    }
}
