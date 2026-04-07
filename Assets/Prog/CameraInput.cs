using UnityEngine;
using UnityEngine.InputSystem;

public class CameraInput : MonoBehaviour
{
    public InputActionReference lookInput;
    public float sensitivity = 2f;

    private Vector2 look;

    public Vector2 GetLook()
    {
        return look;
    }

    private void Update()
    {
        look = lookInput.action.ReadValue<Vector2>() * sensitivity;
    }
}
