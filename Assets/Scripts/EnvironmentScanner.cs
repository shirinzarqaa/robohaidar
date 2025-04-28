using UnityEngine;

public class EnvironmentScanner : MonoBehaviour
{
    [Header("Sensor Settings")]
    public int sensorCount = 8;           
    public float sensorLength = 5f;       
    public float sensorRadius = 0.2f;     
    public LayerMask detectionLayers;     

    [Header("Debug Visualization")]
    public bool showDebugRays = true;     
    public Color hitColor = Color.red;    
    public Color missColor = Color.green; 
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
            float angle = i * (360f / sensorCount);
            Vector3 direction = Quaternion.Euler(0, angle, 0) * transform.forward;
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
                GameObject detectedObject = hitResults[i].collider.gameObject;
                float distance = hitResults[i].distance;
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
                Debug.DrawRay(transform.position, direction * hitResults[i].distance, hitColor);
            }
            else
            {
                Debug.DrawRay(transform.position, direction * sensorLength, missColor);
            }
        }
    }

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