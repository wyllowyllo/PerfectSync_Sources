using UnityEngine;

namespace InGame.Obstacle
{
    /// <summary>
    /// 장애물 동작 스크립트가 구현하여 FreezableObstacle에 반동 정보를 제공한다.
    /// </summary>
    public interface IRecoilSource
    {
        /// <summary>true: DOPunchRotation, false: DOPunchPosition</summary>
        bool IsRotational { get; }

        /// <summary>반동 방향 (정규화). 운동 반대 방향을 반환한다.</summary>
        Vector3 GetRecoilDirection();
    }
}
