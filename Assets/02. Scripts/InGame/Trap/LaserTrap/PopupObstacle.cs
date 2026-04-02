using InGame.Obstacle;
using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PopupObstacle : MonoBehaviour, ITrap, IRecoilSource, IPunObservable
{
    private enum EState { Idle, Attacking, Retracting }

    [Header("Movement Settings")]
    [Tooltip("장애물이 도달할 목표 지점")]
    [SerializeField] private Transform _targetTransform;

    [Tooltip("튀어나올 때 소요 시간")]
    [SerializeField] private float _popupDuration = 0.05f;

    [Tooltip("복귀할 때 소요 시간")]
    [SerializeField] private float _retractDuration = 1.0f;

    private const float NetworkCorrectionFactor = 0.2f;

    private Rigidbody _rb;
    private PhotonView _view;
    private Vector3 _startWorldPosition;
    private Vector3 _targetWorldPosition;
    private Vector3 _networkPosition;
    private Vector3 _moveOrigin;
    private EState _state = EState.Idle;
    private float _elapsedTime;

    // LaserTrapMovementSync가 리플렉션으로 접근한다. 필드명 변경 금지.
    private bool _isActionProcess;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.isKinematic = true;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;

        _view = GetComponent<PhotonView>();

        _startWorldPosition = transform.position;
        _targetWorldPosition = _targetTransform.position;
        _networkPosition = _rb.position;
    }

    // Freeze 해제 시 잔여 이동 상태가 있으면 시작 위치로 복귀한다.
    private void OnEnable()
    {
        if (_state != EState.Idle)
        {
            _state = EState.Retracting;
            _isActionProcess = true;
            _elapsedTime = 0f;
            _moveOrigin = _rb.position;
        }
    }

    private void FixedUpdate()
    {
        bool isMine = _view == null || _view.IsMine;

        if (_state != EState.Idle)
        {
            bool isAttacking = _state == EState.Attacking;
            float duration = isAttacking ? _popupDuration : _retractDuration;
            Vector3 destination = isAttacking ? _targetWorldPosition : _startWorldPosition;

            _elapsedTime += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(_elapsedTime / duration);
            float easedT = isAttacking ? Mathf.Sin(t * Mathf.PI * 0.5f) : t * t;

            Vector3 localPosition = Vector3.Lerp(_moveOrigin, destination, easedT);

            if (isMine)
            {
                _rb.MovePosition(localPosition);
            }
            else
            {
                _rb.MovePosition(
                    Vector3.Lerp(localPosition, _networkPosition, NetworkCorrectionFactor));
            }

            if (t >= 1f)
            {
                _state = EState.Idle;
                _isActionProcess = false;
            }
        }
        else if (!isMine)
        {
            _rb.MovePosition(
                Vector3.Lerp(_rb.position, _networkPosition, NetworkCorrectionFactor));
        }
    }

    // ── ITrap ────────────────────────────────────────────────

    public void Activate()
    {
        if (_state != EState.Idle) return;

        _state = EState.Attacking;
        _isActionProcess = true;
        _elapsedTime = 0f;
        _moveOrigin = _rb.position;
    }

    public void Reset()
    {
        if (_state != EState.Idle) return;

        _state = EState.Retracting;
        _isActionProcess = true;
        _elapsedTime = 0f;
        _moveOrigin = _rb.position;
    }

    // ── IRecoilSource ────────────────────────────────────────

    public bool IsRotational => false;

    public Vector3 GetRecoilDirection()
    {
        return -(_targetWorldPosition - _startWorldPosition).normalized;
    }

    // ── IPunObservable ───────────────────────────────────────

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(_rb.position);
        }
        else
        {
            _networkPosition = (Vector3)stream.ReceiveNext();
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_targetTransform == null) return;

        Gizmos.color = Color.red;

        Vector3 targetWorldPos = Application.isPlaying
            ? _targetWorldPosition
            : _targetTransform.position;

        Gizmos.DrawLine(transform.position, targetWorldPos);

        Gizmos.matrix = Matrix4x4.TRS(targetWorldPos, transform.rotation, transform.localScale);

        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
    }
#endif
}
