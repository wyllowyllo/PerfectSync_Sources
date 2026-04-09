using UnityEngine;
using UnityEngine.UI;

public class UIMaskToggle : MonoBehaviour
{
    [SerializeField] private Mask _mask;

    public void SetMaskEnabled(bool enabled)
    {
        if (_mask == null)
            return;

        _mask.enabled = enabled;
    }

    public void EnableMask()
    {
        SetMaskEnabled(true);
    }

    public void DisableMask()
    {
        SetMaskEnabled(false);
    }
}