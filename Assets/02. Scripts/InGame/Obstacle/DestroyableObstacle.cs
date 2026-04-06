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
        private Transform _initialParent;
        private Vector3 _initialLocalPosition;
        private Quaternion _initialLocalRotation;
        private bool _isDestroyed;
        private bool _isHidden;
        private Renderer[] _renderers;
        private Collider[] _colliders;

        public bool IsDestroyed => _isDestroyed;
        public bool IsHidden => _isHidden;

        public event Action OnDestroyed;
        public event Action OnHidden;
        public event Action OnRespawned;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _initialParent = transform.parent;
            _initialLocalPosition = transform.localPosition;
            _initialLocalRotation = transform.localRotation;
            _renderers = GetComponentsInChildren<Renderer>(true);
            _colliders = GetComponentsInChildren<Collider>(true);
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

            if (isMaster && _rb != null)
            {
                _rb.isKinematic = false;
                _rb.AddForce(force * _destroyForceMultiplier, ForceMode.Impulse);
                _rb.AddTorque(Random.insideUnitSphere * _torqueStrength, ForceMode.Impulse);
            }
        }

        public void Hide()
        {
            if (!_isDestroyed || _isHidden) return;
            _isHidden = true;

            if (_rb != null)
            {
                _rb.isKinematic = true;
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }

            SetVisible(false);
            OnHidden?.Invoke();
        }

        public void Respawn()
        {
            // 원래 부모 아래로 복귀 후 로컬 좌표 복원.
            if (_initialParent != null)
                transform.SetParent(_initialParent, true);

            transform.SetLocalPositionAndRotation(_initialLocalPosition, _initialLocalRotation);

            if (_rb != null)
            {
                _rb.isKinematic = true;
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }

            foreach (var script in _movementScripts)
            {
                if (script != null)
                    script.enabled = true;
            }

            SetVisible(true);
            _isHidden = false;
            _isDestroyed = false;
            OnRespawned?.Invoke();
        }

        private void SetVisible(bool visible)
        {
            foreach (var rend in _renderers)
            {
                if (rend != null) rend.enabled = visible;
            }

            foreach (var col in _colliders)
            {
                if (col != null) col.enabled = visible;
            }
        }

        public void ApplyNetworkState(Vector3 position, Quaternion rotation)
        {
            if (!_isDestroyed) return;

            transform.position = Vector3.Lerp(transform.position, position, CorrectionFactor);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, CorrectionFactor);
        }
    }
}
