using UnityEngine;

using System.Collections.Generic;

/// <summary>
/// 제거 변칙 사건 시스템 - 숨김, 정상 복구와 종료 결과 관리
/// </summary>
public class RemoveAnomalySystem : AnomalyEventSystem
{
    List<RemoveOccurrence> _occurrences = new( );       //관리 중인 사건
    List<AnomalyTarget> _candidates = new( );       //등록 대상 복사본

    /// <summary>
    /// 디버그 표시 이름
    /// </summary>
    public override string DebugName => "제거";
    /// <summary>
    /// 씬에서 설정을 조회할 사건 종류
    /// </summary>
    public override AnomalyEventType EventType => AnomalyEventType.Remove;

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

            RemoveOccurrence occurrence = _occurrences [ i ];

            if ( CheckPendingRestore( occurrence ) == false )
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
    /// 복구 요청과 완료, 대상 무효화, 상태 알림 연결
    /// </summary>
    /// <param name="occurrence">연결할 발생 건</param>
    void Subscribe ( RemoveOccurrence occurrence )
    {
        occurrence.ActionRequestedHandler = id => HandleActionRequested( occurrence, id );
        occurrence.Presence.OnActionRequested += occurrence.ActionRequestedHandler;

        occurrence.RestoreRequestedHandler = id => HandleRestoreRequested( occurrence, id );
        occurrence.RestoredHandler = id => HandleRestored( occurrence, id );
        occurrence.InvalidatedHandler = () => CancelOccurrence( occurrence );
        occurrence.StatusHandler =
            ( previous, current ) => HandleStatusChanged( occurrence, previous, current );

        occurrence.Presence.OnRestoreRequested += occurrence.RestoreRequestedHandler;
        occurrence.Presence.OnRestored += occurrence.RestoredHandler;
        occurrence.Target.OnInvalidated += occurrence.InvalidatedHandler;
        occurrence.Event.OnStatusChanged += occurrence.StatusHandler;
    }

    /// <summary>
    /// 지정 대상의 물건을 숨기고 제거 변칙 사건 시작
    /// </summary>
    /// <param name="target">사건 관리 대상</param>
    /// <param name="startedEvent">시작한 사건, 실패하면 null</param>
    /// <returns>발생 성공 여부</returns>
    bool TryStart ( AnomalyTarget target, out AnomalyEvent startedEvent )
    {
        startedEvent = null;
        if ( isActiveAndEnabled == false )
            return Fail( target, "Inactive", "사건 관리자가 비활성 상태입니다." );

        if ( target == null || target.isActiveAndEnabled == false )
            return Fail( target, "TargetUnavailable", "활성 관리 대상이 필요합니다." );

        PresenceController presence = target.GetComponent<PresenceController>( );

        if ( presence == null )
            return Fail( target, "FeatureMissing", "존재 기능이 없습니다." );

        if ( presence.CheckRemoveForRestore( out string reason ) == false )
            return Fail( target, "RestoreUnavailable", reason );

        if ( TryGetEventTimes(
            target, out AnomalyEventTimes times ) == false )
            return false;

        if ( RemoveAnomalyEvent.CanStart(
            true, target.NormalLocked, times.TimeLimit ) == false )
            return Fail( target, "StartCondition",
                "정상 상태 잠금과 유효한 양수 제한시간을 확인해 주세요." );

        RemoveAnomalyEvent nextEvent = new( times );
        RemoveOccurrence occurrence = new( target, presence, nextEvent );
        _occurrences.Add( occurrence );
        Subscribe( occurrence );
        target.SetNormalLocked( true );

        if ( presence.TryRemoveForRestore( ) == false )
        {
            CancelOccurrence( occurrence );
            return Fail( target,
                "RemoveFailed", "복구용 제거에 실패했습니다." );
        }

        if ( _occurrences.Contains( occurrence ) == false )
            return Fail( target,
                "PreparationCancelled", "발생 준비가 취소됐습니다." );

        occurrence.TargetId = presence.InvestigationId;

        if ( CheckPendingRestore( occurrence ) == false )
        {
            CancelOccurrence( occurrence );
            return Fail( target,
                "TargetInvalidated", "제거 후 대상이나 빈자리 조사 상태가 무효화됐습니다." );
        }

        occurrence.LastTickTime = Time.timeAsDouble;

        if ( nextEvent.TryStart( ) == false || CheckManaged( occurrence ) == false )
        {
            CancelOccurrence( occurrence );
            return Fail( target,
                "StartFailed", "사건을 시작하지 못했습니다." );
        }

        startedEvent = nextEvent;
        return true;
    }

    /// <summary>
    /// 등록 대상 중 발생 가능한 사건 하나 시작
    /// </summary>
    /// <param name="startedEvent">시작한 사건, 실패하면 null</param>
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
    /// 선정한 대상에 제거 사건 시작
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
    /// 발생 실패 사유 전달
    /// </summary>
    /// <param name="target">발생 대상</param>
    /// <param name="code">실패 코드</param>
    /// <param name="message">실패 설명</param>
    /// <returns>발생 실패</returns>
    bool Fail ( AnomalyTarget target, string code, string message )
    {
        ReportStartFailure( target,
            new AnomalyStartFailure( "Remove", code, message ), false );
        return false;
    }


    /// <summary>
    /// 제거 가능한 대상 확인
    /// </summary>
    /// <param name="target">발생 대상</param>
    /// <param name="times">발생 시간 설정</param>
    /// <returns>제거 후보 여부</returns>
    protected override bool CanStartTarget (
        AnomalyTarget target, AnomalyEventTimes times )
    {
        PresenceController presence = target.GetComponent<PresenceController>( );

        return presence != null &&
            presence.CheckRemoveForRestore( out _ ) == true &&
            RemoveAnomalyEvent.CanStart(
                true, target.NormalLocked, times.TimeLimit ) == true;
    }

    #endregion

    #region ----- 복구 -----

    /// <summary>
    /// 빈자리 조사 완료를 기록하고 사건 시간 갱신
    /// </summary>
    /// <param name="occurrence">복구를 요청한 발생 건</param>
    /// <param name="investigatedId">복구를 시작한 조사 ID</param>
    void HandleRestoreRequested (
        RemoveOccurrence occurrence, int investigatedId )
    {
        if ( CheckManaged( occurrence ) == false || investigatedId != occurrence.TargetId )
            return;

        //정식 빈자리 조사를 거친 복구인지 기록
        //아직 사건 해결로 처리하지 않음
        occurrence.RestoreRequested = true;
        if ( CheckPendingRestore( occurrence ) == false )
        {
            CancelOccurrence( occurrence );
            return;
        }

        //조사는 재충전 시작
        //해결은 정상 복구 완료 시 판정
        occurrence.UpdateElapsedTime( Time.timeAsDouble );
    }

    /// <summary>
    /// 정상 복구 완료 알림을 사건 결과에 반영
    /// </summary>
    /// <param name="occurrence">복구된 물건의 발생 건</param>
    /// <param name="restoredId">재충전을 시작했던 조사 ID</param>
    void HandleRestored ( RemoveOccurrence occurrence, int restoredId )
    {
        if ( CheckManaged( occurrence ) == false || restoredId != occurrence.TargetId )
            return;

        if ( occurrence.RestoreRequested == false || CheckTarget( occurrence ) == false )
        {
            CancelOccurrence( occurrence );
            return;
        }

        //정상 복구 시 현재 ID는 이미 갱신
        //완료 알림의 이전 ID로 사건을 식별
        int currentId = occurrence.Presence.InvestigationId;
        occurrence.UpdateElapsedTime( Time.timeAsDouble );

        //시간 초과 결과 콜백에서 취소되거나 다른 상태로 바뀌었는지 재확인
        if ( CheckManaged( occurrence ) == false )
            return;

        if ( CheckTarget( occurrence ) == false ||
             occurrence.Presence.InvestigationId != currentId )
        {
            CancelOccurrence( occurrence );
            return;
        }

        if ( occurrence.Event.TryRestore(
            occurrence.TargetId, restoredId, occurrence.Presence.IsPresent,
            occurrence.Presence.IsVisible, occurrence.Presence.IsRestoring ) == false )
            CancelOccurrence( occurrence );
    }

    #endregion

    #region ----- 종료 -----

    /// <summary>
    /// 실제 조사 또는 복구 적용 전에 사건 시간과 만료 확인
    /// </summary>
    /// <param name="occurrence">요청한 발생 건</param>
    /// <param name="targetId">처리할 조사 ID</param>
    void HandleActionRequested ( RemoveOccurrence occurrence, int targetId )
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
    /// 사건 구독과 정상 기준 잠금 해제
    /// </summary>
    /// <param name="occurrence">정리할 발생 건</param>
    void ReleaseOccurrence ( RemoveOccurrence occurrence )
    {
        if ( _occurrences.Remove( occurrence ) == false )
            return;

        occurrence.Event.OnStatusChanged -= occurrence.StatusHandler;

        if ( occurrence.Presence != null )
        {
            occurrence.Presence.OnRestoreRequested -= occurrence.RestoreRequestedHandler;
            occurrence.Presence.OnRestored -= occurrence.RestoredHandler;
            occurrence.Presence.OnActionRequested -= occurrence.ActionRequestedHandler;

            //만료는 조사와 재충전을 끝내고 숨김 유지, 취소는 일반 복구 흐름에 위임
            if ( occurrence.Event.Status == AnomalyStatus.Expired )
                occurrence.Presence.TryEndInvestigation( occurrence.TargetId );

            if ( occurrence.Event.Status == AnomalyStatus.Cancelled )
                occurrence.Presence.ContinuePendingRestore( occurrence.TargetId );
        }

        if ( occurrence.Target != null )
        {
            occurrence.ReleaseTarget( );
        }

        //만료는 숨김 상태를 유지하고 재충전 종료, 취소는 기존 일반 기능 정책 유지
    }

    /// <summary>
    /// 준비 중이거나 진행 중인 사건 취소
    /// </summary>
    /// <param name="occurrence">취소할 발생 건</param>
    void CancelOccurrence ( RemoveOccurrence occurrence )
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
        RemoveOccurrence occurrence,
        AnomalyStatus previous,
        AnomalyStatus current )
    {
        RecordDebugStatus( occurrence );

        //시간 초과 후에도 복구 결과를 받을 수 있도록 잠금과 구독 유지
        if ( occurrence.Event.RequiresNormalLock == false )
            ReleaseOccurrence( occurrence );

        Result( occurrence.Event, current );
    }

    #endregion

    #region ----- 상태 검사 -----

    /// <summary>
    /// 결과를 받을 수 있는 관리 중인 사건인지 확인
    /// </summary>
    /// <param name="occurrence">확인할 발생 건</param>
    /// <returns>결과 수신 가능 여부</returns>
    bool CheckManaged ( RemoveOccurrence occurrence )
    {
        return _occurrences.Contains( occurrence ) == true &&
            occurrence.Event.RequiresNormalLock == true;
    }

    /// <summary>
    /// 대상과 존재 기능 및 정상 잠금 확인
    /// </summary>
    /// <param name="occurrence">확인할 발생 건</param>
    /// <returns>사건 유지 가능 여부</returns>
    bool CheckTarget ( RemoveOccurrence occurrence )
    {
        return isActiveAndEnabled == true &&
               occurrence.Target != null &&
               occurrence.Target.isActiveAndEnabled == true &&
               occurrence.Target.NormalLocked == true &&
               occurrence.Presence != null &&
               occurrence.Presence.IsReady == true;
    }

    /// <summary>
    /// 빈자리 조사 대기와 재충전 단계에 맞는 상태 확인
    /// </summary>
    /// <param name="occurrence">확인할 발생 건</param>
    /// <returns>현재 복구 단계의 유효 여부</returns>
    bool CheckPendingRestore ( RemoveOccurrence occurrence )
    {
        if ( CheckTarget( occurrence ) == false ||
             occurrence.Presence.InvestigationId != occurrence.TargetId ||
             occurrence.Presence.IsPresent == true )
            return false;

        PresenceController presence = occurrence.Presence;

        if ( presence.IsRestoring == true )
            return presence.IsRestoring == true && presence.IsVisible == true &&
                   presence.InvestigationType == PresenceInvestigationType.None;

        return presence.IsRestoring == false && presence.IsVisible == false &&
               presence.InvestigationType == PresenceInvestigationType.Restore &&
               ( presence.CanInvestigate == true || occurrence.Target.IsHeld == true );
    }

    #endregion

    #region ----- 디버그 -----

    /// <summary>
    /// 디버그에서 지정 대상 사건 시작
    /// </summary>
    /// <param name="target">디버그 창에서 선택한 대상</param>
    /// <param name="secondTarget">교환 사건의 두 번째 대상</param>
    /// <param name="startedEvent">시작한 사건, 실패하면 null</param>
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
        return index >= 0 && index < _occurrences.Count ? _occurrences [ index ] : null;
    }

    #endregion

}
