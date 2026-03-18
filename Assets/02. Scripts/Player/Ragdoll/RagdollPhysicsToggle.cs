using Player.Domain;
using System.Collections.Generic;
using UnityEngine;

namespace Player.Ragdoll
{
    [RequireComponent(typeof(Rigidbody))]
    public class RagdollPhysicsToggle : MonoBehaviour
    {
        [SerializeField] private Transform _hipsRoot;

        private Rigidbody _capsuleRb;
        private Rigidbody[] _ragdollRbs;
        private Collider[] _ragdollCols;
        private Bone[] _boneRbPairs;

        public IReadOnlyList<Rigidbody> RagdollRigidbodies => _ragdollRbs;
        public IReadOnlyList<Bone> Bones => _boneRbPairs;
        public Rigidbody CapsuleRigidbody => _capsuleRb;
        public Transform HipsRoot => _hipsRoot;

        private void Awake()
        {
            _capsuleRb = GetComponent<Rigidbody>();

            _ragdollRbs = _hipsRoot.GetComponentsInChildren<Rigidbody>();
            _ragdollCols = _hipsRoot.GetComponentsInChildren<Collider>();

            _boneRbPairs = new Bone[_ragdollRbs.Length];
            for (int i = 0; i < _ragdollRbs.Length; i++)
                _boneRbPairs[i] = new Bone(_ragdollRbs[i]);

            Deactivate();
            _capsuleRb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        public void Activate()
        {
            foreach (var rb in _ragdollRbs)
                rb.isKinematic = false;

            foreach (var col in _ragdollCols)
                col.enabled = true;

            _capsuleRb.isKinematic = true;
        }

        public void Deactivate()
        {
            DeactivateRagdoll();
            ActivateCapsule();
        }

        public void DeactivateRagdoll()
        {
            foreach (var rb in _ragdollRbs)
                rb.isKinematic = true;

            foreach (var col in _ragdollCols)
                col.enabled = false;
        }

        public void ActivateCapsule()
        {
            _capsuleRb.isKinematic = false;
            _capsuleRb.linearVelocity = Vector3.zero;
            _capsuleRb.angularVelocity = Vector3.zero;
        }
    }
}
