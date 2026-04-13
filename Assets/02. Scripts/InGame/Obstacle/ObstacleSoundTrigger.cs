using InGame.Audio;
using UnityEngine;

namespace InGame.Obstacle
{
    public class ObstacleSoundTrigger : MonoBehaviour
    {
        [SerializeField] private SpatialSfxProfile _destroyProfile;
        [SerializeField] private SpatialSfxProfile _respawnProfile;

        private DestroyableObstacle _obstacle;

        private void Awake()
        {
            _obstacle = GetComponent<DestroyableObstacle>();
        }

        private void OnEnable()
        {
            if (_obstacle == null)
                return;

            _obstacle.OnDestroyed += HandleDestroyed;
            _obstacle.OnRespawned += HandleRespawned;
        }

        private void OnDisable()
        {
            if (_obstacle == null)
                return;

            _obstacle.OnDestroyed -= HandleDestroyed;
            _obstacle.OnRespawned -= HandleRespawned;
        }

        private void HandleDestroyed()
        {
            InGameSfxManager.Instance?.EmitSpatialAt(_destroyProfile, transform.position, this);
        }

        private void HandleRespawned()
        {
            InGameSfxManager.Instance?.EmitSpatialAt(_respawnProfile, transform.position, this);
        }
    }
}
