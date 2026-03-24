using System;
using System.Collections;
using UnityEngine;
using Photon.Pun;

public class AwardsSceneManager : SingletonPunCallbacks<AwardsSceneManager>
{
    protected override bool PersistAcrossScenes => false;

    [Header("Spawn")]
    [SerializeField] private Transform _spawnFirstSlotInTeam;
    [SerializeField] private Transform _spawnSecondSlotInTeam;
    [SerializeField] private string _playerPrefabResourceName = "PlayerPrefab";

    public static event Action RefreshUiRequested;

    [Header("Return")]
    [SerializeField] private float _returnToLobbyDelaySeconds = 10f;
    [SerializeField] private string _lobbySceneName = "Lobby";

    private AwardsShowcaseSpawner _showcaseSpawner;
    private Coroutine _returnCoroutine;

    protected override void Awake()
    {
        base.Awake();
        _showcaseSpawner = new AwardsShowcaseSpawner(_spawnFirstSlotInTeam, _spawnSecondSlotInTeam, _playerPrefabResourceName);
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
            NavigateToLobbyScene();
            yield break;
        }

        _showcaseSpawner.TrySpawnShowcasePlayer();
        yield return null;
        RefreshUiRequested?.Invoke();

        _returnCoroutine = StartCoroutine(ReturnToLobbyRoutine());
    }

    protected override void OnDestroy()
    {
        if (_returnCoroutine != null)
        {
            StopCoroutine(_returnCoroutine);
            _returnCoroutine = null;
        }

        base.OnDestroy();
    }

    private IEnumerator ReturnToLobbyRoutine()
    {
        yield return new WaitForSeconds(_returnToLobbyDelaySeconds);

        if (PhotonNetwork.InRoom)
            PhotonNetwork.LeaveRoom();
        else
            NavigateToLobbyScene();
    }

    public override void OnLeftRoom()
    {
        NavigateToLobbyScene();
    }

    private void NavigateToLobbyScene()
    {
        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadSceneLocal(_lobbySceneName);
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene(_lobbySceneName);
    }
}
