using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// NPC Normal 내부 행동 판단 - 대기, 방문할 방 선택과 이동 요청
/// </summary>
public class NpcNormalBehaviour
{
    NpcMover _mover;     //이동 실행 모듈
    Room _partyRoom;     //기본 거점
    NpcData _data;     //일반 행동 설정
    bool _hasStartedNormal;          //착지 후 일반 행동 시작 완료 여부
    NpcNormalActionType _normalAction;     //Normal 내부 진행 상태
    float _waitRemaining;       //남은 대기 시간
    float _roomCheckRemaining;     //다음 방 이동 판단까지 남은 시간

    Room _currentRoom;     //실제 위치로 확인한 현재 방
    Room _lastRoom;     //마지막으로 확인한 방
    List<Room> _roomCandidates = new ( );     //연결된 방문 후보

    /// <summary>
    /// 일반 행동 설정 연결
    /// </summary>
    /// <param name="mover">이동 실행 모듈</param>
    /// <param name="data">일반 행동 설정</param>
    /// <param name="partyRoom">기본 거점 방</param>
    public NpcNormalBehaviour ( NpcMover mover , NpcData data , Room partyRoom )
    {
        _mover = mover;
        _partyRoom = partyRoom;
        _data = data;
    }

    /// <summary>
    /// 일반 행동 시작 준비
    /// </summary>
    public void Begin ( )
    {
        //실제 일반 행동은 착지와 이동 가능 여부를 확인한 뒤 시작
        _hasStartedNormal = false;
    }

    /// <summary>
    /// 이동 가능 여부를 확인하고 Normal 내부 행동 판단
    /// </summary>
    /// <param name="deltaTime">경과 시간</param>
    public void Update ( float deltaTime )
    {
        if ( _data == null )
            return;

        if ( _mover == null || _mover.IsFalling == true || _mover.CanNavigate == false )
        {
            if ( _hasStartedNormal == true )
            {
                StopNormalMove ( );
                _hasStartedNormal = false;
            }

            return;
        }

        //최초 활성화와 구조 후 착지 완료 시 새 대기부터 시작
        if ( _hasStartedNormal == false )
        {
            RefreshRoomInterval ( );
            ResetRoomInterval ( );
            BeginWait ( );
            _hasStartedNormal = true;
        }

        UpdateNormal ( deltaTime );
    }

    /// <summary>
    /// 이동과 진행 값 정리, 복귀 판단에 사용하는 마지막 방 정보는 유지
    /// </summary>
    public void End ( )
    {
        StopNormalMove ( );
        _hasStartedNormal = false;
        _waitRemaining = 0f;
        _roomCheckRemaining = 0f;
    }


    #region ----- 일반 행동 -----

    /// <summary>
    /// 현재 이동과 목적지 정리
    /// </summary>
    void StopNormalMove ( )
    {
        _mover.StopNormalMove ( );
        _normalAction = NpcNormalActionType.Waiting;
    }

    /// <summary>
    /// 이동을 정리하고 새 대기 시작
    /// </summary>
    void BeginWait ( )
    {
        StopNormalMove ( );

        _waitRemaining = Random.Range (
            _data.LocalMoveIntervalMin , _data.LocalMoveIntervalMax );
    }

    /// <summary>
    /// 대기 시간과 일반 이동 진행
    /// </summary>
    /// <param name="deltaTime">경과 시간</param>
    void UpdateNormal ( float deltaTime )
    {
        //한 번에 하나의 이동만 갱신하여 같은 에이전트의 경로 덮어쓰기 방지
        if ( _normalAction == NpcNormalActionType.RoomMove )
        {
            if ( _mover.UpdateRoomMove ( deltaTime ) != RouteStatusType.Moving )
                FinishRoomMove ( );
            return;
        }

        RefreshRoomInterval ( );

        float elapsed = Mathf.Max ( 0f , deltaTime );
        _roomCheckRemaining = Mathf.Max ( 0f , _roomCheckRemaining - elapsed );

        if ( _normalAction == NpcNormalActionType.LocalMove )
        {
            if ( _mover.UpdateLocalMove ( elapsed ) != RouteStatusType.Moving )
                BeginWait ( );
            return;
        }

        _waitRemaining -= elapsed;

        if ( _waitRemaining > 0f )
            return;

        //방 이동 판단은 로컬 이동이 끝난 대기 구간에서만 수행
        if ( _mover.CanMoveRooms == true && _roomCheckRemaining <= 0f )
        {
            ResetRoomInterval ( );

            //방 선택 또는 목적지 조회 실패 시에도 이번 판단을 종료하고 대기
            if ( TryBeginRoomMove ( ) == false )
                BeginWait ( );
            else
            {
                _normalAction = NpcNormalActionType.RoomMove;
                if ( _mover.RoomMoveStatus != RouteStatusType.Moving )
                    FinishRoomMove ( );
            }

            return;
        }

        if ( TryBeginLocalMove ( ) == false )
            BeginWait ( );
        else
            _normalAction = NpcNormalActionType.LocalMove;
    }

    #endregion

    #region ----- 방 이동 -----

    /// <summary>
    /// 현재 방 변경에 맞춰 체류시간 갱신
    /// </summary>
    void RefreshRoomInterval ( )
    {
        //새 방에 들어왔으면 체류시간부터 다시 시작
        if ( RefreshRoom ( ) == true )
            ResetRoomInterval ( );
    }

    /// <summary>
    /// 다음 방 이동 판단 시간 설정
    /// </summary>
    void ResetRoomInterval ( )
    {
        _roomCheckRemaining = Random.Range (
            _data.RoomMoveIntervalMin , _data.RoomMoveIntervalMax );
    }


    /// <summary>
    /// 방 이동 종료 후 현재 위치에서 대기
    /// </summary>
    void FinishRoomMove ( )
    {
        BeginWait ( );
        //도착과 실패 모두 실제 위치로 현재 방 재확인
        RefreshRoomInterval ( );
        ResetRoomInterval ( );
    }


    /// <summary>
    /// 실제 위치로 현재 방 갱신
    /// </summary>
    /// <returns>새 유효 방에 들어왔는지 여부</returns>
    bool RefreshRoom ( )
    {
        RoomRegister.TryGetRoom ( _mover.Position , out Room room );

        if ( room == _currentRoom )
            return false;

        _currentRoom = room;

        if ( room != null )
        {
            _lastRoom = room;
        }

        return room != null;
    }

    /// <summary>
    /// 현재 방과 확률에 따라 방문할 방 선정
    /// </summary>
    /// <returns>목적지 방, 체류하거나 후보가 없으면 null</returns>
    Room SelectRoom ( )
    {
        //방 밖에서는 마지막 방을 우선하고 없으면 파티룸으로 복귀
        if ( _currentRoom == null )
        {
            if ( _lastRoom != null && _lastRoom.isActiveAndEnabled )
                return _lastRoom;


            return _partyRoom != null && _partyRoom.isActiveAndEnabled
                ? _partyRoom : null;
        }

        float chance = _currentRoom == _partyRoom
            ? _data.PartyRoomMoveChance
            : _data.RoomMoveChance;

        float roll = Random.value;
        if ( roll >= chance )
        {
            return null;
        }

        //NPC 방문에는 적의 잠복 지점을 요구하지 않음
        _currentRoom.GetConnectedRooms ( _roomCandidates , false );

        if ( _roomCandidates.Count == 0 )
        {
            return null;
        }

        bool canReturn = _currentRoom != _partyRoom &&
                         _roomCandidates.Contains ( _partyRoom );

        if ( canReturn )
        {
            if ( Random.value < _data.ReturnPartyRoomChance )
                return _partyRoom;

            //복귀를 선택하지 않았다면 다른 방문 후보만 사용
            _roomCandidates.Remove ( _partyRoom );
        }

        if ( _roomCandidates.Count == 0 )
        {
            return null;
        }

        return _roomCandidates [ Random.Range ( 0 , _roomCandidates.Count ) ];
    }

    /// <summary>
    /// 같은 방 안의 유효 목적지로 이동 시작
    /// </summary>
    /// <returns>이동 시작 여부</returns>
    bool TryBeginLocalMove ( )
    {
        //방 밖이나 방 영역이 겹치는 위치에서는 임의의 방을 선택하지 않음
        if ( RoomRegister.TryGetRoom ( _mover.Position , out Room room ) == false )
            return false;

        return _mover.TryBeginLocalMove ( room );
    }

    /// <summary>
    /// 목적지 방을 선정하고 공통 경로 이동 시작
    /// </summary>
    /// <returns>방 이동을 요청했는지 여부</returns>
    bool TryBeginRoomMove ( )
    {
        Room destination = SelectRoom ( );
        return destination != null && _mover.TryBeginRoomMove ( destination , _currentRoom );
    }

    #endregion

#if UNITY_EDITOR
    /// <summary>
    /// 일반 행동과 대기 상태 조회
    /// </summary>
    /// <param name="details">표시 항목</param>
    public void GetDebugDetails ( List<AnomalyDebugDetail> details )
    {
        details.Add( new( "일반 행동", _normalAction.ToString( ) ) );
        details.Add( new( "현재 방", EnemyDebugUtility.Name( _currentRoom ) ) );
        details.Add( new( "대기 / 방 판단", $"{_waitRemaining:F1} / {_roomCheckRemaining:F1}초" ) );
    }
#endif
}
