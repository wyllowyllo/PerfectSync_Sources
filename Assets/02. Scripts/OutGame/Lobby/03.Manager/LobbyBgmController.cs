using System;
using UnityEngine;

public class LobbyBgmController : MonoBehaviour
{
    public enum ELobbySubState
    {
        Lobby,
        Matchmaking,
        MapSelection,
    }

    [Serializable]
    private class Entry
    {
        public ELobbySubState State;
        public string BgmAddress;
        [Range(0f, 5f)] public float CrossfadeDuration = 1f;
    }

    [SerializeField] private Entry[] _entries;

    private ELobbySubState _current = ELobbySubState.Lobby;
    private GameMatchTransitionHandler _transitionHandler;

    private void OnEnable()
    {
        ReadyButton.OnReadyStateChanged += HandleReadyStateChanged;
    }

    private void Start()
    {
        Apply(ELobbySubState.Lobby);

        _transitionHandler = GameMatchTransitionHandler.Instance;
        if (_transitionHandler != null)
            _transitionHandler.OnMatchConfirmedPendingLeave += HandleMatchConfirmed;
    }

    private void OnDisable()
    {
        ReadyButton.OnReadyStateChanged -= HandleReadyStateChanged;
        if (_transitionHandler != null)
            _transitionHandler.OnMatchConfirmedPendingLeave -= HandleMatchConfirmed;
        _transitionHandler = null;
    }

    private void HandleReadyStateChanged(bool isReady)
    {
        // MapSelection은 터미널: Ready=false RPC 같은 부작용 무시.
        if (_current == ELobbySubState.MapSelection)
            return;

        Apply(isReady ? ELobbySubState.Matchmaking : ELobbySubState.Lobby);
    }

    private void HandleMatchConfirmed(string _)
    {
        Apply(ELobbySubState.MapSelection);
    }

    private void Apply(ELobbySubState next)
    {
        _current = next;

        Entry entry = FindEntry(next);
        if (entry == null || string.IsNullOrEmpty(entry.BgmAddress))
            return;

        AudioManager.Instance?.PlayBgmByAddress(entry.BgmAddress, entry.CrossfadeDuration);
    }

    private Entry FindEntry(ELobbySubState state)
    {
        if (_entries == null)
            return null;

        for (int i = 0; i < _entries.Length; i++)
        {
            if (_entries[i] != null && _entries[i].State == state)
                return _entries[i];
        }
        return null;
    }
}
