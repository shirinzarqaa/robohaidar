using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObstacleAvoidance : MonoBehaviour
{
    public float detectionRange = 1.0f;   // Distance to detect obstacles ahead
    public LayerMask obstacleLayer;       // Layer to identify obstacles

    void Update()
    {
        AvoidObstacle();
    }

    void AvoidObstacle()
    {
        Vector3 forward = transform.TransformDirection(Vector3.forward);

        // Check if obstacle is directly ahead
        if (Physics.Raycast(transform.position, forward, detectionRange, obstacleLayer))
        {
            Debug.Log("Obstacle Detected! Trying to avoid...");
            TryChangeDirection();
        }
    }

    void TryChangeDirection()
    {
        Vector3 right = transform.TransformDirection(Vector3.right);

        // Try turning right if no obstacle
        if (!Physics.Raycast(transform.position, right, detectionRange, obstacleLayer))
        {
            transform.Rotate(0, 90, 0); 
            return;
        }

        Vector3 left = transform.TransformDirection(Vector3.left);

        // Try turning left if no obstacle
        if (!Physics.Raycast(transform.position, left, detectionRange, obstacleLayer))
        {
            transform.Rotate(0, -90, 0); 
            return;
        }

        // Turn around if both sides are blocked
        transform.Rotate(0, 180, 0); 
    }
}
