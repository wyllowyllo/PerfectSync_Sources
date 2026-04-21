using System;
using Unity.Cinemachine;
using UnityEngine;

public enum LobbyVCamZone
{
    Lobby       = 0,
    Face        = 1,   // Hair, Hat, Horn, Ears, Eyes, Nose, Mouth (index 0~6)
    Body        = 2,   // BodyColor, Body, Gloves (index 7~9)
    Tail        = 3,   // Tail (index 10)
    Matchmaking = 4
}

public class LobbyVCamController : MonoBehaviour
{
    [Header("Virtual Cameras (CM3)")]
    [SerializeField] private CinemachineCamera lobbyCam;
    [SerializeField] private CinemachineCamera faceCam;
    [SerializeField] private CinemachineCamera bodyCam;
    [SerializeField] private CinemachineCamera tailCam;

    [Header("Matchmaking Camera")]
    [SerializeField] private CinemachineCamera matchmakingCam;
    [SerializeField] private float matchmakingTargetY = 5f;
    [SerializeField] private float matchmakingDuration = 2f;

    [Tooltip("솔로 매칭 시 매치메이킹 카메라 X 좌표")]
    [SerializeField] private float matchmakingSoloX = 0f;
    [Tooltip("파티 매칭 시 매치메이킹 카메라 X 좌표")]
    [SerializeField] private float matchmakingPartyX = 2f;

    [Header("References")]
    [SerializeField] private CinemachineBrain _brain;
    [SerializeField] private CharacterCustomizationPartNavigator _navigator;

    private CinemachineCamera[] _cameras;
    private LobbyVCamZone _currentZone = LobbyVCamZone.Lobby;

    private float _matchmakingInitialY;
    private Coroutine _matchmakingCoroutine;

    private const int ACTIVE_PRIORITY  = 10;
    private const int INACTIVE_PRIORITY = -1;

    private const int FACE_LAST_INDEX = 6;
    private const int BODY_LAST_INDEX = 9;

    public LobbyVCamZone CurrentZone => _currentZone;
    public event Action<LobbyVCamZone> OnZoneChanged;

    private void Awake()
    {
        _cameras = new[] { lobbyCam, faceCam, bodyCam, tailCam, matchmakingCam };

        if (matchmakingCam != null)
            _matchmakingInitialY = matchmakingCam.transform.position.y;
    }

    private void OnEnable()
    {
        if (_navigator != null)
            _navigator.PartIndexChanged += OnPartIndexChanged;

        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.ShowMatchingScreenRequested += EnterMatchmaking;
            LobbyManager.Instance.ShowMainScreenRequested += BackToLobby;
        }

        SetActiveCamera(LobbyVCamZone.Lobby);
    }

    private void OnDisable()
    {
        if (_navigator != null)
            _navigator.PartIndexChanged -= OnPartIndexChanged;

        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.ShowMatchingScreenRequested -= EnterMatchmaking;
            LobbyManager.Instance.ShowMainScreenRequested -= BackToLobby;
        }
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

    public void EnterMatchmaking()
    {
        // 솔로/파티 모드별로 매치메이킹 카메라의 X 좌표를 먼저 세팅한 뒤 전환.
        // Y 애니메이션은 AnimateMatchmakingY에서 시작 X를 기준으로 돌아가므로 여기서 X만 덮어쓰면 됨.
        bool inParty = LobbyPartyService.Instance != null
                       && LobbyPartyService.Instance.LocalPlayerHasParty;
        ApplyMatchmakingX(inParty ? matchmakingPartyX : matchmakingSoloX);

        SetActiveCamera(LobbyVCamZone.Matchmaking);
    }

    private void ApplyMatchmakingX(float x)
    {
        if (matchmakingCam == null) return;
        Vector3 pos = matchmakingCam.transform.position;
        matchmakingCam.transform.position = new Vector3(x, pos.y, pos.z);
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
        bool involvesMatchmaking = (_currentZone == LobbyVCamZone.Matchmaking
                                    || zone == LobbyVCamZone.Matchmaking);

        // 매치메이킹 관련 전환 시 Cut 블렌드 적용
        CinemachineBlendDefinition originalBlend = default;
        if (involvesMatchmaking && _brain != null)
        {
            originalBlend = _brain.DefaultBlend;
            _brain.DefaultBlend = new CinemachineBlendDefinition(
                CinemachineBlendDefinition.Styles.Cut, 0f);
        }

        // 매치메이킹에서 벗어나면 애니메이션 중단 + Y 복귀
        if (_currentZone == LobbyVCamZone.Matchmaking)
        {
            StopMatchmakingMovement();
        }

        for (int i = 0; i < _cameras.Length; i++)
        {
            _cameras[i].Priority = (i == (int)zone)
                ? ACTIVE_PRIORITY
                : INACTIVE_PRIORITY;
        }

        _currentZone = zone;
        OnZoneChanged?.Invoke(_currentZone);

        // 매치메이킹 진입 시 Y 애니메이션 시작
        if (zone == LobbyVCamZone.Matchmaking)
        {
            _matchmakingCoroutine = StartCoroutine(AnimateMatchmakingY());
        }

        // Cut → 원래 블렌드로 복구 (1프레임 뒤)
        if (involvesMatchmaking && _brain != null)
        {
            StartCoroutine(RestoreBlendNextFrame(originalBlend));
        }
    }

    private System.Collections.IEnumerator AnimateMatchmakingY()
    {
        Transform camTransform = matchmakingCam.transform;
        Vector3 startPos = camTransform.position;
        float startY = _matchmakingInitialY;
        float elapsed = 0f;

        while (elapsed < matchmakingDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / matchmakingDuration);
            float newY = Mathf.Lerp(startY, matchmakingTargetY, t);
            camTransform.position = new Vector3(startPos.x, newY, startPos.z);
            yield return null;
        }

        camTransform.position = new Vector3(startPos.x, matchmakingTargetY, startPos.z);
        _matchmakingCoroutine = null;
    }

    private void StopMatchmakingMovement()
    {
        if (_matchmakingCoroutine != null)
        {
            StopCoroutine(_matchmakingCoroutine);
            _matchmakingCoroutine = null;
        }

        if (matchmakingCam != null)
        {
            Vector3 pos = matchmakingCam.transform.position;
            matchmakingCam.transform.position = new Vector3(pos.x, _matchmakingInitialY, pos.z);
        }
    }

    private System.Collections.IEnumerator RestoreBlendNextFrame(CinemachineBlendDefinition original)
    {
        yield return null;
        if (_brain != null)
            _brain.DefaultBlend = original;
    }

    private static LobbyVCamZone MapPartIndexToZone(int partIndex)
    {
        if (partIndex <= FACE_LAST_INDEX) return LobbyVCamZone.Face;
        if (partIndex <= BODY_LAST_INDEX) return LobbyVCamZone.Body;
        return LobbyVCamZone.Tail;
    }
}
