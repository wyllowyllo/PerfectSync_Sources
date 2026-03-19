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

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
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

            transform.position = Vector3.Lerp(transform.position, _networkPosition,
                Time.deltaTime * _positionLerpSpeed);
            transform.rotation = Quaternion.Slerp(transform.rotation, _networkRotation,
                Time.deltaTime * _rotationLerpSpeed);
        }

        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.IsWriting)
            {
                stream.SendNext(transform.position);
                stream.SendNext(transform.rotation);
                stream.SendNext(_rigidbody != null ? _rigidbody.linearVelocity : Vector3.zero);
            }
            else
            {
                _networkPosition = (Vector3)stream.ReceiveNext();
                _networkRotation = (Quaternion)stream.ReceiveNext();
                _networkVelocity = (Vector3)stream.ReceiveNext();

                // Dead Reckoning: 네트워크 지연만큼 위치 보정
                float lag = (float)(PhotonNetwork.Time - info.SentServerTimestamp);
                _networkPosition += _networkVelocity * lag;
            }
        }
    }
}
