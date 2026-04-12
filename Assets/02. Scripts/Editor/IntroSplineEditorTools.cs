using System.Collections.Generic;
using InGame.Camera.PlayerCamera;
using Unity.Cinemachine;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;


    [CustomEditor(typeof(IntroCameraController))]
    public class IntroSplineEditorTools : UnityEditor.Editor
    {
        private float _previewT;
        private bool _isPreviewing;
        private Vector3 _savedCameraPos;
        private Quaternion _savedCameraRot;

        // ── Inspector GUI ──────────────────────────────────

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Spline Tools", EditorStyles.boldLabel);

            DrawWaypointSection();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

            DrawPreviewSection();
        }

        // ── Waypoint → Spline ──────────────────────────────

        private void DrawWaypointSection()
        {
            EditorGUILayout.HelpBox(
                "자식 오브젝트를 웨이포인트로 사용합니다.\n" +
                "빈 GameObject를 자식으로 배치하고, 시선 지점이 필요하면\n" +
                "각 웨이포인트 아래에 'LookAt' 이름의 자식을 추가하세요.\n" +
                "이름 순서(알파벳)로 Dolly + LookAt Spline이 동시 생성됩니다.",
                MessageType.Info
            );

            var controller = (IntroCameraController)target;

            if (GUILayout.Button("웨이포인트 → Spline 생성 (Dolly + LookAt)", GUILayout.Height(30)))
            {
                BuildSplinesFromChildren(controller);
            }

            EditorGUILayout.Space(4);

            if (GUILayout.Button("웨이포인트 자동 생성 (5개)", GUILayout.Height(24)))
            {
                CreateWaypointTemplate(controller);
            }
        }

        private static void CreateWaypointTemplate(IntroCameraController controller)
        {
            Undo.SetCurrentGroupName("Create Waypoint Template");
            int group = Undo.GetCurrentGroup();

            for (int i = 0; i < 5; i++)
            {
                var wp = new GameObject($"WP_{i + 1:D2}");
                Undo.RegisterCreatedObjectUndo(wp, "Create Waypoint");
                wp.transform.SetParent(controller.transform);
                wp.transform.localPosition = new Vector3(0f, 20f, i * -40f);

                var marker = wp.AddComponent<IntroWaypoint>();
                marker.index = i;

                var lookAt = new GameObject("LookAt");
                Undo.RegisterCreatedObjectUndo(lookAt, "Create LookAt");
                lookAt.transform.SetParent(wp.transform);
                lookAt.transform.localPosition = new Vector3(0f, -15f, -20f);
            }

            Undo.CollapseUndoOperations(group);
            Debug.Log("[IntroSplineTools] 웨이포인트 5개 + LookAt 자식 생성 완료. Scene View에서 위치를 조정하세요.");
        }

        private static void BuildSplinesFromChildren(IntroCameraController controller)
        {
            var waypoints = CollectWaypoints(controller.transform);

            if (waypoints.Count < 2)
            {
                EditorUtility.DisplayDialog(
                    "Spline 생성 실패",
                    "최소 2개 이상의 자식 웨이포인트가 필요합니다.",
                    "확인"
                );
                return;
            }

            var so = new SerializedObject(controller);

            // Dolly Spline 생성/갱신.
            var dollySpline = GetOrCreateSplineContainer(
                so, "_dollySpline", "IntroDollySpline_Generated"
            );
            BuildSpline(dollySpline, waypoints, useWaypointPosition: true);

            // LookAt 자식이 하나라도 있으면 LookAt Spline 생성.
            bool hasAnyLookAt = false;
            foreach (var wp in waypoints)
            {
                if (wp.Find("LookAt") != null)
                {
                    hasAnyLookAt = true;
                    break;
                }
            }

            if (hasAnyLookAt)
            {
                var lookAtSpline = GetOrCreateSplineContainer(
                    so, "_lookAtSpline", "IntroLookAtSpline_Generated"
                );
                BuildSpline(lookAtSpline, waypoints, useWaypointPosition: false);

                Debug.Log(
                    $"[IntroSplineTools] Dolly + LookAt Spline 생성 완료 ({waypoints.Count}개 웨이포인트)"
                );
            }
            else
            {
                Debug.Log(
                    $"[IntroSplineTools] Dolly Spline 생성 완료 ({waypoints.Count}개 웨이포인트). " +
                    "LookAt 자식이 없어 LookAt Spline은 생성하지 않았습니다."
                );
            }
        }

        private static SplineContainer GetOrCreateSplineContainer(
            SerializedObject so, string propertyName, string defaultName)
        {
            var prop = so.FindProperty(propertyName);
            var container = prop.objectReferenceValue as SplineContainer;

            if (container == null)
            {
                var obj = new GameObject(defaultName);
                Undo.RegisterCreatedObjectUndo(obj, $"Create {defaultName}");
                container = obj.AddComponent<SplineContainer>();

                prop.objectReferenceValue = container;
                so.ApplyModifiedProperties();
            }

            return container;
        }

        private static void BuildSpline(
            SplineContainer container,
            List<Transform> waypoints,
            bool useWaypointPosition)
        {
            Undo.RecordObject(container, "Build Spline From Waypoints");

            var spline = container.Spline;
            spline.Clear();

            var containerTransform = container.transform;

            foreach (var wp in waypoints)
            {
                Vector3 worldPos;

                if (useWaypointPosition)
                {
                    // Dolly: 웨이포인트 자체의 위치.
                    worldPos = wp.position;
                }
                else
                {
                    // LookAt: 자식 'LookAt'이 있으면 그 위치, 없으면 웨이포인트 전방.
                    var lookAtChild = wp.Find("LookAt");
                    worldPos = lookAtChild != null
                        ? lookAtChild.position
                        : wp.position + wp.forward * 20f;
                }

                Vector3 localPos = containerTransform.InverseTransformPoint(worldPos);
                spline.Add(new BezierKnot(new float3(localPos.x, localPos.y, localPos.z)));
            }

            spline.SetTangentMode(new SplineRange(0, spline.Count), TangentMode.AutoSmooth);
            EditorUtility.SetDirty(container);
        }

        private static List<Transform> CollectWaypoints(Transform parent)
        {
            var list = new List<Transform>();

            for (int i = 0; i < parent.childCount; i++)
            {
                list.Add(parent.GetChild(i));
            }

            list.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
            return list;
        }

        // ── Preview ────────────────────────────────────────

        private void DrawPreviewSection()
        {
            var controller = (IntroCameraController)target;
            var so = new SerializedObject(controller);
            var dollySplineProp = so.FindProperty("_dollySpline");
            var cameraProp = so.FindProperty("_camera");

            var splineContainer = dollySplineProp.objectReferenceValue as SplineContainer;
            var cmCamera = cameraProp.objectReferenceValue as CinemachineCamera;

            if (splineContainer == null || cmCamera == null)
            {
                EditorGUILayout.HelpBox(
                    "Camera와 Dolly Spline이 할당되어야 미리보기를 사용할 수 있습니다.",
                    MessageType.Warning
                );
                return;
            }

            EditorGUI.BeginChangeCheck();
            _previewT = EditorGUILayout.Slider("Position (0~1)", _previewT, 0f, 1f);
            bool changed = EditorGUI.EndChangeCheck();

            EditorGUILayout.BeginHorizontal();

            if (!_isPreviewing)
            {
                if (GUILayout.Button("미리보기 시작", GUILayout.Height(25)))
                {
                    StartPreview(cmCamera);
                }
            }
            else
            {
                if (GUILayout.Button("미리보기 종료", GUILayout.Height(25)))
                {
                    StopPreview(cmCamera);
                }
            }

            EditorGUILayout.EndHorizontal();

            if (_isPreviewing && changed)
            {
                ApplyPreview(splineContainer, cmCamera, controller);
            }
        }

        private void StartPreview(CinemachineCamera cmCamera)
        {
            _isPreviewing = true;
            _savedCameraPos = cmCamera.transform.position;
            _savedCameraRot = cmCamera.transform.rotation;
            SceneView.RepaintAll();
        }

        private void StopPreview(CinemachineCamera cmCamera)
        {
            _isPreviewing = false;
            cmCamera.transform.position = _savedCameraPos;
            cmCamera.transform.rotation = _savedCameraRot;
            SceneView.RepaintAll();
        }

        private void ApplyPreview(
            SplineContainer splineContainer,
            CinemachineCamera cmCamera,
            IntroCameraController controller)
        {
            if (splineContainer.Spline == null || splineContainer.Spline.Count < 2) return;

            // Spline 위치 계산.
            float3 localPos = SplineUtility.EvaluatePosition(splineContainer.Spline, _previewT);
            Vector3 worldPos = splineContainer.transform.TransformPoint(localPos);

            cmCamera.transform.position = worldPos;

            // LookAt Spline 시선 적용.
            var lookAtSo = new SerializedObject(controller);
            var lookAtProp = lookAtSo.FindProperty("_lookAtSpline");
            var lookAtSpline = lookAtProp.objectReferenceValue as SplineContainer;

            if (lookAtSpline != null && lookAtSpline.Spline != null && lookAtSpline.Spline.Count >= 2)
            {
                float3 lookLocalPos = SplineUtility.EvaluatePosition(lookAtSpline.Spline, _previewT);
                Vector3 lookWorldPos = lookAtSpline.transform.TransformPoint(lookLocalPos);
                Vector3 dir = lookWorldPos - worldPos;

                if (dir.sqrMagnitude > 0.001f)
                    cmCamera.transform.rotation = Quaternion.LookRotation(dir);
            }
            else
            {
                // LookAt Spline 없으면 Spline tangent 방향 사용.
                float3 tangent = SplineUtility.EvaluateTangent(splineContainer.Spline, _previewT);
                Vector3 worldTangent = splineContainer.transform.TransformDirection(tangent);

                if (worldTangent.sqrMagnitude > 0.001f)
                    cmCamera.transform.rotation = Quaternion.LookRotation(worldTangent);
            }

            // Scene View 카메라도 동기화.
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                sceneView.pivot = worldPos;
                sceneView.Repaint();
            }
        }

        private void OnDisable()
        {
            if (_isPreviewing)
            {
                var controller = target as IntroCameraController;
                if (controller != null)
                {
                    var so = new SerializedObject(controller);
                    var cameraProp = so.FindProperty("_camera");
                    var cmCamera = cameraProp.objectReferenceValue as CinemachineCamera;

                    if (cmCamera != null)
                        StopPreview(cmCamera);
                }
            }
        }

        // ── Scene Gizmo ────────────────────────────────────

        private static readonly Color DollyColor = new(0.2f, 0.8f, 1f, 1f);
        private static readonly Color LookAtColor = new(1f, 0.6f, 0.2f, 1f);
        private static readonly Color LineColor = new(0.2f, 0.8f, 1f, 0.4f);
        private static readonly Color SightLineColor = new(1f, 0.6f, 0.2f, 0.5f);

        private void OnSceneGUI()
        {
            var controller = (IntroCameraController)target;
            var waypoints = CollectWaypoints(controller.transform);

            if (waypoints.Count == 0) return;

            for (int i = 0; i < waypoints.Count; i++)
            {
                var wp = waypoints[i];
                Vector3 pos = wp.position;

                // 웨이포인트 구체.
                Handles.color = DollyColor;
                Handles.SphereHandleCap(0, pos, Quaternion.identity, 1.2f, EventType.Repaint);

                // 번호 라벨.
                var style = new GUIStyle(EditorStyles.boldLabel)
                {
                    normal = { textColor = DollyColor },
                    fontSize = 14,
                    alignment = TextAnchor.MiddleCenter
                };
                Handles.Label(pos + Vector3.up * 2f, $"{i + 1}", style);

                // 이전 웨이포인트와 연결선.
                if (i > 0)
                {
                    Handles.color = LineColor;
                    Handles.DrawDottedLine(waypoints[i - 1].position, pos, 4f);
                }

                // LookAt 자식 시각화.
                var lookAtChild = wp.Find("LookAt");
                if (lookAtChild != null)
                {
                    Vector3 lookPos = lookAtChild.position;

                    // LookAt 다이아몬드.
                    Handles.color = LookAtColor;
                    Handles.SphereHandleCap(0, lookPos, Quaternion.identity, 0.7f, EventType.Repaint);

                    // 시선 방향 점선.
                    Handles.color = SightLineColor;
                    Handles.DrawDottedLine(pos, lookPos, 3f);

                    // 시선 방향 화살표.
                    Vector3 dir = (lookPos - pos).normalized;
                    float dist = Vector3.Distance(pos, lookPos);
                    Handles.color = LookAtColor;
                    Handles.ArrowHandleCap(
                        0, pos + dir * (dist * 0.5f), Quaternion.LookRotation(dir),
                        Mathf.Min(dist * 0.3f, 3f), EventType.Repaint
                    );

                    // LookAt 라벨.
                    var lookStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        normal = { textColor = LookAtColor },
                        fontSize = 11,
                        alignment = TextAnchor.MiddleCenter
                    };
                    Handles.Label(lookPos + Vector3.up * 1.2f, $"Eye {i + 1}", lookStyle);
                }
            }
        }
    }

