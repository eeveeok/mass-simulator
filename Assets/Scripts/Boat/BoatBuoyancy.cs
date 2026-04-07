using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using Unity.Mathematics;

/// <summary>
/// 보트 부력 계산 및 물리 적용 시스템
/// </summary>
[RequireComponent(typeof(BoatCore))]
public class BoatBuoyancy : MonoBehaviour
{
    [Header("부력 설정")]
    [Range(0.1f, 0.3f)] public float depthBeforeSubmersion = 0.1f; // 잠기기 시작하는 깊이
    [Range(0.5f, 2f)] public float displacementAmount = 0.5f;      // 부력 배수량 계수

    private BoatCore core;
    private WaterSearchParameters searchParams = new WaterSearchParameters();
    private WaterSearchResult searchResult = new WaterSearchResult();
    private Vector3 averageWaveNormal;

    public Vector3 AverageWaveNormal => averageWaveNormal;           // 평균 파도 노멀
    public Vector3 SmoothedWaveNormal { get; private set; } // 부드러운 파도 노멀

    void Awake() => core = GetComponent<BoatCore>();

    void FixedUpdate()
    {
        UpdateWaveInformation(); // 파도 정보 갱신
        ApplyBuoyancy();         // 부력 적용
    }

    /// <summary>
    /// 모든 부력점에서 파도 정보 수집 및 평균 계산
    /// </summary>
    void UpdateWaveInformation()
    {
        averageWaveNormal = Vector3.zero;
        int validPoints = 0;

        foreach (Transform point in core.buoyancyPoints)
        {
            // 물 표면 검색 파라미터 설정
            searchParams.startPositionWS = searchResult.candidateLocationWS;
            searchParams.targetPositionWS = point.position;

            // 물 표면 프로젝션 성공 시
            if (core.water != null &&
                core.water.ProjectPointOnWaterSurface(searchParams, out searchResult))
            {
                Vector3 waveNormal = Float3ToVector3(searchResult.normalWS);
                averageWaveNormal += waveNormal;
                validPoints++;
            }
        }

        // 유효한 점이 있을 경우 평균 계산
        if (validPoints > 0)
        {
            averageWaveNormal /= validPoints;
            // 부드러운 보간 적용
            SmoothedWaveNormal = Vector3.Lerp(
                SmoothedWaveNormal,
                averageWaveNormal,
                10f * Time.fixedDeltaTime
            );
        }
    }

    /// <summary>
    /// 부력 물리 적용 (아르키메데스 원리)
    /// </summary>
    void ApplyBuoyancy()
    {
        // 중력 분할 적용
        core.RigidBody.AddForce(Physics.gravity / core.buoyancyPoints.Length,
                                ForceMode.Acceleration);

        foreach (Transform point in core.buoyancyPoints)
        {
            searchParams.startPositionWS = searchResult.candidateLocationWS;
            searchParams.targetPositionWS = point.position;

            if (core.water != null &&
                core.water.ProjectPointOnWaterSurface(searchParams, out searchResult))
            {
                // 부력점이 물 아래에 있을 경우
                if (point.position.y < searchResult.projectedPositionWS.y)
                {
                    // 잠긴 깊이 계산 (0-1)
                    float submersionDepth = Mathf.Clamp01(
                        (searchResult.projectedPositionWS.y - point.position.y) /
                        depthBeforeSubmersion
                    );

                    // 부력 계산 (|중력| x 잠긴 깊이 x 배수량)
                    float buoyancyForce = Mathf.Abs(Physics.gravity.y) *
                                         submersionDepth *
                                         displacementAmount;

                    // 부력 적용 (물리 엔진)
                    core.RigidBody.AddForceAtPosition(
                        Vector3.up * buoyancyForce,
                        point.position,
                        ForceMode.Acceleration
                    );
                }
            }
        }

        // 저항력 적용 (물의 저항 효과)
        core.RigidBody.AddForce(-core.RigidBody.linearVelocity * 2f, ForceMode.Acceleration);
        core.RigidBody.AddTorque(-core.RigidBody.angularVelocity * 1.8f, ForceMode.Acceleration);
    }

    /// <summary>
    /// float3를 Vector3로 변환
    /// </summary>
    private Vector3 Float3ToVector3(float3 f3) => new Vector3(f3.x, f3.y, f3.z);
}