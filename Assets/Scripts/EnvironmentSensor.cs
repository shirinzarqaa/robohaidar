using UnityEngine;

public class EnvironmentSensor : MonoBehaviour
{
    public float sensorLength = 5f;
    public int sensorCount = 8;
    public float detectionRadius = 0.2f;

    private RaycastHit[] hitResults;

    void Start()
    {
        hitResults = new RaycastHit[sensorCount];
    }

    void Update()
    {
        ScanEnvironment();
    }

    void ScanEnvironment()
    {
        for (int i = 0; i < sensorCount; i++)
        {
            float angle = i * (360f / sensorCount);
            Vector3 direction = Quaternion.Euler(0, angle, 0) * transform.forward;

            if (Physics.SphereCast(transform.position, detectionRadius, direction, out hitResults[i], sensorLength))
            {
                Debug.DrawRay(transform.position, direction * hitResults[i].distance, Color.red);
                Debug.Log("Detected: " + hitResults[i].collider.gameObject.name +
                          " at distance: " + hitResults[i].distance);
            }
            else
            {
                Debug.DrawRay(transform.position, direction * sensorLength, Color.green);
            }
        }
    }

    public float GetDistanceInDirection(int directionIndex)
    {
        if (directionIndex < 0 || directionIndex >= sensorCount)
            return sensorLength;

        return hitResults[directionIndex].collider != null ? hitResults[directionIndex].distance : sensorLength;
    }
}