using UnityEngine;
using System.Collections.Generic;

public class RobotController : MonoBehaviour
{
    // --- Movement Settings ---
    public float moveSpeed = 3f;                  // Speed when moving forward
    public float rotationSpeed = 60f;              // Speed of rotation (degrees per second)
    public float backwardSpeedMultiplier = 0.7f;   // Slower movement speed when moving backward

    [Header("Sensors")]
    public float sensorLength = 1.5f;              // How far the robot can detect obstacles
    public float sensorRadius = 0.2f;              // Width of the sensor ray
    public LayerMask obstacleLayer;                // Layer to detect obstacles
    public float frontObstacleDistance = 0.8f;     // Distance to detect obstacles in front
    public float backObstacleDistance = 0.5f;      // Distance to detect obstacles behind

    [Header("Bomb Detection")]
    public float bombDetectionRadius = 5.0f;       // Radius around the robot to search for bombs
    public string bombTag = "Bombs";                // Tag used to identify bomb objects

    [Header("Maneuver Settings")]
    public float backtrackDuration = 1.0f;          // How long to back up when obstacle detected
    public float forwardTurnDuration = 1.0f;        // How long to turn after backing up
    public float stuckTurnAngle = 90f;              // Angle to turn when stuck

    [Header("Loop Prevention")]
    public int positionHistoryLength = 20;          // How many past positions to remember
    public float recordPositionInterval = 0.5f;     // How often to record position
    public float repetitionDistanceThreshold = 1.5f;// Minimum distance to consider two positions the same
    public float breakLoopTurnAngle = 90f;           // Angle to turn to break out of loops

    private Queue<Vector3> positionHistory = new Queue<Vector3>(); // Recent position history
    private float positionRecordTimer = 0f;                        // Timer to manage position recording

    [Header("State (Debug Only)")]
    [SerializeField] private RobotState currentState = RobotState.MovingForward;
    private Transform targetBomb = null;            // Current bomb target
    private float maneuverTimer = 0f;               // Timer for backtrack and turning maneuvers
    private float currentTurnDirection = 1f;        // Direction of turn: +1 (right) or -1 (left)
    private Quaternion stuckTargetRotation;         // Target rotation when trying to break out of stuck
    private bool isExecutingStuckTurn = false;      // Whether currently performing stuck turn

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
        RecordPositionHistory(); // Regularly update position history for loop detection

        // Check obstacles
        bool frontBlocked = IsObstacleDetected(transform.forward, frontObstacleDistance);
        bool backBlocked = IsObstacleDetected(-transform.forward, backObstacleDistance);

        // Handle stuck rotation first if currently executing
        if (isExecutingStuckTurn) {
            ExecuteStuckTurn();
            return;
        }

        // If stuck both front and back, initiate a stuck turn
        if (frontBlocked && backBlocked && currentState != RobotState.TurningFromStuck)
        {
            InitiateStuckTurn(Random.Range(0, 2) == 0 ? breakLoopTurnAngle : -breakLoopTurnAngle);
            return;
        }

        // If front is blocked while moving forward, decide how to react
        if (frontBlocked && currentState == RobotState.MovingForward)
        {
            if (IsRepeatingArea())
            {
                // If looping detected, force a stuck turn
                InitiateStuckTurn(Random.Range(0, 2) == 0 ? breakLoopTurnAngle : -breakLoopTurnAngle);
                positionHistory.Clear();
                positionRecordTimer = recordPositionInterval;
            }
            else
            {
                // Otherwise, back up and then turn
                currentState = RobotState.Backtracking;
                maneuverTimer = backtrackDuration;
                currentTurnDirection = Random.Range(0, 2) == 0 ? 1f : -1f;
            }
            return;
        }

        // Look for bombs
        if (CheckAndTargetBomb())
        {
            currentState = RobotState.SeekingBomb;
            ExecuteSeekBomb();
            return;
        }
        // If previously seeking a bomb but now lost it
        if (currentState == RobotState.SeekingBomb && targetBomb == null)
        {
            currentState = RobotState.MovingForward;
        }

        // Normal movement based on current state
        switch (currentState)
        {
            case RobotState.MovingForward:
                MoveForward();
                break;
            case RobotState.SeekingBomb:
                currentState = RobotState.MovingForward;
                break;
            case RobotState.Backtracking:
                ExecuteBacktrack();
                break;
            case RobotState.ForwardTurning:
                ExecuteForwardTurn();
                break;
            case RobotState.TurningFromStuck:
                if (!isExecutingStuckTurn) currentState = RobotState.MovingForward;
                break;
        }
    }

    // Record robot's position periodically to detect looping.
    void RecordPositionHistory()
    {
        positionRecordTimer -= Time.deltaTime;
        if (positionRecordTimer <= 0f)
        {
            positionHistory.Enqueue(transform.position);

            while (positionHistory.Count > positionHistoryLength)
            {
                positionHistory.Dequeue();
            }

            positionRecordTimer = recordPositionInterval;
        }
    }

    // Detects if the robot is stuck in the same area based on position history.
    bool IsRepeatingArea()
    {
        if (positionHistory.Count < positionHistoryLength / 2)
            return false;

        Vector3[] historyArray = positionHistory.ToArray();
        int pointsToCheck = historyArray.Length - 3;
        int closeCount = 0;

        for (int i = 0; i < pointsToCheck; i++)
        {
            float distanceSqr = (transform.position - historyArray[i]).sqrMagnitude;
            if (distanceSqr < repetitionDistanceThreshold * repetitionDistanceThreshold)
                closeCount++;
        }

        return closeCount > 0;
    }

    // Moves the robot straight forward.
    void MoveForward()
    {
        transform.position += transform.forward * moveSpeed * Time.deltaTime;
    }

    // Steers robot toward the detected bomb.
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
        if (distanceToBomb > 1.0f) {
            transform.position += transform.forward * moveSpeed * Time.deltaTime;
        }
    }

    // Backs up and rotates slightly to avoid an obstacle.
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

    // Moves forward while rotating to realign after backing up.
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

    // Start a special rotation to escape from being stuck.
    void InitiateStuckTurn(float angle)
    {
        currentState = RobotState.TurningFromStuck;
        stuckTargetRotation = transform.rotation * Quaternion.Euler(0, angle, 0);
        isExecutingStuckTurn = true;
    }

    // Rotate toward a target angle to break out of stuck situations.
    void ExecuteStuckTurn()
    {
        if (isExecutingStuckTurn)
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, stuckTargetRotation, rotationSpeed * 1.5f * Time.deltaTime);

            if (Quaternion.Angle(transform.rotation, stuckTargetRotation) < 2.0f)
            {
                transform.rotation = stuckTargetRotation;
                isExecutingStuckTurn = false;
                currentState = RobotState.MovingForward;
            }
        }
        else
        {
            currentState = RobotState.MovingForward;
        }
    }

    // Looks for nearby bombs and targets the closest one.
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

        if (potentialTarget != null)
        {
            targetBomb = potentialTarget;
            return true;
        }
        else
        {
            targetBomb = null;
            return false;
        }
    }

    // Uses a spherecast to check if an obstacle is detected in a given direction.
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
        return detected;
    }

    // Draw debug gizmos to visualize detection ranges and history.
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, bombDetectionRadius); // Bomb detection radius

        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * frontObstacleDistance); // Front sensor

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, transform.position - transform.forward * backObstacleDistance); // Back sensor

        if (positionHistory != null && positionHistory.Count > 0)
        {
            Gizmos.color = Color.magenta;
            foreach (Vector3 pos in positionHistory)
            {
                Gizmos.DrawSphere(pos, 0.1f); // Past recorded positions
            }
        }
    }
}
