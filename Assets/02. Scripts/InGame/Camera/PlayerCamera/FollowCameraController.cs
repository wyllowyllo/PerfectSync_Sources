using Core;
using DG.Tweening;
using InGame.Gimmick;
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
        /// <summary>CinemachineBrain이 구동하는 실제 렌더 카메라. 초기화 후 사용 가능.</summary>
        public UnityEngine.Camera OutputCamera => _outputCamera;

        [Header("Root Bodies")]
        [SerializeField] private Transform _mergedRootBody;

        [Header("Ragdoll State Machines")]
        [SerializeField] private RagdollStateMachine _mergedRagdoll;

        [Header("Anchor")]
        [SerializeField] private Vector3 _targetOffset = new Vector3(0f, 0.7f, 0f);
        [SerializeField] private float _animatedSmoothTime = 0.02f;
        [SerializeField] private float _ragdollSmoothTime = 0.15f;
        [SerializeField] private float _smoothTimeTransitionSpeed = 3f;
        [SerializeField] private float _maxAnchorDistance = 8f;

        [Header("Ragdoll Orbital Damping")]
        [SerializeField] private Vector3 _ragdollOrbitalDamping = new Vector3(1f, 1.5f, 1f);

        [Header("FOV Kick")]
        [SerializeField] private float _fovKickAmount = 5f;
        [SerializeField] private float _fovKickDuration = 0.2f;

        [Header("Activation FOV Punch")]
        [SerializeField] private float _activationFovPunch = -8f;
        [SerializeField] private float _activationFovDuration = 0.4f;

        [Header("Obstacle Destroy FOV Kick")]
        [SerializeField] private float _obstacleDestroyFovKick = 8f;
        [SerializeField] private float _obstacleDestroyFovDuration = 0.25f;

        private CinemachineCamera _followCamera;
        private CinemachineOrbitalFollow _orbitalFollow;
        private Rigidbody _anchorRb;
        private Transform _activeTarget;
        private RagdollStateMachine _activeRagdoll;

        private Vector3 _anchorVelocity;
        private Vector3 _defaultOrbitalDamping;
        private float _currentSmoothTime;
        private bool _wasRagdollManaged;
        private bool _initialized;
        private UnityEngine.Camera _outputCamera;
        private float _baseFov;
        private Tween _fovTween;
        private InvincibleContactDetector _contactDetector;
        private InvincibleModeController _invincibleController;
        private RespawnHandler _respawnHandler;

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
            _outputCamera = UnityEngine.Camera.main;
            _baseFov = _followCamera.Lens.FieldOfView;
            _currentSmoothTime = _animatedSmoothTime;

            _contactDetector = GetComponentInChildren<InvincibleContactDetector>();
            if (_contactDetector != null)
            {
                _contactDetector.OnHitLocal += HandleInvincibleHit;
                _contactDetector.OnObstacleDestroyLocal += HandleObstacleDestroy;
            }

            _invincibleController = GetComponent<InvincibleModeController>();
            if (_invincibleController != null)
                _invincibleController.OnInvincibleEnter += HandleInvincibleActivation;

            _respawnHandler = GetComponent<RespawnHandler>();
            if (_respawnHandler != null)
                _respawnHandler.OnCameraResetRequested += HandleCameraReset;

            _anchorRb = CreateInterpolatedProxy("CameraAnchor");

            _orbitalFollow = _followCamera.GetComponent<CinemachineOrbitalFollow>();
            if (_orbitalFollow != null)
                _defaultOrbitalDamping = _orbitalFollow.TrackerSettings.PositionDamping;

            _activeTarget = _mergedRootBody;
            _activeRagdoll = _mergedRagdoll;

            _followCamera.Follow = _anchorRb.transform;
            _followCamera.LookAt = _anchorRb.transform;
            InGameCameraManager.SetCameraPriority(_followCamera, ActivePriority);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (_activeTarget != null)
                _anchorRb.position = _activeTarget.position + _targetOffset;
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
            {
                _contactDetector.OnHitLocal -= HandleInvincibleHit;
                _contactDetector.OnObstacleDestroyLocal -= HandleObstacleDestroy;
            }

            if (_invincibleController != null)
                _invincibleController.OnInvincibleEnter -= HandleInvincibleActivation;

            if (_respawnHandler != null)
                _respawnHandler.OnCameraResetRequested -= HandleCameraReset;

            _fovTween?.Kill();

            if (_anchorRb != null)
                Destroy(_anchorRb.gameObject);
        }

        private void FixedUpdate()
        {
            if (!_initialized)
            {
                TryInitialize();
                if (!_initialized) return;
            }

            if (_activeTarget == null) return;

            // IsRootManagedByRagdoll: Ragdolled, BlendToAnim, Dead 모두 true.
            // BlendToAnim 동안에도 래그돌 스무딩을 유지하여 전환 걸림 방지.
            bool isManaged = _activeRagdoll != null && _activeRagdoll.IsRootManagedByRagdoll;
            float targetSmoothTime = isManaged ? _ragdollSmoothTime : _animatedSmoothTime;
            _currentSmoothTime = Mathf.MoveTowards(
                _currentSmoothTime, targetSmoothTime,
                _smoothTimeTransitionSpeed * Time.fixedDeltaTime);

            Vector3 targetPos = _activeTarget.position + _targetOffset;
            Vector3 smoothed = Vector3.SmoothDamp(
                _anchorRb.position, targetPos, ref _anchorVelocity,
                _currentSmoothTime, Mathf.Infinity, Time.fixedDeltaTime);

            // 앵커가 타겟에서 너무 멀어지면 강제로 끌어당겨 화면 이탈 방지.
            Vector3 delta = smoothed - targetPos;
            if (delta.sqrMagnitude > _maxAnchorDistance * _maxAnchorDistance)
                smoothed = targetPos + delta.normalized * _maxAnchorDistance;

            _anchorRb.MovePosition(smoothed);
        }

        private void LateUpdate()
        {
            if (!_initialized) return;
            if (_activeRagdoll == null || _orbitalFollow == null) return;

            bool isManaged = _activeRagdoll.IsRootManagedByRagdoll;
            if (isManaged == _wasRagdollManaged) return;

            _wasRagdollManaged = isManaged;
            _orbitalFollow.TrackerSettings.PositionDamping = isManaged
                ? _ragdollOrbitalDamping
                : _defaultOrbitalDamping;
        }

        private void HandleCameraReset(Vector3 position, Quaternion rotation)
        {
            if (!_initialized) return;

            // 앵커를 리스폰 위치로 즉시 스냅.
            Vector3 targetPos = position + _targetOffset;
            _anchorRb.position = targetPos;
            _anchorRb.transform.position = targetPos;
            _anchorVelocity = Vector3.zero;

            // 카메라 방향을 리스폰 방향에 맞춤.
            if (_orbitalFollow != null)
            {
                _orbitalFollow.HorizontalAxis.Value = rotation.eulerAngles.y;
                _orbitalFollow.VerticalAxis.Value = _orbitalFollow.VerticalAxis.Center;
            }
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

        private void HandleObstacleDestroy()
        {
            if (_followCamera == null) return;

            _fovTween?.Kill();

            var lens = _followCamera.Lens;
            lens.FieldOfView = _baseFov + _obstacleDestroyFovKick;
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
                _obstacleDestroyFovDuration
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

        private const int ActivePriority = 10;
    }
}
