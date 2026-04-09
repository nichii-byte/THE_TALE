using UnityEngine;

public class ThirdPersonCameraTarget : MonoBehaviour
{
    [SerializeField] private Transform m_followTarget;
    [SerializeField] private CameraInput m_cameraInput;
    [SerializeField] private Vector3 m_followOffset = new Vector3(0f, 1.5f, 0f);
    [SerializeField] private float m_pitchMin = -30f;
    [SerializeField] private float m_pitchMax = 55f;
    [SerializeField] private bool m_invertY;

    private float m_yaw;
    private float m_pitch;

    private void Awake()
    {
        Vector3 eulerAngles = transform.rotation.eulerAngles;
        m_yaw = eulerAngles.y;
        m_pitch = NormalizeAngle(eulerAngles.x);
    }

    private void LateUpdate()
    {
        if (m_followTarget != null)
        {
            transform.position = m_followTarget.position + m_followOffset;
        }

        if (m_cameraInput == null)
            return;

        Vector2 lookInput = m_cameraInput.GetLookDelta();
        float yInput = m_invertY ? lookInput.y : -lookInput.y;

        m_yaw += lookInput.x;
        m_pitch = Mathf.Clamp(m_pitch + yInput, m_pitchMin, m_pitchMax);

        transform.rotation = Quaternion.Euler(m_pitch, m_yaw, 0f);
    }

    private static float NormalizeAngle(float angle)
    {
        if (angle > 180f)
        {
            angle -= 360f;
        }

        return angle;
    }
}
