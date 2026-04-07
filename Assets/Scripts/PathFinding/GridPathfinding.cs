using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEditor;
using UnityEngine;

public class GridPathfinding : MonoBehaviour
{
    [Header("그리드 설정")]
    public Vector2 gridWorldSize = new Vector2(50f, 50f);
    public Vector3 gridAreaOffset = Vector3.zero; // 스캔 영역 위치 오프셋
    public float nodeRadius = 0.5f;
    public LayerMask obstacleLayer;
    public Color validNodeColor = new Color(0.2f, 0.8f, 0.3f, 0.8f);
    public Color pathColor = new Color(0.9f, 0.3f, 0.1f, 1f);
    public Color agentAreaColor = new Color(0.1f, 0.5f, 0.9f, 0.4f);

    [Space(10)]

    [Header("에이전트 설정")]
    public GameObject agentObject;
    [Range(0.1f, 2f)] public float agentScaleMultiplier = 1f;

    [Space(10)]

    [Header("경로 탐색")]
    public Transform target;
    public bool autoUpdatePath = true;
    public float pathUpdateInterval = 0.5f;

    [Space(10)]

    [Header("Scriptable Object 저장")]
    public GridData gridDataAsset;
    public bool loadFromAssetOnStart = true;

    [Header("Debug 시각화")]
    public bool showGrid = true;
    public bool showAgentArea = true;

    [System.NonSerialized] public List<Node> currentPath = new List<Node>();
    private Node[,] grid;
    private float nodeDiameter;
    private int gridSizeX, gridSizeZ;

    private Vector3 agentColliderSize;
    private float lastPathUpdateTime;

    void Start()
    {
        if (loadFromAssetOnStart && gridDataAsset != null)
        {
            LoadGridFromAsset();
        }
        else
        {
            InitializeGrid();
            CreateGrid();
        }
    }

    void Update()
    {
        if (Application.isPlaying && autoUpdatePath &&
            target != null && agentObject != null)
        {
            if (Time.time - lastPathUpdateTime > pathUpdateInterval)
            {
                FindPathInPlayMode(agentObject.transform.position, target.position);
                lastPathUpdateTime = Time.time;
            }
        }
    }

#if UNITY_EDITOR
    void OnValidate() => InitializeGrid();
#endif

    void InitializeGrid()
    {
        nodeDiameter = nodeRadius * 2;
        gridSizeX = Mathf.RoundToInt(gridWorldSize.x / nodeDiameter);
        gridSizeZ = Mathf.RoundToInt(gridWorldSize.y / nodeDiameter);
    }

#if UNITY_EDITOR
    public void SaveGridToAsset()
    {
        if (gridDataAsset == null)
        {
            Debug.LogWarning("No GridData asset assigned!");
            return;
        }

        if (grid == null)
        {
            Debug.LogWarning("No grid data to save!");
            return;
        }

        gridDataAsset.gridWorldSize = gridWorldSize;
        gridDataAsset.nodeRadius = nodeRadius;
        gridDataAsset.gridSizeX = gridSizeX;
        gridDataAsset.gridSizeZ = gridSizeZ;

        // Save walkable status for each node
        gridDataAsset.walkableData = new bool[gridSizeX * gridSizeZ];
        for (int x = 0; x < gridSizeX; x++)
        {
            for (int z = 0; z < gridSizeZ; z++)
            {
                gridDataAsset.walkableData[x * gridSizeZ + z] = grid[x, z].Walkable;
            }
        }

        // Save world positions
        gridDataAsset.worldPositions = new Vector3[gridSizeX * gridSizeZ];
        for (int x = 0; x < gridSizeX; x++)
        {
            for (int z = 0; z < gridSizeZ; z++)
            {
                gridDataAsset.worldPositions[x * gridSizeZ + z] = grid[x, z].WorldPosition;
            }
        }

        // Save path if exists
        if (currentPath != null && currentPath.Count > 0)
        {
            gridDataAsset.pathData = new List<Vector2Int>();
            foreach (Node node in currentPath)
            {
                gridDataAsset.pathData.Add(new Vector2Int(node.GridX, node.GridZ));
            }
        }
        else
        {
            gridDataAsset.pathData = null;
        }

        EditorUtility.SetDirty(gridDataAsset);
        AssetDatabase.SaveAssets();
        Debug.Log($"Grid data saved to {gridDataAsset.name} asset");
    }

    public void LoadGridFromAsset()
    {
        if (gridDataAsset == null)
        {
            Debug.LogWarning("No GridData asset assigned!");
            return;
        }

        // Apply loaded data
        gridWorldSize = gridDataAsset.gridWorldSize;
        nodeRadius = gridDataAsset.nodeRadius;
        gridSizeX = gridDataAsset.gridSizeX;
        gridSizeZ = gridDataAsset.gridSizeZ;
        nodeDiameter = nodeRadius * 2;

        // Recreate grid
        grid = new Node[gridSizeX, gridSizeZ];
        Vector3 worldBottomLeft = transform.position -
                                Vector3.right * gridWorldSize.x / 2 -
                                Vector3.forward * gridWorldSize.y / 2;

        for (int x = 0; x < gridSizeX; x++)
        {
            for (int z = 0; z < gridSizeZ; z++)
            {
                Vector3 nodePoint = gridDataAsset.worldPositions[x * gridSizeZ + z];
                bool isWalkable = gridDataAsset.walkableData[x * gridSizeZ + z];
                grid[x, z] = new Node(isWalkable, nodePoint, x, z);
            }
        }

        // Restore path if exists
        if (gridDataAsset.pathData != null && gridDataAsset.pathData.Count > 0)
        {
            currentPath = new List<Node>();
            foreach (Vector2Int nodePos in gridDataAsset.pathData)
            {
                if (nodePos.x >= 0 && nodePos.x < gridSizeX && nodePos.y >= 0 && nodePos.y < gridSizeZ)
                {
                    currentPath.Add(grid[nodePos.x, nodePos.y]);
                }
            }
        }
    }
#endif

    public void ScanGrid()
    {
        InitializeGrid();
        CalculateAgentBounds();
        grid = new Node[gridSizeX, gridSizeZ];

        // 스캔 중심점 계산
        Vector3 scanCenter = transform.position + gridAreaOffset;

        Vector3 worldBottomLeft = scanCenter -
                                Vector3.right * gridWorldSize.x / 2 -
                                Vector3.forward * gridWorldSize.y / 2;

        for (int x = 0; x < gridSizeX; x++)
        {
            for (int z = 0; z < gridSizeZ; z++)
            {
                Vector3 nodePoint = worldBottomLeft +
                                  Vector3.right * (x * nodeDiameter + nodeRadius) +
                                  Vector3.forward * (z * nodeDiameter + nodeRadius);

                bool isWalkable = CheckNodeWalkability(nodePoint);
                grid[x, z] = new Node(isWalkable, nodePoint, x, z);
            }
        }
        Debug.Log($"Grid scanned: {gridSizeX}x{gridSizeZ} nodes");
    }

#if UNITY_EDITOR
    public void FindPath(Vector3 startPos, Vector3 targetPos)
    {
        if (Application.isPlaying)
        {
            Debug.LogError("Pathfinding is only available in Editor mode!");
            return;
        }

        Node startNode = GetNodeFromWorldPoint(startPos);
        Node targetNode = GetNodeFromWorldPoint(targetPos);
        currentPath.Clear();

        if (startNode == null || targetNode == null || !startNode.Walkable || !targetNode.Walkable)
        {
            Debug.LogWarning("Pathfinding failed: Invalid nodes");
            return;
        }

        List<Node> openSet = new List<Node>();
        HashSet<Node> closedSet = new HashSet<Node>();
        openSet.Add(startNode);

        for (int x = 0; x < gridSizeX; x++)
            for (int z = 0; z < gridSizeZ; z++)
            {
                grid[x, z].GCost = int.MaxValue;
                grid[x, z].HCost = 0;
                grid[x, z].Parent = null;
            }

        startNode.GCost = 0;
        startNode.HCost = GetDistance(startNode, targetNode);

        while (openSet.Count > 0)
        {
            Node currentNode = openSet[0];
            for (int i = 1; i < openSet.Count; i++)
                if (openSet[i].FCost < currentNode.FCost ||
                   (openSet[i].FCost == currentNode.FCost && openSet[i].HCost < currentNode.HCost))
                    currentNode = openSet[i];

            openSet.Remove(currentNode);
            closedSet.Add(currentNode);

            if (currentNode == targetNode)
            {
                RetracePath(startNode, targetNode);
                return;
            }

            foreach (Node neighbour in GetNeighbours(currentNode))
            {
                if (!neighbour.Walkable || closedSet.Contains(neighbour)) continue;

                int newCost = currentNode.GCost + GetDistance(currentNode, neighbour);
                if (newCost < neighbour.GCost || !openSet.Contains(neighbour))
                {
                    neighbour.GCost = newCost;
                    neighbour.HCost = GetDistance(neighbour, targetNode);
                    neighbour.Parent = currentNode;

                    if (!openSet.Contains(neighbour))
                        openSet.Add(neighbour);
                }
            }
        }
    }
#endif

    public void CalculateAgentBounds()
    {
        if (agentObject == null) return;

        Renderer renderer = agentObject.GetComponent<Renderer>();
        Collider collider = agentObject.GetComponent<Collider>();

        Bounds agentBounds = new Bounds();
        bool hasBounds = false;

        if (renderer != null)
        {
            agentBounds = renderer.bounds;
            hasBounds = true;
        }

        if (collider != null)
        {
            if (hasBounds) agentBounds.Encapsulate(collider.bounds);
            else agentBounds = collider.bounds;
        }

        agentColliderSize = agentBounds.size * agentScaleMultiplier;
    }

    // 플레이 모드용 경로 찾기 메서드
    public void FindPathInPlayMode(Vector3 startPos, Vector3 targetPos)
    {
        Node startNode = GetNodeFromWorldPoint(startPos);
        Node targetNode = GetNodeFromWorldPoint(targetPos);

        if (startNode == null || targetNode == null)
        {
            Debug.LogWarning("경로 찾기 실패: 노드를 찾을 수 없음");
            return;
        }

        // 경로 초기화
        currentPath.Clear();

        // 시작/도착 노드 검증
        if (!targetNode.Walkable)
        {
            Debug.LogWarning("경로 찾기 실패: 목적지 노드 통과 불가");
            return;
        }

        // A* 알고리즘 구현
        List<Node> openSet = new List<Node>();
        HashSet<Node> closedSet = new HashSet<Node>();
        openSet.Add(startNode);

        // 노드 초기화
        for (int x = 0; x < gridSizeX; x++)
        {
            for (int z = 0; z < gridSizeZ; z++)
            {
                grid[x, z].GCost = int.MaxValue;
                grid[x, z].HCost = 0;
                grid[x, z].Parent = null;
            }
        }

        startNode.GCost = 0;
        startNode.HCost = GetDistance(startNode, targetNode);

        while (openSet.Count > 0)
        {
            Node currentNode = openSet[0];
            for (int i = 1; i < openSet.Count; i++)
            {
                if (openSet[i].FCost < currentNode.FCost ||
                   (openSet[i].FCost == currentNode.FCost && openSet[i].HCost < currentNode.HCost))
                {
                    currentNode = openSet[i];
                }
            }

            openSet.Remove(currentNode);
            closedSet.Add(currentNode);

            // 목표 노드 도달
            if (currentNode == targetNode)
            {
                RetracePath(startNode, targetNode);
                return;
            }

            // 이웃 노드 탐색
            foreach (Node neighbour in GetNeighbours(currentNode))
            {
                if (!neighbour.Walkable || closedSet.Contains(neighbour))
                    continue;

                int newCost = currentNode.GCost + GetDistance(currentNode, neighbour);
                if (newCost < neighbour.GCost || !openSet.Contains(neighbour))
                {
                    neighbour.GCost = newCost;
                    neighbour.HCost = GetDistance(neighbour, targetNode);
                    neighbour.Parent = currentNode;

                    if (!openSet.Contains(neighbour))
                        openSet.Add(neighbour);
                }
            }
        }
    }

    // 플레이 모드용 그리드 생성 메서드
    public void CreateGrid()
    {
        if (grid != null) return;

        InitializeGrid();
        CalculateAgentBounds();
        grid = new Node[gridSizeX, gridSizeZ];

        // 스캔 중심점 계산
        Vector3 scanCenter = transform.position + gridAreaOffset;
        Vector3 worldBottomLeft = scanCenter -
                                Vector3.right * gridWorldSize.x / 2 -
                                Vector3.forward * gridWorldSize.y / 2;

        for (int x = 0; x < gridSizeX; x++)
        {
            for (int z = 0; z < gridSizeZ; z++)
            {
                Vector3 nodePoint = worldBottomLeft +
                                  Vector3.right * (x * nodeDiameter + nodeRadius) +
                                  Vector3.forward * (z * nodeDiameter + nodeRadius);

                bool isWalkable = CheckNodeWalkability(nodePoint);
                grid[x, z] = new Node(isWalkable, nodePoint, x, z);
            }
        }
        Debug.Log($"플레이 모드 그리드 생성: {gridSizeX}x{gridSizeZ}");
    }

    bool CheckNodeWalkability(Vector3 position)
    {
        if (agentObject == null) return false;

        Quaternion rotation = agentObject.transform.rotation;
        Collider[] colliders = agentObject.GetComponentsInChildren<Collider>();

        foreach (Collider col in colliders)
        {
            if (!col.enabled || !col.gameObject.activeInHierarchy) continue;

            Vector3 worldPos = position + (col.bounds.center - agentObject.transform.position);
            Vector3 size = col.bounds.size * agentScaleMultiplier;

            if (col is BoxCollider && Physics.CheckBox(worldPos, size / 2, rotation, obstacleLayer))
                return false;
            else if (col is SphereCollider sphereCol &&
                    Physics.CheckSphere(worldPos, sphereCol.radius * agentScaleMultiplier, obstacleLayer))
                return false;
            else if (col is CapsuleCollider capsuleCol)
            {
                Vector3 point1 = worldPos + Vector3.up * (capsuleCol.height / 2 - capsuleCol.radius);
                Vector3 point2 = worldPos - Vector3.up * (capsuleCol.height / 2 - capsuleCol.radius);
                if (Physics.CheckCapsule(point1, point2, capsuleCol.radius * agentScaleMultiplier, obstacleLayer))
                    return false;
            }
            else if (col is MeshCollider && Physics.CheckBox(worldPos, size / 2, rotation, obstacleLayer))
                return false;
        }
        return true;
    }

    void RetracePath(Node startNode, Node endNode)
    {
        currentPath = new List<Node>();
        Node currentNode = endNode;

        while (currentNode != startNode && currentNode != null)
        {
            currentPath.Add(currentNode);
            currentNode = currentNode.Parent;
        }

        if (currentNode == startNode) currentPath.Add(startNode);
        currentPath.Reverse();
    }

    int GetDistance(Node nodeA, Node nodeB)
    {
        int dstX = Mathf.Abs(nodeA.GridX - nodeB.GridX);
        int dstZ = Mathf.Abs(nodeA.GridZ - nodeB.GridZ);
        return dstX > dstZ ? 14 * dstZ + 10 * (dstX - dstZ) : 14 * dstX + 10 * (dstZ - dstX);
    }

    public List<Node> GetNeighbours(Node node)
    {
        List<Node> neighbours = new List<Node>();
        for (int x = -1; x <= 1; x++)
            for (int z = -1; z <= 1; z++)
                if (x != 0 || z != 0)
                {
                    int checkX = node.GridX + x;
                    int checkZ = node.GridZ + z;
                    if (checkX >= 0 && checkX < gridSizeX && checkZ >= 0 && checkZ < gridSizeZ)
                        neighbours.Add(grid[checkX, checkZ]);
                }
        return neighbours;
    }

    public Node GetNodeFromWorldPoint(Vector3 worldPosition)
    {
        if (grid == null) return null;

        try
        {
            // 스캔 중심점 계산
            Vector3 scanCenter = transform.position + gridAreaOffset;

            // 상대 좌표 계산 (스캔 중심점 기준)
            Vector3 localPos = worldPosition - scanCenter;

            // 정규화된 그리드 좌표 계산
            float percentX = (localPos.x + gridWorldSize.x / 2) / gridWorldSize.x;
            float percentZ = (localPos.z + gridWorldSize.y / 2) / gridWorldSize.y;

            // 경계값 처리
            percentX = Mathf.Clamp01(percentX);
            percentZ = Mathf.Clamp01(percentZ);

            // 그리드 인덱스 계산
            int x = Mathf.FloorToInt(Mathf.Clamp(gridSizeX * percentX, 0, gridSizeX - 1));
            int z = Mathf.FloorToInt(Mathf.Clamp(gridSizeZ * percentZ, 0, gridSizeZ - 1));

            return grid[x, z];
        }
        catch (System.Exception e)
        {
            Debug.LogError($"노드 변환 실패: {worldPosition} | 오류: {e.Message}");
            return null;
        }
    }

    void OnDrawGizmos()
    {
        // 스캔 영역 중심점 계산
        Vector3 scanCenter = transform.position + gridAreaOffset;

        // 스캔 영역 표시
        Gizmos.color = new Color(1f, 1f, 1f, 1f);
        Gizmos.DrawWireCube(
            scanCenter,
            new Vector3(gridWorldSize.x, 0.1f, gridWorldSize.y)
        );

        if (!showGrid || grid == null) return; 

            foreach (Node node in grid)
        {
            Gizmos.color = node.Walkable ? validNodeColor : new Color(1, 0, 0, 0.2f);
            Gizmos.DrawCube(node.WorldPosition, Vector3.one * (nodeDiameter * (node.Walkable ? 0.9f : 0.3f)));
        }

        if (showAgentArea && agentObject != null)
        {
            Gizmos.color = agentAreaColor;
            Gizmos.DrawWireCube(agentObject.transform.position, agentColliderSize);
        }

        if (currentPath != null && currentPath.Count > 0)
        {
            Gizmos.color = pathColor;
            for (int i = 0; i < currentPath.Count - 1; i++)
            {
                Vector3 start = currentPath[i].WorldPosition + Vector3.up * 0.2f;
                Vector3 end = currentPath[i + 1].WorldPosition + Vector3.up * 0.2f;
                Gizmos.DrawLine(start, end);
            }
        }
    }

    [System.Serializable]
    public class Node
    {
        public bool Walkable;
        public Vector3 WorldPosition;
        public int GridX, GridZ;
        public int GCost, HCost;
        public Node Parent;
        public int FCost => GCost + HCost;

        public Node(bool walkable, Vector3 worldPos, int gridX, int gridZ)
        {
            Walkable = walkable;
            WorldPosition = worldPos;
            GridX = gridX;
            GridZ = gridZ;
        }
    }
}