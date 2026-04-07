using UnityEngine;

/// <summary>
/// 보트 안정화 및 흔들림 효과 시스템
/// </summary>
[RequireComponent(typeof(BoatCore), typeof(BoatBuoyancy))]
public class BoatStabilization : MonoBehaviour
{
    [Header("파도 효과")]
    [Range(0.5f, 50f)] public float tiltIntensity = 2f;    // 기울기 강도
    [Range(0.1f, 5f)] public float tiltSmoothness = 0.5f;  // 기울기 부드러움
    [Range(0.5f, 50f)] public float rollIntensity = 1.5f;  // 좌우 기울기 강도

    [Header("복원 설정")]
    [Range(1f, 50f)] public float restorationForce = 8f;   // 복원력
    [Range(0.1f, 5f)] public float restorationDamping = 0.2f; // 복원 감쇠
    [Range(0.1f, 5f)] public float rockingFrequency = 0.8f; // 흔들림 주기

    private BoatCore core;
    private BoatBuoyancy buoyancy;
    private Vector3 currentTilt;     // 현재 기울기
    private Vector3 targetTilt;      // 목표 기울기
    private Vector3 restorationVelocity; // 복원 속도
    private float rockingTimer;      // 흔들림 타이머

    void Awake()
    {
        core = GetComponent<BoatCore>();
        buoyancy = GetComponent<BoatBuoyancy>();
        rockingTimer = Random.Range(0f, 10f); // 초기 값 랜덤화
    }

    void FixedUpdate()
    {
        ApplyRestorationPhysics(); // 복원 물리 적용
        UpdateBoatRotation();     // 보트 회전 업데이트
    }

    /// <summary>
    /// 복원 물리 계산 (스프링-댐퍼 시스템)
    /// </summary>
    void ApplyRestorationPhysics()
    {
        // 파도 기울기 + 자연 흔들림 효과
        Vector3 waveTilt = CalculateWaveTilt();
        Vector3 rockingEffect = CalculateRockingEffect();
        targetTilt = waveTilt + rockingEffect;

        // 물리 기반 보간
        Vector3 displacement = targetTilt - currentTilt;
        Vector3 acceleration = displacement * restorationForce;
        restorationVelocity += acceleration * Time.fixedDeltaTime;
        restorationVelocity *= (1f - restorationDamping); // 감쇠 적용
        currentTilt += restorationVelocity * Time.fixedDeltaTime;
    }

    /// <summary>
    /// 파도 노멀 기반 기울기 계산
    /// </summary>
    Vector3 CalculateWaveTilt()
    {
        if (buoyancy.SmoothedWaveNormal == Vector3.zero)
            return Vector3.zero;

        // 보트 로컬 좌표계로 변환
        Vector3 localWaveNormal = core.boatModel.InverseTransformDirection(buoyancy.SmoothedWaveNormal);

        // 롤(좌우) 및 피치(앞뒤) 계산
        float roll = Mathf.Clamp(localWaveNormal.x * 20f * rollIntensity, -15f, 15f);
        float pitch = Mathf.Clamp(localWaveNormal.z * 15f * tiltIntensity, -10f, 10f);

        return new Vector3(pitch, 0f, -roll); // Z축에 롤 적용
    }

    /// <summary>
    /// 자연스러운 흔들림 효과 계산
    /// </summary>
    Vector3 CalculateRockingEffect()
    {
        rockingTimer += Time.fixedDeltaTime * rockingFrequency;
        return new Vector3(
            Mathf.Sin(rockingTimer * 1.2f) * 2.5f, // X: 피치
            0f,
            Mathf.Cos(rockingTimer * 0.8f) * 3.5f  // Z: 롤
        );
    }

    /// <summary>
    /// 보트 모델 회전 업데이트
    /// </summary>
    void UpdateBoatRotation()
    {
        if (!core.boatModel) return;

        // 부드러운 기울기 전환
        core.boatModel.localRotation = Quaternion.Slerp(
            core.boatModel.localRotation,
            Quaternion.Euler(currentTilt),
            tiltSmoothness * Time.fixedDeltaTime * 10f
        );

        // 속도 기반 롤 각도 적용
        ApplySpeedBasedRoll();
    }

    /// <summary>
    /// 선회 시 자연스러운 기울기 효과
    /// </summary>
    public void ApplySpeedBasedRoll()
    {
        Vector3 velocity = core.RigidBody.linearVelocity;
        velocity.y = 0; // 수평 속도만 고려

        if (velocity.magnitude > 0.3f) // 이동 중일 때만 적용
        {
            // 전방과 속도 방향 간 각도 차이
            float angle = Vector3.SignedAngle(
                transform.forward,
                velocity.normalized,
                Vector3.up
            );

            float targetRoll = Mathf.Clamp(angle * 0.5f, -5f, 5f);
            Vector3 euler = core.boatModel.localEulerAngles;

            core.boatModel.localRotation = Quaternion.Euler(
                euler.x,
                euler.y,
                Mathf.LerpAngle(euler.z, targetRoll, 5f * Time.fixedDeltaTime)
            );
        }
        else // 정지 시 기울기 복원
        {
            Vector3 euler = core.boatModel.localEulerAngles;
            core.boatModel.localRotation = Quaternion.Euler(
                euler.x,
                euler.y,
                Mathf.LerpAngle(euler.z, 0, 2f * Time.fixedDeltaTime)
            );
        }
    }

    /// <summary>
    /// 화면에 디버그 정보 렌더링
    /// </summary>
    void OnGUI()
    {
        GUIStyle style = new GUIStyle
        {
            fontSize = 10,
            normal = { textColor = Color.white }
        };

        // 기울기 정보 표시
        GUI.Label(new Rect(10, 70, 500, 30), $"현재 기울기: {currentTilt}", style);
        GUI.Label(new Rect(10, 85, 500, 30), $"목표 기울기: {targetTilt}", style);
    }

    /// <summary>
    /// 기즈모를 이용한 시각적 디버깅
    /// </summary>
    void OnDrawGizmos()
    {
        var core = GetComponent<BoatCore>();

        if (core && core.boatModel)
        {
            // 현재 상방 벡터 (파란색)
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(
                core.boatModel.position,
                core.boatModel.position + core.boatModel.up
            );

            // 목표 상방 벡터 (녹색)
            Gizmos.color = Color.green;
            Vector3 targetUp = Quaternion.Euler(targetTilt) * Vector3.up;
            Gizmos.DrawLine(
                core.boatModel.position,
                core.boatModel.position + targetUp * 0.8f
            );
        }
    }
}