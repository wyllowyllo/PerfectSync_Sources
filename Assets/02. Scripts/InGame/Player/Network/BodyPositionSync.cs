using InGame.Player.Animation;
using InGame.Player.Movement;
using InGame.Player.Ragdoll;
using Photon.Pun;
using UnityEngine;

namespace InGame.Player.Network
{
    public class BodyPositionSync : MonoBehaviourPun, IPunObservable
    {
        [Header("Interpolation Settings")]
        [SerializeField] private float _positionLerpSpeed = 10f;
        [SerializeField] private float _rotationLerpSpeed = 10f;

        private Vector3 _networkPosition;
        private Quaternion _networkRotation;
        private Vector3 _networkVelocity;
        private Rigidbody _rigidbody;

        // 애니메이션 동기화
        private PlayerMovement _playerMovement;
        private PlayerAnimation _playerAnimation;
        private IRagdoll _ragdoll;
        private float _networkSpeed;
        private bool _networkIsGrounded;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _playerMovement = GetComponent<PlayerMovement>();
            _playerAnimation = GetComponent<PlayerAnimation>();
            _ragdoll = GetComponent<IRagdoll>();
        }

        private void Start()
        {
            _networkPosition = transform.position;
            _networkRotation = transform.rotation;
        }

        private void Update()
        {
            if (photonView.IsMine)
                return;

            // 래그돌 중에는 보간 건너뜀
            if (_ragdoll != null && _ragdoll.IsRagdollActive)
                return;

            transform.position = Vector3.Lerp(transform.position, _networkPosition,
                Time.deltaTime * _positionLerpSpeed);
            transform.rotation = Quaternion.Slerp(transform.rotation, _networkRotation,
                Time.deltaTime * _rotationLerpSpeed);

            // Guest 애니메이션 구동
            if (_playerAnimation != null)
                _playerAnimation.Locomotion(_networkIsGrounded, _networkSpeed);
        }

        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.IsWriting)
            {
                stream.SendNext(transform.position);
                stream.SendNext(transform.rotation);
                stream.SendNext(_rigidbody != null ? _rigidbody.linearVelocity : Vector3.zero);

                // 애니메이션 데이터 전송
                float speed = _playerMovement != null ? _playerMovement.CurrentSpeed : 0f;
                bool grounded = _playerMovement != null && _playerMovement.Grounded;
                stream.SendNext(speed);
                stream.SendNext(grounded);
            }
            else
            {
                _networkPosition = (Vector3)stream.ReceiveNext();
                _networkRotation = (Quaternion)stream.ReceiveNext();
                _networkVelocity = (Vector3)stream.ReceiveNext();

                // 애니메이션 데이터 수신
                _networkSpeed = (float)stream.ReceiveNext();
                _networkIsGrounded = (bool)stream.ReceiveNext();

                // Dead Reckoning: 네트워크 지연만큼 위치 보정
                float lag = (float)(PhotonNetwork.Time - info.SentServerTimestamp);
                _networkPosition += _networkVelocity * lag;
            }
        }

        // 트리거 애니메이션 RPC (Host → Guest)
        public void SendAnimTrigger(byte triggerId)
        {
            photonView.RPC(nameof(RpcAnimTrigger), RpcTarget.Others, triggerId);
        }

        [PunRPC]
        private void RpcAnimTrigger(byte triggerId)
        {
            if (_playerAnimation == null) return;

            switch (triggerId)
            {
                case 0: // Jump
                    _playerAnimation.Jump();
                    break;
                case 1: // Dive
                    _playerAnimation.Dive();
                    break;
                case 2: // DiveLand
                    _playerAnimation.Land(true);
                    break;
            }
        }
    }
}
