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

        [Header("Entrance (오른쪽 위에서 호를 그리며 등장)")]
        [Tooltip("등장 시작 뷰포트 X (1.0 이상 = 화면 바깥 오른쪽)")]
        [SerializeField] private float _entryOffscreenX = 1.4f;
        [Tooltip("타겟 Y보다 얼마나 위에서 시작할지 (호 궤적)")]
        [SerializeField] private float _entryOffsetY = 0.1f;
        [SerializeField] private float _appearDuration = 0.5f;

        [Header("Exit (위쪽으로 퇴장)")]
        [Tooltip("퇴장 목표 뷰포트 Y (1.0 이상 = 화면 바깥 위쪽)")]
        [SerializeField] private float _exitOffscreenY = 1.5f;
        [Tooltip("퇴장 전 살짝 아래로 찍는 anticipation 크기")]
        [SerializeField] private float _exitAnticipationDip = 0.03f;
        [SerializeField] private float _exitAnticipationDuration = 0.1f;
        [SerializeField] private float _disappearDuration = 0.25f;

        [Header("Timing")]
        [SerializeField] private float _postLandingDelay = 1.5f;

        [Header("Scale Effects")]
        [SerializeField] private float _appearScaleDuration = 0.35f;
        [SerializeField] private Ease _appearScaleEase = Ease.OutBack;
        [Tooltip("원본 스케일 대비 비율 (0.3 = 30% 펀치)")]
        [SerializeField] private float _spinCompletePunchRatio = 0.3f;
        [SerializeField] private float _spinCompletePunchDuration = 0.3f;
        [SerializeField] private float _disappearScaleDuration = 0.25f;

        [Header("Result Reaction")]
        [Tooltip("잭팟: 뷰포트 Y 바운스 크기")]
        [SerializeField] private float _jackpotBounceY = 0.06f;
        [SerializeField] private float _jackpotBounceDuration = 0.4f;
        [Tooltip("잭팟: 추가 스케일 펀치 비율")]
        [SerializeField] private float _jackpotScalePunchRatio = 0.5f;
        [Tooltip("잭팟: 좌우 흔들림 각도 (덩실덩실)")]
        [SerializeField] private float _jackpotDanceTilt = 15f;
        [Tooltip("잭팟: 좌우 뷰포트 X 스웨이 크기")]
        [SerializeField] private float _jackpotSwayX = 0.02f;
        [SerializeField] private int _jackpotDanceSwings = 3;
        [SerializeField] private float _jackpotDanceSpeed = 0.15f;
        [Tooltip("꽝: 뷰포트 Y 처짐 크기")]
        [SerializeField] private float _missDroopY = 0.03f;
        [SerializeField] private float _missDroopDuration = 0.5f;
        [Tooltip("꽝: 앞으로 숙이는 각도 (시무룩)")]
        [SerializeField] private float _missDroopTilt = 20f;

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
        private Tween _reactionTween;
        private Vector3 _prefabBaseScale;

        private Vector2 _currentViewportPos;
        private float _reactionTiltX;
        private float _reactionTiltZ;

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
            _reactionTween?.Kill();

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

            // 미세 틸트 흔들림 + 결과 반응 틸트.
            float tiltZ = Mathf.Sin(Time.time * _tiltFrequency * Mathf.PI * 2f) * _tiltAmplitude + _reactionTiltZ;
            float tiltX = Mathf.Cos(Time.time * _tiltFrequency * 0.7f * Mathf.PI * 2f) * _tiltAmplitude * 0.5f + _reactionTiltX;
            Quaternion wobble = Quaternion.Euler(tiltX, 0f, tiltZ);

            _activeSlotMachine.transform.rotation = lookRot * yFront * wobble;
        }

        // ── 2단계 스핀 핸들러 ───────────────────────────────────

        private void HandleSpinStart()
        {
            _activeTween?.Kill();
            _scaleTween?.Kill();
            _reactionTween?.Kill();
            if (_activeSlotMachine != null)
            {
                Destroy(_activeSlotMachine.gameObject);
                StopAllCoroutines();
            }

            _pendingResult = null;
            _spinStarted = false;
            _pendingMatch = false;
            _reactionTiltX = 0f;
            _reactionTiltZ = 0f;

            var cam = GetCamera();
            if (cam == null) return;

            // 화면 바깥 오른쪽 + 살짝 위에서 시작 (호 궤적).
            _currentViewportPos = new Vector2(_entryOffscreenX, _targetViewportPos.y + _entryOffsetY);
            Vector3 spawnPos = cam.ViewportToWorldPoint(
                new Vector3(_currentViewportPos.x, _currentViewportPos.y, _displayDepth));

            _activeSlotMachine = Instantiate(_slotMachinePrefab, spawnPos, _slotMachinePrefab.transform.rotation);
            _prefabBaseScale = _slotMachinePrefab.transform.localScale;

            // 등장: scale 0 → 원본 스케일 바운스.
            _activeSlotMachine.transform.localScale = Vector3.zero;
            _scaleTween = _activeSlotMachine.transform.DOScale(_prefabBaseScale, _appearScaleDuration)
                .SetEase(_appearScaleEase);

            // 오른쪽 위에서 호를 그리며 슬라이드인 (OutBack = 오버슈트 바운스).
            _activeTween = DOTween.To(
                    () => _currentViewportPos,
                    v => _currentViewportPos = v,
                    _targetViewportPos,
                    _appearDuration)
                .SetEase(Ease.OutBack)
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

                _scaleTween?.Kill();
                _reactionTween?.Kill();
                _activeSlotMachine.transform.localScale = _prefabBaseScale;

                _activeSlotMachine.PlayResultEffects(_pendingMatch);

                if (_pendingMatch)
                {
                    // ── 잭팟: 스케일 펀치 + 위로 바운스 + 덩실덩실 춤.
                    _scaleTween = _activeSlotMachine.transform
                        .DOPunchScale(_prefabBaseScale * _jackpotScalePunchRatio, _jackpotBounceDuration, 1, 0.3f);

                    float baseY = _currentViewportPos.y;
                    float baseX = _currentViewportPos.x;
                    var seq = DOTween.Sequence();

                    // 위로 바운스.
                    seq.Append(DOTween.To(
                        () => _currentViewportPos.y, y => _currentViewportPos.y = y,
                        baseY + _jackpotBounceY, _jackpotBounceDuration * 0.3f).SetEase(Ease.OutQuad));
                    seq.Append(DOTween.To(
                        () => _currentViewportPos.y, y => _currentViewportPos.y = y,
                        baseY, _jackpotBounceDuration * 0.7f).SetEase(Ease.OutBounce));

                    // 좌우 스웨이 (뷰포트 X + Z틸트 동시 — 덩실덩실).
                    var dance = DOTween.Sequence();
                    for (int i = 0; i < _jackpotDanceSwings; i++)
                    {
                        float dir = (i % 2 == 0) ? 1f : -1f;
                        dance.Append(DOTween.To(
                            () => _reactionTiltZ, z => _reactionTiltZ = z,
                            _jackpotDanceTilt * dir, _jackpotDanceSpeed).SetEase(Ease.InOutSine));
                        dance.Join(DOTween.To(
                            () => _currentViewportPos.x, x => _currentViewportPos.x = x,
                            baseX + _jackpotSwayX * dir, _jackpotDanceSpeed).SetEase(Ease.InOutSine));
                    }
                    // 원위치 복귀.
                    dance.Append(DOTween.To(
                        () => _reactionTiltZ, z => _reactionTiltZ = z,
                        0f, _jackpotDanceSpeed * 1.5f).SetEase(Ease.OutSine));
                    dance.Join(DOTween.To(
                        () => _currentViewportPos.x, x => _currentViewportPos.x = x,
                        baseX, _jackpotDanceSpeed * 1.5f).SetEase(Ease.OutSine));

                    seq.Join(dance);
                    _reactionTween = seq;
                }
                else
                {
                    // ── 꽝: 쪼그라들며 + 처지며 + 고개 숙이기.
                    _scaleTween = _activeSlotMachine.transform
                        .DOScale(_prefabBaseScale * 0.85f, _missDroopDuration)
                        .SetEase(Ease.InOutSine);

                    float baseY = _currentViewportPos.y;
                    var seq = DOTween.Sequence();
                    seq.Append(DOTween.To(
                        () => _currentViewportPos.y, y => _currentViewportPos.y = y,
                        baseY - _missDroopY, _missDroopDuration).SetEase(Ease.InOutSine));
                    seq.Join(DOTween.To(
                        () => _reactionTiltX, x => _reactionTiltX = x,
                        _missDroopTilt, _missDroopDuration).SetEase(Ease.InOutSine));
                    _reactionTween = seq;
                }
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

                // 퇴장 시퀀스: anticipation dip → 빠르게 위로.
                Vector2 dipPos = new(_currentViewportPos.x, _currentViewportPos.y - _exitAnticipationDip);
                Vector2 exitPos = new(_currentViewportPos.x, _exitOffscreenY);

                var seq = DOTween.Sequence();
                // 1) 살짝 아래로 찍기 (anticipation).
                seq.Append(DOTween.To(
                    () => _currentViewportPos, v => _currentViewportPos = v,
                    dipPos, _exitAnticipationDuration).SetEase(Ease.OutQuad));
                // 2) 빠르게 위로 쏘아 올림.
                seq.Append(DOTween.To(
                    () => _currentViewportPos, v => _currentViewportPos = v,
                    exitPos, _disappearDuration).SetEase(Ease.InQuart));

                _activeTween = seq;

                // 스케일 축소는 위로 날아가는 구간에서만.
                _scaleTween = _activeSlotMachine.transform
                    .DOScale(Vector3.zero, _disappearScaleDuration)
                    .SetDelay(_exitAnticipationDuration)
                    .SetEase(Ease.InBack);

                yield return seq.WaitForCompletion();

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
