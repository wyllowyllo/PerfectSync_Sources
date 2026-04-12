using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 매치 확정 후 맵 랜덤 선택 연출 시퀀스를 관리합니다.
/// 캔버스를 켜면 하위 오브젝트들이 OnEnable로 자동 연출을 시작합니다.
///
/// Step 1: 캔버스 ON → 검정 페이드(alpha 1→0) + 룰렛 자동 시작
/// Step 2: 룰렛 종료 직전 → 하양 페이드(alpha 0→1) 활성화
/// Step 3: 하양 알파 최대 → 하양 페이드 OFF → 맵 정보 오브젝트 ON → 강제 로딩 대기
/// </summary>
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
    [SerializeField] private float _rouletteDuration = 5f;

    // ── Future hooks ──
    // [SerializeField] private AudioClip _rouletteSfx;
    // [SerializeField] private AudioClip _mapSelectedSfx;

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
    }

    private IEnumerator SequenceRoutine(Action onComplete)
    {
        // ── Step 1: 캔버스 ON → 검정 페이드 + 룰렛 자동 시작 ──
        if (_whiteFadeObject != null) _whiteFadeObject.SetActive(false);
        if (_mapInfoObject != null) _mapInfoObject.SetActive(false);
        if (_mapSelectionCanvas != null) _mapSelectionCanvas.SetActive(true);

        // 룰렛 종료 _whiteFadeLeadTime초 전까지 대기
        float waitBeforeWhite = Mathf.Max(0f, _rouletteDuration - _whiteFadeLeadTime);
        yield return new WaitForSeconds(waitBeforeWhite);

        // ── Step 2: 하양 페이드 활성화 ──
        if (_whiteFadeObject != null) _whiteFadeObject.SetActive(true);

        // 하양 페이드가 완료될 때까지 대기 (leadTime 동안 알파 0→1)
        yield return new WaitForSeconds(_whiteFadeLeadTime);

        // ── Step 3: 하양 페이드 OFF → 맵 정보 ON ──
        if (_whiteFadeObject != null) _whiteFadeObject.SetActive(false);
        if (_mapInfoObject != null) _mapInfoObject.SetActive(true);

        // 강제 로딩 대기 (맵 정보 표시)
        yield return new WaitForSeconds(_mapInfoDisplayDuration);

        _sequenceCoroutine = null;
        onComplete?.Invoke();
    }
}
