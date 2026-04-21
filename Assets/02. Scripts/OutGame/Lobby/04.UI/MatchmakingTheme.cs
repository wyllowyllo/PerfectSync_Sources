using UnityEngine;

public class MatchmakingTheme : MonoBehaviour
{
    [SerializeField, Range(0f, 5f)] private float _crossfadeDuration = 1f;

    private void OnEnable()
    {
        ReadyButton.OnReadyStateChanged += HandleReadyStateChanged;
    }

    private void OnDisable()
    {
        ReadyButton.OnReadyStateChanged -= HandleReadyStateChanged;
    }

    private void HandleReadyStateChanged(bool isMatchmaking)
    {
        string address = isMatchmaking ? AudioBgmAddresses.Matchmaking : AudioBgmAddresses.Lobby;
        AudioManager.Instance?.PlayBgmByAddress(address, _crossfadeDuration);
    }
}
