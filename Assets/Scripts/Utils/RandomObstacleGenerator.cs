using System.Collections.Generic;
using UnityEngine;

public class RandomObstacleGenerator : MonoBehaviour
{
    [Header("Pool Settings")]
    public GameObject obstaclePrefab;
    public Transform exitArea; // 제외 영역 중심점

    [Space(10f)]
    // 장애물 생성할 X범위
    [Range(0f, 45f)]
    public float xRange = 17f;
    // 장애물 생성할 Z범위
    [Range(0f, 15f)]
    public float zRange = 5.4f;

    public float minSpacing = 1.0f; // 최소 간격
    private const float ExclusionRadius = 1.6f; // 호핑 영역 반지름 (지름 3m)
    private const float ExitAreaHalfSize = 1.0f;   // 코너 영역 반길이

    private int rows = 3; // 행 수
    private int columns = 12; // 열 수

    private List<GameObject> objectPool = new List<GameObject>();
    private int totalObjects => rows * columns;

    void Start()
    {
        xRange = xRange - 0.5f;
        zRange = zRange - 0.2f;

        InitializePool();
        GenerateObjects();
    }

    void InitializePool()
    {
        for (int i = 0; i < totalObjects; i++)
        {
            GameObject obj = Instantiate(obstaclePrefab, transform);
            obj.SetActive(false);
            objectPool.Add(obj);
        }
    }

    private void FixedUpdate()
    {
        if(Input.GetKey(KeyCode.R))
        {
            RegenerateObjects();
        }
    }

    public void GenerateObjects()
    {
        Vector3 parentPosition = transform.position;
        List<Vector3> placedPositions = new List<Vector3>();
        float exclusionRadiusSqr = ExclusionRadius * ExclusionRadius;

        // 그리드 셀 크기 계산
        float cellWidth = xRange / columns;
        float cellHeight = zRange / rows;

        // 1. 먼저 제외 영역을 피해 가능한 위치에 배치 시도
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                int index = row * columns + col;
                if (index >= objectPool.Count) break;

                GameObject obj = objectPool[index];
                Vector3 position = CalculatePositionInCell(parentPosition, cellWidth, cellHeight, row, col);

                // 제외 영역 검사 (호핑 + 코너)
                Vector2 toObj = new Vector2(position.x - parentPosition.x, position.z - parentPosition.z);
                bool isExcluded = toObj.sqrMagnitude < exclusionRadiusSqr || IsInExitArea(position);

                if (!isExcluded && !IsTooClose(position, placedPositions))
                {
                    obj.transform.position = position;
                    obj.SetActive(true);
                    placedPositions.Add(position);
                }
                else
                {
                    obj.SetActive(false);
                }
            }
        }

        // 2. 제외 영역에 걸리거나 겹친 오브젝트 재배치
        for (int i = 0; i < objectPool.Count; i++)
        {
            if (!objectPool[i].activeSelf)
            {
                Vector3 newPosition = FindValidPosition(parentPosition, placedPositions, exclusionRadiusSqr);
                objectPool[i].transform.position = newPosition;
                objectPool[i].SetActive(true);
                placedPositions.Add(newPosition);
            }
        }
    }

    Vector3 CalculatePositionInCell(Vector3 parentPos, float cellWidth, float cellHeight, int row, int col)
    {
        float cellCenterX = parentPos.x - xRange / 2 + col * cellWidth + cellWidth / 2;
        float cellCenterZ = parentPos.z - zRange / 2 + row * cellHeight + cellHeight / 2;

        float maxOffsetX = Mathf.Max(0, (cellWidth - minSpacing) / 2);
        float maxOffsetZ = Mathf.Max(0, (cellHeight - minSpacing) / 2);

        return new Vector3(
            cellCenterX + Random.Range(-maxOffsetX, maxOffsetX),
            transform.position.y,
            cellCenterZ + Random.Range(-maxOffsetZ, maxOffsetZ)
        );
    }

    Vector3 FindValidPosition(Vector3 parentPos, List<Vector3> existingPositions, float exclusionRadiusSqr)
    {
        Vector3 position;
        bool positionFound = false;

        int attempts = 0;
        const int maxAttempts = 100;

        do
        {
            attempts++;

            // 전체 영역 내 랜덤 위치 생성
            position = new Vector3(
                parentPos.x + Random.Range(-xRange / 2, xRange / 2),
                transform.position.y,
                parentPos.z + Random.Range(-zRange / 2, zRange / 2)
            );

            // 제외 영역 검사 (원형 + 정사각형)
            Vector2 toObj = new Vector2(position.x - parentPos.x, position.z - parentPos.z);
            bool isExcluded = toObj.sqrMagnitude < exclusionRadiusSqr || IsInExitArea(position);

            // 기존 오브젝트와의 간격 검사
            positionFound = !isExcluded && !IsTooClose(position, existingPositions);

        } while (!positionFound && attempts < maxAttempts);

        // 최대 시도 횟수 내에 위치를 못찾으면 경고 출력
        if (!positionFound)
        {
            Debug.LogWarning($"{maxAttempts}회 시도 후 유효한 위치를 찾지 못함. 마지막 위치 사용");
        }

        return position;
    }

    bool IsTooClose(Vector3 position, List<Vector3> existingPositions)
    {
        foreach (Vector3 existingPos in existingPositions)
        {
            if (Vector3.Distance(position, existingPos) < minSpacing)
            {
                return true;
            }
        }
        return false;
    }

    // 정사각형 제외 영역 검사 함수
    private bool IsInExitArea(Vector3 position)
    {
        if (exitArea == null) return false;

        return Mathf.Abs(position.x - exitArea.position.x) <= ExitAreaHalfSize &&
               Mathf.Abs(position.z - exitArea.position.z) <= ExitAreaHalfSize;
    }

    public void RegenerateObjects()
    {
        foreach (GameObject obj in objectPool)
        {
            obj.SetActive(false);
        }
        GenerateObjects();
    }
}