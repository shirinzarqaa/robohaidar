using UnityEngine;

public class EnvironmentSensor : MonoBehaviour
{
    public float sensorLength = 5f;        // Maximum distance each sensor can detect
    public int sensorCount = 8;             // Number of sensors evenly distributed around the object
    public float detectionRadius = 0.2f;    // Radius of the sphere used in SphereCast

    private RaycastHit[] hitResults;        // Stores the result of each sensor's SphereCast

    void Start()
    {
        // Initialize the hit results array based on the number of sensors
        hitResults = new RaycastHit[sensorCount];
    }

    void Update()
    {
        // Continuously scan the environment every frame
        ScanEnvironment();
    }

    void ScanEnvironment()
    {
        for (int i = 0; i < sensorCount; i++)
        {
            // Calculate the angle for this sensor
            float angle = i * (360f / sensorCount);
            // Get the direction vector after rotating forward by the angle
            Vector3 direction = Quaternion.Euler(0, angle, 0) * transform.forward;

            // Perform a spherecast in the calculated direction
            if (Physics.SphereCast(transform.position, detectionRadius, direction, out hitResults[i], sensorLength))
            {
                // If hit, draw a red ray and log the detected object's name and distance
                Debug.DrawRay(transform.position, direction * hitResults[i].distance, Color.red);
                Debug.Log("Detected: " + hitResults[i].collider.gameObject.name +
                          " at distance: " + hitResults[i].distance);
            }
            else
            {
                // If no hit, draw a green ray to the maximum sensor length
                Debug.DrawRay(transform.position, direction * sensorLength, Color.green);
            }
        }
    }

    public float GetDistanceInDirection(int directionIndex)
    {
        // Return maximum distance if index is invalid
        if (directionIndex < 0 || directionIndex >= sensorCount)
            return sensorLength;

        // Return detected distance if there's a hit, otherwise maximum distance
        return hitResults[directionIndex].collider != null ? hitResults[directionIndex].distance : sensorLength;
    }
}
