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

    private void HandlePlayerDetection()
    {
        Debug.Log("🚨 TrapController: 플레이어 감지! 함정 프로세스 시작.");

        // 1. 중복 발동 방지를 위해 레이저부터 즉시 끕니다.
        _laserTrigger.SetLaserActive(false);

        // 2. 이동 중이라면 정지
        if (_movingObstacle) _movingObstacle.SetPaused(true);

        // 3. 장애물 발동 (지정된 딜레이 후)
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
        // 이동 재개는 복귀 완료 후 HandleResetComplete에서 처리
    }

    private void HandleResetComplete()
    {
        if (_movingObstacle) _movingObstacle.SetPaused(false);
    }
}
