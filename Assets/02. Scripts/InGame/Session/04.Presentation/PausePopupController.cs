using UnityEngine;

public class PausePopupController : MonoBehaviour
{
    [SerializeField] private GameObject _pausePopup;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePopup();
        }
    }

    private void TogglePopup()
    {
        if (_pausePopup != null)
            _pausePopup.SetActive(!_pausePopup.activeSelf);
    }
}
