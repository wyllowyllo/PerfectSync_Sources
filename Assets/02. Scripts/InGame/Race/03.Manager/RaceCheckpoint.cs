using UnityEngine;

[RequireComponent(typeof(Collider))]
public class RaceCheckpoint : MonoBehaviour
{
    [SerializeField] private int _checkpointIndex;
    [SerializeField] private Transform _respawnPoint;

    public int CheckpointIndex => _checkpointIndex;
    public Vector3 RespawnPosition => _respawnPoint != null
        ? _respawnPoint.position
        : transform.position;
    public Quaternion RespawnRotation => _respawnPoint != null
        ? _respawnPoint.rotation
        : transform.rotation;

    private void OnTriggerEnter(Collider other)
    {
        var tracker = other.GetComponentInParent<RaceProgressTracker>();
        if (tracker == null) return;

        tracker.PassCheckpoint(_checkpointIndex);
    }

    private void OnDrawGizmos()
    {
        Vector3 position = RespawnPosition;
        Quaternion rotation = RespawnRotation;

        Gizmos.color = Color.green;

        // 리스폰 위치 표시.
        Gizmos.DrawWireSphere(position, 0.5f);

        // 리스폰 방향 화살표.
        Gizmos.DrawRay(position, rotation * Vector3.forward * 1.5f);

        // 체크포인트 → 리스폰 지점 연결선.
        Gizmos.DrawLine(transform.position, position);
    }
}
