using UnityEngine;

public class BuiltinCamera : MonoBehaviour
{
    [Header("Follow Settings")]
    public Transform boatTransform; // 따라갈 보트의 Transform
    [Range(0.0f, 5.0f)]
    public float distance = 0.8f; // 보트와의 거리
    [Range(0.5f, 10.0f)]
    public float height = 0.9f; // 보트 위의 높이
    private float smoothSpeed = 1000f; // 카메라 이동 부드러움 정도

    [Header("Look Settings")]
    [Range (0.5f, 1.5f)]
    public float lookHeightOffset = 1.1f; // 보트의 어느 높이를 바라볼지

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
        Vector3 desiredPosition = CalculateDesiredPosition();

        // 위치 부드럽게 이동
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        transform.position = smoothedPosition;

        // 보트 바라보기
        Vector3 lookTarget = boatTransform.position +
                             -boatTransform.right * boatTransform.GetComponent<Rigidbody>().linearVelocity.magnitude * 0.1f +
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

        return calculatedPosition;
    }
}