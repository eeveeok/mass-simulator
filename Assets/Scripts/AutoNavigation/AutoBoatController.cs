using UnityEngine;
using System.Collections.Generic;
using System.Collections;

[RequireComponent(typeof(BoatMotor), typeof(LidarSensor))]
public class ReactiveAutonomousBoatController : MonoBehaviour
{
    [System.Serializable]
    public class Waypoint
    {
        public Waypoint(float waitTime, float approachRadius, Transform targetPoint)
        {
            this.waitTime = waitTime;
            this.approachRadius = approachRadius;
            this.targetPoint = targetPoint;
        }

        public Transform targetPoint; // 목표 지점
        public float waitTime = 3f;   // 머무르는 시간 (초)
        public float approachRadius = 1.5f; // 웨이포인트 접근 반경 (개별 설정)

        [Header("도달 시 회전 설정")]
        public bool rotateAtReach = false; // 도달 시 회전 여부
        [Range(0f, 360f)] public float targetRotationAngle = 180f; // 목표 회전 각도 (Y축)

        [Header("도형 인식 설정")]
        public bool requireImageDetection = false; // 도형 인식이 필요한 웨이포인트인지 여부

        [HideInInspector] public bool isReached = false;
    }

    public FigureDetector figureDetector; // 도형 인식 컴포넌트 참조

    [Header("경로 설정")]
    public List<Waypoint> waypoints = new List<Waypoint>();
    public bool loopPath = false; // 경로 반복 여부
    public float globalWaypointThreshold = 8f; // 전역 웨이포인트 도달 거리

    [Header("장애물 회피 설정")]
    public float safeDistance = 2f; // 안전 거리
    public float avoidanceStrength = 1f; // 회피 강도
    public float detectionAngle = 80f; // 전방 감지 각도 (도)
    public float obstacleSlowFactor = 0.8f; // 장애물 근접시 속도 감소 비율

    [Header("주행 파라미터")]
    public float maxMotorInput = 0.7f; // 모터 최대 입력값 (0~1)
    public float steeringResponse = 2f; // 조향 반응성
    public float slowingDistance = 5f; // 감속 시작 거리
    public float minSpeedFactor = 0.8f; // 최소 속도 비율

    [Header("회전 전용 모드 설정")]
    public float rotateOnlyAngleThreshold = 60f; // 회전 전용 모드 진입 각도 임계값
    public float rotateOnlyObstacleDistance = 0.4f; // 회전 전용 모드 진입 장애물 거리

    // 컴포넌트 참조
    private BoatMotor boatMotor;
    private LidarSensor lidar;

    // 내부 상태 변수
    private int currentTargetIndex = 0;
    private bool isWaiting = false;
    private bool isAutopilotActive = false; // 자율주행 활성화 상태
    private Coroutine waitCoroutine;
    private Vector3 currentTargetPosition;
    private float currentWaypointThreshold; // 현재 적용 중인 웨이포인트 임계값

    void Start()
    {
        boatMotor = GetComponent<BoatMotor>();
        lidar = GetComponent<LidarSensor>();

        if (waypoints.Count == 0)
        {
            Debug.LogError("No waypoints assigned!");
        }
        else
        {
            SetCurrentTarget(waypoints[0].targetPoint.position);
            // 첫 웨이포인트 임계값 설정
            currentWaypointThreshold = GetCurrentThreshold();
        }
    }

    void Update()
    {
        // Minus 키로 자율주행 토글
        if (Input.GetKeyDown(KeyCode.Minus))
        {
            ToggleAutopilot();
        }
    }

    void FixedUpdate()
    {
        if (waypoints.Count == 0 || isWaiting || currentTargetIndex >= waypoints.Count) return;

        if (isAutopilotActive)
        {
            // 실시간 주행 제어 실행
            RunReactiveNavigation();
        }
    }

    // 자율주행 활성화/비활성화 토글
    public void ToggleAutopilot()
    {
        isAutopilotActive = !isAutopilotActive;

        if (isAutopilotActive)
        {
            // 현재 웨이포인트가 없으면 첫 번째 웨이포인트 설정
            if (currentTargetIndex >= waypoints.Count)
            {
                currentTargetIndex = 0;
                ResetWaypointStatus();
            }

            SetCurrentTarget(waypoints[currentTargetIndex].targetPoint.position);
            currentWaypointThreshold = GetCurrentThreshold();
        }
        else
        {
            // 보트 정지
            boatMotor.HandleMotorMovement(0, 0);

            // 대기 코루틴 중지
            if (waitCoroutine != null)
            {
                StopCoroutine(waitCoroutine);
                waitCoroutine = null;
            }
            isWaiting = false;
        }
    }

    void RunReactiveNavigation()
    {
        Vector3 boatForward = -transform.right;

        // 1. 목표 방향 계산
        Vector3 toTarget = currentTargetPosition - transform.position;
        toTarget.y = 0;
        float distanceToTarget = toTarget.magnitude;
        Vector3 targetDirection = toTarget.normalized;

        // 2. 장애물 회피 벡터 계산
        Vector3 avoidanceVector = CalculateObstacleAvoidance();

        // 3. 최종 주행 방향 결정 (목표 방향 + 회피 벡터)
        Vector3 desiredDirection = (targetDirection + avoidanceVector).normalized;

        // 4. 현재 방향과 원하는 방향의 차이 계산
        float angleDifference = Vector3.SignedAngle(
            boatForward,
            desiredDirection,
            Vector3.up
        );

        // 5. 회전 전용 모드 확인
        bool shouldRotateOnly = ShouldRotateOnly(distanceToTarget, angleDifference);

        // 6. 조향 입력 계산
        float steeringInput = Mathf.Clamp(
            angleDifference / 45f,
            -1f,
            1f
        ) * steeringResponse;

        // 7. 속도 입력 계산 (회전 전용 모드 적용)
        float speedInput = shouldRotateOnly ? 0 : CalculateSpeedInput(distanceToTarget);

        // 8. 보트 모터에 입력 전달
        boatMotor.HandleMotorMovement(speedInput, steeringInput);

        // 9. 웨이포인트 도달 확인
        CheckWaypointReached(distanceToTarget);
    }

    // 회전 전용 모드 진입 조건 판단
    bool ShouldRotateOnly(float distanceToTarget, float angleDifference)
    {
        // 조건 1: 방향 차이가 임계값보다 클 때
        if (Mathf.Abs(angleDifference) > rotateOnlyAngleThreshold)
        {
            return true;
        }

        // 조건 2: 전방에 매우 가까운 장애물이 있을 때
        if (IsObstacleInPath(rotateOnlyObstacleDistance))
        {
            return true;
        }

        // 조건 3: 목표 지점에 매우 가까우면서 방향이 맞지 않을 때
        if (distanceToTarget < currentWaypointThreshold * 1.5f &&
            Mathf.Abs(angleDifference) > 20f)
        {
            return true;
        }

        return false;
    }

    // 지정된 거리 내 장애물 존재 여부 확인
    bool IsObstacleInPath(float checkDistance)
    {
        List<Vector3> hitPoints = lidar.GetHitPoints();
        Vector3 boatForward = -transform.right;

        foreach (Vector3 point in hitPoints)
        {
            Vector3 toObstacle = point - transform.position;
            float distance = toObstacle.magnitude;

            if (distance < checkDistance)
            {
                Vector3 obstacleDirection = toObstacle.normalized;
                float angle = Vector3.Angle(boatForward, obstacleDirection);

                if (angle < detectionAngle / 2f)
                {
                    return true;
                }
            }
        }
        return false;
    }
    Vector3 CalculateObstacleAvoidance()
    {
        List<Vector3> hitPoints = lidar.GetHitPoints();
        Vector3 avoidanceVector = Vector3.zero;
        Vector3 boatForward = -transform.right;

        foreach (Vector3 point in hitPoints)
        {
            Vector3 toObstacle = point - transform.position;
            float distance = toObstacle.magnitude;

            // 안전 거리 내의 장애물만 처리
            if (distance < safeDistance)
            {
                // 보트의 전방과 장애물 방향의 각도 계산
                Vector3 obstacleDirection = toObstacle.normalized;
                float angle = Vector3.Angle(boatForward, obstacleDirection);

                // 전방 감지 각도 내의 장애물만 고려
                if (angle < detectionAngle / 2f)
                {
                    // 거리에 반비례하는 회피 강도 (가까울수록 강함)
                    float strength = (1f - Mathf.Clamp01(distance / safeDistance)) * avoidanceStrength;

                    // 장애물 방향의 수직 벡터 계산
                    Vector3 avoidanceDirection = Vector3.Cross(obstacleDirection, Vector3.up).normalized;

                    // 진행 방향과의 관계에 따라 회피 방향 결정
                    float dot = Vector3.Dot(avoidanceDirection, boatForward);
                    avoidanceDirection *= (dot > 0) ? 1f : -1f;

                    // 각도에 따른 가중치 적용 (정면에 가까울수록 강하게)
                    float angleWeight = 1f - Mathf.Clamp01(angle / (detectionAngle / 2f));
                    avoidanceVector += avoidanceDirection * strength * angleWeight;
                }
            }
        }

        return avoidanceVector;
    }

    float CalculateSpeedInput(float distanceToTarget)
    {
        // 기본 속도 (최대 입력)
        float speedInput = maxMotorInput;

        // 목표 지점 근처에서 감속
        if (distanceToTarget < slowingDistance)
        {
            // 거리에 따라 선형 감속
            speedInput = Mathf.Lerp(minSpeedFactor * maxMotorInput, maxMotorInput,
                                   distanceToTarget / slowingDistance);
        }

        // 전방에 장애물이 가까우면 추가 감속
        if (IsCloseObstacleInPath())
        {
            speedInput *= obstacleSlowFactor;
        }

        // 웨이포인트 근접시 추가 감속
        if (distanceToTarget < currentWaypointThreshold * 2f)
        {
            speedInput *= 0.7f;
        }

        return Mathf.Clamp(speedInput, minSpeedFactor * maxMotorInput, maxMotorInput);
    }

    bool IsCloseObstacleInPath()
    {
        List<Vector3> hitPoints = lidar.GetHitPoints();
        float criticalDistance = safeDistance * 0.5f;

        foreach (Vector3 point in hitPoints)
        {
            float distance = Vector3.Distance(transform.position, point);
            if (distance < criticalDistance)
            {
                Vector3 toObstacle = point - transform.position;
                float angle = Vector3.Angle(-transform.right, toObstacle);

                // 전방 60도 이내 장애물
                if (angle < detectionAngle)
                {
                    return true;
                }
            }
        }

        return false;
    }

    void CheckWaypointReached(float distance)
    {
        if (distance < currentWaypointThreshold)
        {
            HandleTargetReached();
        }
    }

    void HandleTargetReached()
    {
        // 현재 웨이포인트 도착 상태 표시
        waypoints[currentTargetIndex].isReached = true;

        // 대기 시간 시작
        isWaiting = true;
        boatMotor.HandleMotorMovement(0, 0); // 보트 정지

        if (waitCoroutine != null) StopCoroutine(waitCoroutine);

        // 회전 설정이 활성화된 경우
        if (waypoints[currentTargetIndex].rotateAtReach)
        {
            waitCoroutine = StartCoroutine(RotateToTargetAngleThenWait());
        }
        else
        {
            waitCoroutine = StartCoroutine(WaitAtWaypoint());
        }
    }

    IEnumerator RotateToTargetAngleThenWait()
    {
        Waypoint currentWP = waypoints[currentTargetIndex];

        // 목표 회전값 설정 (Y축만 변경)
        Quaternion targetRotation = Quaternion.Euler(0, currentWP.targetRotationAngle, 0);

        // 회전 수행
        while (Quaternion.Angle(transform.rotation, targetRotation) > 2)
        {
            // 현재 방향과 목표 방향의 차이 계산
            float angleDifference = Mathf.DeltaAngle(
                transform.eulerAngles.y,
                targetRotation.eulerAngles.y
            );

            // 조향 입력 계산 (각도 차이에 비례)
            float steeringInput = Mathf.Clamp(
                angleDifference / 45f,
                -2f,
                2f
            );

            // 회전 중에는 전진하지 않음 (속도 입력 0)
            boatMotor.HandleMotorMovement(0, steeringInput);

            yield return null;
        }

        // 회전 완료 후 정지
        boatMotor.HandleMotorMovement(0, 0);

        // 회전 후 대기
        yield return StartCoroutine(WaitAtWaypoint());
    }

    IEnumerator WaitAtWaypoint()
    {
        float waitTime = waypoints[currentTargetIndex].waitTime;

        if(waypoints[currentTargetIndex].requireImageDetection)
        {
            GameObject detectedFigure = figureDetector.FigureDetect();
            Waypoint selectedLane = new Waypoint(0f, 0.5f, detectedFigure.transform);

            waypoints.Insert(currentTargetIndex + 1, selectedLane);
        }

        yield return new WaitForSeconds(waitTime);

        isWaiting = false;

        // 다음 목표 지점으로 이동
        MoveToNextTarget();
    }

    void MoveToNextTarget()
    {
        currentTargetIndex++;

        // 모든 웨이포인트를 방문한 경우
        if (currentTargetIndex >= waypoints.Count)
        {
            if (loopPath)
            {
                // 경로 반복
                currentTargetIndex = 0;
                ResetWaypointStatus();
            }
            else
            {
                // 최종 정지
                enabled = false;
                return;
            }
        }

        // 다음 목표 설정
        SetCurrentTarget(waypoints[currentTargetIndex].targetPoint.position);
        // 새로운 웨이포인트 임계값 업데이트
        currentWaypointThreshold = GetCurrentThreshold();
    }

    void SetCurrentTarget(Vector3 targetPosition)
    {
        currentTargetPosition = targetPosition;
    }

    float GetCurrentThreshold()
    {
        // 현재 웨이포인트의 개별 임계값이 유효하면 사용, 아니면 전역 값 사용
        if (currentTargetIndex < waypoints.Count &&
            waypoints[currentTargetIndex].approachRadius > 0)
        {
            return waypoints[currentTargetIndex].approachRadius;
        }
        return globalWaypointThreshold;
    }

    void ResetWaypointStatus()
    {
        foreach (Waypoint wp in waypoints)
        {
            wp.isReached = false;
        }
    }

    void OnDrawGizmosSelected()
    {
        if (waypoints.Count == 0) return;

        // 웨이포인트 간 연결선
        Gizmos.color = Color.cyan;
        for (int i = 0; i < waypoints.Count; i++)
        {
            if (waypoints[i].targetPoint == null) continue;

            if (i < waypoints.Count - 1 && waypoints[i + 1].targetPoint != null)
            {
                Gizmos.DrawLine(
                    waypoints[i].targetPoint.position,
                    waypoints[i + 1].targetPoint.position
                );
            }
            else if (loopPath && waypoints[0].targetPoint != null)
            {
                Gizmos.DrawLine(
                    waypoints[i].targetPoint.position,
                    waypoints[0].targetPoint.position
                );
            }

            // 웨이포인트 표시
            Gizmos.color = waypoints[i].isReached ? Color.green : Color.yellow;
            float radius = waypoints[i].approachRadius > 0 ?
                waypoints[i].approachRadius : globalWaypointThreshold;

            Gizmos.DrawWireSphere(waypoints[i].targetPoint.position, radius);
            Gizmos.DrawSphere(waypoints[i].targetPoint.position, 1f);
        }

        // 현재 목표 지점 표시
        if (currentTargetIndex < waypoints.Count && waypoints[currentTargetIndex].targetPoint != null)
        {
            Gizmos.color = Color.red;
            float currentRadius = GetCurrentThreshold();
            Gizmos.DrawWireSphere(waypoints[currentTargetIndex].targetPoint.position, currentRadius);

            // 보트에서 목표 지점으로 선 그리기
            Gizmos.DrawLine(transform.position, waypoints[currentTargetIndex].targetPoint.position);

            // 회피 벡터 시각화
            if (Application.isPlaying)
            {
                Vector3 avoidance = CalculateObstacleAvoidance();
                if (avoidance.magnitude > 0.1f)
                {
                    Gizmos.color = Color.magenta;
                    Gizmos.DrawRay(transform.position, avoidance * 10f);
                }
            }
        }

        // 안전 거리 표시
        Gizmos.color = new Color(1, 0, 0, 0.1f);
        Gizmos.DrawWireSphere(transform.position, safeDistance);

        // 감지 각도 표시
        DrawDetectionAngleGizmo();
    }

    void DrawDetectionAngleGizmo()
    {
        if (!Application.isPlaying) return;

        Vector3 boatForward = -transform.right;

        float halfAngle = detectionAngle * 0.5f;
        float rayDistance = safeDistance;

        // 좌측 감지 경계선
        Vector3 leftDir = Quaternion.Euler(0, -halfAngle, 0) * boatForward;
        Debug.DrawRay(transform.position, leftDir * rayDistance, Color.yellow);

        // 우측 감지 경계선
        Vector3 rightDir = Quaternion.Euler(0, halfAngle, 0) * boatForward;
        Debug.DrawRay(transform.position, rightDir * rayDistance, Color.yellow);

        // 전방 감지 영역 호
        Vector3 prevPoint = transform.position + leftDir * rayDistance;
        for (int i = 0; i <= detectionAngle; i += 10)
        {
            Vector3 dir = Quaternion.Euler(0, -halfAngle + i, 0) * boatForward;
            Vector3 newPoint = transform.position + dir * rayDistance;
            Debug.DrawLine(prevPoint, newPoint, new Color(1, 1, 0, 0.5f));
            prevPoint = newPoint;
        }
    }
}