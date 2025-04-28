using UnityEngine;
using System.Collections;
using System.Collections.Generic; // Untuk List jika mencari bomb

public class RobotController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 3f;
    public float rotationSpeed = 80f; // Derajat per detik
    public float backwardSpeedMultiplier = 0.7f; // Seberapa cepat mundur dibanding maju

    [Header("Sensors")]
    public float sensorLength = 1.5f;
    public float sensorRadius = 0.2f;
    public LayerMask obstacleLayer;
    public float frontObstacleDistance = 0.8f; // Jarak deteksi depan
    public float backObstacleDistance = 0.5f;  // Jarak deteksi belakang (lebih pendek)

    [Header("Bomb Detection")]
    public float bombDetectionRadius = 5.0f;
    public string bombTag = "Bombs"; // Pastikan tag bomb benar

    [Header("Maneuver Settings")]
    public float backtrackDuration = 1.0f; // Durasi mundur sambil belok
    public float forwardTurnDuration = 1.0f; // Durasi maju sambil belok (setelah mundur)
    public float stuckTurnAngle = 90f; // Sudut putar saat buntu

    [Header("State (Debug Only)")]
    [SerializeField] private RobotState currentState = RobotState.MovingForward;
    private Transform targetBomb = null;
    private float maneuverTimer = 0f;
    private float currentTurnDirection = 1f; // 1 for right, -1 for left (digunakan saat manuver)
    private Quaternion stuckTargetRotation;
    private bool isExecutingStuckTurn = false;

    private enum RobotState
    {
        MovingForward,
        SeekingBomb,
        Backtracking,
        ForwardTurning,
        TurningFromStuck
    }

    void Update()
    {
        // --- Prioritas 1: Cari & Kejar Bomb ---
        if (CheckAndTargetBomb())
        {
            currentState = RobotState.SeekingBomb;
            ExecuteSeekBomb();
            return; // Prioritas tertinggi, jangan lakukan yang lain
        }
        // Jika sebelumnya mengejar bom tapi sekarang tidak ada lagi, kembali ke MovingForward
        if (currentState == RobotState.SeekingBomb && targetBomb == null)
        {
            currentState = RobotState.MovingForward;
        }


        // --- Prioritas 2: Cek Kondisi Buntu (Depan & Belakang) ---
        bool frontBlocked = IsObstacleDetected(transform.forward, frontObstacleDistance);
        bool backBlocked = IsObstacleDetected(-transform.forward, backObstacleDistance);

        if (frontBlocked && backBlocked && currentState != RobotState.TurningFromStuck && !isExecutingStuckTurn)
        {
            Debug.Log("Stuck! Initiating turn.");
            currentState = RobotState.TurningFromStuck;
            // Tentukan arah belok (misal, coba kanan dulu)
            float turnAngle = stuckTurnAngle;
             // Atau bisa dibuat acak: float turnAngle = Random.Range(0, 2) == 0 ? stuckTurnAngle : -stuckTurnAngle;
            stuckTargetRotation = transform.rotation * Quaternion.Euler(0, turnAngle, 0);
            isExecutingStuckTurn = true;
            return; // Mulai proses belok
        }


        // --- Prioritas 3: Cek Obstacle Depan (jika tidak sedang manuver lain) ---
        if (frontBlocked && currentState == RobotState.MovingForward)
        {
             Debug.Log("Front obstacle detected. Starting backtrack.");
             currentState = RobotState.Backtracking;
             maneuverTimer = backtrackDuration;
             // Tentukan arah belok saat mundur (misal, selalu kanan)
             currentTurnDirection = 1f;
             return; // Mulai manuver
        }


        // --- Jalankan Aksi Berdasarkan State ---
        switch (currentState)
        {
            case RobotState.MovingForward:
                MoveForward();
                break;

            case RobotState.SeekingBomb:
                 // Logika sudah di handle di awal Update
                 // Jika sampai di sini berarti bom hilang saat sedang kejar
                 currentState = RobotState.MovingForward;
                break;

            case RobotState.Backtracking:
                ExecuteBacktrack();
                break;

            case RobotState.ForwardTurning:
                ExecuteForwardTurn();
                break;

            case RobotState.TurningFromStuck:
                 ExecuteStuckTurn();
                break;
        }
    }

    // --- Fungsi Aksi State ---

    void MoveForward()
    {
        transform.position += transform.forward * moveSpeed * Time.deltaTime;
    }

    void ExecuteSeekBomb()
    {
        if (targetBomb == null) {
             currentState = RobotState.MovingForward; // Target hilang
             return;
        }

        // Arahkan ke bomb
        Vector3 directionToBomb = (targetBomb.position - transform.position).normalized;
        directionToBomb.y = 0; // Jaga agar tetap di ground

        // Rotasi
        if (directionToBomb != Vector3.zero) {
            Quaternion targetRotation = Quaternion.LookRotation(directionToBomb);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * 0.5f * Time.deltaTime); // Belok lebih lambat saat kejar bom?
        }

        // Bergerak maju ke bomb
        // Berhenti jika sudah sangat dekat (jarak defuse)
        float distanceToBomb = Vector3.Distance(transform.position, targetBomb.position);
        if(distanceToBomb > 1.0f) { // Sesuaikan jarak berhenti ini
             transform.position += transform.forward * moveSpeed * Time.deltaTime;
        } else {
             Debug.Log("Reached bomb vicinity. Attempting defuse.");
             // Tambahkan logika defuse di sini jika perlu (atau biarkan A* controller yg handle)
             // Untuk controller ini, kita anggap berhenti saja.
             // currentState = RobotState.MovingForward; // Kembali normal setelah dekat?
             // targetBomb = null; // Hapus target
        }
    }

    void ExecuteBacktrack()
    {
        if (maneuverTimer > 0)
        {
            // Mundur
            transform.position -= transform.forward * moveSpeed * backwardSpeedMultiplier * Time.deltaTime;
            // Sambil belok (misal ke kanan)
            transform.Rotate(0, currentTurnDirection * rotationSpeed * Time.deltaTime, 0);
            maneuverTimer -= Time.deltaTime;
        }
        else
        {
            // Selesai mundur, mulai maju sambil belok
             Debug.Log("Backtrack finished. Starting forward turn.");
            currentState = RobotState.ForwardTurning;
            maneuverTimer = forwardTurnDuration; // Reset timer untuk fase maju
        }
    }

    void ExecuteForwardTurn()
    {
        if (maneuverTimer > 0)
        {
            // Maju
            transform.position += transform.forward * moveSpeed * Time.deltaTime;
            // Sambil melanjutkan belok ke arah yang sama
            transform.Rotate(0, currentTurnDirection * rotationSpeed * Time.deltaTime, 0);
            maneuverTimer -= Time.deltaTime;
        }
        else
        {
            // Selesai manuver, kembali normal
             Debug.Log("Forward turn finished. Resuming normal movement.");
            currentState = RobotState.MovingForward;
        }
    }

     void ExecuteStuckTurn()
     {
         if (isExecutingStuckTurn) {
            // Putar robot secara bertahap menuju targetRotation
            transform.rotation = Quaternion.RotateTowards(transform.rotation, stuckTargetRotation, rotationSpeed * 1.5f * Time.deltaTime); // Putar lebih cepat saat stuck

            // Cek apakah rotasi sudah mendekati target
            if (Quaternion.Angle(transform.rotation, stuckTargetRotation) < 2.0f)
            {
                transform.rotation = stuckTargetRotation; // Snap ke rotasi target
                isExecutingStuckTurn = false;
                currentState = RobotState.MovingForward; // Kembali ke state bergerak lurus
                Debug.Log("Finished turning from stuck position.");
            }
         } else {
             // Seharusnya tidak pernah sampai sini jika logika benar, tapi sbg safety:
             currentState = RobotState.MovingForward;
         }
     }


    // --- Fungsi Helper ---

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
                Debug.Log($"Bomb detected nearby: {potentialTarget.name}");
            }
            targetBomb = potentialTarget;
            return true; // Ada bom terdekat
        } else {
            targetBomb = null;
            return false; // Tidak ada bom terdekat
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
            obstacleLayer // Gunakan layer mask yang sudah didefinisikan
        );

        // Debug Ray
        Color rayColor = detected ? Color.red : Color.green;
        Debug.DrawRay(transform.position, direction * maxDistance, rayColor);

        return detected;
    }

     // --- Gizmos untuk Debug ---
    void OnDrawGizmosSelected() {
        // Draw Bomb Detection Radius
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, bombDetectionRadius);

        // Draw Front Sensor Range
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * frontObstacleDistance);

         // Draw Back Sensor Range
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, transform.position - transform.forward * backObstacleDistance);
    }
}