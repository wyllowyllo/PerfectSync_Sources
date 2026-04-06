using System;
using Unity.Cinemachine;
using UnityEngine;

public enum LobbyVCamZone
{
    Lobby = 0,
    Face  = 1,   // Hair, Hat, Horn, Ears, Eyes, Nose, Mouth (index 0~6)
    Body  = 2,   // BodyColor, Body, Gloves (index 7~9)
    Tail  = 3    // Tail (index 10)
}

public class LobbyVCamController : MonoBehaviour
{
    [Header("Virtual Cameras (CM3)")]
    [SerializeField] private CinemachineCamera lobbyCam;
    [SerializeField] private CinemachineCamera faceCam;
    [SerializeField] private CinemachineCamera bodyCam;
    [SerializeField] private CinemachineCamera tailCam;

    [Header("References")]
    [SerializeField] private CharacterCustomizationPartNavigator _navigator;

    private CinemachineCamera[] _cameras;
    private LobbyVCamZone _currentZone = LobbyVCamZone.Lobby;

    private const int ACTIVE_PRIORITY  = 10;
    private const int INACTIVE_PRIORITY = -1;

    private const int FACE_LAST_INDEX = 6;
    private const int BODY_LAST_INDEX = 9;

    public LobbyVCamZone CurrentZone => _currentZone;
    public event Action<LobbyVCamZone> OnZoneChanged;

    private void Awake()
    {
        _cameras = new[] { lobbyCam, faceCam, bodyCam, tailCam };
    }

    private void OnEnable()
    {
        if (_navigator != null)
            _navigator.PartIndexChanged += OnPartIndexChanged;

        SetActiveCamera(LobbyVCamZone.Lobby);
    }

    private void OnDisable()
    {
        if (_navigator != null)
            _navigator.PartIndexChanged -= OnPartIndexChanged;
    }

    public void EnterCustomize()
    {
        if (_currentZone != LobbyVCamZone.Lobby) return;

        _navigator?.ResetToFirst();
        SetActiveCamera(LobbyVCamZone.Face);
    }

    public void BackToLobby()
    {
        _navigator?.ResetToFirst();
        SetActiveCamera(LobbyVCamZone.Lobby);
    }

    private void OnPartIndexChanged(int partIndex)
    {
        if (_currentZone == LobbyVCamZone.Lobby) return;

        LobbyVCamZone targetZone = MapPartIndexToZone(partIndex);
        if (targetZone != _currentZone)
            SetActiveCamera(targetZone);
    }

    private void SetActiveCamera(LobbyVCamZone zone)
    {
        for (int i = 0; i < _cameras.Length; i++)
        {
            _cameras[i].Priority = (i == (int)zone)
                ? ACTIVE_PRIORITY
                : INACTIVE_PRIORITY;
        }

        _currentZone = zone;
        OnZoneChanged?.Invoke(_currentZone);
    }

    private static LobbyVCamZone MapPartIndexToZone(int partIndex)
    {
        if (partIndex <= FACE_LAST_INDEX) return LobbyVCamZone.Face;
        if (partIndex <= BODY_LAST_INDEX) return LobbyVCamZone.Body;
        return LobbyVCamZone.Tail;
    }
}
