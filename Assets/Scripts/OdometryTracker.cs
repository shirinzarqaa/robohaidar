using UnityEngine;

public class OdometryTracker : MonoBehaviour
{
    public float wheelBase = 0.5f;
    public float trackingInterval = 0.05f;

    [HideInInspector] public Vector3 estimatedPosition = Vector3.zero; // Changed to Vector3
    [HideInInspector] public float estimatedOrientation = 0f;

    public float leftWheelSpeed = 0f;
    public float rightWheelSpeed = 0f;

    private float timer = 0f;

    void FixedUpdate()
    {
        timer += Time.fixedDeltaTime;

        if (timer >= trackingInterval)
        {
            SimulateOdometry(trackingInterval);
            timer -= trackingInterval; // Subtract instead of reset to maintain timing
        }
    }

    void SimulateOdometry(float dt)
    {
        // Differential drive equations
        float v = (rightWheelSpeed + leftWheelSpeed) / 2f;        // linear velocity
        float omega = (rightWheelSpeed - leftWheelSpeed) / wheelBase; // angular velocity

        float dx = v * Mathf.Cos(estimatedOrientation) * dt;
        float dz = v * Mathf.Sin(estimatedOrientation) * dt; // Using z instead of y for ground plane
        float dtheta = omega * dt;

        estimatedPosition += new Vector3(dx, 0, dz);
        estimatedOrientation += dtheta;
        
        // Normalize angle to -π to π
        estimatedOrientation = Mathf.Atan2(Mathf.Sin(estimatedOrientation), Mathf.Cos(estimatedOrientation));
    }

    public void SetWheelSpeeds(float left, float right)
    {
        leftWheelSpeed = left;
        rightWheelSpeed = right;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(estimatedPosition, 0.1f);
        Gizmos.DrawRay(estimatedPosition, 
                       new Vector3(Mathf.Cos(estimatedOrientation), 0, Mathf.Sin(estimatedOrientation)));
    }
}