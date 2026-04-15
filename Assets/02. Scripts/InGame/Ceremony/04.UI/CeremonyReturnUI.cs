using UnityEngine;

public class CeremonyReturnUI : MonoBehaviour
{
    [SerializeField] private GameObject _returnPromptRoot;

    private void Awake()
    {
        if (_returnPromptRoot != null)
            _returnPromptRoot.SetActive(false);
    }

    private void OnEnable()
    {
        CeremonyManager.OnReturnUIShowRequested += Show;
    }

    private void OnDisable()
    {
        CeremonyManager.OnReturnUIShowRequested -= Show;
    }

    public void Show()
    {
        if (_returnPromptRoot != null)
            _returnPromptRoot.SetActive(true);
    }
}
