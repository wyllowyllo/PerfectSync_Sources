using System.Linq;
using UnityEngine;

namespace Player.Ragdoll
{
    [RequireComponent(typeof(Animator), typeof(Rigidbody))]
    public class RagdollPhysicsToggle : MonoBehaviour
    {
        [SerializeField] private Transform _hipsRoot;

        private Animator _animator;
        private CapsuleCollider _capsuleCollider;
        private Rigidbody _capsuleRb;
        private UpperBodyPhysics _upperBodyPhysics;
        private Rigidbody[] _ragdollRbs;
        private Collider[] _ragdollCols;
        private Transform[] _ragdollBones;

        public Rigidbody[] RagdollRigidbodies => _ragdollRbs;
        public Transform[] RagdollBones => _ragdollBones;
        public Rigidbody CapsuleRigidbody => _capsuleRb;
        public Animator Animator => _animator;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _capsuleCollider = GetComponent<CapsuleCollider>();
            _capsuleRb = GetComponent<Rigidbody>();
            _upperBodyPhysics = GetComponent<UpperBodyPhysics>();

            _ragdollRbs = _hipsRoot.GetComponentsInChildren<Rigidbody>();
            _ragdollCols = _hipsRoot.GetComponentsInChildren<Collider>();
            _ragdollBones = _ragdollRbs.Select(rb => rb.transform).ToArray();

            Deactivate();
            _capsuleRb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        public void Activate()
        {
            foreach (var rb in _ragdollRbs)
                rb.isKinematic = false;

            foreach (var col in _ragdollCols)
                col.enabled = true;

            _capsuleCollider.enabled = false;
            _capsuleRb.isKinematic = true;
            _animator.enabled = false;

            if (_upperBodyPhysics != null)
                _upperBodyPhysics.SetActive(false);
        }

        public void Deactivate()
        {
            foreach (var rb in _ragdollRbs)
                rb.isKinematic = true;

            foreach (var col in _ragdollCols)
                col.enabled = false;

            _capsuleCollider.enabled = true;
            _capsuleRb.isKinematic = false;
            _animator.enabled = true;

            if (_upperBodyPhysics != null)
                _upperBodyPhysics.SetActive(true);
        }
    }
}
