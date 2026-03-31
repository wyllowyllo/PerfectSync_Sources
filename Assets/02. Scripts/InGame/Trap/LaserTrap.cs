using UnityEngine;

public class LaserTrap : MonoBehaviour
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

    private void Awake()
    {
        _trap = _obstacleObject.GetComponent<ITrap>();
    }

    private void OnEnable()
    {
        _laserTrigger.OnPlayerDetected += HandlePlayerDetection;
    }

    private void OnDisable()
    {
        _laserTrigger.OnPlayerDetected -= HandlePlayerDetection;
    }

    private void HandlePlayerDetection()
    {
        Debug.Log("🚨 TrapController: 플레이어 감지! 함정 프로세스 시작.");

        // 1. 중복 발동 방지를 위해 레이저부터 즉시 끕니다.
        _laserTrigger.SetLaserActive(false);

        // 2. 장애물 발동 (지정된 딜레이 후)
        Invoke(nameof(ActivateObstacle), _activateDelay);
    }

    private void ActivateObstacle()
    {
        _trap.Activate();

        // 3. 자동 초기화 세팅
        if (_autoReset)
        {
            Invoke(nameof(ResetTrap), _resetDelay);
        }
    }

    private void ResetTrap()
    {
        Debug.Log("🔄 TrapController: 함정 재장전.");
        _trap.Reset();
        _laserTrigger.SetLaserActive(true);
    }
}
