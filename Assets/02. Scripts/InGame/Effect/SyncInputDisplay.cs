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

            // DirectionCanvas가 RootBody의 자식이므로, 월드 방향을 Canvas 로컬 각도로 바꾸려면 RootBody의 yaw를 상쇄해야 함.
            float rootYaw = _rootBodyTransform != null ? _rootBodyTransform.eulerAngles.y : 0f;
            float rotateStep = _profile.ArrowRotateSpeed * Time.deltaTime;

            RotatePivotSmooth(_innerArrowPivot, selfDir, rootYaw, rotateStep);
            RotatePivotSmooth(_outerArrowPivot, teammateDir, rootYaw, rotateStep);
        }

        private static void RotatePivotSmooth(RectTransform pivot, Vector3 dir, float rootYawDegrees, float maxDeltaDegrees)
        {
            if (dir.sqrMagnitude <= 0.001f) return;

            float worldAngle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            float targetZ = rootYawDegrees - worldAngle;
            float currentZ = pivot.localEulerAngles.z;

            // ±180° 래핑 안전한 각도 보간.
            float smoothedZ = Mathf.MoveTowardsAngle(currentZ, targetZ, maxDeltaDegrees);
            pivot.localEulerAngles = new Vector3(0f, 0f, smoothedZ);
        }
    }
}
