using DG.Tweening;
using UnityEngine;

namespace InGame.Obstacle
{
    /// <summary>
    /// 무적 플레이어 접근 시 freeze되는 장애물에 부착하는 컴포넌트.
    /// 충돌 기믹에만 부착하고, 단순 회전 발판 등에는 부착하지 않는다.
    /// Freeze 시 _movementScripts를 disabled 처리하여 동작을 멈춘다.
    /// IRecoilSource를 구현한 movement script가 있으면 자동으로 반동 방향을 계산한다.
    /// </summary>
    public class FreezableObstacle : MonoBehaviour, IFreezable
    {
        [Tooltip("Freeze 시 비활성화할 동작 스크립트 (RotationScript 등)")]
        [SerializeField] private MonoBehaviour[] _movementScripts;

        [Header("Recoil")]
        [SerializeField] private float _recoilStrength = 8f;
        [SerializeField] private float _recoilDuration = 0.3f;
        [SerializeField] private int _recoilVibrato = 5;
        [SerializeField] private float _recoilElasticity = 0.3f;

        private bool _isFrozen;
        private Tween _recoilTween;
        private IRecoilSource _recoilSource;

        public bool IsFrozen => _isFrozen;

        private void Awake()
        {
            foreach (var script in _movementScripts)
            {
                if (script is IRecoilSource source)
                {
                    _recoilSource = source;
                    break;
                }
            }
        }

        public void Freeze()
        {
            _isFrozen = true;

            foreach (var script in _movementScripts)
            {
                if (script != null)
                    script.enabled = false;
            }

            PlayRecoil();
        }

        public void Unfreeze()
        {
            KillRecoil();
            _isFrozen = false;

            foreach (var script in _movementScripts)
            {
                if (script != null)
                    script.enabled = true;
            }
        }

        private void PlayRecoil()
        {
            if (_recoilSource == null) return;

            Vector3 punch = _recoilSource.GetRecoilDirection() * _recoilStrength;

            _recoilTween?.Kill();
            _recoilTween = _recoilSource.IsRotational
                ? transform.DOPunchRotation(punch, _recoilDuration, _recoilVibrato, _recoilElasticity)
                : transform.DOPunchPosition(punch, _recoilDuration, _recoilVibrato, _recoilElasticity);
        }

        private void KillRecoil()
        {
            _recoilTween?.Kill();
            _recoilTween = null;
        }

        private void OnDestroy()
        {
            _recoilTween?.Kill();
        }
    }
}
