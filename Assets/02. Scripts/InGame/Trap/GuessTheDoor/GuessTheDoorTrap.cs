using UnityEngine;
using System.Collections;

/// <summary>
/// 전체 트랩의 생명 주기 관리
/// </summary>
public class GuessTheDoorTrap : MonoBehaviour
{
    [Header("한 사이클에 생성할 장애물 개수 설정")]
    [SerializeField] private GuessTheDoorObstacle _obstaclePrefab;
    [SerializeField] private int _obstacleCount = 3;
    [SerializeField] private Transform _spawnPoint;
    [SerializeField] private Transform _signboardSpawnPoint;

    [Header("스폰 및 대기 시간 설정")]
    [SerializeField] private float _spawnInterval = 1f;
    [SerializeField] private float _sequenceWaitTime = 3f;
    
    [Header("팻말 연결")]
    [SerializeField] private Signboard _signboard;

    private GuessTheDoorObstacle[] _obstacles;
    private EDoorColor[] _currentSequence;

    private WaitForSeconds _intervalWait;
    private WaitForSeconds _sequenceWait;

    private void Awake()
    {
        Initialize();
    }

    private void Start()
    {
        StartCoroutine(SpawnRoutine());
    }

    private void Initialize()
    {
        _intervalWait = new(_spawnInterval);
        _sequenceWait = new(_sequenceWaitTime);
        
        _obstacles = new GuessTheDoorObstacle[_obstacleCount];
        _currentSequence = new EDoorColor[_obstacleCount];
        
        _signboard.SetActive(false);
        
        for (int i = 0; i < _obstacleCount; i++)
        {
            var instance = Instantiate(_obstaclePrefab, transform);
            instance.SetActive(false);
            _obstacles[i] = instance;
        }
    }

    private IEnumerator SpawnRoutine()
    {
        while (true)
        {
            // 새로운 시퀀스 생성 및 섞기
            GenerateNewSequence();

            // 팻말 스폰
            _signboard.transform.position = _signboardSpawnPoint.position;
            _signboard.transform.rotation = _signboardSpawnPoint.rotation;
            _signboard.SetActive(true);
            
            yield return _intervalWait;
            
            // 준비된 장애물을 배열의 인덱스 순서대로 순회하며 스폰
            for (int i = 0; i < _obstacleCount; i++)
            {
                var obstacle = _obstacles[i];
                var correctDoorIndex = _currentSequence[i];

                // 위치 및 회전 초기화
                obstacle.transform.position = _spawnPoint.position;
                obstacle.transform.rotation = _spawnPoint.rotation;

                // 정답 설정
                obstacle.Initialize(correctDoorIndex);
                obstacle.SetActive(true);

                // 다음 장애물이 나올 때까지 대기
                yield return _intervalWait;
            }

            // 한 사이클이 끝나면 다음 시퀀스가 시작될 때까지 대기
            yield return _sequenceWait;
        }
    }

    // 정답 정보를 저장하는 배열 생성
    private void GenerateNewSequence()
    {
        // 각 색깔이 한 번씩은 등장하도록 조정
        for (int i = 0; i < _obstacleCount; i++)
        {
            _currentSequence[i] = (EDoorColor)(i % 3);
        }

        // Fisher-Yates 셔플 알고리즘
        for (int i = 0; i < _currentSequence.Length; i++)
        {
            int randomIndex = Random.Range(i, _currentSequence.Length);
            (_currentSequence[i], _currentSequence[randomIndex]) = (_currentSequence[randomIndex], _currentSequence[i]);
        }

        // 팻말에 생성된 시퀀스 전달하여 UI 업데이트
         _signboard.UpdateSignboard(_currentSequence);
    }
}
