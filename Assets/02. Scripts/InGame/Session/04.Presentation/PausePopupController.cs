using UnityEngine;

public class PausePopupController : MonoBehaviour
{
    [SerializeField] private GameObject _pausePopup;

    private void Start()
    {
        if (_pausePopup != null)
            _pausePopup.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            TogglePopup();
    }

    public void OpenPopup() => SetPopupActive(true);
    public void ClosePopup() => SetPopupActive(false);

    public void TogglePopup()
    {
        if (_pausePopup == null) return;
        SetPopupActive(!_pausePopup.activeSelf);
    }

    private void SetPopupActive(bool active)
    {
        if (_pausePopup != null)
            _pausePopup.SetActive(active);

        InGameManager.Instance?.SetLocalPaused(active);
    }
}
