using Core;
using DG.Tweening;
using InGame.Player.Ragdoll;
using InGame.UserInput;
using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

namespace InGame.Effect
{
    [DefaultExecutionOrder(ExecutionOrderConstants.SyncInputDisplay)]
    [RequireComponent(typeof(CanvasGroup))]
    public class SyncInputDisplay : MonoBehaviour
    {
        [Header("Profile")]
        [SerializeField] private SyncInputDisplayProfile _profile;

        [Header("External References (에디터에서 직접 할당)")]
        [Tooltip("TeamCharacter 루트의 InputRouter")]
        [SerializeField] private InputRouter _inputRouter;
        [Tooltip("TeamCharacter 루트의 PhotonView")]
        [SerializeField] private PhotonView _photonView;
        [Tooltip("PlayerBody/ControllerLogic의 RagdollStateMachine")]
        [SerializeField] private RagdollStateMachine _ragdollStateMachine;
        [Tooltip("yaw 상쇄용 — PlayerBody/RootBody의 Transform")]
        [SerializeField] private Transform _rootBodyTransform;

        [Header("Rings (정적 링 본체)")]
        [SerializeField] private Image _innerRingImage;
        [SerializeField] private Image _outerRingImage;

        [Header("Arrow Heads (호 위를 이동)")]
        [SerializeField] private RectTransform _innerArrowPivot;
        [SerializeField] private RectTransform _outerArrowPivot;
        [SerializeField] private Image _innerArrowImage;
        [SerializeField] private Image _outerArrowImage;

        private CanvasGroup _canvasGroup;
        private Tween _fadeTween;
        private bool _localIsHost;
        private bool _hasSelfTarget;
        private float _selfTargetWorldAngle;
        private bool _hasTeammateTarget;
        private float _teammateTargetWorldAngle;

        private void Start()
        {
            _canvasGroup = GetComponent<CanvasGroup>();

            if (_profile == null || _inputRouter == null)
            {
                enabled = false;
                return;
            }

            if (!IsOwnTeam())
            {
                gameObject.SetActive(false);
                return;
            }

            _localIsHost = _photonView != null && _photonView.IsMine;

            ApplyStaticColors();
            InitializeRagdollStateBinding();
        }

        private void OnDestroy()
        {
            _fadeTween?.Kill();
            if (_ragdollStateMachine != null)
            {
                _ragdollStateMachine.OnStateChanged -= HandleRagdollStateChanged;
            }
        }

        private bool IsOwnTeam()
        {
            if (_photonView == null || _photonView.Owner == null) return true;

            int ownerTeam = PhotonTeamManager.GetTeamRaw(_photonView.Owner);
            int myTeam = PhotonTeamManager.GetLocalTeamRaw();
            if (myTeam == PhotonTeamManager.TeamNone) return false;
            return ownerTeam == myTeam;
        }

        private void ApplyStaticColors()
        {
            _innerRingImage.color = _profile.SelfColor;
            _outerRingImage.color = _profile.TeammateColor;
            _innerArrowImage.color = _profile.SelfColor;
            _outerArrowImage.color = _profile.TeammateColor;
        }

        private void InitializeRagdollStateBinding()
        {
            if (_ragdollStateMachine == null)
            {
                _canvasGroup.alpha = 1f;
                return;
            }

            _canvasGroup.alpha = _ragdollStateMachine.IsRootManagedByRagdoll ? 0f : 1f;
            _ragdollStateMachine.OnStateChanged += HandleRagdollStateChanged;
        }

        private void HandleRagdollStateChanged(ERagdollState newState)
        {
            bool shouldShow = (newState == ERagdollState.Animated);
            Fade(shouldShow);
        }

        private void Fade(bool show)
        {
            _fadeTween?.Kill();
            float target = show ? 1f : 0f;
            float duration = show ? _profile.FadeInDuration : _profile.FadeOutDuration;
            _fadeTween = _canvasGroup.DOFade(target, duration).SetLink(gameObject);
        }

        private void LateUpdate()
        {
            if (_canvasGroup.alpha <= 0.001f) return;

            UpdateArrows();
        }

        private void UpdateArrows()
        {
            Vector3 selfDir = _localIsHost ? _inputRouter.RoutedDirA : _inputRouter.RoutedDirB;
            Vector3 teammateDir = _localIsHost ? _inputRouter.RoutedDirB : _inputRouter.RoutedDirA;

            // DirectionCanvas가 RootBody의 자식이라 바디가 돌면 캔버스도 돎.
            // 월드 타깃 각도를 따로 보관하고 rootYaw를 매 프레임 상쇄해서 화살표를 월드 기준으로 고정.
            float rootYaw = _rootBodyTransform != null ? _rootBodyTransform.eulerAngles.y : 0f;
            float rotateStep = _profile.ArrowRotateSpeed * Time.deltaTime;

            UpdateArrow(_innerArrowPivot, selfDir, rootYaw, rotateStep, ref _hasSelfTarget, ref _selfTargetWorldAngle);
            UpdateArrow(_outerArrowPivot, teammateDir, rootYaw, rotateStep, ref _hasTeammateTarget, ref _teammateTargetWorldAngle);
        }

        private static void UpdateArrow(RectTransform pivot, Vector3 dir, float rootYawDegrees, float maxDeltaDegrees,
                                        ref bool hasTarget, ref float targetWorldAngle)
        {
            // 입력이 있을 때만 타깃 월드 각도를 갱신. 입력 각도 변화에만 smoothing 적용.
            if (dir.sqrMagnitude > 0.001f)
            {
                float newWorldAngle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                targetWorldAngle = hasTarget
                    ? Mathf.MoveTowardsAngle(targetWorldAngle, newWorldAngle, maxDeltaDegrees)
                    : newWorldAngle;
                hasTarget = true;
            }

            // 한 번도 입력이 없었으면 초기 로컬 각도 유지.
            if (!hasTarget) return;

            // rootYaw 상쇄는 매 프레임 즉시 적용 — 바디 회전에 즉시 반응해 월드 기준 고정.
            pivot.localEulerAngles = new Vector3(0f, 0f, rootYawDegrees - targetWorldAngle);
        }
    }
}
