using UnityEngine;
using System.Collections.Generic;
using static GridPathfinding;

[RequireComponent(typeof(BoatCore), typeof(BoatMotor), typeof(BoatStabilization))]
public class BoatPathFollower : MonoBehaviour
{
    [Header("경로 설정")]
    public GridPathfinding pathfinding;
    [Range(0.1f, 10f)] public float arrivalDistance = 3f;
    [Range(0.1f, 25f)] public float rotationSpeed = 2f;
    [Range(0.1f, 1f)] public float minForceMultiplier = 0.3f;

    [Header("이동 설정")]
    [Range(1f, 50f)] public float moveForce = 15f;
    [Range(5f, 90f)] public float rotationThreshold = 30f;

    // 보트의 이동 및 경로 관련 변수
    private BoatCore core;
    private BoatMotor motor;
    private int currentPathIndex;
    private bool isFollowingPath;
    private Vector3 currentTargetPosition;
    private bool isRotating;

    // 시간 및 충돌 측정
    private float startTime;     // 경로 시작 시간
    private float elapsedTime;   // 경과 시간

    void Awake()
    {
        core = GetComponent<BoatCore>();
        motor = GetComponent<BoatMotor>();

        // 초기에는 입력 허용
        motor.allowInput = true;
    }

    void FixedUpdate()
    {
        if (isFollowingPath && pathfinding != null && pathfinding.currentPath != null)
        {
            FollowPath();
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Equals))
        {
            TogglePathFollowing();
        }

        // 시간 측정 중일 때 경과 시간 업데이트
        if (isFollowingPath)
        {
            elapsedTime = Time.time - startTime;
        }
    }

    public void TogglePathFollowing()
    {
        currentPathIndex = 0;
        isRotating = false;
        startTime = Time.time;

        // 경로 따라가기 상태에 따라 입력 제어
        isFollowingPath = !isFollowingPath;
        motor.allowInput = !isFollowingPath;

        if (isFollowingPath && pathfinding != null &&
            pathfinding.currentPath != null && pathfinding.currentPath.Count > 0)
        {
            core.colNumInFinding = 0;
            currentTargetPosition = pathfinding.currentPath[0].WorldPosition;
        }
    }

    private void FollowPath()
    {
        // 1. 경로 유효성 확인
        if (pathfinding.currentPath == null || pathfinding.currentPath.Count == 0)
        {
            Debug.LogWarning("이동할 경로가 없습니다!");
            isFollowingPath = false;
            motor.allowInput = true;
            return;
        }

        // 2. 경로 종료 확인
        if (currentPathIndex >= pathfinding.currentPath.Count)
        {
            Debug.Log("경로 종점에 도착했습니다!");
            isFollowingPath = false;
            motor.allowInput = true;
            return;
        }

        // 3. 현재 목표 노드 업데이트
        Node targetNode = pathfinding.currentPath[currentPathIndex];
        currentTargetPosition = targetNode.WorldPosition;
        currentTargetPosition.y = transform.position.y;

        // 4. 목표 노드까지의 거리 계산
        float distanceToTarget = Vector3.Distance(transform.position, currentTargetPosition);

        // 5. 목표 노드 도착 확인
        if (distanceToTarget < arrivalDistance)
        {
            currentPathIndex++;
            isRotating = false;

            // 마지막 노드에 도달했는지 다시 확인
            if (currentPathIndex >= pathfinding.currentPath.Count)
            {
                isFollowingPath = false;
                motor.allowInput = true;
            }
            return;
        }

        // 6. 방향 계산 (현재 위치에서 목표 노드로의 방향)
        Vector3 directionToTarget = (currentTargetPosition - transform.position).normalized;

        // 7. 회전 처리
        HandleRotation(directionToTarget);

        // 8. 이동 처리
        HandleMovement(directionToTarget, distanceToTarget);
    }

    /// <summary>
    /// 정확한 방향으로 보트 회전
    /// </summary>
    private void HandleRotation(Vector3 targetDirection)
    {
        // 1. 보트의 실제 전진 방향 계산 (오른쪽 방향)
        Vector3 boatForward = core.boatModel.right;

        // 2. 회전 필요 여부 확인 (실제 전진 방향 기준)
        float angle = Vector3.Angle(boatForward, targetDirection);
        isRotating = angle > rotationThreshold;

        // 3. 목표 방향으로의 회전 계산 (90도 Y축 회전 추가)
        Quaternion baseRotation = Quaternion.LookRotation(targetDirection, Vector3.up);
        Quaternion targetRotation = baseRotation * Quaternion.Euler(0, 90, 0);

        // 4. 부드러운 회전 적용
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.fixedDeltaTime
        );
    }

    /// <summary>
    /// 경로를 따라 이동 처리
    /// </summary>
    private void HandleMovement(Vector3 direction, float distance)
    {
        // 1. 전진 힘 계산
        float forceMultiplier = 1f;

        // 회전 중일 때는 전진력 제한
        if (isRotating)
        {
            forceMultiplier = minForceMultiplier;
        }

        // 거리에 따른 힘 조정 (가까울수록 감속)
        float distanceFactor = Mathf.Clamp01(distance / arrivalDistance);
        forceMultiplier *= distanceFactor;

        // 2. 힘 적용 (BoatMotor 방식)
        Vector3 forceDirection = -core.boatModel.right;
        core.RigidBody.AddForceAtPosition(
            forceDirection * moveForce * forceMultiplier,
            core.leftMotor.position
        );
        core.RigidBody.AddForceAtPosition(
            forceDirection * moveForce * forceMultiplier,
            core.rightMotor.position
        );

        // 3. 속도 제한 적용
        motor.LimitSpeed();
    }

    /// <summary>
    /// 시간을 mm:ss:ms 형식으로 포맷팅
    /// </summary>
    private string FormatTime(float time)
    {
        int minutes = (int)(time / 60);
        int seconds = (int)(time % 60);
        int milliseconds = (int)((time * 1000) % 1000);

        return $"{minutes:00}:{seconds:00}.{milliseconds:000}";
    }

    void OnGUI()
    {
        GUIStyle style = new GUIStyle
        {
            fontSize = 20,
            normal = { textColor = Color.yellow }
        };

        if (isFollowingPath)
        {
            // 화면 상단에 시간 및 충돌 표시
            string formattedTime = FormatTime(elapsedTime);
            GUI.Label(new Rect(10, 40, 500, 30), $"경과 시간: {formattedTime} / 충돌: {core.colNumInFinding}회", style);
        }
        else if (!isFollowingPath && elapsedTime > 0)
        {
            // 완주 시간 및 충돌 표시
            string formattedTime = FormatTime(elapsedTime);
            GUI.Label(new Rect(10, 40, 500, 30), $"완주 시간: {formattedTime} / 충돌: {core.colNumInFinding}회", style);
        }
    }

    void OnDrawGizmos()
    {
        if (!isFollowingPath || pathfinding == null ||
            pathfinding.currentPath == null || pathfinding.currentPath.Count == 0 ||
            currentPathIndex >= pathfinding.currentPath.Count)
            return;

        // 현재 목표 노드 표시
        Node targetNode = pathfinding.currentPath[currentPathIndex];
        Gizmos.color = Color.magenta;
        Gizmos.DrawSphere(targetNode.WorldPosition, 0.3f);
        Gizmos.DrawLine(transform.position, targetNode.WorldPosition);

        // 현재 이동 방향 표시
        Vector3 direction = (targetNode.WorldPosition - transform.position).normalized;
        Gizmos.color = isRotating ? Color.red : Color.green;
        Gizmos.DrawRay(transform.position, direction * 5f);

        // 보트 전방 방향 표시
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, -core.boatModel.right * 3f);

        // 현재 목표 위치 표시
        GUIStyle style = new GUIStyle();
        style.normal.textColor = Color.magenta;
        UnityEditor.Handles.Label(targetNode.WorldPosition + Vector3.up * 2f,
                                $"Node {currentPathIndex}",
                                style);
    }
}