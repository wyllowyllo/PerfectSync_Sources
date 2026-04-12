using UnityEngine;

public class PopupObstacleSoundTrigger : MonoBehaviour
{
    [SerializeField] private SpatialSfxPlayer _popupSfx;
    [SerializeField] private SpatialSfxPlayer _retractSfx;

    private PopupObstacle _popup;

    private void Awake()
    {
        _popup = GetComponent<PopupObstacle>();
    }

    private void OnEnable()
    {
        if (_popup == null)
            return;

        _popup.OnActivated += HandleActivated;
        _popup.OnResetComplete += HandleResetComplete;
    }

    private void OnDisable()
    {
        if (_popup == null)
            return;

        _popup.OnActivated -= HandleActivated;
        _popup.OnResetComplete -= HandleResetComplete;
    }

    private void HandleActivated()
    {
        if (_popupSfx != null)
            _popupSfx.Play();
    }

    private void HandleResetComplete()
    {
        if (_retractSfx != null)
            _retractSfx.Play();
    }
}
