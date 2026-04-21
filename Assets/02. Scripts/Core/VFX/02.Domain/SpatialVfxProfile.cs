using UnityEngine;

namespace Core.VFX
{
    [CreateAssetMenu(fileName = "NewSpatialVfxProfile", menuName = "VFX/Spatial VFX Profile")]
    public class SpatialVfxProfile : ScriptableObject
    {
        [Header("Prefab")]
        [Tooltip("재생할 VFX 프리팹. 루트 이하에 하나 이상의 ParticleSystem을 포함해야 합니다.")]
        [SerializeField] private GameObject _prefab;

        [Header("Playback")]
        [Tooltip("같은 호출자(caller)가 이 프로필을 연속 재생할 수 없는 최소 간격(초). 버튼 연타/충돌 플러드를 막습니다.")]
        [SerializeField, Range(0f, 1f)] private float _cooldown = 0.05f;

        [Tooltip("재생 시 스폰되는 인스턴스의 로컬 스케일 최소값. 같은 이펙트의 반복 재생을 덜 기계적으로 만듭니다.")]
        [SerializeField, Range(0.1f, 3f)] private float _scaleMin = 1f;

        [Tooltip("재생 시 스폰되는 인스턴스의 로컬 스케일 최대값. _scaleMin과 같으면 스케일 랜덤이 꺼집니다.")]
        [SerializeField, Range(0.1f, 3f)] private float _scaleMax = 1f;

        [Tooltip("0보다 크면 이 시간(초) 후 강제 회수. 0이면 prefab 하위 모든 ParticleSystem 중 main.duration + startLifetime.constantMax의 최댓값을 사용합니다.")]
        [SerializeField] private float _lifetimeOverride = 0f;

        [Tooltip("true면 EmitOn 호출 시 타겟 Transform을 매 프레임 추적. false면 호출 시점 위치에 고정.")]
        [SerializeField] private bool _followTarget = false;

        [Tooltip("호출자가 전달한 위치에 더해지는 월드 공간 오프셋. 프리팹 피벗 보정/머리 위 스폰 등 프로필별 고유 스폰 보정에 사용.")]
        [SerializeField] private Vector3 _offset = Vector3.zero;

        public GameObject Prefab => _prefab;
        public float Cooldown => _cooldown;
        public bool FollowTarget => _followTarget;
        public Vector3 Offset => _offset;

        public float GetRandomScale()
        {
            return Random.Range(_scaleMin, _scaleMax);
        }

        public float GetEffectiveLifetime()
        {
            if (_lifetimeOverride > 0f)
                return _lifetimeOverride;

            if (_prefab == null)
                return 1f;

            var systems = _prefab.GetComponentsInChildren<ParticleSystem>(true);
            if (systems.Length == 0)
                return 1f;

            float max = 0f;
            for (int i = 0; i < systems.Length; i++)
            {
                var main = systems[i].main;
                float candidate = main.duration + main.startLifetime.constantMax;
                if (candidate > max)
                    max = candidate;
            }
            return max;
        }
    }
}
