using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class UISfxOnClick : MonoBehaviour
{
    [SerializeField] private SfxProfile _profile;

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(Play);
    }

    public void Play()
    {
        if (_profile == null)
            return;

        AudioClip clip = _profile.GetRandomClip();
        if (clip == null)
            return;

        AudioManager.Instance?.Play(AudioType.Sfx, clip);
    }
}
