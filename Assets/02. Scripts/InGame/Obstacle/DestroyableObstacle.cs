using UnityEngine;

namespace InGame.Obstacle
{
    public class DestroyableObstacle : MonoBehaviour, IDestroyable
    {
        [Tooltip("Destroy 시 비활성화할 동작 스크립트 (RotationScript 등)")]
        [SerializeField] private MonoBehaviour[] _movementScripts;

        [Tooltip("장애물별 힘 배율 (크기/무게 튜닝)")]
        [SerializeField] private float _destroyForceMultiplier = 1f;

        [Tooltip("파괴 시 랜덤 토크 크기 (텀블링 연출)")]
        [SerializeField] private float _torqueStrength = 5f;

        private const float CorrectionFactor = 0.2f;

        private Rigidbody _rb;
        private MeshCollider[] _concaveMeshColliders;
        private Vector3 _initialPosition;
        private Quaternion _initialRotation;
        private bool _isDestroyed;

        public bool IsDestroyed => _isDestroyed;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _initialPosition = transform.position;
            _initialRotation = transform.rotation;

            // 원래 concave인 MeshCollider만 캐싱 (원래 convex인 것은 건드리지 않음).
            var allMc = GetComponentsInChildren<MeshCollider>();
            var concaveList = new System.Collections.Generic.List<MeshCollider>();
            foreach (var mc in allMc)
            {
                if (!mc.convex)
                    concaveList.Add(mc);
            }
            _concaveMeshColliders = concaveList.ToArray();
        }

        public void Destroy(Vector3 force, bool isMaster)
        {
            if (_isDestroyed) return;
            _isDestroyed = true;

            foreach (var script in _movementScripts)
            {
                if (script != null)
                    script.enabled = false;
            }

            // Concave MeshCollider는 dynamic Rigidbody와 호환되지 않으므로 convex로 전환.
            foreach (var mc in _concaveMeshColliders)
            {
                if (mc != null)
                    mc.convex = true;
            }

            if (isMaster && _rb != null)
            {
                _rb.isKinematic = false;
                _rb.AddForce(force * _destroyForceMultiplier, ForceMode.Impulse);
                _rb.AddTorque(Random.insideUnitSphere * _torqueStrength, ForceMode.Impulse);
            }
        }

        public void Respawn()
        {
            if (_rb != null)
            {
                _rb.isKinematic = true;
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }

            // Concave MeshCollider 복원.
            foreach (var mc in _concaveMeshColliders)
            {
                if (mc != null)
                    mc.convex = false;
            }

            transform.SetPositionAndRotation(_initialPosition, _initialRotation);

            foreach (var script in _movementScripts)
            {
                if (script != null)
                    script.enabled = true;
            }

            _isDestroyed = false;
        }

        public void ApplyNetworkState(Vector3 position, Quaternion rotation)
        {
            if (!_isDestroyed) return;

            transform.position = Vector3.Lerp(transform.position, position, CorrectionFactor);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, CorrectionFactor);
        }
    }
}
