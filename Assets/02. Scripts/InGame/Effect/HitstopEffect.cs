using DG.Tweening;
using UnityEngine;

namespace InGame.Effect
{
    /// <summary>
    /// 무적 충돌 시 히트스톱(프레임 프리즈) + 슬로우모션 복귀.
    /// 모든 효과는 로컬 연출이며 네트워크 동기화 불필요.
    /// InGame 씬에 싱글톤으로 배치.
    /// </summary>
    public class HitstopEffect : SingletonMonoBehaviour<HitstopEffect>
    {
        protected override bool PersistAcrossScenes => false;

        [Header("Heavy (장애물 파괴)")]
        [SerializeField] private float _heavyFreezeDuration = 0.05f;
        [SerializeField] private float _heavySlowmoDuration = 0.08f;
        [SerializeField, Range(0.01f, 1f)] private float _heavySlowmoTimeScale = 0.3f;

        [Header("Light (플레이어 넉백)")]
        [SerializeField] private float _lightFreezeDuration = 0.03f;
        [SerializeField] private float _lightSlowmoDuration = 0.05f;
        [SerializeField, Range(0.01f, 1f)] private float _lightSlowmoTimeScale = 0.4f;

        private Tween _timeScaleTween;

        public void PlayHeavy() => Play(_heavyFreezeDuration, _heavySlowmoDuration, _heavySlowmoTimeScale);
        public void PlayLight() => Play(_lightFreezeDuration, _lightSlowmoDuration, _lightSlowmoTimeScale);

        private void Play(float freezeDuration, float slowmoDuration, float slowmoTimeScale)
        {
            _timeScaleTween?.Kill();
            Time.timeScale = 1f;

            Time.timeScale = 0f;

            _timeScaleTween = DOTween.Sequence()
                .AppendInterval(freezeDuration)
                .AppendCallback(() => Time.timeScale = slowmoTimeScale)
                .Append(
                    DOTween.To(() => Time.timeScale, v => Time.timeScale = v, 1f, slowmoDuration)
                        .SetEase(Ease.OutQuad)
                )
                .SetUpdate(true);
        }

        protected override void OnDestroy()
        {
            _timeScaleTween?.Kill();
            Time.timeScale = 1f;

            base.OnDestroy();
        }
    }
}
