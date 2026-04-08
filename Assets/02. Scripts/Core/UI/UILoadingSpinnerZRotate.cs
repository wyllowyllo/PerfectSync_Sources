using UnityEngine;

/// <summary>
/// 활성화 동안 local Z 축 기준으로 회전합니다. 비활성화 시 초기 localEulerAngles로 되돌립니다.
/// </summary>
public class UILoadingSpinnerZRotate : MonoBehaviour
{
    [SerializeField] private float _degreesPerSecond = -360f;

    private Vector3 _initialLocalEuler;
    private bool _cachedInitial;

    private void Awake()
    {
        CacheInitialRotation();
    }

    private void OnEnable()
    {
        if (!_cachedInitial)
            CacheInitialRotation();
    }

    private void Update()
    {
        Vector3 e = transform.localEulerAngles;
        e.z += _degreesPerSecond * Time.unscaledDeltaTime;
        transform.localEulerAngles = e;
    }

    private void OnDisable()
    {
        transform.localEulerAngles = _initialLocalEuler;
    }

    private void CacheInitialRotation()
    {
        _initialLocalEuler = transform.localEulerAngles;
        _cachedInitial = true;
    }
}
