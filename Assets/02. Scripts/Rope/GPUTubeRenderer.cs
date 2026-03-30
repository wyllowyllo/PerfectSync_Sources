using System;
using UnityEngine;

/// <summary>
/// GPU 기반 로프 렌더러.
/// 물리 노드 위치를 ComputeBuffer로 셰이더에 전달하여
/// Catmull-Rom 보간과 원통 메쉬 정점 계산을 GPU vertex shader에서 수행합니다.
/// CPU에서는 노드 위치 업로드만 수행하므로 per-vertex 연산이 제거됩니다.
/// </summary>
public class GPUTubeRenderer : IRopeRenderer, IDisposable
{
    private static readonly int NodePositionsId = Shader.PropertyToID("_NodePositions");
    private static readonly int ThicknessId = Shader.PropertyToID("_Thickness");
    private static readonly int PhysicsNodeCountId = Shader.PropertyToID("_PhysicsNodeCount");
    private static readonly int InterpolationSegmentsId = Shader.PropertyToID("_InterpolationSegments");
    private static readonly int RenderNodeCountId = Shader.PropertyToID("_RenderNodeCount");

    private readonly int _physicsNodeCount;
    private readonly int _renderNodeCount;
    private readonly int _sides;

    private Mesh _mesh;
    private readonly MeshFilter _meshFilter;
    private readonly MeshRenderer _meshRenderer;
    private ComputeBuffer _nodeBuffer;
    private readonly MaterialPropertyBlock _propertyBlock;

    public GPUTubeRenderer(MeshFilter meshFilter, MeshRenderer meshRenderer,
        int sides, Gradient color, int physicsNodeCount, int interpolationSegments = 3)
    {
        _meshFilter = meshFilter;
        _meshRenderer = meshRenderer;
        _sides = sides;
        _physicsNodeCount = physicsNodeCount;
        int segments = Mathf.Max(1, interpolationSegments);
        _renderNodeCount = (physicsNodeCount - 1) * segments + 1;

        // 물리 노드 좌표를 GPU에 전달하기 위한 ComputeBuffer
        _nodeBuffer = new ComputeBuffer(physicsNodeCount, sizeof(float) * 3);
        _nodeBuffer.SetData(new Vector3[physicsNodeCount]);

        _propertyBlock = new MaterialPropertyBlock();
        _propertyBlock.SetInt(PhysicsNodeCountId, physicsNodeCount);
        _propertyBlock.SetInt(InterpolationSegmentsId, segments);
        _propertyBlock.SetInt(RenderNodeCountId, _renderNodeCount);

        InitializeMesh(color);
    }

    /// <summary>
    /// 정적 메쉬 토폴로지(UV, 정점 색상, 삼각형)를 1회 생성합니다.
    /// 정점 좌표는 셰이더에서 ComputeBuffer 데이터로 덮어씌워집니다.
    /// </summary>
    private void InitializeMesh(Gradient color)
    {
        _mesh = new Mesh { name = "GPURopeTubeMesh" };
        _mesh.MarkDynamic();
        _meshFilter.mesh = _mesh;

        int vertexCount = _renderNodeCount * _sides;
        int triangleCount = (_renderNodeCount - 1) * _sides * 6;

        var vertices = new Vector3[vertexCount];
        var uvs = new Vector2[vertexCount];
        var colors = new Color[vertexCount];
        var triangles = new int[triangleCount];

        // UV와 정점 색상 설정 (셰이더의 정점 인덱싱 + Gradient 컬러에 사용)
        for (int i = 0; i < _renderNodeCount; i++)
        {
            int offset = i * _sides;
            float uvV = (float)i / (_renderNodeCount - 1);
            Color vertexColor = color.Evaluate(uvV);

            for (int s = 0; s < _sides; s++)
            {
                uvs[offset + s] = new Vector2((float)s / _sides, uvV);
                colors[offset + s] = vertexColor;
            }
        }

        // 삼각형 인덱스 생성
        int ti = 0;
        for (int i = 0; i < _renderNodeCount - 1; i++)
        {
            int curr = i * _sides;
            int next = (i + 1) * _sides;
            for (int s = 0; s < _sides; s++)
            {
                int ns = (s + 1) % _sides;
                int a = curr + s, b = next + s, c = next + ns, d = curr + ns;
                triangles[ti++] = a; triangles[ti++] = b; triangles[ti++] = c;
                triangles[ti++] = a; triangles[ti++] = c; triangles[ti++] = d;
            }
        }

        _mesh.SetVertices(vertices);
        _mesh.SetUVs(0, uvs);
        _mesh.SetColors(colors);
        _mesh.SetTriangles(triangles, 0);

        // 셰이더가 정점을 GPU에서 계산하므로 큰 바운드를 초기값으로 설정
        _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100f);
    }

    /// <summary>
    /// 물리 노드 좌표를 ComputeBuffer에 업로드하고 MaterialPropertyBlock으로 셰이더에 전달합니다.
    /// </summary>
    public void RenderRope(Vector3[] nodePositions, float thickness)
    {
        if (nodePositions == null || nodePositions.Length != _physicsNodeCount) return;

        _nodeBuffer.SetData(nodePositions);

        _propertyBlock.SetBuffer(NodePositionsId, _nodeBuffer);
        _propertyBlock.SetFloat(ThicknessId, thickness);
        _meshRenderer.SetPropertyBlock(_propertyBlock);

        UpdateBounds(nodePositions);
    }

    /// <summary>
    /// 물리 노드 좌표 기반으로 메쉬 바운드를 로컬 공간에서 갱신합니다.
    /// </summary>
    private void UpdateBounds(Vector3[] nodePositions)
    {
        Transform t = _meshFilter.transform;
        Vector3 localPos = t.InverseTransformPoint(nodePositions[0]);
        Vector3 min = localPos, max = localPos;

        for (int i = 1; i < nodePositions.Length; i++)
        {
            localPos = t.InverseTransformPoint(nodePositions[i]);
            min = Vector3.Min(min, localPos);
            max = Vector3.Max(max, localPos);
        }

        _mesh.bounds = new Bounds((min + max) * 0.5f, max - min + Vector3.one * 2f);
    }

    public void Dispose()
    {
        if (_nodeBuffer != null)
        {
            _nodeBuffer.Release();
            _nodeBuffer = null;
        }
    }
}
