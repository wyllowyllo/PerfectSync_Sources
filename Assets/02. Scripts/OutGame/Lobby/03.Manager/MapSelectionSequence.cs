using System;
using System.Collections;
using UnityEngine;

public class MapSelectionSequence : MonoBehaviour
{
    [Header("Canvas")]
    [SerializeField] private GameObject _mapSelectionCanvas;

    [Header("Step 2: White Fade")]
    [SerializeField] private GameObject _whiteFadeObject;
    [SerializeField] private float _whiteFadeLeadTime = 0.5f;

    [Header("Step 3: Map Info")]
    [SerializeField] private GameObject _mapInfoObject;
    [SerializeField] private float _mapInfoDisplayDuration = 5f;

    [Header("Roulette Timing (UIRandomMapRouletteEffect의 totalDuration과 일치시킬 것)")]
    [SerializeField] private float _rouletteDuration = 3f;

    [Header("References")]
    [SerializeField] private UIRandomMapRouletteEffect _rouletteEffect;

    private Coroutine _sequenceCoroutine;

    public bool IsRunning => _sequenceCoroutine != null;

    private void OnEnable()
    {
        var handler = GameMatchTransitionHandler.Instance;
        if (handler != null)
            handler.OnMatchConfirmedPendingLeave += OnMatchConfirmed;
    }

    private void OnDisable()
    {
        var handler = GameMatchTransitionHandler.Instance;
        if (handler != null)
            handler.OnMatchConfirmedPendingLeave -= OnMatchConfirmed;
    }

    private void OnMatchConfirmed(string roomName)
    {
        if (MapSelectionManager.Instance != null)
            MapSelectionManager.Instance.SelectRandomMap();

        Begin(() => GameMatchTransitionHandler.Instance?.CompletePendingMatchTransition());
    }

    public void Begin(Action onComplete)
    {
        if (_sequenceCoroutine != null) return;
        _sequenceCoroutine = StartCoroutine(SequenceRoutine(onComplete));
    }

    public void Cancel()
    {
        if (_sequenceCoroutine == null) return;
        StopCoroutine(_sequenceCoroutine);
        _sequenceCoroutine = null;

        if (_mapSelectionCanvas != null) _mapSelectionCanvas.SetActive(false);
        if (_whiteFadeObject != null) _whiteFadeObject.SetActive(false);
        if (_mapInfoObject != null) _mapInfoObject.SetActive(false);
        // 다음 연출 시 OnEnable이 다시 호출되도록 룰렛 오브젝트는 켜진 상태로 복원
        if (_rouletteEffect != null) _rouletteEffect.gameObject.SetActive(true);
    }

    private IEnumerator SequenceRoutine(Action onComplete)
    {
        if (_whiteFadeObject != null) _whiteFadeObject.SetActive(false);
        if (_mapInfoObject != null) _mapInfoObject.SetActive(false);

        if (_rouletteEffect != null && MapSelectionManager.Instance != null)
            _rouletteEffect.Initialize(MapSelectionManager.Instance.GetAllMaps());

        if (_mapSelectionCanvas != null) _mapSelectionCanvas.SetActive(true);

        float waitBeforeWhite = Mathf.Max(0f, _rouletteDuration - _whiteFadeLeadTime);
        yield return new WaitForSeconds(waitBeforeWhite);

        if (_whiteFadeObject != null) _whiteFadeObject.SetActive(true);
        yield return new WaitForSeconds(_whiteFadeLeadTime);

        // 화이트 플래시로 가려진 동안 룰렛 오브젝트 끄기 (맵 정보와 겹치지 않도록)
        if (_rouletteEffect != null) _rouletteEffect.gameObject.SetActive(false);

        if (_whiteFadeObject != null) _whiteFadeObject.SetActive(false);

        if (MapSelectionManager.Instance != null)
        {
            yield return new WaitUntil(() => MapSelectionManager.Instance.IsMapSelected());
            MapSelectionManager.Instance.NotifyMapSelected();
        }

        if (_mapInfoObject != null) _mapInfoObject.SetActive(true);
        yield return new WaitForSeconds(_mapInfoDisplayDuration);

        _sequenceCoroutine = null;
        onComplete?.Invoke();
    }
}
