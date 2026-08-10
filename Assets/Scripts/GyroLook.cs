using UnityEngine;

public class GyroLook : MonoBehaviour
{
    public float sensitivity = 3f;
    float _yaw, _pitch;

    void Update()
    {
        if (Input.GetMouseButton(1))
        {
            _yaw += Input.GetAxis("Mouse X") * sensitivity;
            _pitch -= Input.GetAxis("Mouse Y") * sensitivity;
            _pitch = Mathf.Clamp(_pitch, -80f, 80f);
            transform.localRotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }
    }
}