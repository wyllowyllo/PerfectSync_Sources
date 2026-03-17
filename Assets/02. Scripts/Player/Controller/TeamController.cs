using Player.Ragdoll;
using Player.Test;
using Player.UserInput;
using UnityEngine;

namespace Player.Controller
{
    [RequireComponent(typeof(LocalPlayerInput))]
    public class TeamController : MonoBehaviour
    {
        [Header("Bodies")]
        [SerializeField] private GameObject _mergedBody;
        [SerializeField] private GameObject _avatarA;
        [SerializeField] private GameObject _avatarB;

        [Header("Settings")]
        [SerializeField] private ETeamMode _startMode = ETeamMode.Merged;
        [SerializeField] private float _separationOffset = 1.0f;

        private LocalPlayerInput _playerInput;
        private CharacterMoveController _mergedController;
        private CharacterMoveController _avatarAController;
        private CharacterMoveController _avatarBController;
        private ETeamMode _currentMode;
        private TpsCameraController _cameraController;
        private bool _pendingSwitch;
        private ETeamMode _pendingSwitchTarget;

        private IRagdoll _mergedRagdoll;
        private IRagdoll _avatarARagdoll;
        private IRagdoll _avatarBRagdoll;

        public ETeamMode CurrentMode => _currentMode;

        private void Start()
        {
            _playerInput = GetComponent<LocalPlayerInput>();
            _mergedController = _mergedBody.GetComponent<CharacterMoveController>();
            _avatarAController = _avatarA.GetComponent<CharacterMoveController>();
            _avatarBController = _avatarB.GetComponent<CharacterMoveController>();

            _cameraController = FindAnyObjectByType<TpsCameraController>();

            Transform cameraTransform = _cameraController != null
                ? _cameraController.transform
                : Camera.main != null ? Camera.main.transform : null;

            _mergedController.SetCameraTransform(cameraTransform);
            _avatarAController.SetCameraTransform(cameraTransform);
            _avatarBController.SetCameraTransform(cameraTransform);

            _mergedRagdoll = _mergedBody.GetComponent<IRagdoll>();
            _avatarARagdoll = _avatarA.GetComponent<IRagdoll>();
            _avatarBRagdoll = _avatarB.GetComponent<IRagdoll>();

            InitializeMode(_startMode);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.M))
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
                    SetCameraTarget(_mergedBody.transform);
                    break;

                case ETeamMode.Separated:
                    _mergedBody.SetActive(false);
                    _avatarA.SetActive(true);
                    _avatarB.SetActive(true);
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

            // SetActive(true) → OnEnable에서 isKinematic = false 처리.
            _mergedBody.transform.position = avgPosition;
            _mergedBody.SetActive(true);
            _mergedController.SetVelocity(avgVelocity);

            // SetActive(false) → OnDisable에서 정리.
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

            // SetActive(true) → OnEnable에서 isKinematic = false 처리.
            _avatarA.transform.position = posA;
            _avatarB.transform.position = posB;
            _avatarA.SetActive(true);
            _avatarB.SetActive(true);
            _avatarAController.SetVelocity(velocity);
            _avatarBController.SetVelocity(velocity);

            // SetActive(false) → OnDisable에서 정리.
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
