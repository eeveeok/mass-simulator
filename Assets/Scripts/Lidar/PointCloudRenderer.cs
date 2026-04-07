using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(LidarSensor))]
public class PointCloudRenderer : MonoBehaviour
{
    public Material pointMaterial;
    [Range(0.001f, 0.5f)] public float pointSize = 0.05f;
    public Color pointColor = Color.white;
    public float maxDistance = 100f;

    private ComputeBuffer _pointBuffer;
    private LidarSensor _lidar;
    private uint[] _args = new uint[5] { 0, 0, 0, 0, 0 };
    private ComputeBuffer _argsBuffer;
    private int _currentPointCount = 0;
    private Bounds _bounds;
    private MaterialPropertyBlock _propBlock; // MaterialPropertyBlock 추가

    struct PointData
    {
        public Vector3 position;
        public float intensity;
    }

    void Start()
    {
        _lidar = GetComponent<LidarSensor>();
        _argsBuffer = new ComputeBuffer(1, _args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        _bounds = new Bounds(transform.position, Vector3.one * maxDistance * 2f);
        _propBlock = new MaterialPropertyBlock(); // 초기화

        // 모든 자식 오브젝트에도 동일한 레이어 할당
        //SetLayerRecursively(transform, gameObject.layer);
    }

    void Update()
    {
        List<Vector3> points = _lidar.GetHitPoints();
        int pointCount = points.Count;

        if (pointCount == 0)
        {
            ReleasePointBuffer();
            return;
        }

        if (_pointBuffer == null || pointCount != _currentPointCount)
        {
            CreatePointBuffer(points);
        }
        else
        {
            UpdatePointBuffer(points);
        }

        // MaterialPropertyBlock을 사용해 프로퍼티 설정
        _propBlock.SetFloat("_PointSize", pointSize);
        _propBlock.SetColor("_PointColor", pointColor);
        _propBlock.SetBuffer("_PointBuffer", _pointBuffer); // 버퍼 설정

        _bounds.center = transform.position;

        // 드로우 호출 시 MaterialPropertyBlock 전달
        Graphics.DrawProceduralIndirect(
            pointMaterial,
            _bounds,
            MeshTopology.Points,
            _argsBuffer,
            0,
            null,
            _propBlock, // MaterialPropertyBlock 추가
            UnityEngine.Rendering.ShadowCastingMode.Off,
            false,
            gameObject.layer
        );
    }

    private void SetLayerRecursively(Transform parent, int layer)
    {
        parent.gameObject.layer = layer;
        foreach (Transform child in parent)
        {
            SetLayerRecursively(child, layer);
        }
    }

    void CreatePointBuffer(List<Vector3> points)
    {
        ReleasePointBuffer();

        _currentPointCount = points.Count;
        _pointBuffer = new ComputeBuffer(_currentPointCount, sizeof(float) * 4);

        // 아규먼트 버퍼 업데이트
        _args[0] = (uint)_currentPointCount;
        _args[1] = 1;
        _argsBuffer.SetData(_args);

        UpdatePointBuffer(points);
    }

    void UpdatePointBuffer(List<Vector3> points)
    {
        PointData[] pointData = new PointData[_currentPointCount];
        for (int i = 0; i < _currentPointCount; i++)
        {
            pointData[i] = new PointData
            {
                position = points[i],
                intensity = 1.0f
            };
        }
        _pointBuffer.SetData(pointData);
    }

    void ReleasePointBuffer()
    {
        if (_pointBuffer != null)
        {
            _argsBuffer.Release();
            _argsBuffer = null;
            _pointBuffer.Release();
            _pointBuffer = null;
            _currentPointCount = 0;
        }
    }

    void OnDisable() => ReleasePointBuffer();
    void OnDestroy() => ReleasePointBuffer();
}