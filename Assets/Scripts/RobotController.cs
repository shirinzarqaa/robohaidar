using UnityEngine;
using System.Collections.Generic;

public class RobotController : MonoBehaviour
{
    public float moveSpeed = 3f;
    public float rotationSpeed = 100f;
    public EnvironmentSensor sensor;

    private bool isMovingRight = false; 
    private float timeSinceDirectionChange = 0f;
    public float directionChangeInterval = 3f; 
    private HashSet<Vector2Int> visitedPositions = new HashSet<Vector2Int>();
    public float gridSize = 1f; 
    private bool isAvoidingObstacle = false;
    private float avoidanceTime = 0f;
    public float obstacleDetectionThreshold = 1.5f; 
    public float obstacleAvoidanceTime = 1f; 

    void Start()
    {
        if (sensor == null)
        {
            sensor = GetComponent<EnvironmentSensor>();
            if (sensor == null)
            {
                Debug.LogError("No EnvironmentSensor component found!");
                enabled = false;
                return;
            }
        }

        MarkCurrentPositionVisited();
    }

    void Update()
    {
        MarkCurrentPositionVisited();
        float frontDistance = sensor.GetDistanceInDirection(0);

        if (isAvoidingObstacle)
        {
            avoidanceTime -= Time.deltaTime;
            if (avoidanceTime <= 0)
            {
                isAvoidingObstacle = false;
                isMovingRight = !isMovingRight;
            }
            else
            {
                RotateToAvoidObstacle();
                MoveForward();
                return;
            }
        }

        if (frontDistance < obstacleDetectionThreshold)
        {
            isAvoidingObstacle = true;
            avoidanceTime = obstacleAvoidanceTime;
            return;
        }

        timeSinceDirectionChange += Time.deltaTime;
        if (timeSinceDirectionChange >= directionChangeInterval)
        {
            timeSinceDirectionChange = 0f;
            isMovingRight = !isMovingRight;
        }

        float rotationDirection = isMovingRight ? 1f : -1f;
        transform.Rotate(0, rotationDirection * rotationSpeed * Time.deltaTime, 0);

        Vector2Int nextPosition = GetPositionAhead();
        if (visitedPositions.Contains(nextPosition))
        {
            TryFindUnvisitedDirection();
        }

        MoveForward();
    }

    private void MoveForward()
    {
        transform.position += transform.forward * moveSpeed * Time.deltaTime;
    }

    private void RotateToAvoidObstacle()
    {
        float rotationDirection = isMovingRight ? -1f : 1f;
        transform.Rotate(0, rotationDirection * rotationSpeed * Time.deltaTime, 0);
    }

    private void MarkCurrentPositionVisited()
    {
        Vector2Int gridPos = WorldToGrid(transform.position);
        visitedPositions.Add(gridPos);
    }

    private Vector2Int WorldToGrid(Vector3 worldPosition)
    {
        return new Vector2Int(
            Mathf.FloorToInt(worldPosition.x / gridSize),
            Mathf.FloorToInt(worldPosition.z / gridSize)
        );
    }

    private Vector2Int GetPositionAhead()
    {
        Vector3 positionAhead = transform.position + transform.forward * gridSize;
        return WorldToGrid(positionAhead);
    }

    private void TryFindUnvisitedDirection()
    {
        for (int i = 0; i < 8; i++)
        {
            transform.Rotate(0, 45f, 0);

            Vector2Int potentialPosition = GetPositionAhead();
            if (!visitedPositions.Contains(potentialPosition))
            {
                return;
            }
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        foreach (Vector2Int pos in visitedPositions)
        {
            Vector3 worldPos = new Vector3(pos.x * gridSize + gridSize / 2, 0.1f, pos.y * gridSize + gridSize / 2);
            Gizmos.DrawCube(worldPos, new Vector3(gridSize * 0.8f, 0.05f, gridSize * 0.8f));
        }
    }
}