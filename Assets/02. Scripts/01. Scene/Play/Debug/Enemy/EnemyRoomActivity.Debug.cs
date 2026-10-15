#if UNITY_EDITOR
using UnityEngine;

/// <summary>
/// 적 방 활동 디버그 - 지정 방에서 테스트 활동 준비
/// </summary>
public partial class EnemyRoomActivity
{
    /// <summary>
    /// 지정 방에서 테스트 활동 준비
    /// </summary>
    /// <param name="room">대상이 있는 방</param>
    /// <returns>재배치와 준비 성공 여부</returns>
    public bool TryPrepareDebug ( Room room )
    {
        if ( _roomMover.TryReturn( room ) == false )
            return false;

        //이전 방 이동 요청을 정리하고 바로 활동 준비
        Reset( );
        _hider.Begin( 0f );
        _isReady = true;
        return true;
    }
}
#endif
