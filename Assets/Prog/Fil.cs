using UnityEngine;

public class Fil : MonoBehaviour
{
    [SerializeField] private float m_thickness = 0.05f;

    private Vector3 m_start;
    private Vector3 m_end;

    public void Init(Vector3 start, Vector3 end)
    {
        m_start = start;
        m_end = end;

        UpdateFil();
    }

    public void UpdateFil()
    {
        Vector3 direction = m_end - m_start;

        transform.position = m_start + direction / 2f;
        transform.up = direction.normalized;

        float distance = direction.magnitude;
        transform.localScale = new Vector3(m_thickness, distance / 2f, m_thickness);
    }
}
