using System.Collections.Generic;
using UnityEngine;

namespace InGame.Player.Ragdoll
{
    public class RagdollRig : MonoBehaviour, IRagdollRig
    {
        [SerializeField] private Transform _pelvis;
        [SerializeField] private Rigidbody _rootBody;

        private Rigidbody[] _ragdollRbs;
        private Collider[] _ragdollCols;

        public IReadOnlyList<Rigidbody> Rigidbodies => _ragdollRbs;
        public Transform PelvisTransform => _pelvis;
        public Rigidbody RootBody => _rootBody;

        private void Awake()
        {
            _ragdollRbs = GetComponentsInChildren<Rigidbody>(true);
            _ragdollCols = GetComponentsInChildren<Collider>(true);
            gameObject.SetActive(false);
        }

        public void Activate(Vector3 inheritedVelocity)
        {
            gameObject.SetActive(true);

            for (int i = 0; i < _ragdollRbs.Length; i++)
            {
                _ragdollRbs[i].isKinematic = false;
                _ragdollRbs[i].linearVelocity = inheritedVelocity;
            }

            for (int i = 0; i < _ragdollCols.Length; i++)
                _ragdollCols[i].enabled = true;

            _rootBody.isKinematic = true;
        }

        public void Deactivate()
        {
            for (int i = 0; i < _ragdollRbs.Length; i++)
                _ragdollRbs[i].isKinematic = true;

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
