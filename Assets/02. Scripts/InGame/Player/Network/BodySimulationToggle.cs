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
        [SerializeField] private LayerMask _remoteBodyLayer;

        private PlayerMovement _playerMovement;
        private PlayerJump _playerJump;
        private int _originalLayer;

        private void Awake()
        {
            _playerMovement = GetComponent<PlayerMovement>();
            _playerJump = GetComponent<PlayerJump>();

            if (_rootBody != null)
                _originalLayer = _rootBody.gameObject.layer;
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
            {
                _rootBody.isKinematic = true;
                _rootBody.interpolation = RigidbodyInterpolation.Interpolate;
                _rootBody.gameObject.layer = ToSingleLayer(_remoteBodyLayer);
            }
        }

        private void ApplyLocalState()
        {
            if (_playerMovement != null)
                _playerMovement.enabled = true;

            if (_playerJump != null)
                _playerJump.enabled = true;

            if (_rootBody != null)
            {
                _rootBody.isKinematic = false;
                _rootBody.interpolation = RigidbodyInterpolation.Interpolate;
                _rootBody.gameObject.layer = _originalLayer;
            }
        }

        private static int ToSingleLayer(LayerMask mask)
        {
            int value = mask.value;
            if (value == 0) return 0;

            int layer = 0;
            while ((value >> layer) > 1)
                layer++;
            return layer;
        }
    }
}
