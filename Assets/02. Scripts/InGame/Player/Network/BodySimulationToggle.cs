using Core;
using InGame.Player.Movement;
using UnityEngine;

namespace InGame.Player.Network
{
    [DefaultExecutionOrder(ExecutionOrderConstants.BodySimulationToggle)]
    [RequireComponent(typeof(PlayerMovement))]
    [RequireComponent(typeof(PlayerJump))]
    public class BodySimulationToggle : MonoBehaviour
    {
        [SerializeField] private Rigidbody _rootBody;
        [SerializeField] private Collider _rootCollider;

        private PlayerMovement _playerMovement;
        private PlayerJump _playerJump;

        private void Awake()
        {
            _playerMovement = GetComponent<PlayerMovement>();
            _playerJump = GetComponent<PlayerJump>();
        }

        private bool _isRemoteBody;
        public bool IsRemote => _isRemoteBody;

        public void SetRemote(bool isRemote)
        {
            _isRemoteBody = isRemote;

            if (isRemote)
                ApplyRemoteState();
            else
                ApplyLocalState();
        }

        private void LateUpdate()
        {
            if (_isRemoteBody && _rootBody != null && !_rootBody.isKinematic)
                _rootBody.isKinematic = true;
        }

        private void ApplyRemoteState()
        {
            if (_playerMovement != null)
                _playerMovement.enabled = false;

            if (_playerJump != null)
                _playerJump.enabled = false;

            if (_rootBody != null)
                _rootBody.isKinematic = true;

            if (_rootCollider != null)
                _rootCollider.enabled = false;
        }

        private void ApplyLocalState()
        {
            if (_playerMovement != null)
                _playerMovement.enabled = true;

            if (_playerJump != null)
                _playerJump.enabled = true;

            if (_rootBody != null)
                _rootBody.isKinematic = false;

            if (_rootCollider != null)
                _rootCollider.enabled = true;
        }

    }
}
