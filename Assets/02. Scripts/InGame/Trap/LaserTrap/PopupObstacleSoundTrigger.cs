using InGame.Audio;
using UnityEngine;

public class PopupObstacleSoundTrigger : MonoBehaviour
{
    [SerializeField] private SpatialSfxProfile _popupProfile;
    [SerializeField] private SpatialSfxProfile _retractProfile;

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
        InGameSfxManager.Instance?.EmitSpatialOn(_popupProfile, transform, this);
    }

    private void HandleResetComplete()
    {
        InGameSfxManager.Instance?.EmitSpatialOn(_retractProfile, transform, this);
    }
}
