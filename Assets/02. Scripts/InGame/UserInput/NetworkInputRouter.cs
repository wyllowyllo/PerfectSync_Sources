using Core.Utilities;
using InGame.Camera.PlayerCamera;
using InGame.Player;
using InGame.Player.Movement;
using InGame.Player.Network;
using InGame.Player.Ragdoll;
using InGame.Team._02._Domain;
using Photon.Pun;
using UnityEngine;

namespace InGame.UserInput
{
    [RequireComponent(typeof(NetworkPlayerInput), typeof(RemotePlayerInput), typeof(PlayerFormController))]
    public class NetworkInputRouter : MonoBehaviourPun
    {
        // ── Fields / SerializeField ──────────────────────────────────

        [Header("Settings")]
        [SerializeField] private ETeamMode _startMode = ETeamMode.Merged;

        [Header("Cameras")]
        [SerializeField] private TpsCameraController _cameraControllerA;

        [Header("Bodies")]
        [SerializeField] private GameObject _mergedBody;
        [SerializeField] private GameObject _avatarA;
        [SerializeField] private GameObject _avatarB;

        private PlayerFormController _playerFormController;
        private NetworkPlayerInput _networkPlayerInput;
        private RemotePlayerInput _remotePlayerInput;
        private NetworkTeamModeManager _teamModeManager;
        private Transform _cameraTransformA;
        private bool _isHost;
        private ETeamMode _currentMode;

        // 입력 RPC 쓰로틀링
        private bool _pendingJump;
        private float _lastSendTime;
        private const float MinSendInterval = 0.05f; // 최대 20Hz

        // 프레임당 1회 읽기 캐시
        private Vector3 _cachedLocalWorldDir;
        private bool _cachedLocalJump;

        // ── Lifecycle ────────────────────────────────────────────────

        private void Start()
        {
            _networkPlayerInput = GetComponent<NetworkPlayerInput>();
            _remotePlayerInput = GetComponent<RemotePlayerInput>();
            _playerFormController = GetComponent<PlayerFormController>();
            _teamModeManager = GetComponent<NetworkTeamModeManager>();

            _isHost = photonView.IsMine;
            _currentMode = _startMode;

            SetupCameras();

            _playerFormController.OnModeChanged += HandleModeChanged;
            _playerFormController.Initialize(_startMode);

            SetCameraTargetByRole();
            RefreshBodyMode(_startMode);

            if (_teamModeManager != null)
                _teamModeManager.OnSwitchRequested += HandleSwitchRequested;

            _networkPlayerInput.OnImpactReceived += HandleImpact;
            _networkPlayerInput.OnDeathReceived += HandleDeath;
        }

        private void OnDestroy()
        {
            if (_playerFormController != null)
                _playerFormController.OnModeChanged -= HandleModeChanged;

            if (_teamModeManager != null)
                _teamModeManager.OnSwitchRequested -= HandleSwitchRequested;

            if (_networkPlayerInput != null)
            {
                _networkPlayerInput.OnImpactReceived -= HandleImpact;
                _networkPlayerInput.OnDeathReceived -= HandleDeath;
            }
        }

        // ── Update Loop ─────────────────────────────────────────────

        private void Update()
        {
            _playerFormController.Tick();
            ReadLocalInput();
            RouteInput();
            SendLocalInput();
        }

        private void ReadLocalInput()
        {
            Vector2 rawInput = new Vector2(
                Input.GetAxisRaw("Horizontal"),
                Input.GetAxisRaw("Vertical"));
            _cachedLocalWorldDir = CameraRelativeConverter.Convert(rawInput, _cameraTransformA);
            _cachedLocalJump = Input.GetButtonDown("Jump");
        }

        private void RouteInput()
        {
            Vector3 worldDirA, worldDirB;
            bool jumpA, jumpB;

            if (_isHost)
            {
                worldDirA = _cachedLocalWorldDir;
                jumpA = _cachedLocalJump;
                worldDirB = CameraRelativeConverter.Convert(_remotePlayerInput.MoveInput, null);
                jumpB = _remotePlayerInput.JumpPressed;
            }
            else
            {
                worldDirA = CameraRelativeConverter.Convert(_remotePlayerInput.MoveInput, null);
                jumpA = _remotePlayerInput.JumpPressed;
                worldDirB = _cachedLocalWorldDir;
                jumpB = _cachedLocalJump;
            }

            _playerFormController.ApplyInput(worldDirA, worldDirB, jumpA, jumpB);
        }

        // ── Input Send ──────────────────────────────────────────────

        private void SendLocalInput()
        {
            _pendingJump |= _cachedLocalJump;

            if (Time.time - _lastSendTime < MinSendInterval) return;

            if (_isHost)
                photonView.RPC(nameof(RpcRemoteInput), RpcTarget.Others, _cachedLocalWorldDir, _pendingJump);
            else
                photonView.RPC(nameof(RpcRemoteInput), photonView.Owner, _cachedLocalWorldDir, _pendingJump);

            _pendingJump = false;
            _lastSendTime = Time.time;
        }

        [PunRPC]
        private void RpcRemoteInput(Vector3 worldDir, bool jump)
        {
            _remotePlayerInput.SetWorldDirection(worldDir, jump);
        }

        // ── Mode ─────────────────────────────────────────────────────

        private void HandleSwitchRequested()
        {
            _playerFormController.ToggleMode();
        }

        private void HandleModeChanged(ETeamMode newMode)
        {
            _currentMode = newMode;
            SetCameraTargetByRole();
            RefreshBodyMode(newMode);
        }

        // ── Camera ───────────────────────────────────────────────────

        private void SetupCameras()
        {
            if (_cameraControllerA == null)
                _cameraControllerA = FindAnyObjectByType<TpsCameraController>();

            _cameraTransformA = _cameraControllerA != null
                ? _cameraControllerA.transform
                : UnityEngine.Camera.main.transform;
        }

        private void SetCameraTargetByRole()
        {
            if (_cameraControllerA == null) return;

            var target = _isHost
                ? _playerFormController.PrimaryCameraFollowPoint
                : _playerFormController.SecondaryCameraFollowPoint;

            _cameraControllerA.SetTarget(target);
        }

        // ── Body ─────────────────────────────────────────────────────

        private void RefreshBodyMode(ETeamMode mode)
        {
            bool isMerged = mode == ETeamMode.Merged;

            // 물리 시뮬레이션
            SetRemoteOnBody(_mergedBody, !isMerged);
            SetRemoteOnBody(_avatarA, isMerged);
            SetRemoteOnBody(_avatarB, isMerged);

            // 위치 보정 동기화
            _mergedBody?.GetComponent<BodySyncBridge>()?.SetSyncEnabled(isMerged);
            _avatarA?.GetComponent<BodySyncBridge>()?.SetSyncEnabled(!isMerged);
            _avatarB?.GetComponent<BodySyncBridge>()?.SetSyncEnabled(!isMerged);
        }

        private void SetRemoteOnBody(GameObject body, bool isRemote)
        {
            if (body == null) return;
            var controller = body.GetComponent<NetworkBodyController>();
            if (controller != null)
                controller.SetRemote(isRemote);
        }

        // ── Impact / Death ───────────────────────────────────────────

        private void HandleImpact(Vector3 impulse, Vector3 hitPoint)
        {
            var ragdoll = GetActiveRagdollController();
            ragdoll?.OnHitImpact(impulse, hitPoint);
        }

        private void HandleDeath()
        {
            var ragdoll = GetActiveRagdollController();
            ragdoll?.EnterDead();
        }

        private RagdollController GetActiveRagdollController()
        {
            switch (_currentMode)
            {
                case ETeamMode.Merged:
                    return _mergedBody != null ? _mergedBody.GetComponent<RagdollController>() : null;
                case ETeamMode.Separated:
                    return _avatarA != null ? _avatarA.GetComponent<RagdollController>() : null;
                default:
                    return null;
            }
        }
    }
}
