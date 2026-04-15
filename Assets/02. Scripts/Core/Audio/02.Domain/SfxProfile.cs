using UnityEngine;

[CreateAssetMenu(fileName = "NewSfxProfile", menuName = "Audio/SFX Profile")]
public class SfxProfile : ScriptableObject
{
    [Tooltip("재생할 클립 풀. 매 재생마다 이 배열에서 무작위로 하나가 선택됩니다. 이벤트성 UI 사운드는 1개, 반복 사운드는 3~5개 권장.")]
    [SerializeField] private AudioClip[] _clips;

    [Tooltip("이 프로필의 최종 볼륨 배수. 실제 재생 볼륨 = Master × Sfx × VolumeScale. 1이 기본, 2가 최대.")]
    [SerializeField, Range(0f, 2f)] private float _volumeScale = 1f;

    [Tooltip("랜덤 피치 최소값. PitchMin과 PitchMax 사이에서 매 재생마다 무작위로 선택되어 반복음을 덜 기계적으로 만듭니다.")]
    [SerializeField, Range(0.5f, 2f)] private float _pitchMin = 1f;

    [Tooltip("랜덤 피치 최대값. PitchMin과 동일하게 두면 피치 랜덤이 꺼집니다.")]
    [SerializeField, Range(0.5f, 2f)] private float _pitchMax = 1f;

    [Tooltip("같은 호출자(caller)가 이 프로필을 연속 재생할 수 없는 최소 간격(초). 버튼 연타 등의 플러드를 막습니다.")]
    [SerializeField, Range(0f, 1f)] private float _cooldown = 0.05f;

    public float VolumeScale => _volumeScale;
    public float Cooldown => _cooldown;

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
}
