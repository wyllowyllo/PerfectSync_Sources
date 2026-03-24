using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovementTest : MonoBehaviour
{
    [Header("Player Settings")]
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _jumpForce = 7f;
    
    [Header("Ground Check Settings")]
    [SerializeField] private float _groundCheckDistance = 1.1f;
    [SerializeField] private LayerMask _groundLayerMask;
    
    private Rigidbody _rigidbody;
    private Vector2 _moveInput;
    private bool _isJumpQueued;
    
    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
    }

    public void OnMove(InputValue value)
    {
        _moveInput = value.Get<Vector2>();
    }
    
    public void OnJump(InputValue value)
    {
        if (!value.isPressed) return;
        _isJumpQueued = true;
    }

    private void FixedUpdate()
    {
        var moveDirection = new Vector3(_moveInput.x, 0f, _moveInput.y);
        var targetVelocity = moveDirection * _moveSpeed;
        
        var currentVelocityY = _rigidbody.linearVelocity.y;
        
        if (_isJumpQueued)
        {
            if (IsGrounded())
            {
                currentVelocityY = _jumpForce;
            }
            _isJumpQueued = false; 
        }
        
        _rigidbody.linearVelocity = new Vector3(targetVelocity.x, currentVelocityY, targetVelocity.z);
    }
    
    private bool IsGrounded()
    {
        return Physics.Raycast(transform.position, Vector3.down, _groundCheckDistance, _groundLayerMask);
    }
}
