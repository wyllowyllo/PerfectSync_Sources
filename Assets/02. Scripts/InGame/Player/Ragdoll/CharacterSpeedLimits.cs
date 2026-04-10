using UnityEngine;

namespace InGame.Player.Ragdoll
{
    /// <summary>
    /// 캐릭터 물리 속도 프로필.
    /// 매카님/래그돌 공통 속도 천장을 단일 SO에서 관리.
    /// 네트워크 30Hz 기준 스냅샷 간 거리 = 속도 × 0.033s.
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterSpeedLimits", menuName = "Game/Character Speed Limits")]
    public class CharacterSpeedLimits : ScriptableObject
    {
        [Header("매카님")]
        [Tooltip("매카님 전체 속도 천장 (m/s). Y축 및 벡터 크기 클램프 모두에 적용.")]
        [SerializeField] private float _maxBodySpeed = 50f;

        [Tooltip("트램폴린 발사 최대 속도 (m/s).")]
        [SerializeField] private float _maxLaunchSpeed = 60f;

        [Header("래그돌")]
        [Tooltip("래그돌 본별 최대 속도 (m/s). 낮을수록 네트워크 부드러움 ↑, 연출 역동성 ↓")]
        [SerializeField] private float _maxBoneSpeed = 20f;

        [Tooltip("래그돌 진입 시 상속 가능한 최대 속도 (m/s).")]
        [SerializeField] private float _maxInheritedSpeed = 18f;

        public float MaxBodySpeed => _maxBodySpeed;
        public float MaxLaunchSpeed => _maxLaunchSpeed;
        public float MaxBoneSpeed => _maxBoneSpeed;
        public float MaxBoneSpeedSqr => _maxBoneSpeed * _maxBoneSpeed;
        public float MaxInheritedSpeed => _maxInheritedSpeed;
    }
}
