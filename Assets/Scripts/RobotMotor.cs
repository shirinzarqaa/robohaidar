using UnityEngine;

public class RobotMotor : MonoBehaviour
{
    public OdometryTracker odometry;
    public float wheelBase = 0.5f;

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        Debug.Log($"[Motor] v: {(odometry.leftWheelSpeed + odometry.rightWheelSpeed) / 2f}");

        float v = (odometry.leftWheelSpeed + odometry.rightWheelSpeed) / 2f;
        float omega = (odometry.rightWheelSpeed - odometry.leftWheelSpeed) / wheelBase;
        
        float dx = v * dt;
        float dtheta = omega * dt;

        transform.Translate(Vector3.forward * dx);

        transform.Rotate(Vector3.up * Mathf.Rad2Deg * dtheta);
    }
}
