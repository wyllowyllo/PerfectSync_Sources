using System;
using System.Collections;
using DG.Tweening;
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

        [Header("Hide Animation")]
        [SerializeField] private float _hideAnimDuration = 0.3f;
        [SerializeField] private float _hideFloatHeight = 0.5f;

        [Header("Respawn Animation")]
        [SerializeField] private float _warningBlinkInterval = 0.1f;
        [SerializeField] private float _respawnScaleDuration = 0.3f;

        private Rigidbody _rb;
        private Transform _initialParent;
        private Vector3 _initialLocalPosition;
        private Quaternion _initialLocalRotation;
        private Vector3 _originalScale;
        private bool _isDestroyed;
        private bool _isHidden;
        private Renderer[] _renderers;
        private Collider[] _colliders;

        private Tween _hideTween;
        private Tween _respawnTween;
        private Coroutine _blinkCoroutine;

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
            _originalScale = transform.localScale;
            _renderers = GetComponentsInChildren<Renderer>(true);
            _colliders = GetComponentsInChildren<Collider>(true);
        }

        public void Destroy(Vector3 force, Vector3 randomTorque)
        {
            if (_isDestroyed) return;
            _isDestroyed = true;

            // 진행 중인 리스폰 연출 정리.
            CleanupTweensAndCoroutines();

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

            if (_rb != null)
            {
                _rb.isKinematic = false;
                _rb.AddForce(force * _destroyForceMultiplier, ForceMode.Impulse);
                _rb.AddTorque(randomTorque * _torqueStrength, ForceMode.Impulse);
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

            _hideTween?.Kill();
            _hideTween = DOTween.Sequence()
                .Append(transform.DOScale(Vector3.zero, _hideAnimDuration).SetEase(Ease.InBack))
                .Join(transform.DOMoveY(transform.position.y + _hideFloatHeight, _hideAnimDuration)
                    .SetEase(Ease.OutQuad))
                .OnComplete(() =>
                {
                    SetVisible(false);
                    transform.localScale = _originalScale;
                });

            OnHidden?.Invoke();
        }

        public void PrepareRespawn()
        {
            if (!_isHidden) return;

            _hideTween?.Kill();

            // 이전 cycle 의 blink 좀비 코루틴이 남아 있을 수 있으므로 먼저 정리.
            StopBlink();

            // 원래 위치 복귀.
            if (_initialParent != null)
                transform.SetParent(_initialParent, true);
            transform.SetLocalPositionAndRotation(_initialLocalPosition, _initialLocalRotation);
            transform.localScale = _originalScale;

            // 렌더러만 ON (깜빡임), 콜라이더는 OFF 유지 → 통과 가능.
            _blinkCoroutine = StartCoroutine(BlinkRoutine());
        }

        public void Respawn()
        {
            CleanupTweensAndCoroutines();

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

            // 스케일 0 → 원래 크기 (OutBack 바운스).
            transform.localScale = Vector3.zero;
            SetVisible(true);

            _respawnTween = transform
                .DOScale(_originalScale, _respawnScaleDuration)
                .SetEase(Ease.OutBack);

            _isHidden = false;
            _isDestroyed = false;
            OnRespawned?.Invoke();
        }

        // ── 내부 유틸 ──────────────────────────────────────────

        private IEnumerator BlinkRoutine()
        {
            var wait = new WaitForSeconds(_warningBlinkInterval);
            while (true)
            {
                SetRenderersEnabled(true);
                yield return wait;
                SetRenderersEnabled(false);
                yield return wait;
            }
        }

        private void StopBlink()
        {
            if (_blinkCoroutine == null) return;
            StopCoroutine(_blinkCoroutine);
            _blinkCoroutine = null;
            // 중단 시 renderer 를 항상 known state(ON) 로 복원.
            SetRenderersEnabled(true);
        }

        private void SetVisible(bool visible)
        {
            SetRenderersEnabled(visible);
            SetCollidersEnabled(visible);
        }

        private void SetRenderersEnabled(bool enabled)
        {
            foreach (var rend in _renderers)
            {
                if (rend != null) rend.enabled = enabled;
            }
        }

        private void SetCollidersEnabled(bool enabled)
        {
            foreach (var col in _colliders)
            {
                if (col != null) col.enabled = enabled;
            }
        }

        private void CleanupTweensAndCoroutines()
        {
            _hideTween?.Kill();
            _respawnTween?.Kill();
            StopBlink();
        }

        private void OnDestroy()
        {
            _hideTween?.Kill();
            _respawnTween?.Kill();
        }
    }
}
