using System.Collections;
using DG.Tweening;
using InGame.Player;
using InGame.Player.Movement;
using InGame.Player.Network;
using UnityEngine;

namespace InGame.Team
{
    public class SlotMachinePresenter : MonoBehaviour
    {
        [Header("Prefab")]
        [SerializeField] private SlotMachine _slotMachinePrefab;

        [Header("Display")]
        [SerializeField] private Vector3 _displayOffset = new(0f, 2.0f, 0f);

        [Header("Animation")]
        [SerializeField] private float _slideDistance = 3f;
        [SerializeField] private float _appearDuration = 0.4f;
        [SerializeField] private float _disappearDuration = 0.35f;

        [Header("Timing")]
        [SerializeField] private float _postLandingDelay = 1.5f;

        [Header("Scale Effects")]
        [SerializeField] private float _appearScaleDuration = 0.35f;
        [SerializeField] private Ease _appearScaleEase = Ease.OutBack;
        [Tooltip("원본 스케일 대비 비율 (0.3 = 30% 펀치)")]
        [SerializeField] private float _spinCompletePunchRatio = 0.3f;
        [SerializeField] private float _spinCompletePunchDuration = 0.3f;
        [SerializeField] private float _disappearScaleDuration = 0.25f;

        [Header("Hovering")]
        [SerializeField] private float _bobAmplitude = 0.15f;
        [SerializeField] private float _bobFrequency = 1.5f;
        [SerializeField] private float _followSmoothTime = 0.18f;
        [SerializeField] private float _verticalSmoothTime = 0.04f;
        [SerializeField] private float _tiltAmplitude = 3f;
        [SerializeField] private float _tiltFrequency = 0.8f;

        private TeamModeSynchronizer _synchronizer;
        private MergedBodyController _formController;

        private SlotMachine _activeSlotMachine;
        private Tween _activeTween;
        private Tween _scaleTween;
        private Quaternion _prefabBaseRotation;
        private Vector3 _prefabBaseScale;
        private float _slideOffset;

        private float _smoothVelX;
        private float _smoothVelY;
        private float _smoothVelZ;
        private Vector3 _currentPos;
        private bool _posInitialized;

        // 2단계 스핀: 결과가 슬라이드인보다 먼저 도착할 경우 큐잉.
        private int[] _pendingResult;
        private bool _spinStarted;

        private void Start()
        {
            _synchronizer = GetComponent<TeamModeSynchronizer>();
            _formController = GetComponent<MergedBodyController>();

            _synchronizer.OnSlotSpinStarted += HandleSpinStart;
            _synchronizer.OnSlotResultReceived += HandleSpinResult;
        }

        private void OnDestroy()
        {
            _activeTween?.Kill();
            _scaleTween?.Kill();

            if (_synchronizer != null)
            {
                _synchronizer.OnSlotSpinStarted -= HandleSpinStart;
                _synchronizer.OnSlotResultReceived -= HandleSpinResult;
            }
        }

        private void LateUpdate()
        {
            if (_activeSlotMachine == null) return;

            Transform body = _formController.PrimaryBodyTransform;
            var cam = UnityEngine.Camera.main;
            if (cam == null) return;

            // 앵커 위치 (슬라이드 오프셋 포함).
            Vector3 camRight = cam.transform.right;
            Vector3 anchorPos = body.position + _displayOffset + camRight * _slideOffset;

            // 상하 부유 (bobbing).
            float bob = Mathf.Sin(Time.time * _bobFrequency * Mathf.PI * 2f) * _bobAmplitude;
            anchorPos.y += bob;

            // 축별 SmoothDamp — 수평은 느긋하게, 수직은 빠르게 추적.
            if (!_posInitialized)
            {
                _currentPos = anchorPos;
                _smoothVelX = _smoothVelY = _smoothVelZ = 0f;
                _posInitialized = true;
            }

            _currentPos.x = Mathf.SmoothDamp(_currentPos.x, anchorPos.x, ref _smoothVelX, _followSmoothTime);
            _currentPos.y = Mathf.SmoothDamp(_currentPos.y, anchorPos.y, ref _smoothVelY, _verticalSmoothTime);
            _currentPos.z = Mathf.SmoothDamp(_currentPos.z, anchorPos.z, ref _smoothVelZ, _followSmoothTime);
            _activeSlotMachine.transform.position = _currentPos;

            // 수평 회전(Yaw) 없이, 수직 피치만 카메라를 향해 앞면을 보이도록 조정.
            Vector3 toCamera = cam.transform.position - _currentPos;
            float horizontalDist = Mathf.Sqrt(toCamera.x * toCamera.x + toCamera.z * toCamera.z);
            float pitchAngle = Mathf.Atan2(toCamera.y, horizontalDist) * Mathf.Rad2Deg;
            Quaternion pitch = Quaternion.Euler(-pitchAngle, 0f, 0f);

            // 미세 틸트 흔들림.
            float tiltZ = Mathf.Sin(Time.time * _tiltFrequency * Mathf.PI * 2f) * _tiltAmplitude;
            float tiltX = Mathf.Cos(Time.time * _tiltFrequency * 0.7f * Mathf.PI * 2f) * _tiltAmplitude * 0.5f;
            Quaternion wobble = Quaternion.Euler(tiltX, 0f, tiltZ);

            _activeSlotMachine.transform.rotation = _prefabBaseRotation * pitch * wobble;
        }

        // ── 2단계 스핀 핸들러 ───────────────────────────────────

        private void HandleSpinStart()
        {
            _activeTween?.Kill();
            _scaleTween?.Kill();
            if (_activeSlotMachine != null)
            {
                Destroy(_activeSlotMachine.gameObject);
                StopAllCoroutines();
            }

            _pendingResult = null;
            _spinStarted = false;
            _slideOffset = _slideDistance;
            _posInitialized = false;

            Transform body = _formController.PrimaryBodyTransform;
            var cam = UnityEngine.Camera.main;
            Vector3 camRight = cam != null ? cam.transform.right : Vector3.right;
            Vector3 spawnPos = body.position + _displayOffset + camRight * _slideDistance;

            _activeSlotMachine = Instantiate(_slotMachinePrefab, spawnPos, _slotMachinePrefab.transform.rotation);
            _prefabBaseRotation = _slotMachinePrefab.transform.rotation;
            _prefabBaseScale = _slotMachinePrefab.transform.localScale;

            // 등장: scale 0 → 원본 스케일 바운스.
            _activeSlotMachine.transform.localScale = Vector3.zero;
            _scaleTween = _activeSlotMachine.transform.DOScale(_prefabBaseScale, _appearScaleDuration)
                .SetEase(_appearScaleEase);

            // 오른쪽에서 슬라이드인 → 완료 후 스핀 시작.
            _activeTween = DOTween.To(() => _slideOffset, v => _slideOffset = v, 0f, _appearDuration)
                .SetEase(Ease.OutCubic)
                .OnComplete(() =>
                {
                    _activeSlotMachine.StartSpin();
                    _spinStarted = true;

                    // 결과가 먼저 도착해서 큐잉된 경우 즉시 정지.
                    if (_pendingResult != null)
                    {
                        _activeSlotMachine.StopOnSymbols(_pendingResult);
                        _activeSlotMachine.OnSpinComplete += OnSpinComplete;
                        _pendingResult = null;
                    }
                });
        }

        private void HandleSpinResult(int[] symbols, bool isMatch)
        {
            if (_activeSlotMachine == null) return;

            if (!_spinStarted)
            {
                // 슬라이드인 완료 전에 결과 도착 → 큐잉.
                _pendingResult = symbols;
                return;
            }

            _activeSlotMachine.StopOnSymbols(symbols);
            _activeSlotMachine.OnSpinComplete += OnSpinComplete;
        }

        // ── 스핀 완료 처리 ──────────────────────────────────────

        private void OnSpinComplete(int[] results)
        {
            if (_activeSlotMachine != null)
            {
                _activeSlotMachine.OnSpinComplete -= OnSpinComplete;

                // 릴 정지 "쿵" 스케일 펀치.
                _scaleTween?.Kill();
                _activeSlotMachine.transform.localScale = _prefabBaseScale;
                _scaleTween = _activeSlotMachine.transform
                    .DOPunchScale(_prefabBaseScale * _spinCompletePunchRatio, _spinCompletePunchDuration, 1, 0.5f);
            }

            StartCoroutine(WaitForLandingAndFinish());
        }

        private IEnumerator WaitForLandingAndFinish()
        {
            yield return new WaitUntil(IsAnyActiveBodyGrounded);
            yield return new WaitForSeconds(_postLandingDelay);

            // 슬라이드아웃 + 스케일 축소 동시 진행.
            if (_activeSlotMachine != null)
            {
                _scaleTween?.Kill();

                _activeTween = DOTween.To(() => _slideOffset, v => _slideOffset = v, _slideDistance, _disappearDuration)
                    .SetEase(Ease.InCubic);
                _scaleTween = _activeSlotMachine.transform
                    .DOScale(Vector3.zero, _disappearScaleDuration)
                    .SetEase(Ease.InBack);

                yield return _activeTween.WaitForCompletion();

                Destroy(_activeSlotMachine.gameObject);
                _activeSlotMachine = null;
            }
        }

        private bool IsAnyActiveBodyGrounded()
        {
            var movements = GetComponentsInChildren<PlayerMovement>();
            foreach (var movement in movements)
            {
                if (movement.gameObject.activeInHierarchy && movement.Grounded)
                    return true;
            }

            return false;
        }
    }
}
