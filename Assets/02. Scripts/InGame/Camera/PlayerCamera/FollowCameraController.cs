using Core;
using DG.Tweening;
using InGame.Gimmick;
using InGame.Player;
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

        [Header("Camera Target")]
        [SerializeField] private CameraTargetProvider _targetProvider;

        [Header("Anchor")]
        [SerializeField] private Vector3 _targetOffset = new Vector3(0f, 0.7f, 0f);

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

            _followCamera.Follow = _anchorRb.transform;
            _followCamera.LookAt = _anchorRb.transform;

            if (_targetProvider != null)
            {
                Vector3 targetPos = _targetProvider.SmoothedPosition + _targetOffset;
                _anchorRb.position = targetPos;
                _anchorRb.transform.position = targetPos;

                // 플레이어가 바라보는 방향 뒤쪽에서 카메라가 시작하도록 HorizontalAxis 설정.
                if (_orbitalFollow != null)
                {
                    float playerYaw = _targetProvider.transform.eulerAngles.y;
                    _orbitalFollow.HorizontalAxis.Value = playerYaw;
                }

                _followCamera.OnTargetObjectWarped(_anchorRb.transform, targetPos - _followCamera.transform.position);
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

            if (_targetProvider == null) return;

            _anchorRb.MovePosition(_targetProvider.SmoothedPosition + _targetOffset);
        }

        private void HandleCameraReset(Vector3 position, Quaternion rotation)
        {
            if (!_initialized) return;

            _targetProvider.WarpTo(position);

            Vector3 targetPos = position + _targetOffset;
            _anchorRb.position = targetPos;
            _anchorRb.transform.position = targetPos;

            if (_orbitalFollow != null)
            {
                _orbitalFollow.HorizontalAxis.Value = rotation.eulerAngles.y;
                _orbitalFollow.VerticalAxis.Value = _orbitalFollow.VerticalAxis.Center;
            }

            Vector3 delta = targetPos - _followCamera.transform.position;
            _followCamera.OnTargetObjectWarped(_anchorRb.transform, delta);
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
