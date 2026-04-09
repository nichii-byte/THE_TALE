using UnityEngine;

public class Fil : MonoBehaviour
{
    [SerializeField] private float m_thickness = 0.05f;

    public Vector3 start;
    public Vector3 end;

    public void Init(Vector3 startPos, Vector3 endPos)
    {
        start = startPos;
        end = endPos;

        UpdateFil();
    }

    public void UpdateFil()
    {
        Vector3 direction = end - start;

        transform.position = start + direction / 2f;
        transform.up = direction.normalized;

        float distance = direction.magnitude;
        transform.localScale = new Vector3(m_thickness, distance / 2f, m_thickness);
    }
}
