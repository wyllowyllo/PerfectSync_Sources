using UnityEngine;

public class SceneBgmPlayer : MonoBehaviour
{
    [SerializeField] private string _bgmAddress;
    [SerializeField, Range(0f, 5f)] private float _crossfadeDuration = 1f;

    private void Start()
    {
        if (!string.IsNullOrEmpty(_bgmAddress))
            AudioManager.Instance?.PlayBgmByAddress(_bgmAddress, _crossfadeDuration);
    }
}
