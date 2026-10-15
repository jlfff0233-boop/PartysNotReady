using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적 방 이동 모듈 - 연결 방 선택과 잠복 중 재배치
/// </summary>
public class EnemyRoomMover
{
    CharacterMover _mover;     //이동 모듈
    HideActivationController _hideView;     //잠복 상태 조회
    float _sampleDist;     //잠복 지점 주변 NavMesh 탐색 거리

    List<Room> _candidates = new ( );     //이번 이동의 후보 방
    Dictionary<int , string> _lastFailures = new ( );     //방별 반복 실패 차단


    /// <summary>
    /// 이동 기능과 잠복 상태 연결
    /// </summary>
    /// <param name="mover">초기화된 이동 모듈</param>
    /// <param name="hideView">같은 적의 잠복 뷰</param>
    /// <param name="sampleDist">NavMesh 탐색 거리</param>
    public EnemyRoomMover (
        CharacterMover mover , HideActivationController hideView , float sampleDist )
    {
        _mover = mover;
        _hideView = hideView;
        _sampleDist = sampleDist;
    }


    /// <summary>
    /// 방별 실패 사유가 달라졌을 때 기록
    /// </summary>
    /// <param name="room">이동 대상 방</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>항상 false</returns>
    bool Reject ( Room room , string reason )
    {
        int id = room == null ? 0 : room.GetInstanceID ( );
        if ( _lastFailures.TryGetValue ( id , out string previous ) == false || previous != reason )
        {
            _lastFailures [ id ] = reason;
            Debug.LogWarning ( $"[방 이동 실패] {_mover} / 방: {room} / {reason}" , _mover );
        }
        return false;
    }

    /// <summary>
    /// 지정한 방의 잠복 지점으로 재배치
    /// </summary>
    /// <param name="room">이동할 방</param>
    /// <returns>재배치 성공 여부</returns>
    bool MoveToRoom ( Room room )
    {
        if ( room == null || room.isActiveAndEnabled == false )
            return Reject ( room , "방이 없거나 비활성" );

        Transform point = room.HidePoint;
        if ( point == null || point.gameObject.activeInHierarchy == false )
            return Reject ( room , "잠복 지점이 없거나 비활성" );

        //현재 에이전트 타입과 이동 가능 영역에 맞는 위치 조회
        if ( _mover.TryGetMovePosition (
            point.position , _sampleDist ,
            out Vector3 position ) == false )
            return Reject ( room , $"잠복 지점 주변 NavMesh 없음 / 지점: {point.position} / 범위: {_sampleDist}" );

        //가까운 NavMesh가 옆방이나 겹친 방 영역에 있으면 제외
        //실제 재배치 전에 검사하므로 실패해도 기존 위치는 유지
        if ( RoomRegister.TryGetRoom (
            position , out Room destination ) == false ||
             destination != room )
            return Reject ( room , $"보정 위치가 목적지 방 밖이거나 방 중첩 / 위치: {position}" );

#if UNITY_EDITOR
        Vector3 previousPosition = _mover.Position;
#endif
        if ( _mover.TryRelocate ( position , point.rotation ) == false )
            return Reject ( room , "뷰 재배치 실패" );

#if UNITY_EDITOR
        PlayDebugHistory.Record( "적 잠복 재배치", _mover.GetComponent<Enemy>( ),
            $"{previousPosition} → {_mover.Position} / 목적지 방: {room.name} / 숨김: {_hideView.IsHidden}" );
#endif

        _lastFailures.Clear ( );
        return true;
    }

    /// <summary>
    /// 활동하던 방의 잠복 지점으로 복귀
    /// </summary>
    /// <param name="room">복귀할 방</param>
    /// <returns>복귀 성공 여부</returns>
    public bool TryReturn ( Room room )
    {
        if ( CanMove ( room ) == false )
            return false;

        //도주 지점에서 계속 활동하지 않도록 원래 방의 잠복 지점 사용
        return MoveToRoom ( room );
    }

    /// <summary>
    /// 연결된 다른 방으로 이동
    /// </summary>
    /// <param name="currentRoom">현재 활동 중인 방</param>
    /// <param name="movedRoom">이동에 성공한 방</param>
    /// <returns>방 이동 성공 여부</returns>
    public bool TryMove (
        Room currentRoom , out Room movedRoom )
    {
        movedRoom = null;

        if ( CanMove ( currentRoom ) == false )
            return false;

        //방 연결 설정을 사용하며 현재 방과 중복 연결은 조회 단계에서 제외
        currentRoom.GetConnectedRooms ( _candidates );
        if ( _candidates.Count == 0 )
            return Reject ( currentRoom , "연결 방 중 활성 상태이며 잠복 지점이 할당된 후보 없음" );

        while ( _candidates.Count > 0 )
        {
            int index = Random.Range ( 0 , _candidates.Count );
            Room candidate = _candidates [ index ];
            _candidates.RemoveAt ( index );

            //선택한 방의 지점이 잘못됐으면 남은 후보를 한 번씩 확인
            if ( MoveToRoom ( candidate ) == false )
                continue;

            movedRoom = candidate;
            _candidates.Clear ( );
            return true;
        }

        //이동 가능한 방이 없으면 기존 위치에서 대기하도록 실패 반환
        return false;
    }


    /// <summary>
    /// 잠복 중 방 이동 조건 확인
    /// </summary>
    /// <param name="currentRoom">현재 활동 중인 방</param>
    /// <returns>이동 시도 가능 여부</returns>
    bool CanMove ( Room currentRoom )
    {
        if ( currentRoom == null ||
             currentRoom.isActiveAndEnabled == false )
            return Reject ( currentRoom , "현재 방이 없거나 비활성" );

        if ( _mover == null || _mover.isActiveAndEnabled == false ||
             _hideView == null || _hideView.IsHidden == false )
            return Reject ( currentRoom , "이동 모듈가 비활성이거나 잠복 상태가 아님" );

        //표시 중인 적의 순간이동을 막고 유효한 탐색 거리만 허용
        bool valid = float.IsNaN ( _sampleDist ) == false &&
               float.IsInfinity ( _sampleDist ) == false &&
               _sampleDist > 0f;
        return valid || Reject ( currentRoom , "NavMesh 탐색 거리 오류" );
    }

}
