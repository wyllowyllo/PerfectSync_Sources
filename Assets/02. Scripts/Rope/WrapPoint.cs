using UnityEngine;

/// <summary>
/// 로프가 장애물 표면에 접촉하여 감기는 지점의 정보를 담는 구조체
/// </summary>
public struct WrapPoint
{
    /// <summary>
    /// 장애물 표면 접선점의 월드 좌표
    /// </summary>
    public Vector3 Position;

    /// <summary>
    /// 감기고 있는 장애물의 콜라이더
    /// </summary>
    public Collider Obstacle;

    /// <summary>
    /// XZ 평면 기준 감김 방향 (+1: 반시계, -1: 시계)
    /// </summary>
    public int WindingSide;

    public WrapPoint(Vector3 position, Collider obstacle, int windingSide)
    {
        Position = position;
        Obstacle = obstacle;
        WindingSide = windingSide;
    }
}
