using InGame.Audio;
using UnityEngine;

public class LaserSoundTrigger : MonoBehaviour
{
    [SerializeField] private SpatialSfxProfile _detectionProfile;

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
        InGameSfxManager.Instance?.EmitSpatialAt(_detectionProfile, transform.position, this);
    }
}
