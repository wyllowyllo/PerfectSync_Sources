using UnityEngine;
using UnityEngine.Splines;

[RequireComponent(typeof(SplineContainer))]
public class RaceSegment : MonoBehaviour
{
    [SerializeField] private int _fromCheckpoint;
    [SerializeField] private int _toCheckpoint;

    public int FromCheckpoint => _fromCheckpoint;
    public int ToCheckpoint => _toCheckpoint;
    public SplineContainer SplineContainer { get; private set; }

    private void Awake()
    {
        SplineContainer = GetComponent<SplineContainer>();
    }
}
