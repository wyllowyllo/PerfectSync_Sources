/// <summary>
/// 플랫폼 투명 효과 방식
/// </summary>
public enum FadeMethod
{
    /// <summary>AllIn1 _GeneralAlpha 프로퍼티로 단순 알파 조절</summary>
    GeneralAlpha,

    /// <summary>AllIn1 Fade 이펙트 (_FadeAmount) 텍스처 기반 디졸브</summary>
    FadeTexture,

    /// <summary>AllIn1 Dither 이펙트 — 점묘화 스타일 클리핑</summary>
    Dither
}
