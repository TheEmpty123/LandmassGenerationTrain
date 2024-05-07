using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

public class MouseMovement : MonoBehaviour
{

    public float mouseSensitivity = 100f;
    public Transform playerHead;

    float xRotation = 0;
    float yRotation = 0;

    // Start is called before the first frame update
    void Start()
    {
        //Locking the cursor to the middle of the screen and making it invisible;
        Cursor.lockState = CursorLockMode.Locked;    
    }

    // Update is called once per frame
    void Update()
    {
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

            //control rotation around x axis (look up and down)
            xRotation -= mouseY;

            //we clamp the rotation so we cant Over-rotate
            xRotation = Mathf.Clamp(xRotation, -90f, 90f);

            //control rotation around y axis
            yRotation += mouseX;

            //applying both rotation
            transform.localRotation = Quaternion.Euler(0f, yRotation, 0f);
            playerHead.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }
}
