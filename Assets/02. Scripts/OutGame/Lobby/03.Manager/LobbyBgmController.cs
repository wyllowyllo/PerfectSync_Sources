using System;
using UnityEngine;

public class LobbyBgmController : MonoBehaviour
{
    public enum ELobbySubState
    {
        Lobby,
        Matchmaking,
        MapRoulette,
        MapDecided,
    }

    [Serializable]
    private class Entry
    {
        public ELobbySubState State;

        [Tooltip("비워두면 메인 BGM을 교체하지 않음 (Matchmaking에서 사용).")]
        public string BgmAddress;
        [Range(0f, 5f)] public float CrossfadeDuration = 1f;

        [Tooltip("메인 BGM을 로우패스로 멍멍하게 처리할지 여부.")]
        public bool Muffle;
        [Range(0f, 5f)] public float MuffleFadeDuration = 0.3f;

        [Tooltip("비워두면 레이어를 중지. 값이 있으면 해당 주소로 교체/시작.")]
        public string LayerAddress;
        [Range(0f, 5f)] public float LayerFadeDuration = 0.5f;
    }

    [SerializeField] private Entry[] _entries;

    private ELobbySubState _current = ELobbySubState.Lobby;
    private GameMatchTransitionHandler _transitionHandler;
    private MapSelectionManager _mapSelectionManager;

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

        _mapSelectionManager = MapSelectionManager.Instance;
        if (_mapSelectionManager != null)
            _mapSelectionManager.OnMapSelected += HandleMapSelected;
    }

    private void OnDisable()
    {
        ReadyButton.OnReadyStateChanged -= HandleReadyStateChanged;

        if (_transitionHandler != null)
            _transitionHandler.OnMatchConfirmedPendingLeave -= HandleMatchConfirmed;
        _transitionHandler = null;

        if (_mapSelectionManager != null)
            _mapSelectionManager.OnMapSelected -= HandleMapSelected;
        _mapSelectionManager = null;
    }

    private void HandleReadyStateChanged(bool isReady)
    {
        // MapRoulette/MapDecided는 터미널: Ready=false RPC 등 부작용 무시.
        if (IsTerminal(_current))
            return;

        Apply(isReady ? ELobbySubState.Matchmaking : ELobbySubState.Lobby);
    }

    private void HandleMatchConfirmed(string _)
    {
        Apply(ELobbySubState.MapRoulette);
    }

    private void HandleMapSelected(MapDefinition _)
    {
        Apply(ELobbySubState.MapDecided);
    }

    private void Apply(ELobbySubState next)
    {
        _current = next;

        Entry entry = FindEntry(next);
        if (entry == null)
            return;

        AudioManager audio = AudioManager.Instance;
        if (audio == null)
            return;

        if (!string.IsNullOrEmpty(entry.BgmAddress))
            audio.PlayBgmByAddress(entry.BgmAddress, entry.CrossfadeDuration);

        audio.SetBgmMuffled(entry.Muffle, entry.MuffleFadeDuration);

        if (string.IsNullOrEmpty(entry.LayerAddress))
            audio.StopBgmLayer(entry.LayerFadeDuration);
        else
            audio.PlayBgmLayer(entry.LayerAddress, entry.LayerFadeDuration);
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

    private static bool IsTerminal(ELobbySubState state) =>
        state == ELobbySubState.MapRoulette || state == ELobbySubState.MapDecided;
}
