using UnityEngine;

/// <summary>
/// 로프 물리 시뮬레이터
/// </summary>
public interface IRopePhysics
{
    /// <summary>
    /// 매 프레임 물리 연산을 수행하여 점들의 위치를 업데이트합니다.
    /// </summary>
    /// <param name="deltaTime">물리 프레임 간격 (보통 Time.fixedDeltaTime)</param>
    void Simulate(float deltaTime);

    /// <summary>
    /// 물리 연산이 끝난 정점들의 좌표를 외부 버퍼에 채워줍니다.
    /// </summary>
    /// <param name="buffer">좌표를 담을 미리 생성된 배열</param>
    /// <returns>실제로 채워진 정점의 개수</returns>
    void GetNodePositions(ref Vector3[] buffer);
    
    /// <summary>
    /// 현재 로프에 걸려있는 장력 수치를 반환합니다.
    /// </summary>
    float GetCurrentTension();

    /// <summary>
    /// 타겟을 당겨야 할 방향 벡터를 계산하여 반환합니다.
    /// </summary>
    /// <param name="targetPosition"></param>
    /// <param name="isStartAnchor"></param>
    /// <returns></returns>
    Vector3 CalculatePullingDirection(Vector3 targetPosition, bool isStartAnchor);
    
    /// <summary>
    /// 특정 인덱스의 노드를 강제로 고정하고 위치를 지정합니다.
    /// </summary>
    void SetNodePosition(int index, Vector3 position);
    
    /// <summary>
    /// 이번 스텝에서 움직이는 장애물과 충돌했는지 여부를 반환합니다.
    /// </summary>
    bool IsOverlappingDynamicObstacle { get; }
}
