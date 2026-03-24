using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SimpleTubeRenderer : MonoBehaviour
{
    public Transform[] points; // 고무줄의 노드(앵커) 포인트들
    public float radius = 0.2f; // 원기둥(고무줄)의 두께
    public int radialSegments = 8; // 원기둥을 몇 각형으로 만들 것인가 (8=팔각형)

    private Mesh _mesh;
    private MeshFilter _meshFilter;

    void Awake()
    {
        _meshFilter = GetComponent<MeshFilter>();
        _mesh = new Mesh();
        _meshFilter.mesh = _mesh;
    }

    void Update()
    {
        if (points == null || points.Length < 2) return;
        GenerateTubeMesh();
    }

    private void GenerateTubeMesh()
    {
        int numPoints = points.Length;
        int numVertices = numPoints * radialSegments;
        int numTriangles = (numPoints - 1) * radialSegments * 6;

        Vector3[] vertices = new Vector3[numVertices];
        int[] triangles = new int[numTriangles];
        Vector2[] uvs = new Vector2[numVertices];

        int vertIndex = 0;
        int triIndex = 0;

        for (int i = 0; i < numPoints; i++)
        {
            Vector3 forward = Vector3.forward;
            if (i < numPoints - 1) forward = (points[i + 1].position - points[i].position).normalized;
            else if (i > 0) forward = (points[i].position - points[i - 1].position).normalized;

            // 로컬 축 계산 (간단한 LookRotation)
            Vector3 up = Vector3.Cross(forward, Vector3.right).normalized;
            if (up == Vector3.zero) up = Vector3.Cross(forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(up, forward).normalized;

            // 원형으로 정점(Vertex) 배치
            for (int j = 0; j < radialSegments; j++)
            {
                float angle = (float)j / radialSegments * Mathf.PI * 2.0f;
                Vector3 localPos = (Mathf.Sin(angle) * right + Mathf.Cos(angle) * up) * radius;
                
                // 로컬 좌표계를 기준으로 정점 위치 계산 (오브젝트 기준)
                vertices[vertIndex] = transform.InverseTransformPoint(points[i].position) + localPos;
                uvs[vertIndex] = new Vector2((float)j / radialSegments, (float)i / (numPoints - 1));

                // 삼각형(폴리곤) 연결
                if (i < numPoints - 1)
                {
                    int nextI = i + 1;
                    int nextJ = (j + 1) % radialSegments;

                    int a = vertIndex;
                    int b = i * radialSegments + nextJ;
                    int c = nextI * radialSegments + j;
                    int d = nextI * radialSegments + nextJ;

                    triangles[triIndex++] = a; triangles[triIndex++] = c; triangles[triIndex++] = b;
                    triangles[triIndex++] = b; triangles[triIndex++] = c; triangles[triIndex++] = d;
                }
                vertIndex++;
            }
        }

        _mesh.Clear();
        _mesh.vertices = vertices;
        _mesh.triangles = triangles;
        _mesh.uv = uvs;
        _mesh.RecalculateNormals(); // 빛 반사를 위한 노말 계산
    }
}
