using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class SimpleMemoryController : MonoBehaviour
{
    public int memorySize = 10;
    public float minDistanceBetweenPoints = 1.0f;
    public float checkAvoidDistance = 2.0f;

    private List<Vector3> visitedPositions = new List<Vector3>();
    private RobotController movementController;

    void Start()
    {
        movementController = GetComponent<RobotController>();
        if (movementController == null)
        {
            Debug.LogError("RobotController tidak ditemukan!");
        }
        RecordPosition(transform.position);
    }

    void Update()
    {
        Vector3 currentPosition = transform.position;
        Vector3 lastRecordedPosition = visitedPositions.LastOrDefault();

        if (Vector3.Distance(currentPosition, lastRecordedPosition) > minDistanceBetweenPoints)
        {
            RecordPosition(currentPosition);
        }
    }

    void RecordPosition(Vector3 position)
    {
        visitedPositions.Add(position);

        if (visitedPositions.Count > memorySize)
        {
            visitedPositions.RemoveAt(0);
        }
    }

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

    void OnDrawGizmos()
    {
        if (visitedPositions.Count > 0)
        {
            Gizmos.color = Color.yellow;
            for (int i = 0; i < visitedPositions.Count - 1; i++)
            {
                Gizmos.DrawLine(visitedPositions[i], visitedPositions[i + 1]);
            }
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(visitedPositions.LastOrDefault(), 0.2f);

            Gizmos.color = Color.blue;
            Vector3 checkPoint = transform.position + transform.forward * checkAvoidDistance;
             if(IsPositionRecentlyVisited(checkPoint, minDistanceBetweenPoints)) Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(checkPoint, 0.3f);
        }
    }
}
