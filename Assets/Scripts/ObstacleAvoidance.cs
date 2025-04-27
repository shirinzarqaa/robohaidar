using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObstacleAvoidance : MonoBehaviour
{
    public float detectionRange = 1.0f;   // Jarak untuk mendeteksi obstacle
    public LayerMask obstacleLayer;       // Layer untuk obstacle

    void Update()
    {
        AvoidObstacle();
    }

    void AvoidObstacle()
    {
        Vector3 forward = transform.TransformDirection(Vector3.forward);

        // Cek apakah ada obstacle di depan dalam jarak tertentu
        if (Physics.Raycast(transform.position, forward, detectionRange, obstacleLayer))
        {
            // Kalau ada obstacle, lakukan aksi menghindar
            Debug.Log("Obstacle Detected! Trying to avoid...");

            // Coba belok ke kanan atau kiri
            TryChangeDirection();
        }
    }

    void TryChangeDirection()
    {
        // Cek kanan
        Vector3 right = transform.TransformDirection(Vector3.right);
        if (!Physics.Raycast(transform.position, right, detectionRange, obstacleLayer))
        {
            transform.Rotate(0, 90, 0); // Belok kanan
            return;
        }

        // Kalau kanan ga bisa, cek kiri
        Vector3 left = transform.TransformDirection(Vector3.left);
        if (!Physics.Raycast(transform.position, left, detectionRange, obstacleLayer))
        {
            transform.Rotate(0, -90, 0); // Belok kiri
            return;
        }

        // Kalau kanan-kiri mentok, putar balik
        transform.Rotate(0, 180, 0); // Putar balik
    }
}
