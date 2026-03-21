using System.Collections.Generic;
using UnityEngine;

namespace InGame.Player.Ragdoll
{
    public class RagdollRig : MonoBehaviour, IRagdollRig
    {
        [SerializeField] private Transform _pelvis;
        [SerializeField] private Rigidbody _rootBody;

        [Header("Ragdoll Damping")]
        [SerializeField] private float _activeLinearDamping = 0.5f;
        [SerializeField] private float _activeAngularDamping = 1.0f;

        private Rigidbody[] _ragdollRbs;
        private Collider[] _ragdollCols;
        private Transform[] _ragdollBoneTransforms;
        private float[] _originalLinearDamping;
        private float[] _originalAngularDamping;

        public IReadOnlyList<Rigidbody> Rigidbodies => _ragdollRbs;
        public IReadOnlyList<Transform> BoneTransforms => _ragdollBoneTransforms;
        public Transform PelvisTransform => _pelvis;
        public Rigidbody RootBody => _rootBody;

        private void Awake()
        {
            _ragdollRbs = GetComponentsInChildren<Rigidbody>(true);
            _ragdollCols = GetComponentsInChildren<Collider>(true);

            _ragdollBoneTransforms = new Transform[_ragdollRbs.Length];
            _originalLinearDamping = new float[_ragdollRbs.Length];
            _originalAngularDamping = new float[_ragdollRbs.Length];
            for (int i = 0; i < _ragdollRbs.Length; i++)
            {
                _ragdollBoneTransforms[i] = _ragdollRbs[i].transform;
                _originalLinearDamping[i] = _ragdollRbs[i].linearDamping;
                _originalAngularDamping[i] = _ragdollRbs[i].angularDamping;
            }

            gameObject.SetActive(false);
        }

        public void ActivateKinematic()
        {
            gameObject.SetActive(true);

            for (int i = 0; i < _ragdollRbs.Length; i++)
                _ragdollRbs[i].isKinematic = true;

            for (int i = 0; i < _ragdollCols.Length; i++)
                _ragdollCols[i].enabled = false;

            _rootBody.isKinematic = true;
        }

        public void Activate(Vector3 inheritedVelocity)
        {
            gameObject.SetActive(true);

            for (int i = 0; i < _ragdollRbs.Length; i++)
            {
                _ragdollRbs[i].isKinematic = false;
                _ragdollRbs[i].linearDamping = _activeLinearDamping;
                _ragdollRbs[i].angularDamping = _activeAngularDamping;
                _ragdollRbs[i].linearVelocity = inheritedVelocity;
            }

            for (int i = 0; i < _ragdollCols.Length; i++)
                _ragdollCols[i].enabled = true;

            _rootBody.isKinematic = true;
        }

        public void Deactivate()
        {
            for (int i = 0; i < _ragdollRbs.Length; i++)
            {
                _ragdollRbs[i].linearDamping = _originalLinearDamping[i];
                _ragdollRbs[i].angularDamping = _originalAngularDamping[i];
                _ragdollRbs[i].isKinematic = true;
            }

            for (int i = 0; i < _ragdollCols.Length; i++)
                _ragdollCols[i].enabled = false;

            _rootBody.isKinematic = false;
            _rootBody.linearVelocity = Vector3.zero;
            _rootBody.angularVelocity = Vector3.zero;

            gameObject.SetActive(false);
        }

        public bool IsSettled(float settleVelocity)
        {
            // 펠비스 수직 속도가 크면 아직 낙하 중.
            if (Mathf.Abs(_ragdollRbs[0].linearVelocity.y) > settleVelocity)
                return false;

            float totalSqrSpeed = 0f;
            for (int i = 0; i < _ragdollRbs.Length; i++)
                totalSqrSpeed += _ragdollRbs[i].linearVelocity.sqrMagnitude;

            return totalSqrSpeed / _ragdollRbs.Length < settleVelocity * settleVelocity;
        }
    }
}
