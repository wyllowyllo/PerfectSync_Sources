using System;
using UnityEngine;
using Random = UnityEngine.Random;

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
        private Transform _initialParent;
        private Vector3 _initialLocalPosition;
        private Quaternion _initialLocalRotation;
        private bool _isDestroyed;

        public bool IsDestroyed => _isDestroyed;

        public event Action OnDestroyed;
        public event Action OnRespawned;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _initialParent = transform.parent;
            _initialLocalPosition = transform.localPosition;
            _initialLocalRotation = transform.localRotation;

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
                {
                    script.StopAllCoroutines();
                    script.enabled = false;
                }
            }

            OnDestroyed?.Invoke();

            // 부모가 있으면 분리 (부모 MovingObstacle 등의 이동이 물리에 간섭하지 않도록).
            if (_initialParent != null)
                transform.SetParent(null, true);

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

            // 원래 부모 아래로 복귀 후 로컬 좌표 복원.
            if (_initialParent != null)
                transform.SetParent(_initialParent, true);

            transform.SetLocalPositionAndRotation(_initialLocalPosition, _initialLocalRotation);

            foreach (var script in _movementScripts)
            {
                if (script != null)
                    script.enabled = true;
            }

            _isDestroyed = false;
            OnRespawned?.Invoke();
        }

        public void ApplyNetworkState(Vector3 position, Quaternion rotation)
        {
            if (!_isDestroyed) return;

            transform.position = Vector3.Lerp(transform.position, position, CorrectionFactor);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, CorrectionFactor);
        }
    }
}
