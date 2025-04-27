using UnityEngine;

public class RobotController : MonoBehaviour
{
    public float moveSpeed = 3f;
    public float rotationSpeed = 100f;
    public float changeDirectionTime = 2f;
    public float obstacleDetectionDistance = 2f;
    public LayerMask obstacleLayer;

    private Rigidbody rb;
    private float timer = 0f;
    private float currentDirection = 1f;
    private bool isAvoidingObstacle = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        // Pastikan Rigidbody tidak dipengaruhi rotasi dari physics
        rb.freezeRotation = true;
    }

    void Update()
    {
        // Cek adanya penghalang dengan Raycast
        bool obstacleDetected = DetectObstacle();

        if (obstacleDetected)
        {
            // Jika ada penghalang, putar menjauh
            AvoidObstacle();
        }
        else
        {
            // Jika tidak ada penghalang, lakukan gerakan zigzag normal
            ZigzagMovement();
        }
    }

    bool DetectObstacle()
    {
        // Buat raycast ke depan untuk mendeteksi obstacle
        RaycastHit hit;
        if (Physics.Raycast(transform.position, transform.forward, out hit, obstacleDetectionDistance, obstacleLayer))
        {
            Debug.DrawRay(transform.position, transform.forward * obstacleDetectionDistance, Color.red);
            return true;
        }

        // Tambahkan beberapa raycast diagonal untuk deteksi yang lebih baik
        Vector3 rightDirection = Quaternion.Euler(0, 30, 0) * transform.forward;
        if (Physics.Raycast(transform.position, rightDirection, out hit, obstacleDetectionDistance, obstacleLayer))
        {
            Debug.DrawRay(transform.position, rightDirection * obstacleDetectionDistance, Color.red);
            return true;
        }

        Vector3 leftDirection = Quaternion.Euler(0, -30, 0) * transform.forward;
        if (Physics.Raycast(transform.position, leftDirection, out hit, obstacleDetectionDistance, obstacleLayer))
        {
            Debug.DrawRay(transform.position, leftDirection * obstacleDetectionDistance, Color.red);
            return true;
        }

        Debug.DrawRay(transform.position, transform.forward * obstacleDetectionDistance, Color.green);
        return false;
    }

    void AvoidObstacle()
    {
        isAvoidingObstacle = true;

        // Putar untuk menghindari obstacle
        float avoidanceRotation = 180f;
        Quaternion targetRotation = Quaternion.Euler(0, avoidanceRotation, 0);
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, rb.rotation * targetRotation, Time.deltaTime * 2f));

        // Tetap bergerak maju saat menghindar (dengan kecepatan lebih lambat)
        Vector3 forwardMovement = transform.forward * (moveSpeed * 0.5f) * Time.deltaTime;
        rb.MovePosition(rb.position + forwardMovement);

        // Reset timer dan arah setelah menghindar
        timer = 0f;
        currentDirection *= -1;

        // Setelah beberapa frame, kembali ke gerakan zigzag normal
        Invoke("ResetAvoidance", 1.0f);
    }

    void ResetAvoidance()
    {
        isAvoidingObstacle = false;
    }

    void ZigzagMovement()
    {
        if (isAvoidingObstacle)
            return;

        // Update timer
        timer += Time.deltaTime;

        // Ganti arah setiap beberapa detik
        if (timer >= changeDirectionTime)
        {
            currentDirection *= -1;
            timer = 0f;
        }

        // Pergerakan maju
        Vector3 forwardMovement = transform.forward * moveSpeed * Time.deltaTime;
        rb.MovePosition(rb.position + forwardMovement);

        // Rotasi zigzag
        float rotationAmount = currentDirection * rotationSpeed * Time.deltaTime;
        Quaternion turnRotation = Quaternion.Euler(0f, rotationAmount, 0f);
        rb.MoveRotation(rb.rotation * turnRotation);
    }
}