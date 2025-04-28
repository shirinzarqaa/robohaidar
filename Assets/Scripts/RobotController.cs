using UnityEngine;
using System.Collections.Generic;

public class RobotController : MonoBehaviour
{
    public float moveSpeed = 3f;
    public float rotationSpeed = 100f;
    public EnvironmentSensor sensor;

    // Untuk pola zigzag
    private bool isMovingRight = false; // Mulai belok kiri (false = kiri)
    private float timeSinceDirectionChange = 0f;
    public float directionChangeInterval = 3f; // Interval waktu perubahan arah untuk zigzag

    // Untuk tracking area yang sudah dikunjungi
    private HashSet<Vector2Int> visitedPositions = new HashSet<Vector2Int>();
    public float gridSize = 1f; // Ukuran grid untuk menandai posisi yang dikunjungi

    // Untuk menghindari obstacle
    private bool isAvoidingObstacle = false;
    private float avoidanceTime = 0f;
    public float obstacleDetectionThreshold = 1.5f; // Jarak deteksi obstacle
    public float obstacleAvoidanceTime = 1f; // Waktu berapa lama menghindari obstacle

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

        // Tandai posisi awal sebagai sudah dikunjungi
        MarkCurrentPositionVisited();
    }

    void Update()
    {
        // Tandai posisi saat ini sebagai sudah dikunjungi
        MarkCurrentPositionVisited();

        // Cek apakah ada obstacle di depan
        float frontDistance = sensor.GetDistanceInDirection(0);

        // Jika sedang menghindari obstacle
        if (isAvoidingObstacle)
        {
            avoidanceTime -= Time.deltaTime;
            if (avoidanceTime <= 0)
            {
                isAvoidingObstacle = false;
                // Setelah menghindari, ganti arah zigzag
                isMovingRight = !isMovingRight;
            }
            else
            {
                // Belok ke arah yang berlawanan dengan kondisi zigzag
                RotateToAvoidObstacle();
                MoveForward();
                return;
            }
        }

        // Jika ada obstacle di depan, mulai menghindari
        if (frontDistance < obstacleDetectionThreshold)
        {
            isAvoidingObstacle = true;
            avoidanceTime = obstacleAvoidanceTime;
            return;
        }

        // Pola zigzag normal ketika tidak ada obstacle
        timeSinceDirectionChange += Time.deltaTime;
        if (timeSinceDirectionChange >= directionChangeInterval)
        {
            timeSinceDirectionChange = 0f;
            isMovingRight = !isMovingRight;
        }

        // Bergerak zigzag
        float rotationDirection = isMovingRight ? 1f : -1f;
        transform.Rotate(0, rotationDirection * rotationSpeed * Time.deltaTime, 0);

        // Cek apakah arah yang dituju sudah dikunjungi
        Vector2Int nextPosition = GetPositionAhead();
        if (visitedPositions.Contains(nextPosition))
        {
            // Coba arah lain jika posisi di depan sudah dikunjungi
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
        // Jika isMovingRight = true (zigzag ke kanan), maka hindari ke kiri
        // Jika isMovingRight = false (zigzag ke kiri), maka hindari ke kanan
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
        // Coba cari arah yang belum dikunjungi
        for (int i = 0; i < 8; i++)
        {
            // Rotasi 45 derajat untuk setiap coba
            transform.Rotate(0, 45f, 0);

            Vector2Int potentialPosition = GetPositionAhead();
            if (!visitedPositions.Contains(potentialPosition))
            {
                // Arah yang belum dikunjungi ditemukan
                return;
            }
        }

        // Jika semua arah telah dikunjungi, lanjutkan dengan arah zigzag saat ini
    }

    // Metode untuk debugging
    void OnDrawGizmos()
    {
        // Visualisasi area yang sudah dikunjungi (untuk debugging)
        Gizmos.color = Color.yellow;
        foreach (Vector2Int pos in visitedPositions)
        {
            Vector3 worldPos = new Vector3(pos.x * gridSize + gridSize / 2, 0.1f, pos.y * gridSize + gridSize / 2);
            Gizmos.DrawCube(worldPos, new Vector3(gridSize * 0.8f, 0.05f, gridSize * 0.8f));
        }
    }
}