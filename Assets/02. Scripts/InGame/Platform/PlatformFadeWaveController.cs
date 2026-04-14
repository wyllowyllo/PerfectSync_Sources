using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플랫폼들을 웨이브 형태로 순차 투명/불투명 전환하는 컨트롤러.
///
/// ▸ platforms 배열에 원하는 순서로 PlatformFadeTarget을 드래그하세요.
/// ▸ FadeMethod를 변경하면 세 가지 투명 방식을 실시간으로 전환할 수 있습니다.
/// ▸ 웨이브 겹침(overlap)으로 자연스러운 순차 페이드를 연출합니다.
/// </summary>
public class PlatformFadeWaveController : MonoBehaviour
{
    // ═══════════════════════════════════════════
    //  Inspector 설정
    // ═══════════════════════════════════════════

    [Header("━━ 플랫폼 목록 (순서대로 드래그) ━━")]
    [Tooltip("투명해지는 순서대로 배치하세요.")]
    [SerializeField] private List<PlatformFadeTarget> platforms = new List<PlatformFadeTarget>();

    [Header("━━ 투명 방식 ━━")]
    [Tooltip("GeneralAlpha: 단순 알파\nFadeTexture: 텍스처 디졸브\nDither: 점묘화 클리핑")]
    [SerializeField] private FadeMethod fadeMethod = FadeMethod.GeneralAlpha;

    [Header("━━ 타이밍 ━━")]
    [Tooltip("전체 사이클 시간 (투명→불투명→투명). 초 단위.")]
    [SerializeField, Range(1f, 20f)] private float cycleDuration = 4.5f;

    [Tooltip("페이드 아웃(사라짐) 시간. 초 단위.")]
    [SerializeField, Range(0.1f, 10f)] private float fadeOutDuration = 1.0f;

    [Tooltip("페이드 인(나타남) 시간. 초 단위.")]
    [SerializeField, Range(0.1f, 10f)] private float fadeInDuration = 1.0f;

    [Tooltip("투명 상태 유지 시간. 초 단위.")]
    [SerializeField, Range(0f, 10f)] private float holdTransparentDuration = 0.7f;

    [Tooltip("불투명 상태 유지 시간. 초 단위.")]
    [SerializeField, Range(0f, 10f)] private float holdOpaqueDuration = 0.7f;

    [Header("━━ 웨이브 설정 ━━")]
    [Tooltip("플랫폼 간 페이드 시작 딜레이 비율 (0~1). 낮을수록 겹침이 많음.")]
    [SerializeField, Range(0.05f, 1f)] private float waveOffsetRatio = 0.25f;

    [Header("━━ 알파 범위 ━━")]
    [Tooltip("최소 알파 (완전히 투명하게 할 때의 값). 0이면 완전 투명.")]
    [SerializeField, Range(0f, 0.3f)] private float minAlpha = 0f;

    [Tooltip("최대 알파 (완전히 불투명할 때의 값).")]
    [SerializeField, Range(0.7f, 1f)] private float maxAlpha = 1f;

    [Header("━━ 이징 커브 ━━")]
    [Tooltip("페이드 아웃(사라짐)의 커브. 기본은 SmoothStep.")]
    [SerializeField] private AnimationCurve fadeOutCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("페이드 인(나타남)의 커브. 기본은 SmoothStep.")]
    [SerializeField] private AnimationCurve fadeInCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("━━ 제어 ━━")]
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool isPlaying = true;

    // ═══════════════════════════════════════════
    //  내부 상태
    // ═══════════════════════════════════════════

    private float _timer;
    private FadeMethod _lastMethod;

    // ═══════════════════════════════════════════
    //  생명 주기
    // ═══════════════════════════════════════════

    private void Start()
    {
        _lastMethod = fadeMethod;
        ConfigureAllMaterials();

        isPlaying = playOnStart;
        _timer = 0f;
    }

    private void Update()
    {
        // 방식이 변경되면 머티리얼 재설정
        if (_lastMethod != fadeMethod)
        {
            RestoreAllMaterials();
            _lastMethod = fadeMethod;
            ConfigureAllMaterials();
        }

        if (!isPlaying || platforms.Count == 0) return;

        _timer += Time.deltaTime;

        for (int i = 0; i < platforms.Count; i++)
        {
            if (platforms[i] == null) continue;

            float alpha = CalculateAlpha(i);
            platforms[i].SetAlpha(alpha, fadeMethod);
        }
    }

    private void OnDisable()
    {
        RestoreAllMaterials();
    }

    private void OnDestroy()
    {
        RestoreAllMaterials();
    }

    // ═══════════════════════════════════════════
    //  핵심 로직: 알파 계산
    // ═══════════════════════════════════════════

    /// <summary>
    /// 실제 사이클 시간을 계산합니다.
    /// cycleDuration을 기본으로 쓰되, 개별 구간 합이 더 크면 그쪽을 사용합니다.
    /// </summary>
    private float EffectiveCycleDuration
    {
        get
        {
            float sum = fadeOutDuration + holdTransparentDuration + fadeInDuration + holdOpaqueDuration;
            return Mathf.Max(cycleDuration, sum);
        }
    }

    /// <summary>
    /// 각 플랫폼의 현재 알파값을 계산합니다.
    ///
    /// 사이클 구조 (초 단위):
    ///   [0 ─ fadeOut ─ holdT ─ fadeIn ─ holdO ─ cycle]
    ///
    ///   fadeOut:  불투명 → 투명  (점점 사라짐)
    ///   holdT:   투명 유지
    ///   fadeIn:  투명 → 불투명  (점점 나타남)
    ///   holdO:   불투명 유지
    /// </summary>
    private float CalculateAlpha(int index)
    {
        float cycle = EffectiveCycleDuration;

        // 웨이브 오프셋: 각 플랫폼마다 시간차
        float offset = index * cycle * waveOffsetRatio;
        float elapsed = Mathf.Repeat(_timer + offset, cycle);

        // 구간 경계 (초 단위)
        float fadeOutEnd = fadeOutDuration;
        float holdTEnd   = fadeOutEnd + holdTransparentDuration;
        float fadeInEnd  = holdTEnd + fadeInDuration;
        // holdOpaqueEnd = cycle

        float alpha;

        if (elapsed < fadeOutEnd)
        {
            // 페이드 아웃: 불투명(1) → 투명(0)
            float progress = elapsed / Mathf.Max(0.001f, fadeOutDuration);
            alpha = 1f - fadeOutCurve.Evaluate(progress);
        }
        else if (elapsed < holdTEnd)
        {
            // 투명 유지
            alpha = 0f;
        }
        else if (elapsed < fadeInEnd)
        {
            // 페이드 인: 투명(0) → 불투명(1)
            float progress = (elapsed - holdTEnd) / Mathf.Max(0.001f, fadeInDuration);
            alpha = fadeInCurve.Evaluate(progress);
        }
        else
        {
            // 불투명 유지
            alpha = 1f;
        }

        // 알파 범위 매핑
        return Mathf.Lerp(minAlpha, maxAlpha, alpha);
    }

    // ═══════════════════════════════════════════
    //  머티리얼 설정
    // ═══════════════════════════════════════════

    private void ConfigureAllMaterials()
    {
        foreach (var platform in platforms)
        {
            if (platform != null)
                platform.ConfigureMaterial(fadeMethod);
        }
    }

    private void RestoreAllMaterials()
    {
        foreach (var platform in platforms)
        {
            if (platform != null)
                platform.RestoreMaterial();
        }
    }

    // ═══════════════════════════════════════════
    //  Public API
    // ═══════════════════════════════════════════

    /// <summary>웨이브 재생 시작</summary>
    public void Play()
    {
        isPlaying = true;
    }

    /// <summary>웨이브 일시정지 (현재 상태 유지)</summary>
    public void Pause()
    {
        isPlaying = false;
    }

    /// <summary>웨이브 정지 및 모든 플랫폼 불투명으로 복원</summary>
    public void Stop()
    {
        isPlaying = false;
        _timer = 0f;

        foreach (var platform in platforms)
        {
            if (platform != null)
                platform.SetAlpha(1f, fadeMethod);
        }
    }

    /// <summary>타이머 리셋</summary>
    public void ResetTimer()
    {
        _timer = 0f;
    }

    /// <summary>런타임에서 투명 방식 변경</summary>
    public void SetFadeMethod(FadeMethod method)
    {
        fadeMethod = method;
    }

    /// <summary>런타임에서 플랫폼 목록 교체</summary>
    public void SetPlatforms(List<PlatformFadeTarget> newPlatforms)
    {
        RestoreAllMaterials();
        platforms = newPlatforms;
        ConfigureAllMaterials();
        _timer = 0f;
    }

#if UNITY_EDITOR
    // ═══════════════════════════════════════════
    //  에디터 시각화
    // ═══════════════════════════════════════════

    private void OnDrawGizmosSelected()
    {
        if (platforms == null) return;

        for (int i = 0; i < platforms.Count; i++)
        {
            if (platforms[i] == null) continue;

            // 순서 번호 표시
            Gizmos.color = Color.Lerp(Color.green, Color.red, (float)i / Mathf.Max(1, platforms.Count - 1));
            Gizmos.DrawWireSphere(platforms[i].transform.position, 0.3f);

            UnityEditor.Handles.Label(
                platforms[i].transform.position + Vector3.up * 0.5f,
                $"[{i}] {platforms[i].name}",
                new GUIStyle
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Gizmos.color }
                });
        }

        // 순서 연결선 표시
        Gizmos.color = Color.yellow;
        for (int i = 0; i < platforms.Count - 1; i++)
        {
            if (platforms[i] == null || platforms[i + 1] == null) continue;
            Gizmos.DrawLine(platforms[i].transform.position, platforms[i + 1].transform.position);
        }
    }
#endif
}
