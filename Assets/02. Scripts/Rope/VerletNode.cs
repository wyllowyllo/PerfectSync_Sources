using UnityEngine;

/// <summary>
/// 베를레 적분 시뮬레이션에 사용되는 정점 정보를 담는 구조체
/// </summary>
public struct VerletNode
{
    /// <summary>
    /// 현재 정점의 좌표
    /// </summary>
    public Vector3 CurrentPosition;
    
    /// <summary>
    /// 이전 정점의 좌표
    /// </summary>
    public Vector3 PreviousPosition;
    
    /// <summary>
    /// 물리 연산을 무시하고 고정되는 정점
    /// </summary>
    public bool IsPinned;

    /// <summary>
    /// 장애물에 닿아있는 정점
    /// </summary>
    public bool IsTouchingObstacle;
    
    // 초기화를 쉽게 하기 위한 생성자
    public VerletNode(Vector3 position, bool isPinned = false)
    {
        CurrentPosition = position;
        PreviousPosition = position;
        IsPinned = isPinned;
        IsTouchingObstacle = false;
    }
}
