using Photon.Pun;
using UnityEngine;

/// <summary>
/// LaserTrap의 네트워크 동기화.
/// Scene Object PhotonView(Master 소유)를 통해 동작한다.
///
/// 연속 상태 (IPunObservable): MovingObstacle 위치 보정 — RotationScript 패턴
///   Master (IsMine): 시뮬레이션 결과 송신
///   Non-Master: 로컬 예측 + 네트워크 보정 블렌딩
///
/// 이산 이벤트 (RPC): 트랩 발동
///   Master: LaserTrigger 감지 → RPC 브로드캐스트 + 로컬 실행
///   Non-Master: RPC 수신 → 발동 시퀀스 실행
/// </summary>
[RequireComponent(typeof(PhotonView))]
[DefaultExecutionOrder(1)]
public class LaserTrapSync : MonoBehaviourPun, IPunObservable
{
    [SerializeField] private LaserTrap _trap;
    [SerializeField] private LaserTrigger _laserTrigger;

    private const float CorrectionFactor = 0.2f;

    private Vector3 _networkPosition;
    private bool _hasNetworkData;

    private void OnEnable()
    {
        _laserTrigger.OnPlayerDetected += HandleDetection;
    }

    private void OnDisable()
    {
        _laserTrigger.OnPlayerDetected -= HandleDetection;
    }

    #region Trap Activation (RPC)

    private void HandleDetection()
    {
        if (!photonView.IsMine) return;
        photonView.RPC(nameof(RpcActivateTrap), RpcTarget.Others);
    }

    [PunRPC]
    private void RpcActivateTrap()
    {
        _trap.ExecuteActivationSequence();
    }

    #endregion

    #region Position Correction (IPunObservable)

    private void FixedUpdate()
    {
        // Non-Master: MovingObstacle의 로컬 예측 결과를 네트워크 값으로 보정
        if (!photonView.IsMine && _hasNetworkData)
        {
            transform.position = Vector3.Lerp(
                transform.position, _networkPosition, CorrectionFactor);
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
}
