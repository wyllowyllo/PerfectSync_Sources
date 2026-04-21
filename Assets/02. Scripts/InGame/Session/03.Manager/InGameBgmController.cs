using System;
using UnityEngine;

public class InGameBgmController : MonoBehaviour
{
    public enum EInGameSubState
    {
        Racing,
        Ceremony,
    }

    [Serializable]
    private class Entry
    {
        public EInGameSubState State;
        public string BgmAddress;
        [Range(0f, 5f)] public float CrossfadeDuration = 1f;
    }

    [SerializeField] private Entry[] _entries;

    private EInGameSubState _current = EInGameSubState.Racing;
    private InGameManager _inGameManager;

    private void Start()
    {
        Apply(EInGameSubState.Racing);

        _inGameManager = InGameManager.Instance;
        if (_inGameManager != null)
            _inGameManager.OnCeremonyReady += HandleCeremonyReady;
    }

    private void OnDisable()
    {
        if (_inGameManager != null)
            _inGameManager.OnCeremonyReady -= HandleCeremonyReady;
        _inGameManager = null;
    }

    private void HandleCeremonyReady()
    {
        Apply(EInGameSubState.Ceremony);
    }

    private void Apply(EInGameSubState next)
    {
        _current = next;

        Entry entry = FindEntry(next);
        if (entry == null || string.IsNullOrEmpty(entry.BgmAddress))
            return;

        AudioManager.Instance?.PlayBgmByAddress(entry.BgmAddress, entry.CrossfadeDuration);
    }

    private Entry FindEntry(EInGameSubState state)
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
