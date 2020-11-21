using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraMovement : MonoBehaviour
{

    public Transform focus;
    public float speed = 1;
    public float distance = 90;

    private float angle = 0;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        angle += speed * Time.deltaTime;
        transform.position = focus.position;
        transform.Rotate(new Vector3(0, 1, 0), speed * Time.deltaTime);
        transform.position = transform.forward * -distance;
    }
}
