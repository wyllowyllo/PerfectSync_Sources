using UnityEngine;

namespace InGame.Obstacle
{
    /// <summary>
    /// 파괴 시 넉백 방향을 제공하는 컴포넌트가 구현한다.
    /// InvincibleContactDetector가 기본 방향(플레이어→장애물) 대신 이 방향을 사용한다.
    /// </summary>
    public interface IDestroyDirectionProvider
    {
        Vector3 GetDestroyDirection();
    }
}
