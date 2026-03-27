using UnityEngine;

/// <summary>
/// 로프를 화면에 그려주는 렌더러
/// </summary>
public interface IRopeRenderer
{
    /// <summary>
    /// 계산된 점들의 위치와 두께 정보를 받아 화면에 메쉬를 그립니다.
    /// </summary>
    /// <param name="nodePositions">로프를 구성하는 점들의 배열</param>
    /// <param name="thickness">현재 로프의 두께</param>
    void RenderRope(Vector3[] nodePositions, float thickness);

    /// <summary>
    /// 장력 수치에 따라 로프의 색상을 갱신합니다.
    /// </summary>
    /// <param name="tension">현재 장력 (1.0 = 기본, 값이 클수록 긴장)</param>
    void UpdateTension(float tension);
}
