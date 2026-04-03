using Photon.Pun;
using UnityEngine;

public class LaserTrap : MonoBehaviour
{
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

    private void OnEnable()
    {
        _laserTrigger.OnPlayerDetected += HandlePlayerDetection;
        _popupObstacle.OnResetComplete += HandleResetComplete;
    }

    private void OnDisable()
    {
        _laserTrigger.OnPlayerDetected -= HandlePlayerDetection;
        _popupObstacle.OnResetComplete -= HandleResetComplete;
    }

    /// <summary>
    /// LaserTrigger 감지 콜백.
    /// 네트워크 연결 시 Master Client만 처리 — 실제 발동은 LaserTrapSync RPC로 전파.
    /// 오프라인이면 직접 실행.
    /// </summary>
    private void HandlePlayerDetection()
    {
        if (PhotonNetwork.IsConnected && !PhotonNetwork.IsMasterClient) return;
        ExecuteActivationSequence();
    }

    /// <summary>
    /// 트랩 발동 시퀀스. Master에서 직접, Non-Master에서는 RPC를 통해 호출된다.
    /// </summary>
    public void ExecuteActivationSequence()
    {
        _laserTrigger.SetLaserActive(false);
        if (_movingObstacle) _movingObstacle.SetPaused(true);
        Invoke(nameof(ActivateObstacle), _activateDelay);
    }

    private void ActivateObstacle()
    {
        _popupObstacle.Activate();

        if (_autoReset)
        {
            Invoke(nameof(ResetTrap), _resetDelay);
        }
    }

    private void ResetTrap()
    {
        _popupObstacle.Reset();
        _laserTrigger.SetLaserActive(true);
    }

    private void HandleResetComplete()
    {
        if (_movingObstacle) _movingObstacle.SetPaused(false);
    }
}
