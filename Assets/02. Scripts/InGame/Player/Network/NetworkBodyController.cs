using InGame.Player.Movement;
using UnityEngine;

namespace InGame.Player.Network
{
    /// <summary>
    /// Guest 클라이언트에서 물리 시뮬레이션 컴포넌트를 비활성화하고
    /// Rigidbody를 kinematic으로 전환하여 네트워크 보간이 정상 동작하도록 한다.
    /// 각 바디(MergedBody, AvatarA, AvatarB)에 부착.
    /// </summary>
    [DefaultExecutionOrder(10)]
    public class NetworkBodyController : MonoBehaviour
    {
        private bool _isRemoteBody;
        private PlayerMovement _playerMovement;
        private PlayerJump _playerJump;
        private Rigidbody _rigidbody;
        private bool _initialized;

        private void Awake()
        {
            _playerMovement = GetComponent<PlayerMovement>();
            _playerJump = GetComponent<PlayerJump>();
            _rigidbody = GetComponent<Rigidbody>();
            _initialized = true;
        }

        public void SetRemote(bool isRemote)
        {
            _isRemoteBody = isRemote;

            if (!_initialized)
            {
                _playerMovement = GetComponent<PlayerMovement>();
                _playerJump = GetComponent<PlayerJump>();
                _rigidbody = GetComponent<Rigidbody>();
                _initialized = true;
            }

            ApplyRemoteState();
        }

        private void OnEnable()
        {
            if (_isRemoteBody)
                ApplyRemoteState();
        }

        private void LateUpdate()
        {
            // RagdollPhysicsToggle.Deactivate()가 isKinematic=false로 복원하므로
            // Guest에서 래그돌 복귀 후 다시 kinematic으로 강제
            if (_isRemoteBody && _rigidbody != null && !_rigidbody.isKinematic)
            {
                _rigidbody.isKinematic = true;
            }
        }

        private void ApplyRemoteState()
        {
            if (!_isRemoteBody) return;

            if (_playerMovement != null)
                _playerMovement.enabled = false;

            if (_playerJump != null)
                _playerJump.enabled = false;

            if (_rigidbody != null)
                _rigidbody.isKinematic = true;
        }
    }
}
