using UnityEngine;
using System.Collections.Generic;

public class OdometryTracker : MonoBehaviour
{
    public float trackingInterval = 0.5f; // How often to record position
    public int maxPositionHistory = 100;  // Maximum number of positions to store
    
    private List<Vector3> positionHistory = new List<Vector3>();
    private float timer = 0f;
    
    // For visualization
    public bool showTrail = true;
    public Color trailColor = Color.blue;
    
    void Update()
    {
        timer += Time.deltaTime;
        
        if (timer >= trackingInterval)
        {
            // Record current position
            RecordPosition();
            timer = 0f;
        }
    }
    
    void RecordPosition()
    {
        positionHistory.Add(transform.position);
        
        // Limit history size
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
        
        for (int i = 0; i < positionHistory.Count - 1; i++)
        {
            Gizmos.DrawLine(positionHistory[i], positionHistory[i + 1]);
        }
    }
    
    public Vector3 GetDistanceTraveled()
    {
        if (positionHistory.Count < 2)
            return Vector3.zero;
            
        return positionHistory[positionHistory.Count - 1] - positionHistory[0];
    }
}