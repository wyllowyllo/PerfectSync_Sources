using UnityEngine;

/// <summary>
/// 개별 플랫폼에 부착하는 컴포넌트.
/// PlatformFadeWaveController가 이 컴포넌트를 통해 투명도를 제어합니다.
/// Renderer의 Material을 자동으로 찾아 AllIn1 쉐이더 프로퍼티를 조작합니다.
/// </summary>
public class PlatformFadeTarget : MonoBehaviour
{
    // ── 캐싱 ──
    private Renderer[] _renderers;
    private MaterialPropertyBlock[] _propBlocks;

    // ── 쉐이더 프로퍼티 ID ──
    private static readonly int ID_GeneralAlpha = Shader.PropertyToID("_GeneralAlpha");
    private static readonly int ID_FadeAmount   = Shader.PropertyToID("_FadeAmount");

    // ── 상태 ──
    private float _currentAlpha = 1f;   // 0 = 투명, 1 = 불투명  (논리 값)

    public float CurrentAlpha => _currentAlpha;

    private void Awake()
    {
        CacheRenderers();
    }

    /// <summary>
    /// 하위 Renderer들을 캐싱합니다. 런타임 중 구조가 바뀌면 수동 호출 가능.
    /// </summary>
    public void CacheRenderers()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
        _propBlocks = new MaterialPropertyBlock[_renderers.Length];
        for (int i = 0; i < _propBlocks.Length; i++)
        {
            _propBlocks[i] = new MaterialPropertyBlock();
        }
    }

    /// <summary>
    /// 알파값을 설정합니다 (0 = 완전 투명, 1 = 완전 불투명).
    /// mode에 따라 적절한 쉐이더 프로퍼티를 조작합니다.
    /// </summary>
    public void SetAlpha(float alpha, FadeMethod method)
    {
        _currentAlpha = Mathf.Clamp01(alpha);

        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;

            _renderers[i].GetPropertyBlock(_propBlocks[i]);

            switch (method)
            {
                case FadeMethod.GeneralAlpha:
                    // _GeneralAlpha: 1 = 불투명, 0 = 투명 (논리 값과 동일)
                    _propBlocks[i].SetFloat(ID_GeneralAlpha, _currentAlpha);
                    break;

                case FadeMethod.FadeTexture:
                    // _FadeAmount: 0 = 불투명, 1 = 투명 (반전)
                    _propBlocks[i].SetFloat(ID_FadeAmount, 1f - _currentAlpha);
                    break;

                case FadeMethod.Dither:
                    // Dither는 _GeneralAlpha를 낮추면 디더 패턴이 적용됨
                    _propBlocks[i].SetFloat(ID_GeneralAlpha, _currentAlpha);
                    break;
            }

            _renderers[i].SetPropertyBlock(_propBlocks[i]);
        }
    }

    /// <summary>
    /// 머티리얼의 쉐이더 키워드와 블렌드 모드를 설정합니다.
    /// GeneralAlpha/FadeTexture 모드에서는 알파 블렌딩이 필요합니다.
    /// ※ Material 인스턴스를 변경합니다. 초기화 시 한 번만 호출하세요.
    /// </summary>
    public void ConfigureMaterial(FadeMethod method)
    {
        foreach (var rend in _renderers)
        {
            if (rend == null) continue;

            foreach (var mat in rend.materials)
            {
                switch (method)
                {
                    case FadeMethod.GeneralAlpha:
                        // 알파 블렌딩 활성화
                        SetAlphaBlend(mat, true);
                        mat.DisableKeyword("_FADE_ON");
                        mat.DisableKeyword("_DITHER_ON");
                        break;

                    case FadeMethod.FadeTexture:
                        // Fade 이펙트 활성화
                        SetAlphaBlend(mat, true);
                        mat.EnableKeyword("_FADE_ON");
                        mat.SetFloat("_FadeOn", 1f);
                        mat.DisableKeyword("_DITHER_ON");
                        break;

                    case FadeMethod.Dither:
                        // Dither — 알파 블렌딩 불필요 (클리핑 방식)
                        SetAlphaBlend(mat, false);
                        mat.DisableKeyword("_FADE_ON");
                        mat.EnableKeyword("_DITHER_ON");
                        mat.SetFloat("_DitherOn", 1f);
                        break;
                }
            }
        }
    }

    /// <summary>
    /// 머티리얼을 원래 상태(불투명)로 복원합니다.
    /// </summary>
    public void RestoreMaterial()
    {
        foreach (var rend in _renderers)
        {
            if (rend == null) continue;

            foreach (var mat in rend.materials)
            {
                SetAlphaBlend(mat, false);
                mat.DisableKeyword("_FADE_ON");
                mat.DisableKeyword("_DITHER_ON");
                mat.SetFloat("_FadeOn", 0f);
                mat.SetFloat("_DitherOn", 0f);
                mat.SetFloat("_GeneralAlpha", 1f);
                mat.SetFloat("_FadeAmount", 0f);
            }
        }

        // PropertyBlock도 클리어
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;
            _propBlocks[i].Clear();
            _renderers[i].SetPropertyBlock(_propBlocks[i]);
        }
    }

    private void SetAlphaBlend(Material mat, bool enable)
    {
        if (enable)
        {
            // SrcAlpha, OneMinusSrcAlpha
            mat.SetFloat("_BlendSrc", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_BlendDst", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        else
        {
            // One, Zero (불투명 기본)
            mat.SetFloat("_BlendSrc", (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_BlendDst", (float)UnityEngine.Rendering.BlendMode.Zero);
            mat.SetFloat("_ZWrite", 1f);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
        }
    }
}
