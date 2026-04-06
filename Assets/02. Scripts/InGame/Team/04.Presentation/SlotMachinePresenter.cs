using System.Collections;
using DG.Tweening;
using InGame.Camera.PlayerCamera;
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

        [Header("Viewport Positioning")]
        [Tooltip("슬롯머신이 머무르는 뷰포트 좌표 (0~1)")]
        [SerializeField] private Vector2 _targetViewportPos = new(0.8f, 0.65f);
        [Tooltip("카메라로부터의 거리")]
        [SerializeField] private float _displayDepth = 5f;

        [Header("Entrance (오른쪽에서 등장)")]
        [Tooltip("등장 시작 뷰포트 X (1.0 이상 = 화면 바깥 오른쪽)")]
        [SerializeField] private float _entryOffscreenX = 1.4f;
        [SerializeField] private float _appearDuration = 0.4f;

        [Header("Exit (위쪽으로 퇴장)")]
        [Tooltip("퇴장 목표 뷰포트 Y (1.0 이상 = 화면 바깥 위쪽)")]
        [SerializeField] private float _exitOffscreenY = 1.5f;
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
        [SerializeField] private float _tiltAmplitude = 3f;
        [SerializeField] private float _tiltFrequency = 0.8f;

        private FollowCameraController _cameraController;
        private TeamModeSynchronizer _synchronizer;
        private MergedBodyController _formController;

        private SlotMachine _activeSlotMachine;
        private Tween _activeTween;
        private Tween _scaleTween;
        private Vector3 _prefabBaseScale;

        private Vector2 _currentViewportPos;

        // 2단계 스핀: 결과가 슬라이드인보다 먼저 도착할 경우 큐잉.
        private int[] _pendingResult;
        private bool _spinStarted;
        private bool _pendingMatch;

        private UnityEngine.Camera GetCamera() => _cameraController != null ? _cameraController.OutputCamera : null;

        private void Start()
        {
            _cameraController = GetComponent<FollowCameraController>();
            _synchronizer = GetComponent<TeamModeSynchronizer>();
            _formController = GetComponent<MergedBodyController>();

            _synchronizer.OnSlotSpinStarted += HandleSpinStart;
            _synchronizer.OnSlotResultReceived += HandleSpinResult;
            _synchronizer.OnSlotFinishReceived += HandleSlotFinish;
        }

        private void OnDestroy()
        {
            _activeTween?.Kill();
            _scaleTween?.Kill();

            if (_synchronizer != null)
            {
                _synchronizer.OnSlotSpinStarted -= HandleSpinStart;
                _synchronizer.OnSlotResultReceived -= HandleSpinResult;
                _synchronizer.OnSlotFinishReceived -= HandleSlotFinish;
            }
        }

        private void LateUpdate()
        {
            if (_activeSlotMachine == null) return;

            var cam = GetCamera();
            if (cam == null) return;

            // 뷰포트 좌표 → 월드 좌표 변환.
            Vector3 worldPos = cam.ViewportToWorldPoint(
                new Vector3(_currentViewportPos.x, _currentViewportPos.y, _displayDepth));

            // 상하 부유 (bobbing) — 카메라 up 방향으로 적용.
            float bob = Mathf.Sin(Time.time * _bobFrequency * Mathf.PI * 2f) * _bobAmplitude;
            worldPos += cam.transform.up * bob;

            _activeSlotMachine.transform.position = worldPos;

            // 카메라를 향해 +Y 면(정면)이 보이도록 풀 빌보드 회전.
            Vector3 dirToCamera = (cam.transform.position - worldPos).normalized;
            Quaternion lookRot = Quaternion.LookRotation(dirToCamera, Vector3.up);
            Quaternion yFront = Quaternion.Euler(90f, 0f, 0f); // +Y → +Z(카메라 방향)

            // 미세 틸트 흔들림.
            float tiltZ = Mathf.Sin(Time.time * _tiltFrequency * Mathf.PI * 2f) * _tiltAmplitude;
            float tiltX = Mathf.Cos(Time.time * _tiltFrequency * 0.7f * Mathf.PI * 2f) * _tiltAmplitude * 0.5f;
            Quaternion wobble = Quaternion.Euler(tiltX, 0f, tiltZ);

            _activeSlotMachine.transform.rotation = lookRot * yFront * wobble;
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
            _pendingMatch = false;

            var cam = GetCamera();
            if (cam == null) return;

            // 화면 바깥 오른쪽에서 시작.
            _currentViewportPos = new Vector2(_entryOffscreenX, _targetViewportPos.y);
            Vector3 spawnPos = cam.ViewportToWorldPoint(
                new Vector3(_currentViewportPos.x, _currentViewportPos.y, _displayDepth));

            _activeSlotMachine = Instantiate(_slotMachinePrefab, spawnPos, _slotMachinePrefab.transform.rotation);
            _prefabBaseScale = _slotMachinePrefab.transform.localScale;

            // 등장: scale 0 → 원본 스케일 바운스.
            _activeSlotMachine.transform.localScale = Vector3.zero;
            _scaleTween = _activeSlotMachine.transform.DOScale(_prefabBaseScale, _appearScaleDuration)
                .SetEase(_appearScaleEase);

            // 오른쪽에서 슬라이드인 (뷰포트 좌표 트윈) → 완료 후 스핀 시작.
            _activeTween = DOTween.To(
                    () => _currentViewportPos,
                    v => _currentViewportPos = v,
                    _targetViewportPos,
                    _appearDuration)
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

            _pendingMatch = isMatch;

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

            // 릴 정지 후 매치 결과에 따라 무적 모드 전환.
            if (_pendingMatch && _synchronizer.photonView.IsMine)
            {
                _synchronizer.BroadcastInvincibleMode(true);
                _pendingMatch = false;
            }

            // Owner만 착지 감지 → RPC로 종료 브로드캐스트.
            if (_synchronizer.photonView.IsMine)
                StartCoroutine(WaitForLandingAndBroadcastFinish());
        }

        private IEnumerator WaitForLandingAndBroadcastFinish()
        {
            yield return new WaitUntil(IsAnyActiveBodyGrounded);
            yield return new WaitForSeconds(_postLandingDelay);

            _synchronizer.BroadcastSlotFinish();
        }

        private void HandleSlotFinish()
        {
            StopAllCoroutines();
            StartCoroutine(SlideOutAndDestroy());
        }

        private IEnumerator SlideOutAndDestroy()
        {
            if (_activeSlotMachine != null)
            {
                _scaleTween?.Kill();

                // 위쪽으로 슬라이드아웃 (뷰포트 Y 트윈).
                Vector2 exitPos = new(_currentViewportPos.x, _exitOffscreenY);
                _activeTween = DOTween.To(
                        () => _currentViewportPos,
                        v => _currentViewportPos = v,
                        exitPos,
                        _disappearDuration)
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
