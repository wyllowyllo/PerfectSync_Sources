using UnityEngine;

namespace InGame.Obstacle
{
    public class ObstacleSoundTrigger : MonoBehaviour
    {
        [SerializeField] private SpatialSfxPlayer _destroySfx;
        [SerializeField] private SpatialSfxPlayer _respawnSfx;

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
            if (_destroySfx != null)
                _destroySfx.Play();
        }

        private void HandleRespawned()
        {
            if (_respawnSfx != null)
                _respawnSfx.Play();
        }
    }
}
