using System.Linq;
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
        private Transform[] _ragdollBones;

        public Rigidbody[] RagdollRigidbodies => _ragdollRbs;
        public Transform[] RagdollBones => _ragdollBones;
        public Rigidbody CapsuleRigidbody => _capsuleRb;
        public Transform HipsRoot => _hipsRoot;

        private void Awake()
        {
            _capsuleRb = GetComponent<Rigidbody>();

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

            _capsuleRb.isKinematic = true;
        }

        public void Deactivate()
        {
            foreach (var rb in _ragdollRbs)
                rb.isKinematic = true;

            foreach (var col in _ragdollCols)
                col.enabled = false;

            _capsuleRb.isKinematic = false;
        }
    }
}
