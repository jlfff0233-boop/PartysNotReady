using UnityEngine;

/// <summary>
/// 적 방 활동 모듈 - 잠복 대기와 이동/복귀 위치 준비
/// </summary>
public partial class EnemyRoomActivity
{
    bool _isReady;		//활동 위치 준비 여부
    bool _moveRequested;		//다른 방 이동 요청
    bool _returnRequested;		//활동 방 복귀 요청

    CharacterMover _mover;		//적 위치
    EnemyRoomMover _roomMover;		//잠복 위치 이동
    HideController _hider;		//잠복 대기와 표시

    /// <summary>
    /// 활동 위치 준비 여부
    /// </summary>
    public bool IsReady => _isReady;

    /// <summary>
    /// 다른 방 이동 요청 여부
    /// </summary>
    public bool IsMoveRequested => _moveRequested;

    /// <summary>
    /// 위치 준비와 잠복 대기 완료 여부
    /// </summary>
    public bool CanRun => _isReady && _hider.IsReady &&
        _moveRequested == false && _returnRequested == false;

    /// <summary>
    /// 잠복과 방 이동 연결
    /// </summary>
    /// <param name="mover">적 이동</param>
    /// <param name="roomMover">잠복 위치 이동</param>
    /// <param name="hider">잠복 중재</param>
    public EnemyRoomActivity (
        CharacterMover mover, EnemyRoomMover roomMover,
        HideController hider )
    {
        _mover = mover;
        _roomMover = roomMover;
        _hider = hider;
    }


    /// <summary>
    /// 위치 준비와 이동/복귀 요청 초기화
    /// </summary>
    public void Reset ()
    {
        _isReady = false;
        _moveRequested = false;
        _returnRequested = false;
    }

    /// <summary>
    /// 잠복 대기 시작
    /// </summary>
    /// <param name="hideDuration">잠복 대기시간</param>
    public void BeginWait ( float hideDuration )
    {
        //도주 결과와 이동 실패로 남은 요청은 유지
        _isReady = false;
        _hider.Begin( hideDuration );
    }

    /// <summary>
    /// 다른 방 이동 예약
    /// </summary>
    public void RequestMove ()
    {
        _moveRequested = true;
        _returnRequested = false;
    }

    /// <summary>
    /// 활동 방 복귀 예약
    /// </summary>
    public void RequestReturn ()
    {
        _moveRequested = false;
        _returnRequested = true;
    }


    /// <summary>
    /// 요청에 따른 잠복 위치 준비
    /// </summary>
    /// <param name="currentRoom">이전 활동 방</param>
    /// <param name="hideDuration">실패 후 잠복 대기시간</param>
    /// <param name="room">준비된 활동 방</param>
    /// <returns>위치 준비 여부</returns>
    public bool TryPrepare ( Room currentRoom, float hideDuration, out Room room )
    {
        room = currentRoom;
        bool positioned;

        if ( _moveRequested == true )
            positioned = _roomMover.TryMove( currentRoom, out room );
        else if ( _returnRequested == true )
            positioned = _roomMover.TryReturn( currentRoom );
        else
            positioned = RoomRegister.TryGetRoom( _mover.Position, out room );

        if ( positioned == false || room == null )
        {
            //실패한 요청은 다음 잠복 대기 후 재시도
            BeginWait( hideDuration );
            return false;
        }

        _moveRequested = false;
        _returnRequested = false;
        _isReady = true;
        return true;
    }

}
