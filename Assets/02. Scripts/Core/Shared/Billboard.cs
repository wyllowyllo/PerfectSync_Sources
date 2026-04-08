using UnityEngine;

public class BillboardUI : MonoBehaviour
{
    private Transform _cameraTransform;

    private void LateUpdate()
    {
        if (_cameraTransform == null)
            _cameraTransform = Camera.main?.transform;
        if (_cameraTransform == null) return;
        transform.forward = _cameraTransform.forward;
    }
}
