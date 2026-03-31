using UnityEngine;

/// <summary>
/// 한 번 생성되는 문을 묶어서 관리하는 장애물 클래스
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class GuessTheDoorObstacle : MonoBehaviour
{
    [Header("이동 관련 설정")]
    [SerializeField] private float _moveSpeed = 10f;
    [SerializeField] private Vector3 _moveDirection = Vector3.back;

    [Header("문 연결")]
    [SerializeField] private DoorObstacle[] _doors;

    private Rigidbody _rigidbody;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _rigidbody.isKinematic = true; 
    }

    private void FixedUpdate()
    {
        MoveObstacle();
    }
    
    /// <summary>
    /// 장애물을 초기화하고 정답 문을 설정합니다.
    /// </summary>
    public void Initialize(EDoorColor correctColor)
    {
        Debug.Log($"정답 : {correctColor}");
        
        foreach (var door in _doors)
        {
            door.ResetState();
            door.SetCorrect(door.Color == correctColor);
        }

        ShuffleDoorPositions();
    }

    private void ShuffleDoorPositions()
    {
        var positions = new Vector3[_doors.Length];
        
        for (int i = 0; i < _doors.Length; i++)
        {
            positions[i] = _doors[i].transform.localPosition;
        }

        // Fisher-Yates 셔플
        for (int i = 0; i < positions.Length; i++)
        {
            int randomIndex = Random.Range(i, positions.Length);
            (positions[i], positions[randomIndex]) = (positions[randomIndex], positions[i]);
        }

        for (int i = 0; i < _doors.Length; i++)
        {
            _doors[i].transform.localPosition = positions[i];
        }
    }

    private void MoveObstacle()
    {
        var movement = _moveDirection.normalized * (_moveSpeed * Time.fixedDeltaTime);
        _rigidbody.MovePosition(_rigidbody.position + movement);
    }

    public void SetActive(bool active)
    {
        gameObject.SetActive(active);
    }
}
