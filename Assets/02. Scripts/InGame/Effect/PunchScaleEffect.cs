using DG.Tweening;
using UnityEngine;

namespace InGame.Effect
{
    public class PunchScaleEffect : MonoBehaviour
    {
        [Header("Punch Settings")]
        [SerializeField] private Vector3 _punch = new Vector3(0f, -0.3f, 0f);
        [SerializeField] private float _duration = 0.3f;
        [SerializeField] private int _vibrato = 6;
        [SerializeField] private float _elasticity = 0.5f;

        [Header("Collision")]
        [SerializeField] private LayerMask _collisionLayers;

        private Vector3 _originalScale;
        private Tween _activeTween;

        private void Awake()
        {
            _originalScale = transform.localScale;
        }

        public void Play()
        {
            _activeTween?.Kill();
            transform.localScale = _originalScale;
            _activeTween = transform.DOPunchScale(_punch, _duration, _vibrato, _elasticity);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if ((_collisionLayers & (1 << collision.gameObject.layer)) == 0) return;

            Play();
        }

        private void OnDestroy()
        {
            _activeTween?.Kill();
        }
    }
}
