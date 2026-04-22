using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIButtonSfxAutoHook : MonoBehaviour
{
    [SerializeField] private SfxProfile _defaultClickProfile;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void Start()
    {
        WireButtonsInScene(SceneManager.GetActiveScene());
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        WireButtonsInScene(scene);
    }

    public void WireButton(Button button)
    {
        if (button == null || _defaultClickProfile == null)
            return;

        // 수동 override: UISfxOnClick이 붙은 버튼은 그 쪽이 담당.
        if (button.GetComponent<UISfxOnClick>() != null)
            return;

        // 이중 후킹 방지.
        if (button.TryGetComponent<UISfxAutoHookMarker>(out _))
            return;

        button.gameObject.AddComponent<UISfxAutoHookMarker>();
        button.onClick.AddListener(PlayDefault);
    }

    private void WireButtonsInScene(Scene scene)
    {
        if (_defaultClickProfile == null)
            return;

        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button.gameObject.scene != scene)
                continue;
            WireButton(button);
        }
    }

    private void PlayDefault()
    {
        AudioClip clip = _defaultClickProfile.GetRandomClip();
        if (clip == null)
            return;

        AudioManager.Instance?.Play(AudioType.Sfx, clip);
    }
}

internal class UISfxAutoHookMarker : MonoBehaviour { }
