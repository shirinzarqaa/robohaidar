using UnityEngine;
using System.Collections.Generic;

public class RobotController : MonoBehaviour
{
    public float moveSpeed = 3f;
    public float rotationSpeed = 60f;
    public float backwardSpeedMultiplier = 0.7f;

    [Header("Sensors")]
    public float sensorLength = 1.5f;
    public float sensorRadius = 0.2f;
    public LayerMask obstacleLayer;
    public float frontObstacleDistance = 0.8f;
    public float backObstacleDistance = 0.5f;

    [Header("Bomb Detection")]
    public float bombDetectionRadius = 5.0f;
    public string bombTag = "Bombs";

    [Header("Maneuver Settings")]
    public float backtrackDuration = 1.0f;
    public float forwardTurnDuration = 1.0f;
    public float stuckTurnAngle = 90f;

    [Header("Loop Prevention")]
    public int positionHistoryLength = 20;   // Berapa banyak posisi yang disimpan
    public float recordPositionInterval = 0.5f; // Catat posisi setiap X detik
    public float repetitionDistanceThreshold = 1.5f; // Jarak minimum untuk dianggap kembali ke area lama
    public float breakLoopTurnAngle = 90f;   // Sudut putar saat keluar loop
    private Queue<Vector3> positionHistory = new Queue<Vector3>();
    private float positionRecordTimer = 0f;


    [Header("State (Debug Only)")]
    [SerializeField] private RobotState currentState = RobotState.MovingForward;
    private Transform targetBomb = null;
    private float maneuverTimer = 0f;
    private float currentTurnDirection = 1f;
    private Quaternion stuckTargetRotation;
    private bool isExecutingStuckTurn = false; // Flag ini mungkin bisa digabung ke state TurningFromStuck

    private enum RobotState
    {
        MovingForward,
        SeekingBomb,
        Backtracking,
        ForwardTurning,
        TurningFromStuck // Digunakan untuk buntu depan-belakang DAN untuk keluar loop
    }

    void Update()
    {
        // --- Pencatatan History Posisi ---
        RecordPositionHistory();

        // --- Prioritas 2: Cek Kondisi Buntu (Depan & Belakang) ---
        bool frontBlocked = IsObstacleDetected(transform.forward, frontObstacleDistance);
        bool backBlocked = IsObstacleDetected(-transform.forward, backObstacleDistance);

        // Gunakan state TurningFromStuck untuk Buntu dan Keluar Loop
        if (isExecutingStuckTurn) {
             ExecuteStuckTurn(); // Lanjutkan putaran jika sedang berjalan
             return;
        }

        if (frontBlocked && backBlocked && currentState != RobotState.TurningFromStuck)
        {
            Debug.Log("Stuck (Front & Back)! Initiating recovery turn.");
            InitiateStuckTurn(Random.Range(0, 2) == 0 ? breakLoopTurnAngle : -breakLoopTurnAngle); // Putar acak Kiri/Kanan
             // Kita tidak clear history di sini, hanya saat loop terdeteksi
            return;
        }

        // --- Prioritas 3: Cek Obstacle Depan & Deteksi Loop ---
        if (frontBlocked && currentState == RobotState.MovingForward)
        {
            // Cek dulu apakah ini perulangan SEBELUM memutuskan backtrack
            if (IsRepeatingArea())
            {
                Debug.LogWarning("Repetition Detected! Breaking loop pattern.");
                // Lakukan putaran besar untuk keluar loop
                InitiateStuckTurn(Random.Range(0, 2) == 0 ? breakLoopTurnAngle : -breakLoopTurnAngle); // Putar acak Kiri/Kanan
                // KOSONGKAN HISTORY setelah memutuskan keluar loop
                positionHistory.Clear();
                positionRecordTimer = recordPositionInterval; // Reset timer record juga
            }
            else
            {
                // Tidak ada perulangan, lakukan backtrack standar
                 Debug.Log("Front obstacle detected. Starting backtrack maneuver.");
                 currentState = RobotState.Backtracking;
                 maneuverTimer = backtrackDuration;
                 currentTurnDirection = Random.Range(0, 2) == 0 ? 1f : -1f; // Arah belok backtrack bisa random juga
            }
            return; // Aksi sudah ditentukan (entah backtrack atau turning)
        }

        // --- Prioritas 1: Cari & Kejar Bomb ---
        if (CheckAndTargetBomb())
        {
            currentState = RobotState.SeekingBomb;
            ExecuteSeekBomb();
            return;
        }
        if (currentState == RobotState.SeekingBomb && targetBomb == null)
        {
            currentState = RobotState.MovingForward;
        }

        

        // --- Jalankan Aksi Berdasarkan State (jika tidak ada prioritas di atas yg terpenuhi) ---
        switch (currentState)
        {
            case RobotState.MovingForward:
                MoveForward();
                break;
            case RobotState.SeekingBomb:
                // Handled di atas, jika sampai sini bom hilang
                 currentState = RobotState.MovingForward;
                break;
            case RobotState.Backtracking:
                ExecuteBacktrack();
                break;
            case RobotState.ForwardTurning:
                ExecuteForwardTurn();
                break;
             case RobotState.TurningFromStuck:
                 // Seharusnya ditangani oleh check isExecutingStuckTurn di atas
                 // Jika sampai sini, mungkin proses turn selesai, kembali normal
                 if (!isExecutingStuckTurn) currentState = RobotState.MovingForward;
                break;
        }
    }

    // --- Fungsi Pencatatan dan Deteksi Loop ---

    void RecordPositionHistory()
    {
        positionRecordTimer -= Time.deltaTime;
        if (positionRecordTimer <= 0f)
        {
            // Tambahkan posisi saat ini ke queue
            positionHistory.Enqueue(transform.position);

            // Jika queue terlalu panjang, buang yang paling lama
            while (positionHistory.Count > positionHistoryLength)
            {
                positionHistory.Dequeue();
            }

            // Reset timer
            positionRecordTimer = recordPositionInterval;
        }
    }

    bool IsRepeatingArea()
    {
        // Jangan cek jika history belum cukup terisi
        if (positionHistory.Count < positionHistoryLength / 2) // Misalnya, butuh setengah history terisi dulu
        {
            return false;
        }

        int checkCount = 0; // Untuk debug, berapa banyak poin yg dicek
        int closeCount = 0; // Berapa banyak poin yg dekat

        // Iterasi melalui history (kecuali beberapa poin terakhir)
        // Kita pakai ToArray agar bisa iterasi tanpa mengubah queue asli saat ini
        Vector3[] historyArray = positionHistory.ToArray();
        // Cek dari paling lama ke yang lebih baru, tapi lewati N terakhir
        int pointsToCheck = historyArray.Length - 3; // Jangan cek 3 posisi terakhir

        for (int i = 0; i < pointsToCheck; i++)
        {
             checkCount++;
            float distanceSqr = (transform.position - historyArray[i]).sqrMagnitude; // Pakai sqrMagnitude lebih cepat
            if (distanceSqr < repetitionDistanceThreshold * repetitionDistanceThreshold)
            {
                // Ditemukan posisi lama yang dekat!
                closeCount++;
                 // Kita bisa return true di sini jika 1 saja cukup,
                 // atau hitung berapa banyak yg dekat sbg indikator confidence
                // return true;
            }
        }

         // Debug.Log($"Checking repetition: Checked {checkCount} points, Found {closeCount} close points.");
        // Tentukan threshold berapa banyak poin dekat yg dianggap loop, misal > 1
        return closeCount > 0; // Anggap loop jika ada SATU saja posisi lama yg dekat
    }


    // --- Fungsi Aksi State (MoveForward, ExecuteSeekBomb, ExecuteBacktrack, ExecuteForwardTurn tetap SAMA) ---
     void MoveForward()
    {
        transform.position += transform.forward * moveSpeed * Time.deltaTime;
    }

     void ExecuteSeekBomb()
     {
          if (targetBomb == null) {
              currentState = RobotState.MovingForward;
              return;
          }
          Vector3 directionToBomb = (targetBomb.position - transform.position).normalized;
          directionToBomb.y = 0;
          if (directionToBomb != Vector3.zero) {
              Quaternion targetRotation = Quaternion.LookRotation(directionToBomb);
              transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * 0.5f * Time.deltaTime);
          }
          float distanceToBomb = Vector3.Distance(transform.position, targetBomb.position);
          if(distanceToBomb > 1.0f) {
              transform.position += transform.forward * moveSpeed * Time.deltaTime;
          } else {
              // Debug.Log("Reached bomb vicinity.");
              // Logika defuse mungkin lebih baik di controller lain
          }
     }

     void ExecuteBacktrack()
     {
         if (maneuverTimer > 0)
         {
             transform.position -= transform.forward * moveSpeed * backwardSpeedMultiplier * Time.deltaTime;
             transform.Rotate(0, currentTurnDirection * rotationSpeed * Time.deltaTime, 0);
             maneuverTimer -= Time.deltaTime;
         }
         else
         {
             currentState = RobotState.ForwardTurning;
             maneuverTimer = forwardTurnDuration;
         }
     }

     void ExecuteForwardTurn()
     {
         if (maneuverTimer > 0)
         {
             transform.position += transform.forward * moveSpeed * Time.deltaTime;
             transform.Rotate(0, currentTurnDirection * rotationSpeed * Time.deltaTime, 0);
             maneuverTimer -= Time.deltaTime;
         }
         else
         {
             currentState = RobotState.MovingForward;
         }
     }

    // --- Modifikasi Fungsi Stuck Turn ---
     void InitiateStuckTurn(float angle) {
         // Fungsi ini dipanggil saat buntu depan-belakang ATAU saat loop terdeteksi
         currentState = RobotState.TurningFromStuck;
         stuckTargetRotation = transform.rotation * Quaternion.Euler(0, angle, 0);
         isExecutingStuckTurn = true; // Set flag bahwa sedang proses belok
         Debug.Log($"Initiating recovery turn by {angle} degrees.");
     }

     void ExecuteStuckTurn() // Nama tetap sama, tapi sekarang dipanggil berulang
     {
         if (isExecutingStuckTurn) {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, stuckTargetRotation, rotationSpeed * 1.5f * Time.deltaTime);

            if (Quaternion.Angle(transform.rotation, stuckTargetRotation) < 2.0f)
            {
                transform.rotation = stuckTargetRotation;
                isExecutingStuckTurn = false; // Selesai belok
                currentState = RobotState.MovingForward; // Kembali normal
                Debug.Log("Finished recovery turn.");
            }
         } else {
             // Safety fallback
             currentState = RobotState.MovingForward;
         }
     }

    // --- Fungsi Helper (CheckAndTargetBomb, IsObstacleDetected, OnDrawGizmosSelected tetap SAMA) ---
     bool CheckAndTargetBomb()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, bombDetectionRadius);
        float closestDistSqr = Mathf.Infinity;
        Transform potentialTarget = null;

        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.CompareTag(bombTag) && hitCollider.gameObject.activeInHierarchy)
            {
                float distSqr = (hitCollider.transform.position - transform.position).sqrMagnitude;
                if (distSqr < closestDistSqr)
                {
                    closestDistSqr = distSqr;
                    potentialTarget = hitCollider.transform;
                }
            }
        }

        if (potentialTarget != null) {
            if (targetBomb != potentialTarget) {
                // Debug.Log($"Bomb detected nearby: {potentialTarget.name}");
            }
            targetBomb = potentialTarget;
            return true;
        } else {
            targetBomb = null;
            return false;
        }
    }

    bool IsObstacleDetected(Vector3 direction, float maxDistance)
    {
        RaycastHit hit;
        bool detected = Physics.SphereCast(
            transform.position,
            sensorRadius,
            direction,
            out hit,
            maxDistance,
            obstacleLayer
        );
        // Color rayColor = detected ? Color.red : Color.green;
        // Debug.DrawRay(transform.position, direction * maxDistance, rayColor);
        return detected;
    }

     void OnDrawGizmosSelected() {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, bombDetectionRadius);
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * frontObstacleDistance);
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, transform.position - transform.forward * backObstacleDistance);

        // Visualize position history
        if (positionHistory != null && positionHistory.Count > 0) {
            Gizmos.color = Color.magenta;
            Vector3 lastPos = positionHistory.Peek(); // Ambil yg paling lama tanpa menghapus
            foreach(Vector3 pos in positionHistory) {
                Gizmos.DrawSphere(pos, 0.1f);
                 // Gizmos.DrawLine(lastPos, pos); // Gambar garis antar history (bisa jadi berantakan)
                 // lastPos = pos;
            }
        }
    }
}