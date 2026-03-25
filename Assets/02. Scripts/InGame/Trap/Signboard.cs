using UnityEngine;
using UnityEngine.UI; // UI 요소를 제어하기 위해 필요합니다.

[RequireComponent(typeof(Rigidbody))]
public class Signboard : MonoBehaviour
{
    [Header("UI 연결")]
    [Tooltip("정답 순서를 표시할 UI 이미지")]
    [SerializeField] private Image[] _sequenceImages;

    [Header("색상 설정")]
    [SerializeField] private Color _greenColor = Color.green;
    [SerializeField] private Color _yellowColor = Color.yellow;
    [SerializeField] private Color _redColor = Color.red;

    [Header("이동 관련 설정")]
    [SerializeField] private float _moveSpeed = 10f;
    [SerializeField] private Vector3 _moveDirection = Vector3.back;
    
    private Rigidbody _rigidbody;
    
    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _rigidbody.isKinematic = true; 
    }

    private void FixedUpdate()
    {
        var movement = _moveDirection.normalized * (_moveSpeed * Time.fixedDeltaTime);
        _rigidbody.MovePosition(_rigidbody.position + movement);
    }
    
    /// <summary>
    /// 새로운 정답 시퀀스를 받아 팻말 UI의 색상을 갱신합니다.
    /// </summary>
    public void UpdateSignboard(EDoorColor[] sequence)
    {
        if (_sequenceImages == null || _sequenceImages.Length < sequence.Length)
        {
            Debug.LogWarning("Signboard에 연결된 UI 이미지가 없거나 부족합니다!");
            return;
        }
        
        for (int i = 0; i < sequence.Length; i++)
        {
            _sequenceImages[i].color = GetColorFromEnum(sequence[i]);
        }
    }

    /// <summary>
    /// EDoorColor Enum 값에 매칭되는 실제 Color를 반환합니다.
    /// </summary>
    private Color GetColorFromEnum(EDoorColor doorColor)
    {
        return doorColor switch
        {
            EDoorColor.Green => _greenColor,
            EDoorColor.Yellow => _yellowColor,
            EDoorColor.Red => _redColor,
            _ => Color.white
        };
    }

    public void SetActive(bool active)
    {
        gameObject.SetActive(active);
    }
}