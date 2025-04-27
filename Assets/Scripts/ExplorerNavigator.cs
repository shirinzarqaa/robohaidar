using UnityEngine;
using System.Collections.Generic;

public class ExplorerNavigator : MonoBehaviour
{
    public OdometryTracker odometry;
    public float forwardSpeed = 1f;
    public float turnSpeed = 0.5f;
    public float waypointThreshold = 0.3f;
    public float angleThreshold = 15f; // Increased for smoother turns
    public float detectionRange = 3f;
    public LayerMask bombLayer;

    public Vector2 areaMin = new Vector2(-5, -5);
    public Vector2 areaMax = new Vector2(5, 5);
    public float stepSize = 1f;

    private List<Vector2> searchWaypoints;
    private int currentWaypoint = 0;

    private List<Vector3> detectedBombs = new();
    private Vector3? currentTarget = null;
    private bool goingToBomb = false;

    void Start()
    {
        searchWaypoints = GenerateZigZagPattern(areaMin, areaMax, stepSize);
    }

    void FixedUpdate()
    {
        if (goingToBomb && currentTarget.HasValue)
        {
            NavigateTo(currentTarget.Value);

            if (Vector2.Distance(odometry.estimatedPosition, new Vector2(currentTarget.Value.x, currentTarget.Value.z)) < waypointThreshold)
            {
                Debug.Log("Reached bomb at: " + currentTarget.Value);
                MarkBomb(currentTarget.Value);
                detectedBombs.Remove(currentTarget.Value);
                currentTarget = null;
                goingToBomb = false;
            }
        }
        else
        {
            if (currentWaypoint < searchWaypoints.Count)
            {
                Vector2 target = searchWaypoints[currentWaypoint];
                NavigateTo(new Vector3(target.x, 0, target.y));

                if (Vector2.Distance(odometry.estimatedPosition, target) < waypointThreshold)
                    currentWaypoint++;

                ScanForBombs();
            }
            else
            {
                Debug.Log("Exploration complete!");
            }
        }
    }

    void NavigateTo(Vector3 worldTarget)
    {
        Vector2 current = odometry.estimatedPosition;
        Vector2 target = new Vector2(worldTarget.x, worldTarget.z);
        Vector2 direction = target - current;

        float angleToTarget = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        // Offset estimatedOrientation to match Unity's +Z forward
        float currentAngle = Mathf.Rad2Deg * (odometry.estimatedOrientation - Mathf.PI / 2f);

        angleToTarget = NormalizeAngle(angleToTarget);
        currentAngle = NormalizeAngle(currentAngle);

        float angleError = Mathf.DeltaAngle(currentAngle, angleToTarget);

        // Debug log for checking angles
        Debug.Log($"Target Angle: {angleToTarget}, Current Angle: {currentAngle}, Error: {angleError}");

        if (Mathf.Abs(angleError) > angleThreshold)
        {
            // Rotate
            odometry.SetWheelSpeeds(-turnSpeed, turnSpeed);
        }
        else
        {
            // Move forward
            odometry.SetWheelSpeeds(forwardSpeed, forwardSpeed);
        }
    }

    float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }

    void ScanForBombs()
    {
        Vector3[] directions = {
            transform.forward, -transform.forward,
            transform.right, -transform.right
        };

        foreach (var dir in directions)
        {
            if (Physics.Raycast(transform.position, dir, out RaycastHit hit, detectionRange, bombLayer))
            {
                if (hit.collider.CompareTag("Bomb") && !detectedBombs.Contains(hit.point))
                {
                    Debug.Log("Bomb detected!");
                    detectedBombs.Add(hit.point);
                    currentTarget = hit.point;
                    goingToBomb = true;
                    break;
                }
            }
        }
    }

    void MarkBomb(Vector3 pos)
    {
        Debug.DrawRay(pos, Vector3.up * 2, Color.red, 2f);
    }

    List<Vector2> GenerateZigZagPattern(Vector2 min, Vector2 max, float step)
    {
        List<Vector2> pattern = new();
        bool flip = false;

        for (float y = min.y; y <= max.y; y += step)
        {
            if (!flip)
            {
                for (float x = min.x; x <= max.x; x += step)
                    pattern.Add(new Vector2(x, y));
            }
            else
            {
                for (float x = max.x; x >= min.x; x -= step)
                    pattern.Add(new Vector2(x, y));
            }
            flip = !flip;
        }
        return pattern;
    }
}
