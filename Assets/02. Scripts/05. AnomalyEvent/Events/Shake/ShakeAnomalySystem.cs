using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 흔들림 변칙 사건 시스템 - 발생, 조사와 종료 결과 관리
/// </summary>
public class ShakeAnomalySystem : AnomalyEventSystem
{
    List<ShakeOccurrence> _occurrences = new ( );     //진행 및 사후 처리 중인 사건

    /// <summary>
    /// 디버그 화면의 사건 종류
    /// </summary>
    public override string DebugName => "흔들림";
    /// <summary>
    /// 씬에서 설정을 조회할 사건 종류
    /// </summary>
    public override AnomalyEventType EventType => AnomalyEventType.Shake;
    /// <summary>
    /// 현재 관리 중인 발생 건 수
    /// </summary>
    public override int DebugOccurrenceCount => _occurrences.Count;


    private void Update ( )
    {
        for ( int i = _occurrences.Count - 1 ; i >= 0 ; i-- )
        {
            if ( i >= _occurrences.Count )
                continue;

            ShakeOccurrence occurrence = _occurrences [ i ];

            //조사 없이 정지하거나 다른 흔들림으로 바뀌면 취소
            if ( IsTargetValid ( occurrence ) == false ||
                 occurrence.Shake.IsShaking == false )
            {
                CancelOccurrence ( occurrence );
                continue;
            }

            //실패 이후에도 사후 처리 시간 진행
            occurrence.UpdateElapsedTime( Time.timeAsDouble );
            if ( _occurrences.Contains ( occurrence ) == true )
                occurrence.Event.TryExpire ( );
        }
    }

    private void OnDisable ( )
    {
        while ( _occurrences.Count > 0 )
            CancelOccurrence ( _occurrences [ _occurrences.Count - 1 ] );
    }


    #region ----- 발생 -----

    /// <summary>
    /// 흔들림 변칙 사건 시작
    /// </summary>
    /// <param name="startedEvent">발생한 사건</param>
    /// <returns>발생 성공 여부</returns>
    public override bool TryStart ( out AnomalyEvent startedEvent )
    {
        startedEvent = null;

        if ( isActiveAndEnabled == false )
            return false;

        List<AnomalyTarget> candidates = new ( AnomalyRegister.Targets );

        foreach ( AnomalyTarget target in candidates )
        {
            if ( TryStart ( target , out startedEvent ) == true )
                return true;
        }

        return false;
    }


    /// <summary>
    /// 선정한 대상에 흔들림 사건 시작
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
    /// 지정 대상에 흔들림 변칙 사건 시작
    /// </summary>
    /// <param name="target">발생 대상</param>
    /// <param name="startedEvent">발생한 사건</param>
    /// <returns>발생 성공 여부</returns>
    bool TryStart (
        AnomalyTarget target , out AnomalyEvent startedEvent )
    {
        startedEvent = null;

        if ( isActiveAndEnabled == false )
            return RejectStart( target, "Inactive" );

        if ( target == null || target.isActiveAndEnabled == false )
            return RejectStart( target, "TargetUnavailable" );

        if ( target.NormalState == null )
            return RejectStart( target, "NormalMissing" );

        if ( target.NormalLocked == true )
            return RejectStart( target, "Locked" );

        if ( TryGetEventTimes(
            target, out AnomalyEventTimes times ) == false )
            return false;

        ShakePresenter shake = target.GetComponent<ShakePresenter> ( );

        //이미 흔들리는 대상은 새 사건 후보에서 제외
        if ( shake == null )
            return RejectStart( target, "Unavailable" );

        if ( shake.IsShaking == true )
            return RejectStart( target, "AlreadyActive" );

        ShakeAnomalyEvent nextEvent = new( times );
        ShakeOccurrence occurrence = new ( target , shake , nextEvent );

        //발생 준비 중 대상 무효화도 정리할 수 있도록 먼저 연결
        _occurrences.Add ( occurrence );
        Subscribe ( occurrence );
        target.SetNormalLocked ( true );

        if ( shake.TrySetShaking ( true ) == false )
        {
            CancelOccurrence ( occurrence );
            return RejectStart( target, "ApplyFailed" );
        }

        //흔들림 시작 알림에서 관리자가 종료됐을 수 있음
        if ( _occurrences.Contains ( occurrence ) == false )
            return RejectStart( target, "PreparationCancelled" );

        occurrence.ShakeId = shake.InvestigationId;

        if ( IsTargetValid ( occurrence ) == false ||
             shake.CanInvestigate == false )
        {
            CancelOccurrence ( occurrence );
            return RejectStart( target, "InvalidAfterApply" );
        }

        occurrence.LastTickTime = Time.timeAsDouble;

        if ( nextEvent.TryStart ( ) == false )
        {
            CancelOccurrence ( occurrence );
            return RejectStart( target, "StartFailed" );
        }

        startedEvent = nextEvent;
        return true;
    }

    /// <summary>
    /// 발생 건의 조사와 무효화 및 상태 알림 연결
    /// </summary>
    /// <param name="occurrence">연결할 발생 건</param>
    void Subscribe ( ShakeOccurrence occurrence )
    {
        occurrence.InvestigatedHandler =
            shakeId => HandleInvestigated ( occurrence , shakeId );

        occurrence.InvalidatedHandler =
            ( ) => CancelOccurrence ( occurrence );

        occurrence.StatusHandler =
            ( previous , current ) => HandleStatusChanged ( occurrence , previous , current );

        occurrence.Shake.OnInvestigated += occurrence.InvestigatedHandler;
        occurrence.Target.OnInvalidated += occurrence.InvalidatedHandler;
        occurrence.Event.OnStatusChanged += occurrence.StatusHandler;
    }


    /// <summary>
    /// 흔들림 가능한 대상 확인
    /// </summary>
    /// <param name="target">발생 대상</param>
    /// <param name="times">발생 시간 설정</param>
    /// <returns>흔들림 후보 여부</returns>
    protected override bool CanStartTarget (
        AnomalyTarget target, AnomalyEventTimes times )
    {
        ShakePresenter shake = target.GetComponent<ShakePresenter>( );

        return target.NormalState != null &&
            target.NormalLocked == false &&
            shake != null && shake.IsShaking == false;
    }

    #endregion

    #region ----- 진행/종료 -----

    /// <summary>
    /// 해당 흔들림의 조사 완료를 사건 결과로 연결
    /// </summary>
    /// <param name="occurrence">조사한 발생 건</param>
    /// <param name="shakeId">조사 완료된 흔들림 ID</param>
    void HandleInvestigated ( ShakeOccurrence occurrence , int shakeId )
    {
        if ( _occurrences.Contains ( occurrence ) == false ||
             occurrence.Event.RequiresNormalLock == false ||
             occurrence.ShakeId != shakeId )
            return;

        if ( IsTargetValid ( occurrence ) == false )
        {
            CancelOccurrence ( occurrence );
            return;
        }

        //조사 완료 시점까지의 시간을 먼저 반영
        occurrence.UpdateElapsedTime( Time.timeAsDouble );

        //Failed 결과의 외부 콜백에서 취소됐을 수 있음
        if ( _occurrences.Contains ( occurrence ) == false )
            return;

        if ( IsTargetValid ( occurrence ) == false )
        {
            CancelOccurrence ( occurrence );
            return;
        }

        occurrence.Event.TryInvestigate (
            occurrence.Shake.IsShaking == false );
    }

    /// <summary>
    /// 사건 상태 변경에 따른 정리와 결과 전달
    /// </summary>
    /// <param name="occurrence">상태가 변경된 발생 건</param>
    /// <param name="previous">이전 상태</param>
    /// <param name="current">현재 상태</param>
    void HandleStatusChanged (
        ShakeOccurrence occurrence ,
        AnomalyStatus previous ,
        AnomalyStatus current )
    {
        RecordDebugStatus( occurrence );

        if ( occurrence.Event.RequiresNormalLock == false )
            ReleaseOccurrence ( occurrence );


        Result ( occurrence.Event , current );
    }

    /// <summary>
    /// 발생 건의 구독과 정상 기준 잠금 해제
    /// </summary>
    /// <param name="occurrence">정리할 발생 건</param>
    void ReleaseOccurrence ( ShakeOccurrence occurrence )
    {
        if ( _occurrences.Remove ( occurrence ) == false )
            return;

        if ( occurrence.Shake != null )
            occurrence.Shake.OnInvestigated -= occurrence.InvestigatedHandler;

        occurrence.Event.OnStatusChanged -= occurrence.StatusHandler;

        //만료 시 같은 ID의 흔들림만 정지하여 이후 시작한 다른 흔들림은 유지
        //조사 성공 알림 없이 정지한 뒤 정상 기준 잠금 해제
        if ( occurrence.Event.Status == AnomalyStatus.Expired &&
             occurrence.Shake != null &&
             occurrence.Shake.InvestigationId == occurrence.ShakeId )
            occurrence.Shake.TrySetShaking ( false );

        if ( occurrence.Target != null )
        {
            occurrence.ReleaseTarget( );

            //복구 또는 만료 후 현재 상태를 저장, 보유 중이면 이후 배치 알림에서 저장
            bool restored =
                occurrence.Event.Status == AnomalyStatus.Resolved ||
                occurrence.Event.Status == AnomalyStatus.Recovered ||
                occurrence.Event.Status == AnomalyStatus.Expired;

            if ( restored == true && occurrence.Target.IsHeld == false &&
                 occurrence.Target.NormalState != null )
            {
                PlacementTarget placement =
                    occurrence.Target.NormalState.PlacementTarget;

                occurrence.Target.SaveNormalState ( placement );
            }
        }
    }

    /// <summary>
    /// 준비 또는 진행 중인 발생 건 취소
    /// </summary>
    /// <param name="occurrence">취소할 발생 건</param>
    void CancelOccurrence ( ShakeOccurrence occurrence )
    {
        CancelManagedOccurrence(
            _occurrences, occurrence, occurrence.Event, ReleaseOccurrence );
    }

    #endregion

    #region ----- 상태 검사 -----

    /// <summary>
    /// 저장한 대상과 흔들림 ID의 유효성 확인
    /// </summary>
    /// <param name="occurrence">검사할 발생 건</param>
    /// <returns>현재 발생 건 유지 가능 여부</returns>
    bool IsTargetValid ( ShakeOccurrence occurrence )
    {
        //조사 성공 직후에는 정지 상태이므로 IsShaking은 여기서 검사하지 않음
        return isActiveAndEnabled == true &&
               occurrence.Target != null &&
               occurrence.Target.isActiveAndEnabled == true &&
               occurrence.Target.NormalState != null &&
               occurrence.Target.NormalLocked == true &&
               occurrence.Shake != null &&
               occurrence.Shake.IsReady == true &&
               occurrence.Shake.InvestigationId == occurrence.ShakeId;
    }

    #endregion

    #region ----- 디버그 -----

    /// <summary>
    /// 발생 건 읽기 전용 조회
    /// </summary>
    /// <param name="index">조회할 발생 건 번호</param>
    public override IAnomalyDebugOccurrence GetDebugOccurrence ( int index )
    {
        return _occurrences [ index ];
    }

    /// <summary>
    /// 지정 대상 디버그 요청을 실제 발생 함수에 연결
    /// </summary>
    /// <param name="target">디버그 창에서 선택한 대상</param>
    /// <param name="secondTarget">교환 사건의 두 번째 대상</param>
    /// <param name="startedEvent">발생한 사건</param>
    /// <returns>지정 대상 발생 성공 여부</returns>
    protected override bool TryStartDebugManual (
        AnomalyTarget target, AnomalyTarget secondTarget,
        out AnomalyEvent startedEvent )
    {
        return TryStart( target, out startedEvent );
    }


    #endregion

}
