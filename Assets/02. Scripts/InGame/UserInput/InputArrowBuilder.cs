using UnityEngine;

namespace InGame.UserInput
{
    /// <summary>
    /// [InputViz] LineRenderer 기반 화살표 및 점프 마커 팩토리.
    /// 테스트용 - 이 파일 전체 삭제로 제거 가능.
    /// </summary>
    public static class InputArrowBuilder
    {
        /// <summary>
        /// 3점 V자 화살표를 생성한다. LineRenderer로 구성.
        /// </summary>
        public static GameObject CreateArrow(string name, Color color, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = color;
            lr.endColor = color;
            lr.startWidth = 0.08f;
            lr.endWidth = 0.08f;
            lr.positionCount = 3;

            // V자 화살표 형태: 좌측 날개 → 꼭짓점 → 우측 날개
            float wingLen = 0.3f;
            float wingAngle = 30f * Mathf.Deg2Rad;

            Vector3 tip = Vector3.forward * 0.5f;
            Vector3 leftWing = tip + new Vector3(-Mathf.Sin(wingAngle), 0, -Mathf.Cos(wingAngle)) * wingLen;
            Vector3 rightWing = tip + new Vector3(Mathf.Sin(wingAngle), 0, -Mathf.Cos(wingAngle)) * wingLen;

            lr.SetPosition(0, leftWing);
            lr.SetPosition(1, tip);
            lr.SetPosition(2, rightWing);

            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;

            return go;
        }

        /// <summary>
        /// ▲ 삼각형 점프 마커를 생성한다. 기본 비활성 상태.
        /// </summary>
        public static GameObject CreateJumpIndicator(string name, Color color, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = color;
            lr.endColor = color;
            lr.startWidth = 0.06f;
            lr.endWidth = 0.06f;
            lr.loop = true;
            lr.positionCount = 3;

            // ▲ 삼각형
            float size = 0.15f;
            lr.SetPosition(0, new Vector3(0, size, 0));
            lr.SetPosition(1, new Vector3(-size * 0.7f, 0, 0));
            lr.SetPosition(2, new Vector3(size * 0.7f, 0, 0));

            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;

            go.SetActive(false);
            return go;
        }
    }
}
