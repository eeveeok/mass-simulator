using UnityEngine;

/// <summary>
/// 보트 추진 및 조향 제어 시스템
/// </summary>
[RequireComponent(typeof(BoatCore))]
public class BoatMotor : MonoBehaviour
{
    [Header("모터 설정")]
    public float motorForwardForce = 35f;  // 전진 힘
    public float motorBackwardForce = 15f; // 후진 힘
    public float maxSpeed = 5f;            // 최대 속도

    [Header("조향 설정")]
    [Range(0.1f, 2f)] public float steeringIntensity = 0.4f; // 조향 감도
    public float rotationTorque = 0.5f;    // 회전 토크

    [HideInInspector] public bool allowInput = true; // 입력 허용 여부

    private BoatCore core;

    void Awake() => core = GetComponent<BoatCore>();

    void FixedUpdate()
    {
        if (allowInput) // 입력 허용 상태일 때만 처리
        {
            // 입력 처리
            float vertical = Input.GetAxis("Vertical");
            float horizontal = Input.GetAxis("Horizontal");
            HandleMotorMovement(vertical, horizontal);
        }
    }

    /// <summary>
    /// 모터 기반 이동 처리 (차동 구동)
    /// </summary>
    public void HandleMotorMovement(float vertical, float horizontal)
    {
        // 보트 전진 방향
        Vector3 forceDirection = -core.boatModel.right;

        // 차동 입력 계산
        float leftInput = Mathf.Clamp(vertical + (horizontal * steeringIntensity), -1f, 1f);
        float rightInput = Mathf.Clamp(vertical - (horizontal * steeringIntensity), -1f, 1f);

        // 전진/후진 별 힘 적용
        float leftForce = leftInput * (leftInput > 0 ? motorForwardForce : motorBackwardForce);
        float rightForce = rightInput * (rightInput > 0 ? motorForwardForce : motorBackwardForce);

        // 모터 위치에 힘 적용
        core.RigidBody.AddForceAtPosition(forceDirection * leftForce, core.leftMotor.position);
        core.RigidBody.AddForceAtPosition(forceDirection * rightForce, core.rightMotor.position);

        // 회전 토크 적용
        core.RigidBody.AddTorque(transform.up * rotationTorque * horizontal);

        // 속도 제한
        LimitSpeed();
    }

    /// <summary>
    /// 최대 속도 제한 적용
    /// </summary>
    public void LimitSpeed()
    {
        // 수평 속도 계산 (Y축 무시)
        Vector3 horizontalVelocity = new Vector3(
            core.RigidBody.linearVelocity.x,
            0,
            core.RigidBody.linearVelocity.z
        );

        // 최대 속도 초과 시 제한
        if (horizontalVelocity.magnitude > maxSpeed)
        {
            Vector3 limitedVelocity = horizontalVelocity.normalized * maxSpeed;
            core.RigidBody.linearVelocity = new Vector3(
                limitedVelocity.x,
                core.RigidBody.linearVelocity.y,
                limitedVelocity.z
            );
        }
    }
}