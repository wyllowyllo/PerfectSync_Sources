using UnityEngine;

public class LaserSoundTrigger : MonoBehaviour
{
    [SerializeField] private SpatialSfxPlayer _detectionSfx;

    private LaserTrigger _laser;

    private void Awake()
    {
        _laser = GetComponent<LaserTrigger>();
    }

    private void OnEnable()
    {
        if (_laser != null)
            _laser.OnPlayerDetected += HandlePlayerDetected;
    }

    private void OnDisable()
    {
        if (_laser != null)
            _laser.OnPlayerDetected -= HandlePlayerDetected;
    }

    private void HandlePlayerDetected()
    {
        if (_detectionSfx != null)
            _detectionSfx.Play();
    }
}
