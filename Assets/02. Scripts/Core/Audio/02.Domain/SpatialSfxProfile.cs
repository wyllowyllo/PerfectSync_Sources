using UnityEngine;

[CreateAssetMenu(fileName = "NewSpatialSfxProfile", menuName = "Audio/Spatial SFX Profile")]
public class SpatialSfxProfile : ScriptableObject
{
    [Header("Clip Settings")]
    [Tooltip("재생할 클립 풀. 매 재생마다 이 배열에서 무작위로 하나가 선택됩니다. 반복음 피로를 줄이려면 3~5개 권장.")]
    [SerializeField] private AudioClip[] _clips;

    [Tooltip("이 프로필의 최종 볼륨 배수. 실제 재생 볼륨 = Master × Sfx × VolumeScale. 1이 기본, 2가 최대.")]
    [SerializeField, Range(0f, 2f)] private float _volumeScale = 1f;

    [Tooltip("랜덤 피치 최소값. PitchMin과 PitchMax 사이에서 매 재생마다 무작위로 선택되어 반복음을 덜 기계적으로 만듭니다.")]
    [SerializeField, Range(0.5f, 2f)] private float _pitchMin = 1f;

    [Tooltip("랜덤 피치 최대값. PitchMin과 동일하게 두면 피치 랜덤이 꺼집니다.")]
    [SerializeField, Range(0.5f, 2f)] private float _pitchMax = 1f;

    [Tooltip("같은 호출자(caller)가 이 프로필을 연속 재생할 수 없는 최소 간격(초). 연타/난사 사운드 플러드를 막습니다.")]
    [SerializeField, Range(0f, 1f)] private float _cooldown = 0.05f;

    [Header("Spatial Settings")]
    [Tooltip("이 거리(m) 이내에서는 감쇠 없이 풀 볼륨으로 재생됩니다. 플레이어 사운드는 카메라 오프셋을 고려해 5~8 권장.")]
    [SerializeField] private float _minDistance = 1f;

    [Tooltip("이 거리(m)를 넘으면 거의 무음(Linear)이 되는 감쇠 한계. 맵 크기에 맞춰 설정.")]
    [SerializeField] private float _maxDistance = 30f;

    [Tooltip("MinDistance와 MaxDistance 사이 구간의 감쇠 커브 종류. Linear=직관적·예측 가능, Logarithmic=현실적·근거리 강조, Custom=커브 에디터로 수동 조정.")]
    [SerializeField] private AudioRolloffMode _rolloffMode = AudioRolloffMode.Linear;

    [Tooltip("클립을 반복 재생할지 여부. true면 StopSpatial(handle) 호출 전까지 무한 재생됩니다. 지속 사운드(엔진, 스핀 루프 등)에 사용.")]
    [SerializeField] private bool _loop;

    [Tooltip("루프 재생 시작 시 0에서 목표 볼륨까지 서서히 올리는 시간(초). 0이면 즉시 재생. Loop=true일 때만 의미가 있습니다.")]
    [SerializeField, Range(0f, 5f)] private float _fadeInDuration = 0f;

    [Tooltip("StopSpatial 호출 시 목표 볼륨에서 0까지 서서히 줄이는 시간(초). 0이면 즉시 정지. Loop=true일 때만 의미가 있습니다.")]
    [SerializeField, Range(0f, 5f)] private float _fadeOutDuration = 0f;

    public float VolumeScale => _volumeScale;
    public float Cooldown => _cooldown;
    public bool Loop => _loop;
    public float FadeInDuration => _fadeInDuration;
    public float FadeOutDuration => _fadeOutDuration;

    public AudioClip GetRandomClip()
    {
        if (_clips == null || _clips.Length == 0)
            return null;

        return _clips[Random.Range(0, _clips.Length)];
    }

    public float GetRandomPitch()
    {
        return Random.Range(_pitchMin, _pitchMax);
    }

    public void ConfigureSource(AudioSource source)
    {
        source.spatialBlend = 1f;
        source.minDistance = _minDistance;
        source.maxDistance = _maxDistance;
        source.rolloffMode = _rolloffMode;
        source.spread = 0f;
        source.dopplerLevel = 0f;
    }
}
