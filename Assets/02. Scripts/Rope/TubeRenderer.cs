using UnityEngine;

//Todo: 최적화
/// <summary>
/// 물리 엔진의 노드 데이터를 기반으로 Tube 3D 메쉬를 실시간으로 생성하는 렌더러입니다.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class TubeRenderer : MonoBehaviour, IRopeRenderer
{
    [Header("Tube Settings")]
    [Tooltip("원통 단면의 각 수 (8=팔각형)")]
    [SerializeField, Range(3, 32)] private int _sides = 8; 

    [Tooltip("렌더링 색상")]
    [SerializeField] private Gradient _color;
    
    private Mesh _mesh;
    private MeshFilter _meshFilter;

    private Vector3[] _vertices;
    private Vector2[] _uvs;
    private Color[] _colors;
    private int[] _triangles;
    
    private int _lastNodeCount = -1;

    private void Awake()
    {
        _meshFilter = GetComponent<MeshFilter>();
        
        // 실시간 갱신용 메쉬 생성
        _mesh = new Mesh { name = "DynamicTubeMesh" };
        
        // 엔진 내부적으로 잦은 갱신에 최적화된 메모리 배치를 사용하도록 지시
        _mesh.MarkDynamic(); 
        
        _meshFilter.mesh = _mesh;
    }

    /// <summary>
    /// 노드 개수에 맞춰 정점과 폴리곤 배열의 크기를 할당합니다.
    /// 개수가 변하지 않았다면 할당을 건너뜁니다.
    /// </summary>
    private void EnsureBufferCapacity(int nodeCount)
    {
        // 노드 개수가 동일하고, 단면의 각 수(_sides)도 그대로라면 재할당 방지
        if (nodeCount == _lastNodeCount && _vertices != null) return;

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

        // 삼각형 연결 구조는 노드 개수가 변하지 않는 한 영원히 동일하므로 여기서 1회만 계산
        GenerateTriangles(nodeCount);

        _lastNodeCount = nodeCount;
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
        if (nodePositions == null || nodePositions.Length < 2) return;

        int nodeCount = nodePositions.Length;

        // 1. 메모리 확보 (노드 개수가 바뀌었을 때만 재할당)
        EnsureBufferCapacity(nodeCount);

        // 튜브의 꼬임을 막기 위해 첫 번째 노드의 기준 상단(Up) 벡터를 임의로 잡습니다.
        Vector3 currentUp = Vector3.up; 

        // 2. 각 노드를 순회하며 단면(Ring)의 정점들을 계산
        for (int i = 0; i < nodeCount; i++)
        {
            CalculateSegmentOrientation(i, nodePositions, ref currentUp, out Vector3 forward, out Vector3 right);
            BuildRingVertices(i, nodePositions[i], currentUp, right, thickness);
        }

        // 3. 조립된 데이터를 실제 메쉬에 적용
        ApplyMeshData();
    }
    
    /// <summary>
    /// 현재 마디가 바라볼 방향(Forward)과 직교하는 Up, Right 벡터를 계산합니다.
    /// '메쉬 꼬임(Twist)'을 방지하기 위해 이전 마디의 Up 벡터(ref)를 참조하여 회전량을 누적합니다.
    /// </summary>
    private void CalculateSegmentOrientation(int index, Vector3[] nodes, ref Vector3 up, out Vector3 forward, out Vector3 right)
    {
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

    /// <summary>
    /// 계산된 방향 축을 바탕으로 원통의 단면(원형 테두리)을 이루는 정점 위치와 UV를 조립합니다.
    /// </summary>
    private void BuildRingVertices(int nodeIndex, Vector3 center, Vector3 up, Vector3 right, float thickness)
    {
        int offset = nodeIndex * _sides;
        float angleStep = (Mathf.PI * 2f) / _sides;
        
        // V축(세로축) UV 좌표: 로프 길이에 따라 텍스처가 타일링되도록 진행률(0~1)로 설정
        float uvV = (float)nodeIndex / (_lastNodeCount - 1);

        // 정점 색상 계산
        var vertexColor = _color.Evaluate(uvV);
        
        for (int s = 0; s < _sides; s++)
        {
            float angle = s * angleStep;

            // 삼각함수를 이용해 원의 둘레를 따라 도는 로컬 오프셋 계산
            // 반경은 두께의 절반(thickness * 0.5f)
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            Vector3 localPos = (right * cos + up * sin) * (thickness * 0.5f);

            // 최종 정점 좌표 적용
            _vertices[offset + s] = center + localPos;
            _colors[offset + s] = vertexColor;
            
            // U축(가로축) UV 좌표: 원통 단면을 감싸는 비율(0~1)
            float uvU = (float)s / _sides;
            _uvs[offset + s] = new Vector2(uvU, uvV);
        }
    }
    
    /// <summary>
    /// 조립이 완료된 정점(Vertices)과 UV 데이터를 실제 Mesh 객체에 덮어씌웁니다.
    /// </summary>
    private void ApplyMeshData()
    {
        // 1. GC 할당 없는 최신 API를 사용하여 데이터 주입
        _mesh.SetVertices(_vertices);
        _mesh.SetUVs(0, _uvs);
        _mesh.SetColors(_colors);

        // 삼각형 인덱스는 뼈대이므로, 정점 갱신 후 한 번 더 명시해주는 것이 안전합니다.
        // (만약 노드 개수가 절대 변하지 않는 게임이라면 최초 1회만 세팅하도록 최적화 가능)
        _mesh.SetTriangles(_triangles, 0);

        // 2. 조명(Lighting) 연산을 위한 법선 벡터(Normals) 자동 계산
        // 우리가 직접 법선을 수학적으로 계산해서 넣을 수도 있지만, 
        // 튜브 형태는 Unity의 내장 RecalculateNormals()가 충분히 빠르고 부드럽게 처리해 줍니다.
        _mesh.RecalculateNormals();

        // 3. 바운딩 박스(Bounding Box) 갱신 (매우 중요!!!)
        // 이걸 안 해주면 렌더러의 중심점(고무줄의 시작점)이 카메라 시야 밖으로 나갔을 때,
        // 고무줄 끝부분이 아직 화면에 보이는데도 전체 메쉬가 렌더링에서 제외(Culling)되어 
        // 고무줄이 픽셀 단위로 깜빡거리거나 갑자기 사라지는 치명적인 버그가 발생합니다.
        _mesh.RecalculateBounds();
    }
}
