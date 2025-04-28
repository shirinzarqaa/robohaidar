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
    public int positionHistoryLength = 20;
    public float recordPositionInterval = 0.5f;
    public float repetitionDistanceThreshold = 1.5f;
    public float breakLoopTurnAngle = 90f;
    private Queue<Vector3> positionHistory = new Queue<Vector3>();
    private float positionRecordTimer = 0f;

    [Header("State (Debug Only)")]
    [SerializeField] private RobotState currentState = RobotState.MovingForward;
    private Transform targetBomb = null;
    private float maneuverTimer = 0f;
    private float currentTurnDirection = 1f;
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
        RecordPositionHistory();

        bool frontBlocked = IsObstacleDetected(transform.forward, frontObstacleDistance);
        bool backBlocked = IsObstacleDetected(-transform.forward, backObstacleDistance);

        if (isExecutingStuckTurn) {
            ExecuteStuckTurn();
            return;
        }

        if (frontBlocked && backBlocked && currentState != RobotState.TurningFromStuck)
        {
            InitiateStuckTurn(Random.Range(0, 2) == 0 ? breakLoopTurnAngle : -breakLoopTurnAngle);
            return;
        }

        if (frontBlocked && currentState == RobotState.MovingForward)
        {
            if (IsRepeatingArea())
            {
                InitiateStuckTurn(Random.Range(0, 2) == 0 ? breakLoopTurnAngle : -breakLoopTurnAngle);
                positionHistory.Clear();
                positionRecordTimer = recordPositionInterval;
            }
            else
            {
                currentState = RobotState.Backtracking;
                maneuverTimer = backtrackDuration;
                currentTurnDirection = Random.Range(0, 2) == 0 ? 1f : -1f;
            }
            return;
        }

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

    bool IsRepeatingArea()
    {
        if (positionHistory.Count < positionHistoryLength / 2)
        {
            return false;
        }

        Vector3[] historyArray = positionHistory.ToArray();
        int pointsToCheck = historyArray.Length - 3;
        int closeCount = 0;

        for (int i = 0; i < pointsToCheck; i++)
        {
            float distanceSqr = (transform.position - historyArray[i]).sqrMagnitude;
            if (distanceSqr < repetitionDistanceThreshold * repetitionDistanceThreshold)
            {
                closeCount++;
            }
        }

        return closeCount > 0;
    }

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

    void InitiateStuckTurn(float angle)
    {
        currentState = RobotState.TurningFromStuck;
        stuckTargetRotation = transform.rotation * Quaternion.Euler(0, angle, 0);
        isExecutingStuckTurn = true;
    }

    void ExecuteStuckTurn()
    {
        if (isExecutingStuckTurn) {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, stuckTargetRotation, rotationSpeed * 1.5f * Time.deltaTime);

            if (Quaternion.Angle(transform.rotation, stuckTargetRotation) < 2.0f)
            {
                transform.rotation = stuckTargetRotation;
                isExecutingStuckTurn = false;
                currentState = RobotState.MovingForward;
            }
        } else {
            currentState = RobotState.MovingForward;
        }
    }

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
        return detected;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, bombDetectionRadius);
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * frontObstacleDistance);
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, transform.position - transform.forward * backObstacleDistance);

        if (positionHistory != null && positionHistory.Count > 0) {
            Gizmos.color = Color.magenta;
            foreach(Vector3 pos in positionHistory) {
                Gizmos.DrawSphere(pos, 0.1f);
            }
        }
    }
}
