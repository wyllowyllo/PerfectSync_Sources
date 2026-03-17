using Player.Ragdoll;
using Player.Test;
using Player.UserInput;
using UnityEngine;

namespace Player.Controller
{
    public class TeamController : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private LocalPlayerInput _playerInput;

        [Header("Controllers")]
        [SerializeField] private CharacterMoveController _mergedController;
        [SerializeField] private CharacterMoveController _avatarAController;
        [SerializeField] private CharacterMoveController _avatarBController;

        [Header("Bodies")]
        [SerializeField] private GameObject _mergedBody;
        [SerializeField] private GameObject _avatarA;
        [SerializeField] private GameObject _avatarB;

        [Header("Settings")]
        [SerializeField] private ETeamMode _startMode = ETeamMode.Merged;
        [SerializeField] private float _separationOffset = 1.0f;

        private ETeamMode _currentMode;
        private TpsCameraController _cameraController;
        private bool _pendingSwitch;
        private ETeamMode _pendingSwitchTarget;

        private IRagdollInput _mergedRagdoll;
        private IRagdollInput _avatarARagdoll;
        private IRagdollInput _avatarBRagdoll;

        public ETeamMode CurrentMode => _currentMode;

        private void Start()
        {
            _cameraController = FindAnyObjectByType<TpsCameraController>();

            Transform cameraTransform = _cameraController != null
                ? _cameraController.transform
                : Camera.main != null ? Camera.main.transform : null;

            _mergedController.SetCameraTransform(cameraTransform);
            _avatarAController.SetCameraTransform(cameraTransform);
            _avatarBController.SetCameraTransform(cameraTransform);

            _mergedRagdoll = _mergedBody.GetComponent<IRagdollInput>();
            _avatarARagdoll = _avatarA.GetComponent<IRagdollInput>();
            _avatarBRagdoll = _avatarB.GetComponent<IRagdollInput>();

            InitializeMode(_startMode);
        }

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.M))
            {
                ETeamMode target = _currentMode == ETeamMode.Merged
                    ? ETeamMode.Separated
                    : ETeamMode.Merged;
                SwitchMode(target);
            }

            if (_pendingSwitch && !IsAnyRagdollActive())
            {
                _pendingSwitch = false;
                ExecuteSwitch(_pendingSwitchTarget);
            }

            RouteInput();
        }

        public void SwitchMode(ETeamMode targetMode)
        {
            if (_currentMode == targetMode)
                return;

            if (IsAnyRagdollActive())
            {
                _pendingSwitch = true;
                _pendingSwitchTarget = targetMode;
                return;
            }

            ExecuteSwitch(targetMode);
        }

        private void RouteInput()
        {
            IPlayerInput input = _playerInput;
            if (!input.IsOwner)
                return;

            Vector2 move = input.MoveInput;
            bool jump = input.JumpPressed;
            bool dive = input.DivePressed;

            switch (_currentMode)
            {
                case ETeamMode.Separated:
                    _avatarAController.ApplyInput(move, jump, dive);
                    _avatarBController.ApplyInput(move, jump, dive);
                    break;

                case ETeamMode.Merged:
                    Vector2 combined = DualInputCombiner.Combine(move, move);
                    _mergedController.ApplyInput(combined, jump, dive);
                    break;
            }
        }

        private void InitializeMode(ETeamMode mode)
        {
            _currentMode = mode;

            switch (mode)
            {
                case ETeamMode.Merged:
                    _mergedBody.SetActive(true);
                    _avatarA.SetActive(false);
                    _avatarB.SetActive(false);
                    _mergedController.SetPhysicsActive(true);
                    _avatarAController.SetPhysicsActive(false);
                    _avatarBController.SetPhysicsActive(false);
                    SetCameraTarget(_mergedBody.transform);
                    break;

                case ETeamMode.Separated:
                    _mergedBody.SetActive(false);
                    _avatarA.SetActive(true);
                    _avatarB.SetActive(true);
                    _mergedController.SetPhysicsActive(false);
                    _avatarAController.SetPhysicsActive(true);
                    _avatarBController.SetPhysicsActive(true);
                    SetCameraTarget(_avatarA.transform);
                    break;
            }
        }

        private void ExecuteSwitch(ETeamMode targetMode)
        {
            switch (targetMode)
            {
                case ETeamMode.Merged:
                    SwitchToMerged();
                    break;
                case ETeamMode.Separated:
                    SwitchToSeparated();
                    break;
            }

            _currentMode = targetMode;
        }

        private void SwitchToMerged()
        {
            // 위치/속도 평균 계산.
            Vector3 avgPosition = (_avatarA.transform.position + _avatarB.transform.position) * 0.5f;
            Vector3 avgVelocity = (_avatarAController.GetVelocity() + _avatarBController.GetVelocity()) * 0.5f;

            // 먼저 velocity 설정 후 활성화 (한 프레임 원점 물리 방지).
            _mergedBody.transform.position = avgPosition;
            _mergedController.SetVelocity(avgVelocity);
            _mergedBody.SetActive(true);
            _mergedController.SetPhysicsActive(true);

            // 비활성화.
            _avatarAController.SetPhysicsActive(false);
            _avatarBController.SetPhysicsActive(false);
            _avatarA.SetActive(false);
            _avatarB.SetActive(false);

            SetCameraTarget(_mergedBody.transform);
        }

        private void SwitchToSeparated()
        {
            Vector3 basePosition = _mergedBody.transform.position;
            Vector3 velocity = _mergedController.GetVelocity();

            // 좌/우 오프셋으로 배치.
            Vector3 right = _mergedBody.transform.right;
            Vector3 posA = basePosition - right * _separationOffset;
            Vector3 posB = basePosition + right * _separationOffset;

            // 먼저 velocity 설정 후 활성화.
            _avatarA.transform.position = posA;
            _avatarB.transform.position = posB;
            _avatarAController.SetVelocity(velocity);
            _avatarBController.SetVelocity(velocity);
            _avatarA.SetActive(true);
            _avatarB.SetActive(true);
            _avatarAController.SetPhysicsActive(true);
            _avatarBController.SetPhysicsActive(true);

            // 비활성화.
            _mergedController.SetPhysicsActive(false);
            _mergedBody.SetActive(false);

            SetCameraTarget(_avatarA.transform);
        }

        private void SetCameraTarget(Transform target)
        {
            if (_cameraController != null)
                _cameraController.SetTarget(target);
        }

        private bool IsAnyRagdollActive()
        {
            switch (_currentMode)
            {
                case ETeamMode.Merged:
                    return _mergedRagdoll?.IsRagdollActive ?? false;

                case ETeamMode.Separated:
                    bool aActive = _avatarARagdoll?.IsRagdollActive ?? false;
                    bool bActive = _avatarBRagdoll?.IsRagdollActive ?? false;
                    return aActive || bActive;

                default:
                    return false;
            }
        }
    }
}
