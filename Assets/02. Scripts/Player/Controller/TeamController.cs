using Player.Test;
using Player.UserInput;
using UnityEngine;

namespace Player.Controller
{
    [RequireComponent(typeof(LocalPlayerInput), typeof(AvatarTransformer))]
    public class TeamController : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private ETeamMode _startMode = ETeamMode.Merged;

        private LocalPlayerInput _playerInput;
        private AvatarTransformer _avatarTransformer;
        private TpsCameraController _cameraController;

        private void Start()
        {
            _playerInput = GetComponent<LocalPlayerInput>();
            _avatarTransformer = GetComponent<AvatarTransformer>();
            _cameraController = FindAnyObjectByType<TpsCameraController>();

            Transform cameraTransform = _cameraController != null
                ? _cameraController.transform
                : Camera.main.transform;

            _avatarTransformer.OnModeChanged += HandleModeChanged;
            _avatarTransformer.Initialize(_startMode, cameraTransform);

            TeamModeManager.Instance.OnSwitchRequested += HandleSwitchRequested;
        }
        

        private void Update()
        {
            _avatarTransformer.Tick();
            RouteInput();
        }

        private void HandleSwitchRequested()
        {
            _avatarTransformer.ToggleMode();
        }

        private void HandleModeChanged(ETeamMode newMode)
        {
            if (_cameraController == null)
                return;

            var activeBodies = _avatarTransformer.ActiveBodies;
            if (activeBodies.Count > 0)
                _cameraController.SetTarget(activeBodies[0].BodyTransform);
        }

        private void RouteInput()
        {
            IPlayerInput input = _playerInput;
            if (!input.IsOwner)
                return;

            Vector2 move = input.MoveInput;
            bool jump = input.JumpPressed;
            bool dive = input.DivePressed;

            var activeBodies = _avatarTransformer.ActiveBodies;

            if (_avatarTransformer.CurrentMode == ETeamMode.Merged)
            {
                Vector2 combined = DualInputCombiner.Combine(move, move);
                activeBodies[0].ApplyInput(combined, jump, dive);
            }
            else
            {
                for (int i = 0; i < activeBodies.Count; i++)
                {
                    activeBodies[i].ApplyInput(move, jump, dive);
                }
            }
        }
    }
}
