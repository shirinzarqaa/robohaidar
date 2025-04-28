using UnityEngine;
using System.Collections.Generic;

public class OdometryTracker : MonoBehaviour
{
    public float trackingInterval = 0.5f; // Time between position samples
    public int maxPositionHistory = 100;  // Max number of saved positions

    private List<Vector3> positionHistory = new List<Vector3>();
    private float timer = 0f;
    public bool showTrail = true;          // Whether to visualize the trail
    public Color trailColor = Color.blue;  // Color of the trail

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= trackingInterval)
        {
            RecordPosition();
            timer = 0f;
        }
    }

    void RecordPosition()
    {
        positionHistory.Add(transform.position);
        
        // Limit the history size
        if (positionHistory.Count > maxPositionHistory)
        {
            positionHistory.RemoveAt(0);
        }
    }

    void OnDrawGizmos()
    {
        if (!showTrail || positionHistory.Count < 2)
            return;

        Gizmos.color = trailColor;

        // Draw trail lines between recorded positions
        for (int i = 0; i < positionHistory.Count - 1; i++)
        {
            Gizmos.DrawLine(positionHistory[i], positionHistory[i + 1]);
        }
    }

    public Vector3 GetDistanceTraveled()
    {
        if (positionHistory.Count < 2)
            return Vector3.zero;

        // Return vector from first to last position
        return positionHistory[positionHistory.Count - 1] - positionHistory[0];
    }
}
