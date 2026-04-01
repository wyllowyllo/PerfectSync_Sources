using UnityEngine;

public interface ITrap
{
    /// <summary>
    /// 함정을 발동시킵니다.
    /// </summary>
    void Activate();
    
    /// <summary>
    /// 함정을 다시 발동할 수 있는 상태로 되돌립니다.
    /// </summary>
    void Reset();
}
