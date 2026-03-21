using Core;
using InGame.Player;
using InGame.Player.Ragdoll;
using InGame.Team._02._Domain;
using Photon.Pun;
using Unity.Cinemachine;
using UnityEngine;

namespace InGame.Camera.PlayerCamera
{
    [DefaultExecutionOrder(ExecutionOrderConstants.CinemachineCameraManager)]
    public class FollowCameraController : MonoBehaviourPun
    {
        [Header("Cinemachine")]
        [SerializeField] private CinemachineCamera _animatedCamera;

        [Header("Root Bodies (각 Body의 Rigidbody Transform)")]
        [SerializeField] private Transform _mergedRootBody;
        [SerializeField] private Transform _avatarARootBody;
        [SerializeField] private Transform _avatarBRootBody;

        [Header("Ragdoll State Machines")]
        [SerializeField] private RagdollStateMachine _mergedRagdoll;
        [SerializeField] private RagdollStateMachine _avatarARagdoll;
        [SerializeField] private RagdollStateMachine _avatarBRagdoll;

        [Header("Proxy")]
        [SerializeField] private Vector3 _targetOffset = new Vector3(0f, 0.7f, 0f);

        [Header("Ragdoll Camera")]
        [SerializeField] private Vector3 _ragdollPositionDamping = new Vector3(2f, 2.5f, 2f);

        private CinemachineCamera _ragdollCamera;
        private Transform _animatedProxy;
        private Rigidbody _ragdollProxyRb;
        private PlayerFormController _playerFormController;
        private Transform _activeTarget;
        private RagdollStateMachine _activeRagdoll;
        private bool _isHost;
        private bool _isRagdollCameraActive;

        private const int ActivePriority = 10;
        private const int StandbyPriority = 0;

        private void Awake()
        {
            _animatedProxy = new GameObject("AnimatedCameraProxy").transform;
            _ragdollProxyRb = CreateInterpolatedProxy("RagdollCameraProxy");
        }

        private void Start()
        {
            _playerFormController = GetComponent<PlayerFormController>();
            _isHost = photonView.IsMine;

            if (_animatedCamera == null)
                _animatedCamera = FindAnyObjectByType<CinemachineCamera>();

            _ragdollCamera = CreateRagdollCamera();
            SetupCameraTargets();

            _playerFormController.OnModeChanged += HandleModeChanged;
            UpdateActiveTarget(_playerFormController.CurrentMode);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (_activeTarget != null)
            {
                Vector3 initPos = _activeTarget.position + _targetOffset;
                _animatedProxy.position = initPos;
                _ragdollProxyRb.position = initPos;
            }
        }

        private void OnDestroy()
        {
            if (_playerFormController != null)
                _playerFormController.OnModeChanged -= HandleModeChanged;

            if (_animatedProxy != null)
                Destroy(_animatedProxy.gameObject);

            if (_ragdollProxyRb != null)
                Destroy(_ragdollProxyRb.gameObject);

            if (_ragdollCamera != null)
                Destroy(_ragdollCamera.gameObject);
        }

        private void FixedUpdate()
        {
            if (_activeTarget == null) return;

            _ragdollProxyRb.MovePosition(_activeTarget.position + _targetOffset);
        }

        private void LateUpdate()
        {
            if (_activeTarget != null)
                _animatedProxy.position = _activeTarget.position + _targetOffset;

            if (_activeRagdoll == null) return;

            bool shouldBeRagdoll = _activeRagdoll.IsPhysicsRagdoll;
            if (shouldBeRagdoll == _isRagdollCameraActive) return;

            _isRagdollCameraActive = shouldBeRagdoll;

            CinemachineCamera from = shouldBeRagdoll ? _animatedCamera : _ragdollCamera;
            CinemachineCamera to = shouldBeRagdoll ? _ragdollCamera : _animatedCamera;

            SyncOrbitalAxes(from, to);
            SetPriority(to, ActivePriority);
            SetPriority(from, StandbyPriority);
        }

        private void HandleModeChanged(ETeamMode newMode)
        {
            UpdateActiveTarget(newMode);
        }

        private void UpdateActiveTarget(ETeamMode mode)
        {
            _activeTarget = mode switch
            {
                ETeamMode.Merged => _mergedRootBody,
                ETeamMode.Separated => _isHost ? _avatarARootBody : _avatarBRootBody,
                _ => _mergedRootBody
            };

            _activeRagdoll = mode switch
            {
                ETeamMode.Merged => _mergedRagdoll,
                ETeamMode.Separated => _isHost ? _avatarARagdoll : _avatarBRagdoll,
                _ => _mergedRagdoll
            };
        }

        private void SetupCameraTargets()
        {
            _animatedCamera.Follow = _animatedProxy;
            _animatedCamera.LookAt = _animatedProxy;
            _ragdollCamera.Follow = _ragdollProxyRb.transform;
            _ragdollCamera.LookAt = _ragdollProxyRb.transform;

            SetPriority(_animatedCamera, ActivePriority);
            SetPriority(_ragdollCamera, StandbyPriority);
        }

        private CinemachineCamera CreateRagdollCamera()
        {
            var clone = Instantiate(_animatedCamera.gameObject);
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

        private static void SetPriority(CinemachineCamera camera, int priority)
        {
            camera.Priority.Enabled = true;
            camera.Priority.Value = priority;
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
