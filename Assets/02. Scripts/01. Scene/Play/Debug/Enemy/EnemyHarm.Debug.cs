#if UNITY_EDITOR
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 공격 적 디버그 - 행동 상태 조회와 테스트 실행
/// </summary>
public partial class EnemyHarm
{
    /// <summary>
    /// 도주 목적지 선택 근거와 경로 시작 결과 기록
    /// </summary>
    /// <param name="position">선택한 도주 목적지</param>
    void RecordDebugEscape ( Vector3 position )
    {
        string source = _hasEscapePoint == true ? "개별 도주 지점" : "거리 기준 입구 도주";		//목적지 선택 근거
        PlayDebugHistory.Record( "공격 적 도주 시작", this,
            $"{source} / 목적지: {position} / 시작 결과: {_escapeNavigator.Status}" );
    }

    /// <summary>
    /// 공격 적 활동 상세 조회
    /// </summary>
    /// <param name="details">표시 항목</param>
    public override void GetDebugDetails (
        List<AnomalyDebugDetail> details )
    {
        base.GetDebugDetails( details );

        if ( _model == null )
            return;
        details.Add( new( "활동 방", EnemyDebugUtility.Name( _model.CurrentRoom ) ) );

        details.Add( new( "방 준비 / 활동 가능",
            $"{_roomActivity.IsReady} / {_roomActivity.CanRun}" ) );

        details.Add( new( "잠복 남은 시간",
            $"{_hider.DebugRemainingTime:F1}초" ) );

        details.Add( new( "RequiredCount / 현재 사건 저지",
            $"{_data.RequiredCount} / {_model.ResolvedCount}" ) );

        details.Add( new( "공격 기준 / 수집 요청",
            $"{CanAttack} / {_collectRequested}" ) );

        details.Add( new( "표적", EnemyDebugUtility.Name( _target as Component ) ) );

        details.Add( new( "도주 경로",
            $"{_escapeNavigator.Status} / {_escapeNavigator.FailureReason}" ) );
    }

    /// <summary>
    /// 현재 위치 또는 지정 방에서 테스트 활동 준비
    /// </summary>
    /// <param name="reason">실패 사유</param>
    /// <param name="room">지정 방, 없으면 현재 위치 사용</param>
    /// <returns>준비 여부</returns>
    public bool TryPrepareDebugActivity ( out string reason, Room room = null )
    {
        //이전 상태와 연결 사건을 정리하고 새 테스트 준비
        room = room != null ? room : _model.CurrentRoom;
        if ( TryResetDebugActivity( out reason ) == false )
            return false;

        if ( room != null )
        {
            if ( _roomActivity.TryPrepareDebug( room ) == false )
            {
                reason = "지정 방 재배치 실패. 콘솔의 방 이동 실패 사유를 확인해 주세요.";
                return false;
            }
        }
        else
        {
            _hider.Begin( 0f );

            if ( _roomActivity.TryPrepare(
                _model.CurrentRoom, _data.HideDuration, out room ) == false )
            {
                reason = "현재 위치의 방 또는 잠복 이동 경로를 준비하지 못했습니다.";
                return false;
            }
        }

        _model.ResetDebugCounts( room );
        _model.Begin( room );
        _anomalyLinker.Begin( );

        PlayDebugHistory.Record( "테스트", this,
            $"방 준비: {room.name} / 잠복 대기 생략" );

        return true;
    }

    /// <summary>
    /// 지정 사건 테스트를 위한 시작과 방 활동 준비
    /// </summary>
    /// <param name="room">사건을 발생시킬 방</param>
    /// <param name="reason">준비 실패 사유</param>
    /// <returns>준비 성공 여부</returns>
    public bool TryPrepareDebugEvent ( Room room, out string reason )
    {
        if ( TryPrepareDebugActivity( out reason, room ) == false )
            return false;

        _isPressureApproved = true;
        return true;
    }

    /// <summary>
    /// 지정 방에서 연결 사건 테스트 준비
    /// </summary>
    /// <param name="requestedRoom">테스트 방</param>
    /// <param name="room">실제 적용 방</param>
    /// <param name="reason">준비 실패 사유</param>
    /// <returns>준비 성공 여부</returns>
    public override bool TryPrepareDebugSpawn ( Room requestedRoom, out Room room, out string reason )
    {
        room = requestedRoom;
        return TryPrepareDebugEvent( room, out reason );
    }

    /// <summary>
    /// 사건 저지 기준을 채워 보복 분기 준비
    /// </summary>
    /// <param name="reason">실행 결과</param>
    /// <param name="room">테스트 방, 없으면 현재 활동 방</param>
    /// <returns>준비 여부</returns>
    public bool TryDebugRetaliation ( out string reason, Room room = null )
    {
        if ( TryPrepareDebugActivity( out reason, room ) == false )
            return false;

        for ( int i = 0; i < _model.RequiredCount && CanAttack == false; i++ )
            _model.RecordInterruption( _model.CurrentRoom );

        _isPressureApproved = true;

        reason = "사건 저지 기준 적용. 정상 보복 확률과 표적 검사는 유지합니다.";

        PlayDebugHistory.Record( "테스트", this, reason );

        return true;
    }
}
#endif
