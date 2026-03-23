using UnityEngine;

[RequireComponent(typeof(CharacterController), typeof(PlayerController))]
public class PlayerMoveAbility : PlayerAbility
{
    [Header("Movement")]
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _jumpPower = 5f;
    [SerializeField] private float _gravity = -9.81f;

    private CharacterController _characterController;
    private float _verticalVelocity;

    protected override void Awake()
    {
        base.Awake();
        _characterController = GetComponent<CharacterController>();
    }

    private void Update()
    {
        if (!Owner.PhotonView.IsMine) return;
        if (!InGameManager.IsLocalPlayerControllable) return;

        Vector3 direction = GetMoveDirection();
        HandleJumpAndGravity();

        direction.y = _verticalVelocity;
        _characterController.Move(direction * _moveSpeed * Time.deltaTime);
    }

    private Vector3 GetMoveDirection()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        return (transform.forward * v + transform.right * h).normalized;
    }

    private void HandleJumpAndGravity()
    {
        if (_characterController.isGrounded)
        {
            _verticalVelocity = -0.5f;

            if (Input.GetKeyDown(KeyCode.Space))
                _verticalVelocity = _jumpPower;
        }
        else
        {
            _verticalVelocity += _gravity * Time.deltaTime;
        }
    }
}
