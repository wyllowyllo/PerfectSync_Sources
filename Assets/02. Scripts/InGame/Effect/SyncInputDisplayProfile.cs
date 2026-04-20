using UnityEngine;

namespace InGame.Effect
{
    [CreateAssetMenu(fileName = "SyncInputDisplayProfile", menuName = "InGame/Sync Input Display Profile")]
    public class SyncInputDisplayProfile : ScriptableObject
    {
        [Header("Colors (알파 포함 — 자신은 반투명, 팀원은 선명)")]
        [Tooltip("로컬 플레이어 자신(안쪽 링/화살표) 색상. 알파를 낮춰 반투명 처리")]
        [SerializeField] private Color _selfColor = new Color(1f, 0.3f, 0.3f, 0.5f);
        [Tooltip("팀원(바깥쪽 링/화살표) 색상. 알파 1.0으로 선명하게")]
        [SerializeField] private Color _teammateColor = new Color(0.3f, 0.5f, 1f, 1f);

        [Header("Fade (래그돌 상태 전환)")]
        [Tooltip("메카님 복귀(블렌드 완료) 시 서서히 나타나는 시간")]
        [SerializeField] private float _fadeInDuration = 0.3f;
        [Tooltip("래그돌 진입 시 빠르게 사라지는 시간")]
        [SerializeField] private float _fadeOutDuration = 0.15f;

        [Header("Arrow Rotation Smoothing")]
        [Tooltip("화살표 헤드가 목표 각도로 따라가는 속도 (도/초). 값이 클수록 빠르게 정착")]
        [SerializeField] private float _arrowRotateSpeed = 720f;

        public Color SelfColor => _selfColor;
        public Color TeammateColor => _teammateColor;
        public float FadeInDuration => _fadeInDuration;
        public float FadeOutDuration => _fadeOutDuration;
        public float ArrowRotateSpeed => _arrowRotateSpeed;
    }
}
