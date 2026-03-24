using UnityEngine;

public class Billboard : MonoBehaviour
{
    private Transform _cameraTransform;

    private void Start()
    {
        _cameraTransform = Camera.main.transform;
    }

    private void LateUpdate()
    {
        if (_cameraTransform == null) return;
        transform.forward = _cameraTransform.forward;
    }
}
