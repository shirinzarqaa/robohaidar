using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class SimpleMemoryController : MonoBehaviour
{
    public int memorySize = 10; // How many past positions to remember
    public float minDistanceBetweenPoints = 1.0f; // Minimum distance before recording a new position
    public float checkAvoidDistance = 2.0f; // Distance ahead to check for recently visited areas

    private List<Vector3> visitedPositions = new List<Vector3>(); // List of past visited positions
    private RobotController movementController; // Reference to the robot's movement script

    void Start()
    {
        movementController = GetComponent<RobotController>();
        if (movementController == null)
        {
            Debug.LogError("RobotController not found!"); // Error if no movement controller attached
        }
        RecordPosition(transform.position); // Record starting position
    }

    void Update()
    {
        Vector3 currentPosition = transform.position;
        Vector3 lastRecordedPosition = visitedPositions.LastOrDefault();

        // Record position if moved far enough from last point
        if (Vector3.Distance(currentPosition, lastRecordedPosition) > minDistanceBetweenPoints)
        {
            RecordPosition(currentPosition);
        }
    }

    // Record a new position and keep memory size limited
    void RecordPosition(Vector3 position)
    {
        visitedPositions.Add(position);

        if (visitedPositions.Count > memorySize)
        {
            visitedPositions.RemoveAt(0); // Remove oldest position if memory is full
        }
    }

    // Check if a position was visited recently within a radius
    public bool IsPositionRecentlyVisited(Vector3 targetPosition, float radius)
    {
        foreach (Vector3 visitedPos in visitedPositions)
        {
            if (Vector3.Distance(targetPosition, visitedPos) < radius)
            {
                return true;
            }
        }
        return false;
    }

    // Draw debug gizmos in the editor
    void OnDrawGizmos()
    {
        if (visitedPositions.Count > 0)
        {
            Gizmos.color = Color.yellow;
            // Draw lines connecting past visited points
            for (int i = 0; i < visitedPositions.Count - 1; i++)
            {
                Gizmos.DrawLine(visitedPositions[i], visitedPositions[i + 1]);
            }

            Gizmos.color = Color.red;
            // Draw sphere at last recorded position
            Gizmos.DrawSphere(visitedPositions.LastOrDefault(), 0.2f);

            // Visualize the next checkpoint and color it differently if already visited
            Gizmos.color = Color.blue;
            Vector3 checkPoint = transform.position + transform.forward * checkAvoidDistance;
            if (IsPositionRecentlyVisited(checkPoint, minDistanceBetweenPoints)) 
                Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(checkPoint, 0.3f);
        }
    }
}
