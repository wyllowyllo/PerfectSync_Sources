using Core;
using DG.Tweening;
using InGame.Player;
using InGame.Player.Ragdoll;
using InGame.Team;
using Photon.Pun;
using Unity.Cinemachine;
using UnityEngine;

namespace InGame.Camera.PlayerCamera
{
    [DefaultExecutionOrder(ExecutionOrderConstants.CinemachineCameraManager)]
    public class FollowCameraController : MonoBehaviourPun
    {
        [Header("Root Bodies")]
        [SerializeField] private Transform _mergedRootBody;

        [Header("Ragdoll State Machines")]
        [SerializeField] private RagdollStateMachine _mergedRagdoll;

        [Header("Proxy")]
        [SerializeField] private Vector3 _targetOffset = new Vector3(0f, 0.7f, 0f);

        [Header("Ragdoll Camera")]
        [SerializeField] private Vector3 _ragdollPositionDamping = new Vector3(2f, 2.5f, 2f);

        [Header("FOV Kick")]
        [SerializeField] private float _fovKickAmount = 5f;
        [SerializeField] private float _fovKickDuration = 0.2f;

        [Header("Activation FOV Punch")]
        [SerializeField] private float _activationFovPunch = -8f;
        [SerializeField] private float _activationFovDuration = 0.4f;

        private CinemachineCamera _followCamera;
        private CinemachineCamera _ragdollCamera;
        private Rigidbody _animatedProxyRb;
        private Rigidbody _ragdollProxyRb;
        private Transform _activeTarget;
        private RagdollStateMachine _activeRagdoll;
        private bool _isRagdollCameraActive;

        private const int ActivePriority = 10;
        private const int StandbyPriority = 0;
        private bool _initialized;
        private float _baseFov;
        private Tween _fovTween;
        private InvincibleContactDetector _contactDetector;
        private InvincibleModeController _invincibleController;

        private void Start()
        {
            TryInitialize();
        }

        private void TryInitialize()
        {
            if (_initialized) return;
            if (!IsMyTeam()) return;
            if (InGameCameraManager.Instance == null) return;

            _followCamera = InGameCameraManager.Instance.FollowCamera;
            if (_followCamera == null)
            {
                Debug.LogError("[FollowCameraController] InGameCameraManager에 FollowCamera가 할당되지 않았습니다.", this);
                return;
            }

            _initialized = true;
            _baseFov = _followCamera.Lens.FieldOfView;

            _contactDetector = GetComponentInChildren<InvincibleContactDetector>();
            if (_contactDetector != null)
                _contactDetector.OnHitLocal += HandleInvincibleHit;

            _invincibleController = GetComponent<InvincibleModeController>();
            if (_invincibleController != null)
                _invincibleController.OnInvincibleEnter += HandleInvincibleActivation;

            _animatedProxyRb = CreateInterpolatedProxy("AnimatedCameraProxy");
            _ragdollProxyRb = CreateInterpolatedProxy("RagdollCameraProxy");

            _ragdollCamera = CreateRagdollCamera();
            SetupCameraTargets();

            _activeTarget = _mergedRootBody;
            _activeRagdoll = _mergedRagdoll;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (_activeTarget != null)
            {
                Vector3 initPos = _activeTarget.position + _targetOffset;
                _animatedProxyRb.position = initPos;
                _ragdollProxyRb.position = initPos;
            }
        }

        private bool IsMyTeam()
        {
            var owner = photonView.Owner;
            if (owner == null) return false;

            int ownerTeam = PhotonTeamManager.GetTeamRaw(owner);
            int myTeam = PhotonTeamManager.GetLocalTeamRaw();

            return ownerTeam != PhotonTeamManager.TeamNone
                && ownerTeam == myTeam;
        }

        private void OnDestroy()
        {
            if (!_initialized) return;

            if (_contactDetector != null)
                _contactDetector.OnHitLocal -= HandleInvincibleHit;

            if (_invincibleController != null)
                _invincibleController.OnInvincibleEnter -= HandleInvincibleActivation;

            _fovTween?.Kill();

            if (_animatedProxyRb != null)
                Destroy(_animatedProxyRb.gameObject);

            if (_ragdollProxyRb != null)
                Destroy(_ragdollProxyRb.gameObject);

            if (_ragdollCamera != null)
                Destroy(_ragdollCamera.gameObject);
        }

        private void FixedUpdate()
        {
            if (!_initialized)
            {
                TryInitialize();
                if (!_initialized) return;
            }

            if (_activeTarget == null) return;

            Vector3 targetPos = _activeTarget.position + _targetOffset;
            _animatedProxyRb.MovePosition(targetPos);
            _ragdollProxyRb.MovePosition(targetPos);
        }

        private void LateUpdate()
        {
            if (!_initialized) return;
            if (_activeRagdoll == null) return;

            bool shouldBeRagdoll = _activeRagdoll.IsPhysicsRagdoll;
            if (shouldBeRagdoll == _isRagdollCameraActive) return;

            _isRagdollCameraActive = shouldBeRagdoll;

            CinemachineCamera from = shouldBeRagdoll ? _followCamera : _ragdollCamera;
            CinemachineCamera to = shouldBeRagdoll ? _ragdollCamera : _followCamera;

            SyncOrbitalAxes(from, to);
            InGameCameraManager.SetCameraPriority(to, ActivePriority);
            InGameCameraManager.SetCameraPriority(from, StandbyPriority);
        }

        private void SetupCameraTargets()
        {
            _followCamera.Follow = _animatedProxyRb.transform;
            _followCamera.LookAt = _animatedProxyRb.transform;
            _ragdollCamera.Follow = _ragdollProxyRb.transform;
            _ragdollCamera.LookAt = _ragdollProxyRb.transform;

            InGameCameraManager.SetCameraPriority(_followCamera, ActivePriority);
            InGameCameraManager.SetCameraPriority(_ragdollCamera, StandbyPriority);
        }

        private CinemachineCamera CreateRagdollCamera()
        {
            var clone = Instantiate(_followCamera.gameObject);
            clone.name = "CinemachineCamera_Ragdoll";

            var cam = clone.GetComponent<CinemachineCamera>();

            var orbital = clone.GetComponent<CinemachineOrbitalFollow>();
            if (orbital != null)
                orbital.TrackerSettings.PositionDamping = _ragdollPositionDamping;

            return cam;
        }

        private static void SyncOrbitalAxes(CinemachineCamera source, CinemachineCamera target)
        {
            var sourceOrbital = source.GetComponent<CinemachineOrbitalFollow>();
            var targetOrbital = target.GetComponent<CinemachineOrbitalFollow>();
            if (sourceOrbital == null || targetOrbital == null) return;

            targetOrbital.HorizontalAxis.Value = sourceOrbital.HorizontalAxis.Value;
            targetOrbital.VerticalAxis.Value = sourceOrbital.VerticalAxis.Value;
        }

        private void HandleInvincibleActivation()
        {
            if (_followCamera == null) return;

            _fovTween?.Kill();

            var lens = _followCamera.Lens;
            lens.FieldOfView = _baseFov + _activationFovPunch;
            _followCamera.Lens = lens;

            _fovTween = DOTween.To(
                () => _followCamera.Lens.FieldOfView,
                v =>
                {
                    var l = _followCamera.Lens;
                    l.FieldOfView = v;
                    _followCamera.Lens = l;
                },
                _baseFov,
                _activationFovDuration
            ).SetEase(Ease.OutBack);
        }

        private void HandleInvincibleHit()
        {
            if (_followCamera == null) return;

            _fovTween?.Kill();

            var lens = _followCamera.Lens;
            lens.FieldOfView = _baseFov + _fovKickAmount;
            _followCamera.Lens = lens;

            _fovTween = DOTween.To(
                () => _followCamera.Lens.FieldOfView,
                v =>
                {
                    var l = _followCamera.Lens;
                    l.FieldOfView = v;
                    _followCamera.Lens = l;
                },
                _baseFov,
                _fovKickDuration
            ).SetEase(Ease.OutQuad);
        }

        private static Rigidbody CreateInterpolatedProxy(string name)
        {
            var obj = new GameObject(name);
            var rb = obj.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.useGravity = false;
            return rb;
        }
    }
}
