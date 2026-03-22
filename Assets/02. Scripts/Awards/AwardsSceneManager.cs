using System;
using System.Collections;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

/// <summary>
/// Awards 씬: 로컬 플레이어 스폰, 로비 복귀 타이밍, Photon 콜백.
/// 씬 내 싱글톤(DontDestroyOnLoad 없음). UI는 <see cref="OnAwardsUiRefreshRequested"/>로만 연결.
/// </summary>
public class AwardsSceneManager : MonoBehaviourPunCallbacks
{
    public static AwardsSceneManager Instance { get; private set; }

    /// <summary>순위 UI 갱신이 필요할 때(스폰 직후 등).</summary>
    public static event Action OnAwardsUiRefreshRequested;

    [Header("Spawn")]
    [Tooltip("팀 내 첫 번째 슬롯(Photon PlayerList 순) 스폰 위치.")]
    [SerializeField] private Transform _spawnFirstSlotInTeam;
    [Tooltip("팀 내 두 번째 슬롯 스폰 위치.")]
    [SerializeField] private Transform _spawnSecondSlotInTeam;
    [SerializeField] private string _playerPrefabResourceName = "PlayerPrefab";

    [Header("Return")]
    [SerializeField] private float _returnToLobbyDelaySeconds = 10f;
    [SerializeField] private string _lobbySceneName = "Lobby";

    private Coroutine _returnCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[AwardsSceneManager] 중복 인스턴스 제거.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        StartCoroutine(StartSequence());
    }

    private IEnumerator StartSequence()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (!PhotonNetwork.InRoom)
        {
            Debug.LogWarning("[AwardsSceneManager] 방에 없습니다. 로비로 이동합니다.");
            LoadLobbySceneLocal();
            yield break;
        }

        SpawnLocalPlayer();
        yield return null;
        OnAwardsUiRefreshRequested?.Invoke();

        _returnCoroutine = StartCoroutine(ReturnToLobbyRoutine());
    }

    private void OnDestroy()
    {
        if (_returnCoroutine != null)
        {
            StopCoroutine(_returnCoroutine);
            _returnCoroutine = null;
        }

        if (Instance == this)
            Instance = null;
    }

    private void SpawnLocalPlayer()
    {
        if (_spawnFirstSlotInTeam == null || _spawnSecondSlotInTeam == null)
        {
            Debug.LogError("[AwardsSceneManager] 스폰 포인트가 비어 있습니다.");
            return;
        }

        int team = InGameManager.GetPlayerTeam(PhotonNetwork.LocalPlayer);
        if (team == PhotonTeamManager.TeamNone)
        {
            Debug.LogWarning("[AwardsSceneManager] 팀 정보가 없습니다.");
            return;
        }

        int slot = GetSlotIndexInTeam(team);
        if (slot < 0)
        {
            Debug.LogWarning("[AwardsSceneManager] 팀 슬롯을 찾지 못했습니다.");
            return;
        }

        Transform spawn = slot == 0 ? _spawnFirstSlotInTeam : _spawnSecondSlotInTeam;
        GameObject player = PhotonNetwork.Instantiate(_playerPrefabResourceName, spawn.position, spawn.rotation);

        foreach (var move in player.GetComponentsInChildren<PlayerMoveAbility>(true))
            move.enabled = false;
        foreach (var rot in player.GetComponentsInChildren<PlayerRotateAbility>(true))
            rot.enabled = false;

        var tracker = player.GetComponentInChildren<RaceProgressTracker>(true);
        if (tracker != null)
            tracker.enabled = false;
    }

    private static int GetSlotIndexInTeam(int teamNumber)
    {
        int slot = 0;
        foreach (var p in PhotonNetwork.PlayerList)
        {
            if (InGameManager.GetPlayerTeam(p) != teamNumber) continue;
            if (p == PhotonNetwork.LocalPlayer)
                return slot;
            slot++;
        }

        return -1;
    }

    private IEnumerator ReturnToLobbyRoutine()
    {
        yield return new WaitForSeconds(_returnToLobbyDelaySeconds);

        if (PhotonNetwork.InRoom)
            PhotonNetwork.LeaveRoom();
        else
            LoadLobbySceneLocal();
    }

    public override void OnLeftRoom()
    {
        LoadLobbySceneLocal();
    }

    private void LoadLobbySceneLocal()
    {
        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadSceneLocal(_lobbySceneName);
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene(_lobbySceneName);
    }
}
