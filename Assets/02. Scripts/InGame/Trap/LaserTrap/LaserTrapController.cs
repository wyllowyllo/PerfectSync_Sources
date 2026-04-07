using InGame.Obstacle;
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
        Resetting,
        SpikeDestroyed
    }

    [Header("Components")]
    [SerializeField] private LaserTrigger _laserTrigger;
    [SerializeField] private PopupObstacle _popupObstacle;

    [Header("Movement (Optional)")]
    [Tooltip("이동 플랫폼 위에 있을 경우, 트랩 발동 시 이동 정지용")]
    [SerializeField] private MovingObstacle _movingObstacle;

    [Header("Destroyable (Optional)")]
    [Tooltip("스파이크에 부착된 DestroyableObstacle")]
    [SerializeField] private DestroyableObstacle _destroyable;

    [Header("Trap Settings")]
    [Tooltip("감지 후 장애물 발동까지 딜레이")]
    [SerializeField] private float _activateDelay = 0.25f;

    [Tooltip("체크 시 발동 후 자동으로 초기화")]
    [SerializeField] private bool _autoReset = true;

    [Tooltip("자동 초기화 대기 시간")]
    [SerializeField] private float _resetDelay = 1f;

    private TrapState _state = TrapState.Idle;
    private float _timer;

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
        // 모든 클라이언트에서 레이저 감지 활성화 (로컬 플레이어 위치로 즉시 감지).
        _laserTrigger.SetDetectionEnabled(true);

        if (_movingObstacle != null && !PhotonNetwork.IsMasterClient)
            _movingObstacle.enabled = false;
    }

    public override void OnEnable()
    {
        base.OnEnable();
        _laserTrigger.OnPlayerDetected += HandlePlayerDetection;
        _popupObstacle.OnResetComplete += HandleResetComplete;

        if (_destroyable != null)
        {
            _destroyable.OnDestroyed += HandleSpikeDestroyed;
            _destroyable.OnRespawned += HandleSpikeRespawned;
        }
    }

    public override void OnDisable()
    {
        _laserTrigger.OnPlayerDetected -= HandlePlayerDetection;
        _popupObstacle.OnResetComplete -= HandleResetComplete;

        if (_destroyable != null)
        {
            _destroyable.OnDestroyed -= HandleSpikeDestroyed;
            _destroyable.OnRespawned -= HandleSpikeRespawned;
        }

        base.OnDisable();
    }

    #endregion

    #region Trap State Machine

    private void Update()
    {
        if (_state != TrapState.Activating && _state != TrapState.Activated)
            return;

        _timer -= Time.deltaTime;
        if (_timer > 0f) return;

        switch (_state)
        {
            case TrapState.Activating:
                _state = TrapState.Activated;
                _popupObstacle.Activate();

                if (_autoReset && IsMasterOrOffline())
                {
                    _timer = _resetDelay;
                }
                else
                {
                    // Non-Master 또는 autoReset=false: 타이머 진행 안 함.
                    _timer = float.MaxValue;
                }
                break;

            case TrapState.Activated:
                // Master만 여기 도달 (Non-Master는 timer=MaxValue).
                ExecuteReset();
                if (PhotonNetwork.IsConnected)
                    photonView.RPC(nameof(RpcReset), RpcTarget.Others);
                break;
        }
    }

    private void HandlePlayerDetection()
    {
        if (_state != TrapState.Idle) return;

        ExecuteActivation();

        if (PhotonNetwork.IsConnected)
            photonView.RPC(nameof(RpcActivate), RpcTarget.Others);
    }

    private void HandleResetComplete()
    {
        _state = TrapState.Idle;

        if (_movingObstacle != null)
            _movingObstacle.SetPaused(false);
    }

    private void ExecuteActivation()
    {
        _state = TrapState.Activating;
        _timer = _activateDelay;
        _laserTrigger.SetLaserActive(false);

        if (_movingObstacle != null)
            _movingObstacle.SetPaused(true);
    }

    private void ExecuteReset()
    {
        _state = TrapState.Resetting;
        _popupObstacle.Reset();
        _laserTrigger.SetLaserActive(true);
    }

    #endregion

    #region Destroy / Respawn

    private void HandleSpikeDestroyed()
    {
        _state = TrapState.SpikeDestroyed;
        _laserTrigger.SetLaserActive(false);

        if (_movingObstacle != null)
            _movingObstacle.SetPaused(false);
    }

    private void HandleSpikeRespawned()
    {
        _state = TrapState.Idle;
        _laserTrigger.SetLaserActive(true);
    }

    #endregion

    #region RPC

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

    #endregion

    #region Position Sync (IPunObservable)

    private void FixedUpdate()
    {
        if (photonView.IsMine || !_hasNetworkData) return;

        // 스파이크 이동 중 parent transform 변경 금지.
        // Kinematic child Rigidbody의 MovePosition 호출 후 parent transform.position이
        // 변경되면 MovePosition이 무효화되어 충돌이 발생하지 않는다.
        if (_state != TrapState.Idle && _state != TrapState.SpikeDestroyed)
            return;

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

        if (_movingObstacle != null)
            _movingObstacle.enabled = iAmNewMaster;

        if (iAmNewMaster && _state != TrapState.Idle && _state != TrapState.SpikeDestroyed)
            ForceReset();
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

    private bool IsMasterOrOffline()
    {
        return !PhotonNetwork.IsConnected || PhotonNetwork.IsMasterClient;
    }
}
