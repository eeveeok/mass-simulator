#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GridData", menuName = "Pathfinding/Grid Data", order = 1)]
public class GridData : ScriptableObject
{
    public Vector2 gridWorldSize;
    public float nodeRadius;
    public int gridSizeX;
    public int gridSizeZ;
    public bool[] walkableData;
    public Vector3[] worldPositions;
    public List<Vector2Int> pathData;
}
#endif