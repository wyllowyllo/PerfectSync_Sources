using System.Collections.Generic;
using UnityEngine;

namespace InGame.Player.Ragdoll
{
    public class PoseTransfer : MonoBehaviour, IPoseTransfer
    {
        [SerializeField] private Transform _visualRoot;
        [SerializeField] private Transform _ragdollRoot;

        private Transform[] _visualBones;
        private Transform[] _ragdollBones;

        // 초기 로컬 포즈 스냅샷 (복구용).
        private Vector3[] _restLocalPositions;
        private Quaternion[] _restLocalRotations;

        private EPoseDirection _direction;
        private bool _isActive;

        private void Awake()
        {
            BuildBoneMapping();
            CaptureRestPose();
        }

        public void SetDirection(EPoseDirection direction)
        {
            _direction = direction;
            _isActive = true;
        }

        public void CopyPose()
        {
            if (!_isActive) return;

            if (_direction == EPoseDirection.AnimToRagdoll)
                CopyBones(_visualBones, _ragdollBones);
            else
                CopyBones(_ragdollBones, _visualBones);
        }

        public void Stop()
        {
            _isActive = false;
        }

        // 매핑된 비주얼 본을 초기 로컬 포즈로 복원.
        public void RestoreVisualBones()
        {
            for (int i = 0; i < _visualBones.Length; i++)
            {
                _visualBones[i].localPosition = _restLocalPositions[i];
                _visualBones[i].localRotation = _restLocalRotations[i];
            }
        }

        private void CopyBones(Transform[] source, Transform[] target)
        {
            for (int i = 0; i < source.Length; i++)
            {
                target[i].position = source[i].position;
                target[i].rotation = source[i].rotation;
            }
        }

        private void CaptureRestPose()
        {
            _restLocalPositions = new Vector3[_visualBones.Length];
            _restLocalRotations = new Quaternion[_visualBones.Length];

            for (int i = 0; i < _visualBones.Length; i++)
            {
                _restLocalPositions[i] = _visualBones[i].localPosition;
                _restLocalRotations[i] = _visualBones[i].localRotation;
            }
        }

        private void BuildBoneMapping()
        {
            // RagdollRig에서 Rigidbody 있는 물리 본만 수집.
            var ragdollMap = new Dictionary<string, Transform>();
            foreach (var rb in _ragdollRoot.GetComponentsInChildren<Rigidbody>(true))
                ragdollMap[rb.name] = rb.transform;

            // VisualRoot 전체 본에서 매칭 대상 검색.
            var visualMap = new Dictionary<string, Transform>();
            CollectAllBones(_visualRoot, visualMap);

            var visualList = new List<Transform>();
            var ragdollList = new List<Transform>();

            foreach (var kvp in ragdollMap)
            {
                if (visualMap.TryGetValue(kvp.Key, out Transform visualBone))
                {
                    visualList.Add(visualBone);
                    ragdollList.Add(kvp.Value);
                }
                else
                {
                    Debug.LogWarning($"[PoseTransfer] 물리 본 '{kvp.Key}'에 대응하는 VisualRoot 본을 찾지 못했습니다.");
                }
            }

            _visualBones = visualList.ToArray();
            _ragdollBones = ragdollList.ToArray();

            Debug.Log($"[PoseTransfer] 본 매핑 완료: {_visualBones.Length}개 물리 본 연결됨.");
        }

        private void CollectAllBones(Transform root, Dictionary<string, Transform> map)
        {
            var stack = new Stack<Transform>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                Transform current = stack.Pop();
                map[current.name] = current;

                for (int i = 0; i < current.childCount; i++)
                    stack.Push(current.GetChild(i));
            }
        }
    }
}
