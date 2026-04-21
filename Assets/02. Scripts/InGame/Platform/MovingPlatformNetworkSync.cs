using Core;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace InGame.Platform
{
    // 이동 발판의 Host-Authoritative 동기화.
    // Master는 MovingObstacle로 결정론적 이동을 수행하고, Non-Master는 이동 스크립트를
    // 비활성화한 뒤 네트워크 위치를 SmoothDamp로 보간한다.
    // PlatformCarrier(4)가 transform.position 변화량으로 라이더를 운반하므로,
    // 그 이전(3)에 위치를 갱신해야 한다.
    [RequireComponent(typeof(PhotonView))]
    [DefaultExecutionOrder(ExecutionOrderConstants.MovingPlatformSync)]
    public class MovingPlatformNetworkSync : MonoBehaviourPunCallbacks, IPunObservable
    {
        [Header("Components")]
        [SerializeField] private MovingObstacle _movingObstacle;

        [Header("Smoothing")]
        [Tooltip("위치 보간 시간(작을수록 즉각 추종, 클수록 부드러움).")]
        [SerializeField] private float _smoothTime = 0.1f;

        [Tooltip("이 거리 이상 벌어지면 즉시 스냅하여 누적 오차를 끊는다.")]
        [SerializeField] private float _snapThreshold = 2f;

        private Vector3 _networkPosition;
        private Vector3 _smoothVelocity;
        private bool _hasNetworkData;

        #region Lifecycle

        private void Start()
        {
            if (PhotonNetwork.InRoom)
                ConfigureForNetwork();
        }

        public override void OnJoinedRoom()
        {
            ConfigureForNetwork();
        }

        private void ConfigureForNetwork()
        {
            // Non-Master: 이동 시뮬레이션을 끄고 네트워크 위치만 따른다.
            if (_movingObstacle != null)
                _movingObstacle.enabled = PhotonNetwork.IsMasterClient;
        }

        #endregion

        #region Position Sync

        private void FixedUpdate()
        {
            if (photonView.IsMine || !_hasNetworkData) return;

            float dist = Vector3.Distance(transform.position, _networkPosition);

            if (dist > _snapThreshold)
            {
                transform.position = _networkPosition;
                _smoothVelocity = Vector3.zero;
            }
            else if (dist > 0.001f)
            {
                transform.position = Vector3.SmoothDamp(
                    transform.position, _networkPosition,
                    ref _smoothVelocity, _smoothTime, Mathf.Infinity, Time.fixedDeltaTime);
            }
        }

        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.IsWriting)
            {
                stream.SendNext(transform.position);
            }
            else
            {
                _networkPosition = (Vector3)stream.ReceiveNext();
                _hasNetworkData = true;
            }
        }

        #endregion

        #region Master Migration

        public override void OnMasterClientSwitched(Photon.Realtime.Player newMasterClient)
        {
            // 새 마스터에게 이동 시뮬레이션 권한을 이양한다.
            if (_movingObstacle != null)
                _movingObstacle.enabled = PhotonNetwork.IsMasterClient;
        }

        #endregion
    }
}
