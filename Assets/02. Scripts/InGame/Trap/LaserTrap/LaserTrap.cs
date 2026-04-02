using Photon.Pun;
using UnityEngine;

public class LaserTrap : MonoBehaviourPun
{
    [Header("Components")]
    [SerializeField] private LaserTrigger _laserTrigger;

    [SerializeField] private GameObject _obstacleObject;
    private ITrap _trap;

    [Header("Trap Settings")]
    [Tooltip("감지 후 장애물 발동까지 딜레이")]
    [SerializeField] private float _activateDelay = 0.25f;

    [Tooltip("체크 시 발동 후 자동으로 초기화")]
    [SerializeField] private bool _autoReset = true;

    [Tooltip("자동 초기화 대기 시간")]
    [SerializeField] private float _resetDelay = 1f;

    private PhotonView _view;
    private bool IsMine => _view == null || PhotonNetwork.IsMasterClient;

    private void Awake()
    {
        _trap = _obstacleObject.GetComponent<ITrap>();
        _view = GetComponent<PhotonView>();
    }

    private void OnEnable()
    {
        _laserTrigger.OnPlayerDetected += HandlePlayerDetection;
    }

    private void OnDisable()
    {
        _laserTrigger.OnPlayerDetected -= HandlePlayerDetection;
        CancelInvoke();
    }

    private void HandlePlayerDetection()
    {
        if (!IsMine) return;

        if (_view != null)
            _view.RPC(nameof(RpcSetLaserActive), RpcTarget.All, false);
        else
            RpcSetLaserActive(false);

        Invoke(nameof(ActivateObstacle), _activateDelay);
    }

    private void ActivateObstacle()
    {
        if (_view != null)
            _view.RPC(nameof(RpcActivateObstacle), RpcTarget.All);
        else
            RpcActivateObstacle();
    }

    private void ResetTrap()
    {
        if (_view != null)
            _view.RPC(nameof(RpcResetTrap), RpcTarget.All);
        else
            RpcResetTrap();
    }

    [PunRPC]
    private void RpcSetLaserActive(bool active)
    {
        _laserTrigger.SetLaserActive(active);
    }

    [PunRPC]
    private void RpcActivateObstacle()
    {
        _trap.Activate();

        if (IsMine && _autoReset)
        {
            Invoke(nameof(ResetTrap), _resetDelay);
        }
    }

    [PunRPC]
    private void RpcResetTrap()
    {
        _trap.Reset();
        _laserTrigger.SetLaserActive(true);
    }
}
