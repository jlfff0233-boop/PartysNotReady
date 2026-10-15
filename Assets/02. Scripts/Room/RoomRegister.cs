using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 방 등록 - 활성 방, 대상의 위치 기반 조회
/// </summary>
public static class RoomRegister
{
    static HashSet<Room> _rooms = new( );     //활성 방
    static HashSet<IRoomTarget> _targets = new( );     //조회 대상


    /// <summary>
    /// 플레이 시작 시 이전 등록 정보 초기화
    /// </summary>
    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration )]
    static void Reset ()
    {
        _rooms.Clear( );
        _targets.Clear( );
    }


    /// <summary>
    /// 활성 방 등록
    /// </summary>
    /// <param name="room">등록할 방</param>
    public static void RegisterRoom ( Room room )
    {
        if ( room != null )
            _rooms.Add( room );
    }

    /// <summary>
    /// 비활성 방 등록 해제
    /// </summary>
    /// <param name="room">해제할 방</param>
    public static void UnregisterRoom ( Room room )
    {
        _rooms.Remove( room );
    }

    /// <summary>
    /// 방 조회에 참여할 대상 등록
    /// </summary>
    /// <param name="target">등록할 대상</param>
    public static void RegisterTarget ( IRoomTarget target )
    {
        if ( target != null )
            _targets.Add( target );
    }

    /// <summary>
    /// 방 대상 등록 해제
    /// </summary>
    /// <param name="target">해제할 대상</param>
    public static void UnregisterTarget ( IRoomTarget target )
    {
        _targets.Remove( target );
    }


    /// <summary>
    /// 활성 방 조회
    /// </summary>
    /// <param name="results">조회 결과를 받을 목록</param>
    public static void GetRooms ( List<Room> results )
    {
        results.Clear( );

        foreach ( Room room in _rooms )
        {
            if ( room != null && room.isActiveAndEnabled == true )
                results.Add( room );
        }
    }

    /// <summary>
    /// 월드 위치를 포함하는 방 조회
    /// </summary>
    /// <param name="position">조회할 월드 위치</param>
    /// <param name="room">조회한 방</param>
    /// <returns>해당 위치를 포함하는 방이 하나이면 true</returns>
    public static bool TryGetRoom ( Vector3 position, out Room room )
    {
        room = null;

        foreach ( Room candidate in _rooms )
        {
            if ( candidate == null || candidate.Contains( position ) == false )
                continue;

            //겹친 영역에서 임의의 방을 선택하지 않음
            if ( room != null )
            {
                room = null;
                return false;
            }

            room = candidate;
        }

        return room != null;
    }

    /// <summary>
    /// 지정한 방에 있는 활성 대상 조회
    /// </summary>
    /// <param name="room">조회할 방</param>
    /// <param name="results">조회 결과를 받을 목록</param>
    public static void GetTargets (
        Room room, List<IRoomTarget> results )
    {
        results.Clear( );

        if ( room == null || room.isActiveAndEnabled == false )
            return;

        foreach ( IRoomTarget target in _targets )
        {
            if ( IsTargetValid( target ) == false )
                continue;

            Vector3 position = target.TargetTransform.position;

            //대부분의 외부 대상은 전체 방 조회 전에 제외
            if ( room.Contains( position ) == false )
                continue;

            //영역이 겹친 위치의 대상도 임의로 포함하지 않음
            if ( TryGetRoom( position, out Room currentRoom ) == false )
                continue;

            if ( currentRoom == room )
                results.Add( target );
        }
    }


    /// <summary>
    /// 대상의 생존과 조회 참여 상태 확인
    /// </summary>
    /// <param name="target">검사할 대상</param>
    /// <returns>위치 조회 가능 여부</returns>
    static bool IsTargetValid ( IRoomTarget target )
    {
        if ( target == null )
            return false;

        //인터페이스 참조가 남은 파괴된 Unity 오브젝트 제외
        if ( target is Object unityObject && unityObject == null )
            return false;

        return target.IsRoomTargetActive == true &&
               target.TargetTransform != null;
    }

}
