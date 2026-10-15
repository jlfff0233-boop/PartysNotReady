using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 추가 변칙 사건 시스템 - 등장, 조사와 종료 결과 관리
/// </summary>
public class AddAnomalySystem : AnomalyEventSystem
{
    List<AddOccurrence> _occurrences = new( );      //관리 중인 사건
    List<AnomalyTarget> _candidates = new( );       //등록 대상 복사본

    /// <summary>
    /// 디버그 표시 이름
    /// </summary>
    public override string DebugName => "추가";
    /// <summary>
    /// 씬에서 설정을 조회할 사건 종류
    /// </summary>
    public override AnomalyEventType EventType => AnomalyEventType.Add;

    /// <summary>
    /// 관리 중인 사건 수
    /// </summary>
    public override int DebugOccurrenceCount => _occurrences.Count;


    private void Update ()
    {
        for ( int i = _occurrences.Count - 1; i >= 0; i-- )
        {
            //결과 콜백에서 다른 발생 건까지 정리될 수 있음
            if ( i >= _occurrences.Count )
                continue;

            AddOccurrence occurrence = _occurrences [ i ];

            if ( IsTargetValid( occurrence ) == false )
            {
                CancelOccurrence( occurrence );
                continue;
            }

            //조사 알림 없이 사라지거나 다른 물건으로 바뀌면 취소
            if ( occurrence.Presence.IsPresent == false ||
                 occurrence.Presence.IsVisible == false ||
                 occurrence.Presence.InvestigationId != occurrence.TargetId )
            {
                CancelOccurrence( occurrence );
                continue;
            }

            occurrence.UpdateElapsedTime( Time.timeAsDouble );
            if ( _occurrences.Contains( occurrence ) == true )
                occurrence.Event.TryExpire( );
        }
    }

    private void OnDisable ()
    {
        //모든 사건 취소
        while ( _occurrences.Count > 0 )
            CancelOccurrence( _occurrences [ _occurrences.Count - 1 ] );
    }


    #region ----- 발생 -----

    /// <summary>
    /// 조사 제거와 무효화 및 상태 알림 연결
    /// </summary>
    /// <param name="occurrence">연결할 발생 건</param>
    void Subscribe ( AddOccurrence occurrence )
    {
        occurrence.ActionRequestedHandler =
            id => HandleActionRequested( occurrence, id );
        occurrence.Presence.OnActionRequested += occurrence.ActionRequestedHandler;

        occurrence.InvestigatedHandler =
            id => HandleInvestigated( occurrence, id );

        occurrence.InvalidatedHandler =
            () => CancelOccurrence( occurrence );

        occurrence.StatusHandler =
            ( previous, current ) =>
                HandleStatusChanged( occurrence, previous, current );

        occurrence.Presence.OnInvestigated += occurrence.InvestigatedHandler;
        occurrence.Target.OnInvalidated += occurrence.InvalidatedHandler;
        occurrence.Event.OnStatusChanged += occurrence.StatusHandler;
    }

    /// <summary>
    /// 지정 대상에 물건을 등장시키고 사건 시작
    /// </summary>
    /// <param name="target">사건 관리 대상</param>
    /// <param name="startedEvent">시작한 사건</param>
    /// <returns>발생 성공 여부</returns>
    bool TryStart (
        AnomalyTarget target, out AnomalyEvent startedEvent )
    {
        startedEvent = null;

        if ( isActiveAndEnabled == false )
            return Fail( target, "Inactive", "사건 관리자가 비활성 상태입니다." );

        if ( target == null || target.isActiveAndEnabled == false )
            return Fail( target, "TargetUnavailable", "활성 관리 대상이 필요합니다." );

        PresenceController presence = target.GetComponent<PresenceController>( );

        if ( presence == null )
            return Fail( target, "FeatureMissing", "등장 기능이 없습니다." );

        if ( TryGetEventTimes(
            target, out AnomalyEventTimes times ) == false )
            return false;

        if ( AddAnomalyEvent.CanStart(
            presence.IsReady,
            presence.IsPresent,
            presence.IsVisible,
            target.NormalLocked,
            times.TimeLimit ) == false )
            return Fail( target,
                "StartCondition", "준비 상태, 존재 여부, " +
                "잠금 또는 제한시간을 확인해 주세요." );

        AddAnomalyEvent nextEvent = new( times );
        AddOccurrence occurrence = new( target, presence, nextEvent );

        //등장 중 콜백으로 대상이 무효화돼도 정리할 수 있도록 먼저 등록과 구독
        _occurrences.Add( occurrence );
        Subscribe( occurrence );
        target.SetNormalLocked( true );

        if ( presence.TryShow( ) == false )
        {
            string reason = presence.SpaceFailureReason;
            CancelOccurrence( occurrence );

            return Fail( target, "ShowFailed",
                string.IsNullOrEmpty( reason ) ? "물건 표시에 실패했습니다." : reason );
        }

        if ( _occurrences.Contains( occurrence ) == false )
            return Fail( target, "PreparationCancelled", "발생 준비가 취소됐습니다." );

        if ( IsTargetValid( occurrence ) == false ||
             presence.IsPresent == false || presence.IsVisible == false )
        {
            CancelOccurrence( occurrence );
            return Fail( target, "TargetInvalidated", "등장 후 대상이 무효화됐습니다." );
        }

        occurrence.TargetId = presence.InvestigationId;
        occurrence.LastTickTime = Time.timeAsDouble;

        if ( nextEvent.TryStart( ) == false )
        {
            CancelOccurrence( occurrence );
            return Fail( target, "StartFailed", "사건을 시작하지 못했습니다." );
        }

        startedEvent = nextEvent;
        return true;
    }

    /// <summary>
    /// 등록 대상 중 발생 가능한 사건 하나 시작
    /// </summary>
    /// <param name="startedEvent">시작한 사건</param>
    /// <returns>발생 성공 여부</returns>
    public override bool TryStart ( out AnomalyEvent startedEvent )
    {
        startedEvent = null;
        _candidates.Clear( );
        _candidates.AddRange( AnomalyRegister.Targets );

        for ( int i = 0; i < _candidates.Count; i++ )
        {
            if ( TryStart( _candidates [ i ], out startedEvent ) == true )
                return true;
        }

        return false;
    }


    /// <summary>
    /// 선정한 대상에 추가 사건 시작
    /// </summary>
    /// <param name="target">발생 대상</param>
    /// <param name="startedEvent">발생한 사건</param>
    /// <returns>실제 시작 여부</returns>
    protected override bool TryStartTarget (
        AnomalyTarget target, out AnomalyEvent startedEvent )
    {
        return TryStart( target, out startedEvent );
    }


    /// <summary>
    /// 등장 가능한 대상 확인
    /// </summary>
    /// <param name="target">발생 대상</param>
    /// <param name="times">발생 시간 설정</param>
    /// <returns>추가 후보 여부</returns>
    protected override bool CanStartTarget (
        AnomalyTarget target, AnomalyEventTimes times )
    {
        PresenceController presence = target.GetComponent<PresenceController>( );

        return presence != null &&
            AddAnomalyEvent.CanStart(
                presence.IsReady, presence.IsPresent,
                presence.IsVisible, target.NormalLocked,
                times.TimeLimit ) == true;
    }

    #endregion

    #region ----- 종료 -----

    /// <summary>
    /// 사건 구독과 정상 기준 잠금 해제
    /// </summary>
    /// <param name="occurrence">정리할 발생 건</param>
    void ReleaseOccurrence ( AddOccurrence occurrence )
    {
        if ( _occurrences.Remove( occurrence ) == false )
            return;

        occurrence.Event.OnStatusChanged -= occurrence.StatusHandler;

        if ( occurrence.Presence != null )
        {
            occurrence.Presence.OnInvestigated -= occurrence.InvestigatedHandler;
            occurrence.Presence.OnActionRequested -= occurrence.ActionRequestedHandler;

            if ( occurrence.Event.Status == AnomalyStatus.Expired )
                occurrence.Presence.TryEndInvestigation( occurrence.TargetId );
        }

        if ( occurrence.Target != null )
        {
            occurrence.ReleaseTarget( );
        }

        //만료는 등장한 물건을 유지하고 조사만 종료, 취소도 자동으로 숨기지 않음
    }

    /// <summary>
    /// 준비 중/진행 중인 사건 취소
    /// </summary>
    /// <param name="occurrence">취소할 발생 건</param>
    void CancelOccurrence ( AddOccurrence occurrence )
    {
        CancelManagedOccurrence(
            _occurrences, occurrence, occurrence.Event, ReleaseOccurrence );
    }

    /// <summary>
    /// 상태 변경에 따른 정리와 결과 전달
    /// </summary>
    /// <param name="occurrence">상태가 변경된 발생 건</param>
    /// <param name="previous">이전 상태</param>
    /// <param name="current">현재 상태</param>
    void HandleStatusChanged (
        AddOccurrence occurrence,
        AnomalyStatus previous,
        AnomalyStatus current )
    {
        RecordDebugStatus( occurrence );

        if ( occurrence.Event.RequiresNormalLock == false )
            ReleaseOccurrence( occurrence );


        Result( occurrence.Event, current );
    }

    #endregion

    #region ----- 조사/시간 갱신 -----

    /// <summary>
    /// 조사/복구 적용 전 사건 시간과 만료 확인
    /// </summary>
    /// <param name="occurrence">요청한 발생 건</param>
    /// <param name="targetId">처리할 조사 ID</param>
    void HandleActionRequested ( AddOccurrence occurrence, int targetId )
    {
        if ( _occurrences.Contains( occurrence ) == false ||
             occurrence.Event.RequiresNormalLock == false ||
             occurrence.TargetId != targetId )
            return;

        occurrence.UpdateElapsedTime( Time.timeAsDouble );
        if ( _occurrences.Contains( occurrence ) == true )
            occurrence.Event.TryExpire( );
    }

    /// <summary>
    /// 정식 조사 제거 결과를 사건 모델에 전달
    /// </summary>
    /// <param name="occurrence">제거된 물건의 발생 건</param>
    /// <param name="investigatedId">제거 완료된 조사 ID</param>
    void HandleInvestigated (
        AddOccurrence occurrence, int investigatedId )
    {
        if ( _occurrences.Contains( occurrence ) == false ||
             occurrence.Event.RequiresNormalLock == false )
            return;

        if ( IsTargetValid( occurrence ) == false )
        {
            CancelOccurrence( occurrence );
            return;
        }

        //제거 시점이 제한시간 이후라면 실패를 먼저 반영
        occurrence.UpdateElapsedTime( Time.timeAsDouble );

        //실패 결과 콜백에서 취소됐을 수 있으므로 재확인
        if ( _occurrences.Contains( occurrence ) == false )
            return;

        if ( IsTargetValid( occurrence ) == false ||
             occurrence.Presence.InvestigationId != occurrence.TargetId )
        {
            CancelOccurrence( occurrence );
            return;
        }

        occurrence.Event.TryRemove(
            occurrence.TargetId,
            investigatedId,
            occurrence.Presence.IsPresent,
            occurrence.Presence.IsVisible );
    }

    #endregion

    #region ----- 상태 검사 -----

    /// <summary>
    /// 사건 대상과 존재 기능의 유효성 확인
    /// </summary>
    /// <param name="occurrence">확인할 발생 건</param>
    /// <returns>사건 유지 가능 여부</returns>
    bool IsTargetValid ( AddOccurrence occurrence )
    {
        return isActiveAndEnabled == true &&
               occurrence.Target != null &&
               occurrence.Target.isActiveAndEnabled == true &&
               occurrence.Target.NormalLocked == true &&
               occurrence.Presence != null &&
               occurrence.Presence.IsReady == true;
    }

    /// <summary>
    /// 발생 실패 사유 전달
    /// </summary>
    /// <param name="target">발생 대상</param>
    /// <param name="code">실패 코드</param>
    /// <param name="message">실패 설명</param>
    /// <returns>발생 실패</returns>
    bool Fail ( AnomalyTarget target, string code, string message )
    {
        ReportStartFailure( target,
            new AnomalyStartFailure( "Add", code, message ), false );

        return false;
    }

    #endregion

    #region ----- 디버그 -----

    /// <summary>
    /// 디버그에서 지정 대상 사건 시작
    /// </summary>
    /// <param name="target">디버그 창에서 선택한 대상</param>
    /// <param name="secondTarget">교환 사건의 두 번째 대상</param>
    /// <param name="startedEvent">시작한 사건</param>
    /// <returns>발생 성공 여부</returns>
    protected override bool TryStartDebugManual (
        AnomalyTarget target, AnomalyTarget secondTarget,
        out AnomalyEvent startedEvent )
    {
        return TryStart( target, out startedEvent );
    }

    /// <summary>
    /// 지정 순서의 발생 건 조회
    /// </summary>
    /// <param name="index">조회할 순서</param>
    /// <returns>발생 건, 범위를 벗어나면 null</returns>
    public override IAnomalyDebugOccurrence GetDebugOccurrence ( int index )
    {
        return index >= 0 && index < _occurrences.Count
            ? _occurrences [ index ] : null;
    }

    #endregion


}
