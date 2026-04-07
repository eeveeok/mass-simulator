using UnityEngine;

public class BoatFollowCamera : MonoBehaviour
{
    [Header("Follow Settings")]
    public Transform boatTransform; // 따라갈 보트의 Transform
    [Range(0.0f, 5.0f)]
    public float distance = 2.4f; // 보트와의 거리
    [Range(0.5f, 10f)]
    public float height = 1.5f; // 보트 위의 높이
    public float smoothSpeed = 100f; // 카메라 이동 부드러움 정도

    [Header("Look Settings")]
    public float lookAheadFactor = 0.5f; // 보트 이동 방향 미리보기 정도
    [Range(0.5f, 1.5f)]
    public float lookHeightOffset = 1f; // 보트의 어느 높이를 바라볼지

    [Header("Collision Avoidance")]
    public LayerMask collisionMask; // 충돌 체크할 레이어
    public float minDistance = 1.5f; // 최소 거리
    public float collisionOffset = 0.5f; // 충돌 시 오프셋

    private Vector3 desiredPosition;
    private Vector3 smoothedPosition;

    void Start()
    {
        if (boatTransform == null)
        {
            Debug.LogError("Boat transform not assigned to camera!");
            return;
        }

        // 초기 위치 설정
        transform.position = CalculateDesiredPosition();
        transform.LookAt(boatTransform.position + Vector3.up * lookHeightOffset);
    }

    void LateUpdate()
    {
        if (boatTransform == null) return;

        // 원하는 위치 계산
        desiredPosition = CalculateDesiredPosition();

        // 위치 부드럽게 이동
        smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        transform.position = smoothedPosition;

        // 보트 바라보기
        Vector3 lookTarget = boatTransform.position +
                             -boatTransform.right * lookAheadFactor * boatTransform.GetComponent<Rigidbody>().linearVelocity.magnitude * 0.1f +
                             Vector3.up * lookHeightOffset;
        transform.LookAt(lookTarget);
    }

    Vector3 CalculateDesiredPosition()
    {
        // 보트의 뒤쪽 방향 계산
        Vector3 backDirection = boatTransform.right;

        // 기본 위치 계산
        Vector3 calculatedPosition = boatTransform.position +
                                     backDirection * distance +
                                     Vector3.up * height;

        // 충돌 회피
        RaycastHit hit;
        Vector3 dirToCamera = (calculatedPosition - boatTransform.position).normalized;

        if (Physics.Raycast(boatTransform.position, dirToCamera, out hit, distance, collisionMask))
        {
            float adjustedDistance = hit.distance - collisionOffset;
            return boatTransform.position + dirToCamera * Mathf.Max(adjustedDistance, minDistance);
        }

        return calculatedPosition;
    }

    // 디버그용 기즈모 그리기
    void OnDrawGizmosSelected()
    {
        if (boatTransform != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(boatTransform.position, desiredPosition);
            Gizmos.DrawWireSphere(desiredPosition, 0.3f);
        }
    }
}