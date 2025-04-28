using UnityEngine;
using System.Collections;
using System.Collections.Generic; // Dibutuhkan untuk Queue

public class RobotController : MonoBehaviour
{
    // ... (Semua deklarasi Header dan variabel tetap sama) ...
    [Header("Movement")]
    public float moveSpeed = 3f;
    public float rotationSpeed = 70f;
    public float backwardSpeedMultiplier = 0.7f;

    [Header("Sensors")]
    public float sensorLength = 1.5f;
    public float sensorRadius = 0.25f;
    public LayerMask obstacleLayer;
    public float frontObstacleDistance = 0.8f;
    public float backObstacleDistance = 0.5f;

    [Header("Bomb Detection")]
    public float bombDetectionRadius = 6.0f;
    public string bombTag = "Bombs";

    [Header("Maneuver Settings")]
    public float backtrackDuration = 0.8f;
    public float forwardTurnDuration = 0.8f;
    public float stuckTurnAngle = 90f;

    [Header("Loop Prevention & Memory")]
    [Tooltip("Berapa banyak posisi terakhir yg diingat.")]
    public int positionHistoryLength = 50;
    [Tooltip("Seberapa sering posisi dicatat (detik).")]
    public float recordPositionInterval = 0.3f;
    [Tooltip("Jarak kuadrat minimum ke posisi lama agar dianggap loop.")]
    public float repetitionDistanceThreshold = 3.0f;
    [Tooltip("Sudut putar saat keluar loop kosong.")]
    public float breakLoopTurnAngle = 135f;
    [Tooltip("Berapa banyak titik history dekat utk anggap loop.")]
    public int minClosePointsForLoop = 2;

    [Header("Obstacle Navigation")]
    [Tooltip("Durasi satu langkah manuver navigasi.")]
    public float navigationStepDuration = 1.0f;
    [Tooltip("Kecepatan saat navigasi (multiplier).")]
    public float navigationSpeedMultiplier = 0.6f;
    [Tooltip("Berapa kali mencoba navigasi sebelum menyerah.")]
    public int maxNavigationAttempts = 5;

    private Queue<Vector3> positionHistory = new Queue<Vector3>();
    private float positionRecordTimer = 0f;

    [Header("State (Debug Only)")]
    [SerializeField] private RobotState currentState = RobotState.MovingForward;
    private Transform targetBomb = null;
    private float maneuverTimer = 0f;
    private float currentTurnDirection = 1f;
    private Quaternion targetManeuverRotation;
    private bool isExecutingManeuverTurn = false;
    private int navigationAttempts = 0;

    private enum RobotState
    {
        MovingForward,
        SeekingBomb,
        Backtracking,
        ForwardTurning,
        ExecutingManeuverTurn,
        NavigatingAroundObstacle
    }

    // =========================================================================
    //  Update Cycle - PRIORITAS DIUBAH
    // =========================================================================
    void Update()
    {
        // 1. Catat Posisi (Selalu lakukan)
        RecordPositionHistory();

        // 2. Handle State Aktif (Jika sedang bermanuver, selesaikan dulu)
        //    Ini penting agar manuver tidak diinterupsi oleh deteksi baru di frame yg sama
        switch (currentState)
        {
            case RobotState.ExecutingManeuverTurn:
                ExecuteManeuverTurn();
                return; // Fokus selesaikan putaran
            case RobotState.SeekingBomb:
                 // ExecuteSeekBomb sekarang dipanggil nanti HANYA jika tidak ada obstacle/stuck
                break; // Jangan return, biarkan cek obstacle dulu
            case RobotState.Backtracking:
                ExecuteBacktrack();
                return; // Fokus selesaikan backtrack
            case RobotState.ForwardTurning:
                ExecuteForwardTurn();
                return; // Fokus selesaikan forward turn
            case RobotState.NavigatingAroundObstacle:
                ExecuteNavigateAroundObstacle();
                return; // Fokus selesaikan navigasi
        }
        // Jika state bukan manuver aktif di atas, lanjut ke pengecekan prioritas berikutnya

        // 3. PRIORITAS #1: Cek Obstacle & Kondisi Buntu
        //    Lakukan ini SEBELUM cek bom atau loop
        bool frontBlocked = IsObstacleDetected(transform.forward, frontObstacleDistance);
        bool backBlocked = IsObstacleDetected(-transform.forward, backObstacleDistance);

        // 3a. Cek Kondisi Buntu (Paling Kritis)
        if (frontBlocked && backBlocked)
        {
            // Hanya initiate jika tidak sedang bermanuver besar
            if (!isExecutingManeuverTurn)
            {
                Debug.Log("Priority Obstacle: Stuck (Front & Back)! Initiating recovery turn.");
                InitiateManeuverTurn(Random.Range(0, 2) == 0 ? stuckTurnAngle : -stuckTurnAngle);
            }
            // Setelah initiate, state akan berubah di frame berikutnya dan ditangani di step 2
            return; // Aksi sudah ditentukan (atau sedang berjalan), hentikan proses frame ini
        }

        // 3b. Cek Obstacle Depan (Jika tidak buntu)
        if (frontBlocked && currentState == RobotState.MovingForward) // Hanya trigger jika sedang maju normal
        {
            // Langsung backtrack, jangan pedulikan bom/loop dulu
            Debug.Log("Priority Obstacle: Front blocked. Starting backtrack maneuver.");
            currentState = RobotState.Backtracking;
            maneuverTimer = backtrackDuration;
            currentTurnDirection = Random.Range(0, 2) == 0 ? 1f : -1f;
            // State Backtracking akan dihandle di frame berikutnya pada step 2
            return; // Aksi sudah ditentukan, hentikan proses frame ini
        }

        // --- Jika lolos dari Cek Obstacle & Buntu ---

        // 4. PRIORITAS #2: Cek & Kejar Bom (jika terdeteksi)
        //    Dilakukan HANYA jika jalan di depan tampak aman
        bool bombDetectedNearby = CheckAndTargetBomb();
        if (bombDetectedNearby && currentState != RobotState.SeekingBomb && currentState != RobotState.NavigatingAroundObstacle)
        {
            // Jalan depan aman (tidak frontBlocked), ada bom, dan belum mengejar/navigasi
            Debug.Log($"Obstacles Clear. Bomb detected: {targetBomb.name}. Switching to SeekingBomb.");
            currentState = RobotState.SeekingBomb;
            // Jalankan ExecuteSeekBomb SEKARANG juga agar bisa langsung cek obstacle ke bom
            ExecuteSeekBomb();
            return; // Fokus kejar bom (atau navigasi jika ExecuteSeekBomb mendeteksi halangan ke bom)
        }
        // Jika sudah SeekingBomb dari frame sebelumnya (dan tidak ada obstacle depan), jalankan juga
        else if (currentState == RobotState.SeekingBomb)
        {
            ExecuteSeekBomb(); // Lanjutkan pengejaran / navigasi
            return;
        }


        // 5. PRIORITAS #3: Cek Loop Kosong (jika bergerak maju & tidak ada bom)
        //    Dilakukan HANYA jika jalan aman, tidak ada bom terdeteksi, dan sedang maju normal
        if (currentState == RobotState.MovingForward && !bombDetectedNearby && IsRepeatingArea())
        {
             Debug.LogWarning("Obstacles/Bomb Clear. Repetition detected! Breaking loop pattern.");
             InitiateManeuverTurn(Random.Range(0, 2) == 0 ? breakLoopTurnAngle : -breakLoopTurnAngle);
             positionHistory.Clear();
             positionRecordTimer = recordPositionInterval;
             Debug.Log("Position History Cleared due to loop break.");
             // State ExecutingManeuverTurn akan dihandle di frame berikutnya
             return; // Aksi sudah ditentukan
        }


        // 6. PRIORITAS #4 (Default): Bergerak Maju Lurus
        //    Jika tidak ada kondisi prioritas di atas yang terpenuhi
        if (currentState == RobotState.MovingForward)
        {
            MoveForward();
        }
        // Safety net: Jika state aneh tapi sampai sini, reset ke MovingForward
        // (Kecuali state manuver yg sudah di-handle return di atas)
        else if (currentState != RobotState.SeekingBomb &&
                 currentState != RobotState.NavigatingAroundObstacle) // Pastikan tidak reset jika baru switch ke seeking/navigating
        {
             Debug.LogWarning($"Unexpected state {currentState} reached default movement logic. Resetting to MovingForward.");
             currentState = RobotState.MovingForward;
             MoveForward();
        }
    }

    // ... (Semua fungsi lainnya: RecordPositionHistory, IsRepeatingArea, MoveForward, ExecuteSeekBomb, ExecuteNavigateAroundObstacle, DecideNavigationTurnDirection, GetDistanceOrMax, ExecuteBacktrack, ExecuteForwardTurn, InitiateManeuverTurn, ExecuteManeuverTurn, CheckAndTargetBomb, IsObstacleDetected, OnDrawGizmosSelected tetap SAMA seperti sebelumnya) ...


    // =========================================================================
    //  History & Loop Detection (Tetap Sama)
    // =========================================================================
    void RecordPositionHistory()
    {
        positionRecordTimer -= Time.deltaTime;
        if (positionRecordTimer <= 0f)
        {
            positionHistory.Enqueue(transform.position);
            while (positionHistory.Count > positionHistoryLength) { positionHistory.Dequeue(); }
            positionRecordTimer = recordPositionInterval;
        }
    }

    bool IsRepeatingArea()
    {
        if (positionHistory.Count < positionHistoryLength * 0.7f) { return false; }
        int closeCount = 0;
        Vector3 currentPos = transform.position;
        Vector3[] historyArray = positionHistory.ToArray();
        int pointsToCheck = historyArray.Length - 5;
        for (int i = 0; i < pointsToCheck; i++)
        {
            float distanceSqr = (currentPos - historyArray[i]).sqrMagnitude;
            if (distanceSqr < repetitionDistanceThreshold * repetitionDistanceThreshold)
            {
                closeCount++;
                if (closeCount >= minClosePointsForLoop) { return true; }
            }
        }
        return false;
    }


    // =========================================================================
    //  Movement & Maneuver Functions (Tetap Sama)
    // =========================================================================
     void MoveForward()
    {
        transform.position += transform.forward * moveSpeed * Time.deltaTime;
    }

    void ExecuteSeekBomb()
    {
        // 1. Cek Target Valid
        if (targetBomb == null || !targetBomb.gameObject.activeInHierarchy) {
            targetBomb = null;
            currentState = RobotState.MovingForward;
            return;
        }
        // 2. Arahkan ke Bom
        Vector3 directionToBomb = (targetBomb.position - transform.position);
        directionToBomb.y = 0;
        float distanceToBomb = directionToBomb.magnitude;
        if (distanceToBomb > 0.1f) {
             directionToBomb.Normalize();
             Quaternion targetRotation = Quaternion.LookRotation(directionToBomb);
             transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
        // 3. Cek Obstacle LANGSUNG di jalur ke bom
        float checkDistance = Mathf.Min(distanceToBomb, frontObstacleDistance * 1.2f);
        if (distanceToBomb > 0.5f && IsObstacleDetected(directionToBomb, checkDistance))
        {
             // Debug.Log($"Obstacle detected in direct path to bomb {targetBomb.name}. Switching to NavigatingAroundObstacle.");
             navigationAttempts = 0;
             currentTurnDirection = DecideNavigationTurnDirection();
             maneuverTimer = navigationStepDuration;
             currentState = RobotState.NavigatingAroundObstacle;
             return;
        }
        // 4. Bergerak Maju Jika Jalan Bebas dan Masih Jauh
        float stopDistance = 1.5f;
        if(distanceToBomb > stopDistance) {
            transform.position += transform.forward * moveSpeed * Time.deltaTime;
        } else {
             // Debug.Log("Reached bomb vicinity.");
        }
     }

    void ExecuteNavigateAroundObstacle()
    {
        // 1. Cek Target Valid
        if (targetBomb == null || !targetBomb.gameObject.activeInHierarchy) {
            // Debug.Log("Target bomb lost during navigation. Returning to MovingForward.");
            currentState = RobotState.MovingForward;
            targetBomb = null;
            navigationAttempts = 0;
            return;
        }
        // 2. Lakukan Manuver Navigasi
        if (maneuverTimer > 0) {
            transform.position += transform.forward * moveSpeed * navigationSpeedMultiplier * Time.deltaTime;
            transform.Rotate(0, currentTurnDirection * rotationSpeed * 0.8f * Time.deltaTime, 0);
            maneuverTimer -= Time.deltaTime;
            if (IsObstacleDetected(transform.forward, frontObstacleDistance * 0.6f)) {
                 // Debug.Log("Hit obstacle during navigation step. Reversing turn direction.");
                 currentTurnDirection *= -1f;
                 maneuverTimer = navigationStepDuration * 0.5f;
            }
        } else { // Timer habis
            navigationAttempts++;
            // Debug.Log($"Navigation step {navigationAttempts}/{maxNavigationAttempts} finished.");
            // 3. Cek Lagi Jalan ke Bom
            Vector3 directionToBomb = (targetBomb.position - transform.position);
            directionToBomb.y = 0;
            float distanceToBomb = directionToBomb.magnitude;
            if (distanceToBomb > 0.1f) { directionToBomb.Normalize(); }
            float checkDistance = Mathf.Min(distanceToBomb, frontObstacleDistance * 1.2f);
            if (!IsObstacleDetected(directionToBomb, checkDistance)) {
                // Debug.Log($"Path to bomb {targetBomb.name} is now clear! Switching back to SeekingBomb.");
                currentState = RobotState.SeekingBomb;
                navigationAttempts = 0;
            } else { // Masih terhalang
                if (navigationAttempts >= maxNavigationAttempts) {
                    Debug.LogWarning($"Failed to navigate around obstacle for bomb {targetBomb.name}. Giving up.");
                    currentState = RobotState.MovingForward;
                    targetBomb = null;
                    navigationAttempts = 0;
                    InitiateManeuverTurn(Random.Range(100f, 170f) * (Random.Range(0,2)==0?1f:-1f));
                } else {
                    // Debug.Log("Path still blocked. Initiating next navigation step.");
                    currentTurnDirection = DecideNavigationTurnDirection();
                    maneuverTimer = navigationStepDuration;
                }
            }
        }
    }

    float DecideNavigationTurnDirection() {
        Vector3 rightCheckDir = Quaternion.Euler(0, 45, 0) * transform.forward;
        Vector3 leftCheckDir = Quaternion.Euler(0, -45, 0) * transform.forward;
        float distRight = GetDistanceOrMax(rightCheckDir, frontObstacleDistance * 1.5f);
        float distLeft = GetDistanceOrMax(leftCheckDir, frontObstacleDistance * 1.5f);
        if (distRight > distLeft + 0.2f) { return 1f; }
        else if (distLeft > distRight + 0.2f) { return -1f; }
        else { return (Random.Range(0,2) == 0 ? 1f : -1f); }
    }

    float GetDistanceOrMax(Vector3 direction, float maxDistance) {
        Vector3 castOrigin = transform.position + direction * sensorRadius * 0.1f + Vector3.up * 0.1f;
        RaycastHit hit;
        if (Physics.SphereCast(castOrigin, sensorRadius*0.8f, direction, out hit, maxDistance, obstacleLayer, QueryTriggerInteraction.Ignore)) {
            return hit.distance;
        }
        return maxDistance;
    }

     void ExecuteBacktrack()
     {
         if (maneuverTimer > 0) {
             transform.position -= transform.forward * moveSpeed * backwardSpeedMultiplier * Time.deltaTime;
             transform.Rotate(0, currentTurnDirection * rotationSpeed * Time.deltaTime, 0);
             maneuverTimer -= Time.deltaTime;
         } else {
             currentState = RobotState.ForwardTurning;
             maneuverTimer = forwardTurnDuration;
         }
     }

     void ExecuteForwardTurn()
     {
         if (maneuverTimer > 0) {
             transform.position += transform.forward * moveSpeed * Time.deltaTime;
             if (IsObstacleDetected(transform.forward, frontObstacleDistance * 0.5f)) {
                 currentState = RobotState.Backtracking;
                 maneuverTimer = backtrackDuration;
                 return;
             }
             transform.Rotate(0, currentTurnDirection * rotationSpeed * Time.deltaTime, 0);
             maneuverTimer -= Time.deltaTime;
         } else {
             currentState = RobotState.MovingForward;
         }
     }

    void InitiateManeuverTurn(float angle) {
         // Hanya set state dan target, jangan set flag isExecuting di sini
         // flag akan dicek di awal Update berikutnya
         currentState = RobotState.ExecutingManeuverTurn;
         targetManeuverRotation = transform.rotation * Quaternion.Euler(0, angle, 0);
         isExecutingManeuverTurn = true; // Set flag agar dikenali di frame berikutnya
     }

     void ExecuteManeuverTurn()
     {
         if (isExecutingManeuverTurn) { // Cek flag internal
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetManeuverRotation, rotationSpeed * 1.1f * Time.deltaTime);
            if (Quaternion.Angle(transform.rotation, targetManeuverRotation) < 5.0f) {
                transform.rotation = targetManeuverRotation;
                isExecutingManeuverTurn = false; // Selesai, reset flag
                currentState = RobotState.MovingForward; // Kembali normal setelah selesai
            }
         } else {
             // Jika state somehow ExecutingManeuverTurn tapi flag false, reset
             currentState = RobotState.MovingForward;
         }
     }

    // =========================================================================
    //  Sensor & Detection Functions (Tetap Sama)
    // =========================================================================
    bool CheckAndTargetBomb()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, bombDetectionRadius);
        float closestDistSqr = bombDetectionRadius * bombDetectionRadius + 1.0f;
        Transform potentialTarget = null;
        foreach (var hitCollider in hitColliders) {
            if (hitCollider.CompareTag(bombTag) && hitCollider.gameObject.activeInHierarchy) {
                float distSqr = (hitCollider.transform.position - transform.position).sqrMagnitude;
                if (distSqr < closestDistSqr) {
                    closestDistSqr = distSqr;
                    potentialTarget = hitCollider.transform;
                }
            }
        }
        if (potentialTarget != null) {
            if (targetBomb != potentialTarget) { targetBomb = potentialTarget; }
            return true;
        } else {
            if (targetBomb != null) { targetBomb = null; }
            return false;
        }
    }

    bool IsObstacleDetected(Vector3 direction, float maxDistance)
    {
        Vector3 castOrigin = transform.position + direction * sensorRadius * 0.1f + Vector3.up * 0.1f;
        RaycastHit hit;
        bool detected = Physics.SphereCast(
            castOrigin, sensorRadius, direction, out hit, maxDistance,
            obstacleLayer, QueryTriggerInteraction.Ignore
        );
        return detected;
    }

    // =========================================================================
    //  Gizmos (Visualisasi di Editor - Tetap Sama)
    // =========================================================================
    void OnDrawGizmosSelected()
    {
        // Bomb detection radius
        if(Application.isPlaying) Gizmos.color = Color.yellow; else Gizmos.color = Color.grey;
        Gizmos.DrawWireSphere(transform.position, bombDetectionRadius);

        // Sensor Depan & Belakang (Visualisasi sederhana)
         Vector3 fwdOrigin = transform.position + transform.forward * sensorRadius * 0.1f + Vector3.up * 0.1f;
         Vector3 backOrigin = transform.position - transform.forward * sensorRadius * 0.1f + Vector3.up * 0.1f;
        Gizmos.color = Color.blue; Gizmos.DrawLine(fwdOrigin, fwdOrigin + transform.forward * frontObstacleDistance);
        Gizmos.color = Color.cyan; Gizmos.DrawLine(backOrigin, backOrigin - transform.forward * backObstacleDistance);

        if(Application.isPlaying) {
            // Visualisasi Arah Cek Navigasi
            if (currentState == RobotState.NavigatingAroundObstacle) {
                Gizmos.color = Color.white;
                Vector3 rightCheckDir = Quaternion.Euler(0, 45, 0) * transform.forward;
                Vector3 leftCheckDir = Quaternion.Euler(0, -45, 0) * transform.forward;
                Gizmos.DrawRay(transform.position + Vector3.up*0.1f, rightCheckDir * frontObstacleDistance * 1.5f);
                Gizmos.DrawRay(transform.position + Vector3.up*0.1f, leftCheckDir * frontObstacleDistance * 1.5f);
            }

            // Visualisasi position history
            if (positionHistory != null && positionHistory.Count > 0) {
                Gizmos.color = Color.magenta; int i = 0;
                foreach(Vector3 pos in positionHistory) {
                    float alpha = (float)i / positionHistory.Count;
                    Gizmos.color = new Color(Color.magenta.r, Color.magenta.g, Color.magenta.b, alpha * 0.8f + 0.2f);
                    Gizmos.DrawSphere(pos, 0.1f * (alpha * 0.5f + 0.5f)); i++;
                }
            }

            // Visualisasi arah ke bom jika ada target
            if (targetBomb != null) {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position + Vector3.up * 0.2f, targetBomb.position);
            }
        }
    }
}