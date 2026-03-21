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
        private EPoseDirection _direction;
        private bool _isActive;

        private void Awake()
        {
            BuildBoneMapping();
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

        private void CopyBones(Transform[] source, Transform[] target)
        {
            for (int i = 0; i < source.Length; i++)
            {
                target[i].localPosition = source[i].localPosition;
                target[i].localRotation = source[i].localRotation;
            }
        }

        private void BuildBoneMapping()
        {
            var ragdollMap = new Dictionary<string, Transform>();
            CollectBones(_ragdollRoot, ragdollMap);

            var visualList = new List<Transform>();
            var ragdollList = new List<Transform>();

            var visualMap = new Dictionary<string, Transform>();
            CollectBones(_visualRoot, visualMap);

            foreach (var kvp in ragdollMap)
            {
                if (visualMap.TryGetValue(kvp.Key, out Transform visualBone))
                {
                    visualList.Add(visualBone);
                    ragdollList.Add(kvp.Value);
                }
                else
                {
                    Debug.LogWarning($"[PoseTransfer] RagdollRig 본 '{kvp.Key}'에 대응하는 VisualRoot 본을 찾지 못했습니다.");
                }
            }

            _visualBones = visualList.ToArray();
            _ragdollBones = ragdollList.ToArray();

            Debug.Log($"[PoseTransfer] 본 매핑 완료: {_visualBones.Length}개 본 연결됨.");
        }

        private void CollectBones(Transform root, Dictionary<string, Transform> map)
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
