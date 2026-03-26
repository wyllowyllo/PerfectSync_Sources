using UnityEngine;

[RequireComponent(typeof(Collider))]
public class RaceCheckpoint : MonoBehaviour
{
    [SerializeField] private int _checkpointIndex;

    public int CheckpointIndex => _checkpointIndex;

    private void OnTriggerEnter(Collider other)
    {
        var tracker = other.GetComponentInParent<RaceProgressTracker>();
        if (tracker == null) return;

       
        tracker.PassCheckpoint(_checkpointIndex);
    }
}
