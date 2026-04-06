using DG.Tweening;
using UnityEngine;

namespace InGame.Effect
{
    /// <summary>
    /// 장애물 파괴 시 히트스톱(프레임 프리즈) + 슬로우모션 복귀.
    /// 모든 효과는 로컬 연출이며 네트워크 동기화 불필요.
    /// InGame 씬에 싱글톤으로 배치.
    /// </summary>
    public class HitstopEffect : SingletonMonoBehaviour<HitstopEffect>
    {
        protected override bool PersistAcrossScenes => false;

        [Header("Hitstop")]
        [Tooltip("완전 정지 시간 (초, unscaled)")]
        [SerializeField] private float _freezeDuration = 0.05f;

        [Tooltip("정지 후 슬로우모션 복귀 시간 (초, unscaled)")]
        [SerializeField] private float _slowmoDuration = 0.08f;

        [Tooltip("슬로우모션 시작 타임스케일")]
        [SerializeField, Range(0.01f, 1f)] private float _slowmoTimeScale = 0.3f;

        private Tween _timeScaleTween;

        public void Play()
        {
            _timeScaleTween?.Kill();
            Time.timeScale = 1f;

            Time.timeScale = 0f;

            _timeScaleTween = DOTween.Sequence()
                .AppendInterval(_freezeDuration)
                .AppendCallback(() => Time.timeScale = _slowmoTimeScale)
                .Append(
                    DOTween.To(() => Time.timeScale, v => Time.timeScale = v, 1f, _slowmoDuration)
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
