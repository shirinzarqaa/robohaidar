using UnityEngine;
using System.Collections.Generic;

public class RobotController : MonoBehaviour
{
    public float moveSpeed = 3f;
    public float rotationSpeed = 100f;
    public EnvironmentSensor sensor; // Pastikan ini terhubung
    public MappingSystem mapSystem; // Hubungkan ini di Inspector atau via FindObjectOfType

    // Pengaturan Navigasi Peta
    public float mapCheckDistance = 1.5f;        // Jarak cek peta ke depan
    public float turnThreshold = 0.8f;         // Seberapa dekat obstacle/scanned area sebelum belok
    public float explorationTurnAngle = 45f;   // Sudut belok saat mencari arah baru
    public LayerMask bombLayer; // Tambahkan LayerMask untuk deteksi bom

    // Visited Grid (untuk menghindari loop kecil)
    private HashSet<Vector2Int> visitedPositions = new HashSet<Vector2Int>();
    public float gridSize = 1f;

    // State Obstacle Avoidance
    private bool isAvoidingObstacle = false;
    private float avoidanceTime = 0f;
    public float obstacleDetectionThreshold = 1.0f; // Kurangi sedikit agar lebih sensitif
    public float obstacleAvoidanceTime = 1.0f;
    private bool preferRightAvoidance = true; // Arah belok saat menghindari

    // Status Robot
    private enum RobotState { Exploring, AvoidingObstacle, TargetingBomb }
    private RobotState currentState = RobotState.Exploring;
    private Transform targetBomb = null; // Untuk menyimpan bom yang terdeteksi

    void Start()
    {
        if (sensor == null) sensor = GetComponent<EnvironmentSensor>();
        if (sensor == null) { /* Error handling */ enabled = false; return; }

        // Cari MappingSystem jika belum di-assign
        if (mapSystem == null) mapSystem = FindObjectOfType<MappingSystem>();
        if (mapSystem == null) { Debug.LogError("MappingSystem not found!"); enabled = false; return; }

        MarkCurrentPositionVisited();
    }

    void Update()
    {
        MarkCurrentPositionVisited(); // Tandai grid cell saat ini

        // 1. Cek Sensor Langsung untuk Obstacle & Bom
        float frontDistance = sensor.GetDistanceInDirection(0); // Asumsi sensor 0 adalah depan
        CheckForBombsNearby(); // Cek bom dalam jangkauan sensor

        // 2. Logika State Machine
        switch (currentState)
        {
            case RobotState.Exploring:
                UpdateExploring(frontDistance);
                break;
            case RobotState.AvoidingObstacle:
                UpdateAvoidingObstacle();
                break;
            case RobotState.TargetingBomb:
                UpdateTargetingBomb();
                break;
        }
    }

    // --- State Updates ---

    void UpdateExploring(float frontDistance)
    {
        // Jika ada bom terdeteksi, ganti state
        if (targetBomb != null) {
            currentState = RobotState.TargetingBomb;
            return;
        }

        // Jika ada obstacle dekat, ganti state
        if (frontDistance < obstacleDetectionThreshold)
        {
            StartAvoidingObstacle(frontDistance);
            return;
        }

        // Cek Peta di Depan
        Vector3 checkPosForward = transform.position + transform.forward * mapCheckDistance;
        Vector2Int nextGridPos = GetPositionAhead();
        Color mapColorForward = mapSystem.GetColorAtWorldPos(checkPosForward);

        bool isForwardBlockedOnMap = (mapColorForward == mapSystem.obstacleColor || mapColorForward == mapSystem.scannedSafeColor);
        bool isNextGridVisited = visitedPositions.Contains(nextGridPos);

        if (isForwardBlockedOnMap || isNextGridVisited)
        {
            // Arah depan diblok di peta atau grid cell sudah dikunjungi -> Cari arah baru
            float turnAngle = FindBestExplorationDirection();
            if (Mathf.Abs(turnAngle) > 1f) // Jika ada arah bagus ditemukan
            {
                // Berputar lebih cepat saat mencari arah baru
                 transform.Rotate(0, Mathf.Sign(turnAngle) * rotationSpeed * Time.deltaTime * 1.5f, 0);
            } else {
                 // Jika tidak ada arah bagus (misal terpojok), putar saja perlahan
                 transform.Rotate(0, rotationSpeed * Time.deltaTime * 0.5f, 0); // Putar pelan
            }
        }
        else
        {
            // Arah depan aman di peta dan grid cell belum dikunjungi -> Maju
            MoveForward();
        }
    }

    void StartAvoidingObstacle(float frontDistance)
    {
         currentState = RobotState.AvoidingObstacle;
         avoidanceTime = obstacleAvoidanceTime;

         // Coba putar ke arah yang lebih bebas berdasarkan sensor samping (jika ada)
         // Untuk sekarang, kita tentukan saja berdasarkan preferensi
         preferRightAvoidance = !preferRightAvoidance; // Ganti arah tiap kali menghindari
    }


    void UpdateAvoidingObstacle()
    {
        avoidanceTime -= Time.deltaTime;
        if (avoidanceTime <= 0)
        {
            currentState = RobotState.Exploring; // Kembali eksplorasi
            isAvoidingObstacle = false; // Reset flag lama jika masih ada
            return;
        }

        // Logika avoidance sederhana: putar dan maju sedikit
        float rotationDirection = preferRightAvoidance ? 1f : -1f;
        transform.Rotate(0, rotationDirection * rotationSpeed * Time.deltaTime, 0);
        // Optional: maju sedikit saat berputar agar tidak diam di tempat
        transform.position += transform.forward * moveSpeed * 0.3f * Time.deltaTime;
    }

    void UpdateTargetingBomb() {
         if (targetBomb == null) {
             currentState = RobotState.Exploring; // Bom hilang? Kembali eksplorasi
             return;
         }

        // Cek obstacle di depan saat menuju bom
         float frontDistance = sensor.GetDistanceInDirection(0);
         if (frontDistance < obstacleDetectionThreshold * 0.8f) { // Lebih sensitif saat targetting
              StartAvoidingObstacle(frontDistance); // Hindari dulu, lalu lanjut targetting
              return;
         }


         // Arahkan ke bom dan maju
         Vector3 directionToBomb = (targetBomb.position - transform.position).normalized;
         directionToBomb.y = 0; // Jaga agar robot tidak miring
         Quaternion lookRotation = Quaternion.LookRotation(directionToBomb);
         transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * rotationSpeed * 0.5f); // Putar lebih lambat & halus

         // Maju jika sudah cukup menghadap bom
         if(Vector3.Angle(transform.forward, directionToBomb) < 15f) {
              MoveForward();
         }

         // Jika sangat dekat, biarkan trigger collision yang bekerja
         if (Vector3.Distance(transform.position, targetBomb.position) < 0.5f) {
             // Biarkan OnTriggerEnter di Bomb.cs handle defusal
             // Setelah defuse (di Bomb.cs atau BombCounter.cs), kita perlu reset targetBomb
             // Kita bisa panggil method di sini dari BombCounter
         }
    }

    // Panggil ini dari BombCounter setelah bom berhasil di-defuse
    public void OnBombDefused() {
        targetBomb = null;
        currentState = RobotState.Exploring; // Lanjut cari bom lain
    }

    // --- Fungsi Bantu ---

    void CheckForBombsNearby() {
        // Gunakan sensor atau Physics check untuk mendeteksi bom
        // Contoh menggunakan SphereCast (mirip sensor tapi fokus ke bom)
        RaycastHit hit;
        if (Physics.SphereCast(transform.position, 1.0f, transform.forward, out hit, sensor.sensorLength, bombLayer)) {
             if (hit.collider.CompareTag("Bombs")) { // Pastikan tag benar
                 if(targetBomb == null || targetBomb != hit.transform) {
                    Debug.Log("BOMB DETECTED BY SENSOR: " + hit.collider.name);
                    targetBomb = hit.transform;
                    currentState = RobotState.TargetingBomb; // Langsung targetkan
                 }
             }
        }
         // Jika state masih targeting tapi bom sudah tidak terdeteksi sensor (mungkin terhalang)
         // Biarkan saja state targeting, dia akan coba mendekat. OnTriggerEnter akan jadi backup.
         // Atau jika bom sudah dihancurkan, OnBombDefused() akan dipanggil.
    }

    float FindBestExplorationDirection()
    {
        float[] angles = { 0, explorationTurnAngle, -explorationTurnAngle, explorationTurnAngle * 2, -explorationTurnAngle * 2, 180f }; // Cek depan, samping, belakang
        float bestScore = -100f; // Mulai dari skor sangat rendah
        float bestAngle = 0f;    // Sudut relatif terbaik yang ditemukan

        foreach (float angle in angles)
        {
            Vector3 checkDirection = Quaternion.Euler(0, angle, 0) * transform.forward;
            Vector3 checkPosWorld = transform.position + checkDirection * mapCheckDistance;
            Vector2Int checkPosGrid = WorldToGrid(transform.position + checkDirection * gridSize); // Cek juga grid cell

            Color mapColor = mapSystem.GetColorAtWorldPos(checkPosWorld);
            float currentScore = 0f;

            // Penalti/Bonus berdasarkan Peta
            if (mapColor == mapSystem.obstacleColor || !mapSystem.IsInMapBounds(mapSystem.WorldToMapCoords(checkPosWorld).x, mapSystem.WorldToMapCoords(checkPosWorld).y))
            {
                currentScore = -100f; // Sangat buruk (obstacle atau di luar map)
            }
            else if (mapColor == mapSystem.initialColor)
            {
                currentScore = 10f;  // Sangat bagus (area belum discan)
            }
            else if (mapColor == mapSystem.scannedSafeColor)
            {
                currentScore = 1f;   // Kurang bagus (sudah discan aman)
            }
            else {
                currentScore = 5f; // Warna lain? Mungkin area belum terdefinisi, beri skor sedang
            }

            // Penalti jika grid cell sudah dikunjungi (kecuali arah depan 0 derajat)
            if (angle != 0 && visitedPositions.Contains(checkPosGrid))
            {
                currentScore -= 5f; // Kurangi skor jika grid sudah dilewati
            }

            // Sedikit preferensi untuk tidak berbelok terlalu tajam jika skor mirip
            currentScore -= Mathf.Abs(angle) / 45f * 0.5f;


            if (currentScore > bestScore)
            {
                bestScore = currentScore;
                bestAngle = angle;
            }
        }

         // Jika skor terbaik masih sangat rendah, mungkin terjebak. Coba putar balik.
        if (bestScore < 0 && !Mathf.Approximately(bestAngle, 180f)) {
             // Cek apakah 180 derajat (belakang) valid
             Vector3 checkPosBehind = transform.position - transform.forward * mapCheckDistance;
             Color mapColorBehind = mapSystem.GetColorAtWorldPos(checkPosBehind);
              if (mapColorBehind != mapSystem.obstacleColor) {
                 return 180f; // Pilih putar balik jika memungkinkan
              }
        }


        // Kembalikan sudut *relatif* terbaik yang ditemukan
        return bestAngle;
    }

    private void MoveForward()
    {
        transform.position += transform.forward * moveSpeed * Time.deltaTime;
    }

    private void MarkCurrentPositionVisited()
    {
        Vector2Int gridPos = WorldToGrid(transform.position);
        visitedPositions.Add(gridPos);

        // Juga panggil MappingSystem untuk menandai area sekitar robot sebagai 'scanned safe'
        // Jika MappingSystem.InternalEnvironmentScan sudah berjalan, ini mungkin tidak perlu
        // Jika InternalEnvironmentScan TIDAK otomatis menandai area aman, maka panggil ini:
        // mapSystem.MarkAreaAsScannedSafe(transform.position, sensor.sensorLength * 0.5f);
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
        Vector3 positionAhead = transform.position + transform.forward * gridSize; // Cek 1 grid ke depan
        return WorldToGrid(positionAhead);
    }

    // Gizmos untuk Debug
    void OnDrawGizmos()
    {
         // Gizmo untuk visited positions
        Gizmos.color = Color.yellow;
        foreach (Vector2Int pos in visitedPositions)
        {
            Vector3 worldPos = new Vector3(pos.x * gridSize + gridSize / 2, 0.1f, pos.y * gridSize + gridSize / 2);
            Gizmos.DrawCube(worldPos, new Vector3(gridSize * 0.8f, 0.05f, gridSize * 0.8f));
        }

         // Gizmo untuk state
         Gizmos.color = Color.cyan;
         Gizmos.DrawWireSphere(transform.position + Vector3.up * 1.5f, 0.3f);
         // Tampilkan state di atas robot jika perlu (pakai UnityEditor namespace)
         #if UNITY_EDITOR
             UnityEditor.Handles.Label(transform.position + Vector3.up * 1.7f, currentState.ToString());
              if (targetBomb != null) {
                  Gizmos.color = Color.magenta;
                  Gizmos.DrawLine(transform.position + Vector3.up, targetBomb.position);
              }
         #endif

    }
}