using UnityEngine;
using UnityEngine.InputSystem;

public class CameraInput : MonoBehaviour
{
    [SerializeField] private InputActionReference lookInput;
    [SerializeField] private float sensitivity = 2f;

    private Vector2 look;

    private void OnEnable()
    {
        if (lookInput != null)
        {
            lookInput.action.Enable();
        }
    }

    public Vector2 GetLookDelta()
    {
        return look;
    }

    private void Update()
    {
        if (GameUIManager.Instance != null && !GameUIManager.Instance.CanProcessGameplayInput)
        {
            look = Vector2.zero;
            return;
        }

        if (lookInput == null)
        {
            look = Vector2.zero;
            return;
        }

        look = lookInput.action.ReadValue<Vector2>() * sensitivity;
    }
}
