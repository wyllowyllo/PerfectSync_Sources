using UnityEngine;

public class InGameCanvasController : MonoBehaviour
{
    [SerializeField] private GameObject _racingUI;
    [SerializeField] private GameObject _ceremonyUI;

    private void Awake()
    {
        if (_racingUI != null) _racingUI.SetActive(true);
        if (_ceremonyUI != null) _ceremonyUI.SetActive(false);
    }

    private void Start()
    {
        if (InGameManager.Instance != null)
            InGameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
    }

    private void OnDestroy()
    {
        if (InGameManager.Instance != null)
            InGameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
    }

    private void HandleGameStateChanged(GameState state)
    {
        if (state != GameState.Ceremony) return;

        if (_racingUI != null) _racingUI.SetActive(false);
        if (_ceremonyUI != null) _ceremonyUI.SetActive(true);
    }
}
