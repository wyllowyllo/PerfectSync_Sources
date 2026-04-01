using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class DoorObstacle : MonoBehaviour
{
    [Header("문 설정")]
    [SerializeField] private EDoorColor _doorColor;
    public EDoorColor Color => _doorColor;

    private Rigidbody _rigidbody;
    private Vector3 _initialLocalPosition;
    private Quaternion _initialLocalRotation;

    private bool _isCorrectDoor;
    
    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        
        _initialLocalPosition = transform.localPosition;
        _initialLocalRotation = transform.localRotation;
    }

    /// <summary>
    /// 이전 사이클에서 밀려났던 물리 상태와 위치를 원상 복구합니다.
    /// </summary>
    public void ResetState()
    {
        transform.localPosition = _initialLocalPosition;
        transform.localRotation = _initialLocalRotation;
        
        _rigidbody.isKinematic = true;
    }

    /// <summary>
    /// 이 문이 정답인지 여부에 따라 물리 상태를 세팅합니다.
    /// </summary>
    public void SetCorrect(bool isCorrect)
    {
        _isCorrectDoor = isCorrect;
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        if (!_isCorrectDoor || !collision.gameObject.CompareTag("Player")) return;
        _rigidbody.isKinematic = false;
        // 문이 날라가는 연출 등...
        _rigidbody.AddForce(Vector3.forward * 2f + Vector3.up * 2f, ForceMode.Impulse);
    }
}
