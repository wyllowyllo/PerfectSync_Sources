using Core;
using InGame.Player.Movement;
using Photon.Pun;
using UnityEngine;

namespace InGame.Player.Network
{
    /// <summary>
    /// 바디의 물리 시뮬레이션 활성/비활성을 제어한다.
    /// 비활성(remote) 바디는 컴포넌트를 끄고 kinematic으로 전환한다.
    /// 각 바디(MergedBody, AvatarA, AvatarB)에 부착.
    /// 분리 모드에서는 양쪽 클라이언트 모두 AvatarA/B를 로컬 시뮬레이션하므로
    /// 현재 모드에서 비활성인 바디(예: 분리 모드의 MergedBody)에만 remote 적용.
    /// </summary>
    [DefaultExecutionOrder(ExecutionOrderConstants.BodySimulationToggle)]
    public class BodySimulationToggle : MonoBehaviour
    {
        private bool _isRemoteBody;
        private PlayerMovement _playerMovement;
        private PlayerJump _playerJump;
        private Rigidbody _rigidbody;
        private PhotonView _photonView;

        private void Awake()
        {
            _playerMovement = GetComponent<PlayerMovement>();
            _playerJump = GetComponent<PlayerJump>();
            _rigidbody = GetComponent<Rigidbody>();
            _photonView = GetComponent<PhotonView>();
        }

        public void SetRemote(bool isRemote)
        {
            _isRemoteBody = isRemote;

            if (isRemote)
                ApplyRemoteState();
            else
                ApplyLocalState();
        }

        // OnEnable 자동 평가 제거 — 외부(NetworkInputRouter)에서만 SetRemote를 호출한다.
        // 합체 모드에서는 Guest도 물리 시뮬레이션을 실행해야 하므로
        // 소유권 기반 자동 판단이 올바르지 않다.

        private void LateUpdate()
        {
            // RagdollPhysicsToggle.Deactivate()가 isKinematic=false로 복원하므로
            // 비활성 바디(예: 분리 모드의 MergedBody)에서 래그돌 복귀 후 다시 kinematic으로 강제
            if (_isRemoteBody && _rigidbody != null && !_rigidbody.isKinematic)
            {
                _rigidbody.isKinematic = true;
            }
        }

        private void ApplyRemoteState()
        {
            _isRemoteBody = true;

            if (_playerMovement != null)
                _playerMovement.enabled = false;

            if (_playerJump != null)
                _playerJump.enabled = false;

            if (_rigidbody != null)
                _rigidbody.isKinematic = true;
        }

        private void ApplyLocalState()
        {
            _isRemoteBody = false;

            if (_playerMovement != null)
                _playerMovement.enabled = true;

            if (_playerJump != null)
                _playerJump.enabled = true;

            if (_rigidbody != null)
                _rigidbody.isKinematic = false;
        }
    }
}
