using UnityEngine;

public class CeremonyReturnUI : MonoBehaviour
{
    [SerializeField] private GameObject _returnPromptRoot;

    private void Awake()
    {
        if (_returnPromptRoot != null)
            _returnPromptRoot.SetActive(false);
    }

    public void Show()
    {
        if (_returnPromptRoot != null)
            _returnPromptRoot.SetActive(true);
    }
}
