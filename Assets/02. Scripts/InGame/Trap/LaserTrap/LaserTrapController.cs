using System.Collections;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

[RequireComponent(typeof(PhotonView))]
[DefaultExecutionOrder(1)]
public class LaserTrapController : MonoBehaviourPunCallbacks, IPunObservable
{
    private enum TrapState
    {
        Idle,
        Activating,
        Activated,
        Resetting
    }

    [Header("Components")]
    [SerializeField] private LaserTrigger _laserTrigger;
    [SerializeField] private PopupObstacle _popupObstacle;

    [Header("Movement (Optional)")]
    [Tooltip("이동 플랫폼 위에 있을 경우, 트랩 발동 시 이동 정지용")]
    [SerializeField] private MovingObstacle _movingObstacle;

    [Header("Trap Settings")]
    [Tooltip("감지 후 장애물 발동까지 딜레이")]
    [SerializeField] private float _activateDelay = 0.25f;

    [Tooltip("체크 시 발동 후 자동으로 초기화")]
    [SerializeField] private bool _autoReset = true;

    [Tooltip("자동 초기화 대기 시간")]
    [SerializeField] private float _resetDelay = 1f;

    private TrapState _state = TrapState.Idle;
    private Coroutine _activeSequence;

    // Non-Master 위치 동기화.
    private Vector3 _networkPosition;
    private Vector3 _smoothVelocity;
    private bool _hasNetworkData;

    private const float SmoothTime = 0.15f;
    private const float SnapThreshold = 3f;

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
        bool isMaster = PhotonNetwork.IsMasterClient;
        _laserTrigger.SetDetectionEnabled(isMaster);

        if (_movingObstacle != null && !isMaster)
            _movingObstacle.enabled = false;
    }

    public override void OnEnable()
    {
        base.OnEnable();
        _laserTrigger.OnPlayerDetected += HandlePlayerDetection;
        _popupObstacle.OnResetComplete += HandleResetComplete;
    }

    public override void OnDisable()
    {
        _laserTrigger.OnPlayerDetected -= HandlePlayerDetection;
        _popupObstacle.OnResetComplete -= HandleResetComplete;
        StopActiveSequence();
        base.OnDisable();
    }

    #endregion

    #region Trap Activation (RPC)

    private void HandlePlayerDetection()
    {
        if (PhotonNetwork.IsConnected && !PhotonNetwork.IsMasterClient) return;
        if (_state != TrapState.Idle) return;

        ExecuteActivation();

        if (PhotonNetwork.IsConnected)
            photonView.RPC(nameof(RpcActivate), RpcTarget.Others);
    }

    [PunRPC]
    private void RpcActivate()
    {
        if (_state != TrapState.Idle) return;
        ExecuteActivation();
    }

    [PunRPC]
    private void RpcReset()
    {
        if (_state != TrapState.Activated) return;
        ExecuteReset();
    }

    private void ExecuteActivation()
    {
        StopActiveSequence();
        _activeSequence = StartCoroutine(ActivationSequence());
    }

    private void ExecuteReset()
    {
        StopActiveSequence();
        _state = TrapState.Resetting;
        _popupObstacle.Reset();
        _laserTrigger.SetLaserActive(true);
    }

    private IEnumerator ActivationSequence()
    {
        _state = TrapState.Activating;
        _laserTrigger.SetLaserActive(false);

        if (_movingObstacle != null)
            _movingObstacle.SetPaused(true);

        yield return new WaitForSeconds(_activateDelay);

        _state = TrapState.Activated;
        _popupObstacle.Activate();
        _activeSequence = null;

        if (!_autoReset) yield break;

        // Reset 타이밍은 Master만 계산하여 RPC로 전파.
        if (PhotonNetwork.IsConnected && !PhotonNetwork.IsMasterClient) yield break;

        yield return new WaitForSeconds(_resetDelay);

        ExecuteReset();

        if (PhotonNetwork.IsConnected)
            photonView.RPC(nameof(RpcReset), RpcTarget.Others);
    }

    private void HandleResetComplete()
    {
        _state = TrapState.Idle;

        if (_movingObstacle != null)
            _movingObstacle.SetPaused(false);
    }

    private void StopActiveSequence()
    {
        if (_activeSequence != null)
        {
            StopCoroutine(_activeSequence);
            _activeSequence = null;
        }
    }

    #endregion

    #region Position Sync (IPunObservable)

    private void FixedUpdate()
    {
        if (photonView.IsMine || !_hasNetworkData) return;

        float dist = Vector3.Distance(transform.position, _networkPosition);

        if (dist > SnapThreshold)
        {
            transform.position = _networkPosition;
            _smoothVelocity = Vector3.zero;
        }
        else if (dist > 0.001f)
        {
            transform.position = Vector3.SmoothDamp(
                transform.position, _networkPosition,
                ref _smoothVelocity, SmoothTime, Mathf.Infinity, Time.fixedDeltaTime);
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

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        bool iAmNewMaster = PhotonNetwork.IsMasterClient;
        _laserTrigger.SetDetectionEnabled(iAmNewMaster);

        if (_movingObstacle != null)
            _movingObstacle.enabled = iAmNewMaster;

        if (iAmNewMaster && _state != TrapState.Idle)
        {
            StopActiveSequence();
            ForceReset();
        }
    }

    private void ForceReset()
    {
        _state = TrapState.Resetting;
        _popupObstacle.Reset();
        _laserTrigger.SetLaserActive(true);

        if (_movingObstacle != null)
            _movingObstacle.SetPaused(false);
    }

    #endregion
}
