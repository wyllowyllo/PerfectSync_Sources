using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 물리 엔진의 노드 데이터를 기반으로 Tube 3D 메쉬를 실시간으로 생성하는 렌더러입니다.
/// </summary>
public class TubeRenderer : IRopeRenderer
{
    private readonly int _sides; 
    private readonly Gradient _color;
    
    private Mesh _mesh;
    private readonly MeshFilter _meshFilter;

    private Vector3[] _vertices;
    private Vector2[] _uvs;
    private Color[] _colors;
    private int[] _triangles;

    private int _nodeCount;
    private float[] _cos;
    private float[] _sin;

    public TubeRenderer(MeshFilter meshFilter, int sides, Gradient color, int nodeCount)
    {
        _meshFilter = meshFilter;
        _sides = sides;
        _color = color;
        _nodeCount = nodeCount;
        
        Initialize(nodeCount);
    }
    
    /// <summary>
    /// 노드 개수에 맞춰 정점과 폴리곤 배열의 크기를 할당합니다.
    /// </summary>
    public void Initialize(int nodeCount)
    {
        if (nodeCount < 2) throw new ArgumentOutOfRangeException(nameof(nodeCount), "로프를 구성하기 위해 노드는 최소 2개 이상 필요합니다.");
        
        _mesh = new Mesh { name = "DynamicTubeMesh" };
        
        // 엔진 내부적으로 잦은 갱신에 최적화된 메모리 배치를 사용하도록 지시
        _mesh.MarkDynamic(); 
        
        _meshFilter.mesh = _mesh;
        
        _nodeCount = nodeCount;
        
        // 원통 메쉬에 필요한 총 정점 수: (노드 개수) * (단면의 점 개수)
        int vertexCount = nodeCount * _sides;
        
        // 원통의 마디(Segment) 수: 노드 개수 - 1
        // 한 마디당 사각형(Quad)이 _sides개 필요하고, 사각형 1개는 삼각형 2개(인덱스 6개)로 구성됨
        int triangleIndexCount = (nodeCount - 1) * _sides * 6;

        // 배열 할당
        _vertices = new Vector3[vertexCount];
        _uvs = new Vector2[vertexCount];
        _colors = new Color[vertexCount];
        _triangles = new int[triangleIndexCount];

        // 삼각함수 계산
        GenerateSinCos();
        
        // uv와 색상 세팅
        GenerateStaticData();
        
        // 삼각형 연결 구조 계산
        GenerateTriangles(nodeCount);

        _mesh.SetVertices(_vertices);
        _mesh.SetUVs(0, _uvs);
        _mesh.SetColors(_colors);
        _mesh.SetTriangles(_triangles, 0);
    }

    /// <summary>
    /// 삼각함수 배열을 생성합니다.
    /// </summary>
    private void GenerateSinCos()
    {
        _cos = new float[_sides];
        _sin = new float[_sides];
        float angleStep = (Mathf.PI * 2f) / _sides;

        for (int s = 0; s < _sides; s++)
        {
            float angle = s * angleStep;
            _cos[s] = Mathf.Cos(angle);
            _sin[s] = Mathf.Sin(angle);
        }
    }

    /// <summary>
    /// 정적 데이터를 세팅합니다.
    /// </summary>
    private void GenerateStaticData()
    {
        for (int i = 0; i < _nodeCount; i++)
        {
            int offset = i * _sides;
            float uvV = (float)i / (_nodeCount - 1);
            Color vertexColor = _color.Evaluate(uvV);

            for (int s = 0; s < _sides; s++)
            {
                float uvU = (float)s / _sides;
                _uvs[offset + s] = new Vector2(uvU, uvV);
                _colors[offset + s] = vertexColor;
            }
        }
    }
    
    /// <summary>
    /// 정점들을 이어붙여 표면을 구성하는 삼각형 인덱스 배열을 생성합니다.
    /// </summary>
    private void GenerateTriangles(int nodeCount)
    {
        int ti = 0; // 삼각형 배열 인덱스
        
        // 각 마디(Segment)를 순회하며 사각형(Quad) 단위를 삼각형 2개로 쪼개어 연결
        for (int i = 0; i < nodeCount - 1; i++)
        {
            int currentSegmentOffset = i * _sides;
            int nextSegmentOffset = (i + 1) * _sides;

            for (int s = 0; s < _sides; s++)
            {
                int currentSide = s;
                int nextSide = (s + 1) % _sides; // 마지막 점은 첫 번째 점과 연결되어 닫힌 원을 만듦

                // 사각형을 구성하는 4개의 정점 인덱스 계산
                int a = currentSegmentOffset + currentSide;
                int b = nextSegmentOffset + currentSide;
                int c = nextSegmentOffset + nextSide;
                int d = currentSegmentOffset + nextSide;

                // 첫 번째 삼각형 (a -> b -> c)
                _triangles[ti++] = a;
                _triangles[ti++] = b;
                _triangles[ti++] = c;

                // 두 번째 삼각형 (a -> c -> d)
                _triangles[ti++] = a;
                _triangles[ti++] = c;
                _triangles[ti++] = d;
            }
        }
    }
    
    /// <summary>
    /// 물리 엔진에서 계산된 노드 좌표들을 받아 원통형 메쉬를 생성/갱신합니다.
    /// </summary>
    public void RenderRope(Vector3[] nodePositions, float thickness)
    {
        if (nodePositions == null || nodePositions.Length != _nodeCount) throw new ArgumentException("렌더링을 위한 노드가 부족합니다.");

        int nodeCount = nodePositions.Length;

        // 튜브의 꼬임을 막기 위해 첫 번째 노드의 기준 상단(Up) 벡터를 임의로 잡습니다.
        Vector3 currentUp = Vector3.up; 
        float radius = thickness * 0.5f;
        
        // 튜브 정점 생성 로직 - 노드를 순회하며 Ring 형태를 만드는 정점 계산
        for (int i = 0; i < _nodeCount; i++)
        {
            CalculateSegmentOrientation(i, nodePositions, ref currentUp, out var right);
            
            int offset = i * _sides;
            Vector3 center = nodePositions[i];
            
            for (int s = 0; s < _sides; s++)
            {
                Vector3 localPosition = (right * _cos[s] + currentUp * _sin[s]) * radius;
                _vertices[offset + s] = center + localPosition;
            }
        }
        
        // 정점만 메쉬에 덮어씌움
        _mesh.SetVertices(_vertices, 0, _vertices.Length, MeshUpdateFlags.DontRecalculateBounds);
        
        // 조명 연산 필요 시 _mesh.RecalculateNormals() 추가

        // 카메라 컬링을 위한 바운딩 박스 갱신
        _mesh.RecalculateBounds();
    }
    
    /// <summary>
    /// 현재 마디가 바라볼 방향(Forward)과 직교하는 Up, Right 벡터를 계산합니다.
    /// '메쉬 꼬임(Twist)'을 방지하기 위해 이전 마디의 Up 벡터(ref)를 참조하여 회전량을 누적합니다.
    /// </summary>
    private void CalculateSegmentOrientation(int index, Vector3[] nodes, ref Vector3 up, out Vector3 right)
    {
        Vector3 forward;
        
        // 1. Forward 방향 벡터 계산
        if (index < nodes.Length - 1)
        {
            // 다음 노드를 향하는 방향
            forward = (nodes[index + 1] - nodes[index]).normalized;
        }
        else
        {
            // 마지막 노드인 경우, 직전 마디의 방향을 그대로 유지
            forward = (nodes[index] - nodes[index - 1]).normalized;
        }

        // 0으로 나누기 및 벡터 소실 방지 (노드들이 완전히 겹쳤을 때)
        if (forward == Vector3.zero) forward = Vector3.forward;

        // 2. 꼬임 방지 로직 (Parallel Transport)
        // 이전 노드의 Up 벡터를 현재의 Forward 벡터에 직교하는 평면으로 투영(Project)하여 새로운 Up을 구합니다.
        // 이렇게 하면 고무줄이 심하게 꺾여도 메쉬가 180도 뒤집어지지 않습니다.
        Vector3 projectedUp = up - Vector3.Project(up, forward);
        
        // 투영된 결과가 0벡터에 가깝다면 (Forward와 Up이 평행해지는 특이점 발생 시) 임의의 축으로 재설정
        if (projectedUp.sqrMagnitude < 0.0001f)
        {
            projectedUp = Vector3.Cross(forward, Vector3.right);
            if (projectedUp.sqrMagnitude < 0.0001f)
                projectedUp = Vector3.Cross(forward, Vector3.up);
        }

        up = projectedUp.normalized;

        // 3. 완벽하게 직교하는 Right 벡터 도출
        right = Vector3.Cross(up, forward).normalized;
    }
}
