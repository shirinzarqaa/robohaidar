using UnityEngine;

public class EnvironmentScanner : MonoBehaviour
{
    [Header("Sensor Settings")]
    public int sensorCount = 8;           // Number of sensors around the robot
    public float sensorLength = 5f;       // How far each sensor can detect
    public float sensorRadius = 0.2f;     // Thickness of the sensor ray
    public LayerMask detectionLayers;     // What layers to detect

    [Header("Debug Visualization")]
    public bool showDebugRays = true;     // Whether to draw the rays
    public Color hitColor = Color.red;    // Color when ray hits something
    public Color missColor = Color.green; // Color when ray doesn't hit

    // Array to store detection results
    private RaycastHit[] hitResults;
    private bool[] hasHit;

    void Start()
    {
        hitResults = new RaycastHit[sensorCount];
        hasHit = new bool[sensorCount];
    }

    void Update()
    {
        ScanEnvironment();

        if (showDebugRays)
        {
            VisualizeRays();
        }
    }

    void ScanEnvironment()
    {
        for (int i = 0; i < sensorCount; i++)
        {
            // Calculate direction for this sensor
            float angle = i * (360f / sensorCount);
            Vector3 direction = Quaternion.Euler(0, angle, 0) * transform.forward;

            // Perform the detection
            hasHit[i] = Physics.SphereCast(
                transform.position,
                sensorRadius,
                direction,
                out hitResults[i],
                sensorLength,
                detectionLayers
            );

            if (hasHit[i])
            {
                // Object detected!
                GameObject detectedObject = hitResults[i].collider.gameObject;
                float distance = hitResults[i].distance;

                // You can do something with this information
                // For example, print what was detected
                Debug.Log($"Sensor {i}: Detected {detectedObject.name} at distance {distance:F2}");
            }
        }
    }

    void VisualizeRays()
    {
        for (int i = 0; i < sensorCount; i++)
        {
            float angle = i * (360f / sensorCount);
            Vector3 direction = Quaternion.Euler(0, angle, 0) * transform.forward;

            if (hasHit[i])
            {
                // Draw ray up to hit point
                Debug.DrawRay(transform.position, direction * hitResults[i].distance, hitColor);
            }
            else
            {
                // Draw full-length ray
                Debug.DrawRay(transform.position, direction * sensorLength, missColor);
            }
        }
    }

    // Public method to check if there's an object in a specific direction
    public bool IsObjectDetected(int directionIndex, out float distance, out GameObject detectedObject)
    {
        if (directionIndex < 0 || directionIndex >= sensorCount)
        {
            distance = 0;
            detectedObject = null;
            return false;
        }

        if (hasHit[directionIndex])
        {
            distance = hitResults[directionIndex].distance;
            detectedObject = hitResults[directionIndex].collider.gameObject;
            return true;
        }
        else
        {
            distance = sensorLength;
            detectedObject = null;
            return false;
        }
    }

    // Get the closest object in any direction
    public GameObject GetClosestObject(out float distance, out int direction)
    {
        distance = sensorLength;
        direction = -1;
        GameObject closestObject = null;

        for (int i = 0; i < sensorCount; i++)
        {
            if (hasHit[i] && hitResults[i].distance < distance)
            {
                distance = hitResults[i].distance;
                direction = i;
                closestObject = hitResults[i].collider.gameObject;
            }
        }

        return closestObject;
    }
}