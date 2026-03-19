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
    [RequireComponent(typeof(LocalPlayerInput), typeof(RemotePlayerInput), typeof(PlayerFormController))]
    public class InputRouter : MonoBehaviourPun
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
        private LocalPlayerInput localPlayerInput;
        private RemotePlayerInput _remotePlayerInput;
        private TeamModeSynchronizer teamModeSynchronizer;
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
            localPlayerInput = GetComponent<LocalPlayerInput>();
            _remotePlayerInput = GetComponent<RemotePlayerInput>();
            _playerFormController = GetComponent<PlayerFormController>();
            teamModeSynchronizer = GetComponent<TeamModeSynchronizer>();

            _isHost = photonView.IsMine;
            _currentMode = _startMode;

            SetupCameras();

            _playerFormController.OnModeChanged += HandleModeChanged;
            _playerFormController.Initialize(_startMode);

            SetCameraTargetByRole();
            RefreshBodyMode(_startMode);

            if (teamModeSynchronizer != null)
                teamModeSynchronizer.OnSwitchRequested += HandleSwitchRequested;

            localPlayerInput.OnImpactReceived += HandleImpact;
            localPlayerInput.OnDeathReceived += HandleDeath;
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
            Vector2 rawInput;
            bool jump;

#if UNITY_EDITOR
            if (NativeKeyInput.IsActive)
            {
                NativeKeyInput.Update();

                if (_isHost)
                {
                    rawInput = new Vector2(
                        NativeKeyInput.GetAxis(NativeKeyInput.VK_D, NativeKeyInput.VK_A),
                        NativeKeyInput.GetAxis(NativeKeyInput.VK_W, NativeKeyInput.VK_S));
                    jump = NativeKeyInput.IsKeyDown(NativeKeyInput.VK_SPACE);
                }
                else
                {
                    rawInput = new Vector2(
                        NativeKeyInput.GetAxis(NativeKeyInput.VK_RIGHT, NativeKeyInput.VK_LEFT),
                        NativeKeyInput.GetAxis(NativeKeyInput.VK_UP, NativeKeyInput.VK_DOWN));
                    jump = NativeKeyInput.IsKeyDown(NativeKeyInput.VK_RETURN);
                }
            }
            else
#endif
            {
                rawInput = new Vector2(
                    Input.GetAxisRaw("Horizontal"),
                    Input.GetAxisRaw("Vertical"));
                jump = Input.GetButtonDown("Jump");
            }

            _cachedLocalWorldDir = CameraRelativeConverter.Convert(rawInput, _cameraTransformA);
            _cachedLocalJump = jump;
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
            _mergedBody?.GetComponent<BodyPositionSynchronizer>()?.SetSyncEnabled(isMerged);
            _avatarA?.GetComponent<BodyPositionSynchronizer>()?.SetSyncEnabled(!isMerged);
            _avatarB?.GetComponent<BodyPositionSynchronizer>()?.SetSyncEnabled(!isMerged);
        }

        private void SetRemoteOnBody(GameObject body, bool isRemote)
        {
            if (body == null) return;
            var controller = body.GetComponent<BodySimulationToggle>();
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
        
        private void OnDestroy()
        {
            if (_playerFormController != null)
                _playerFormController.OnModeChanged -= HandleModeChanged;

            if (teamModeSynchronizer != null)
                teamModeSynchronizer.OnSwitchRequested -= HandleSwitchRequested;

            if (localPlayerInput != null)
            {
                localPlayerInput.OnImpactReceived -= HandleImpact;
                localPlayerInput.OnDeathReceived -= HandleDeath;
            }
        }
    }
}
