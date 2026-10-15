#if UNITY_EDITOR
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 적 디버그 - 공통 상태 조회
/// </summary>
public abstract partial class Enemy
{

    /// <summary>
    /// 디버그 상세 조회에 필요한 초기화 완료 여부
    /// </summary>
    public bool DebugIsReady => _isReady;

    /// <summary>
    /// 고정 등장 테스트에 적용할 시작 방
    /// </summary>
    public Room DebugStartRoom => _startRoom;

    /// <summary>
    /// 테스트 재실행 전에 연결 사건과 기존 활동 정리
    /// </summary>
    /// <param name="reason">준비 실패 사유</param>
    /// <returns>재준비 성공 여부</returns>
    public bool TryResetDebugActivity ( out string reason )
    {
        if ( EnemyDebugUtility.CheckReady( this, out reason ) == false )
            return false;

        //사건 취소 결과가 적의 이전 행동으로 돌아오지 않도록 먼저 연결 해제
        if ( this is IAnomalyEventOwner owner )
        {
            AnomalyEvent occurrence = owner.EventLinker.DebugEvent;
            owner.EventLinker.Stop( );
            occurrence?.TryCancel( );
        }

        //공격과 추적 등 현재 상태가 소유한 작업도 정상 종료
        _currentState?.Exit( );
        //초기화 상태를 유지하고 각 적의 정리와 구독 복구 흐름 재사용
        ResetAppearance( );
        Begin( );
        PlayDebugHistory.Record( "테스트 준비", this, "기존 활동 정리 완료" );
        return true;
    }

    /// <summary>
    /// 고정 시작 방에서 반복 등장 테스트 준비
    /// </summary>
    /// <param name="requestedRoom">디버그에서 선택한 방</param>
    /// <param name="room">실제 적용 방</param>
    /// <param name="reason">준비 실패 사유</param>
    /// <returns>준비 성공 여부</returns>
    public virtual bool TryPrepareDebugSpawn ( Room requestedRoom, out Room room, out string reason )
    {
        room = _startRoom;
        if ( room == null || room.isActiveAndEnabled == false )
        {
            reason = "활성 시작 방이 연결되어 있지 않습니다.";
            return false;
        }
        return TryResetDebugActivity( out reason );
    }

    /// <summary>
    /// 적 공통 상태 조회
    /// </summary>
    /// <param name="details">표시 항목</param>
    public virtual void GetDebugDetails ( List<AnomalyDebugDetail> details )
    {
        details.Add( new( "상태", CurrentState?.StateType.ToString( ) ?? "미시작" ) );
        details.Add( new( "시작 방", _startRoom != null ? _startRoom.name : "미연결" ) );
        details.Add( new( "활성", isActiveAndEnabled.ToString( ) ) );
        details.Add( new( "준비 / 시작 허용", $"{_isReady} / {_isRunning}" ) );
    }

}
#endif
