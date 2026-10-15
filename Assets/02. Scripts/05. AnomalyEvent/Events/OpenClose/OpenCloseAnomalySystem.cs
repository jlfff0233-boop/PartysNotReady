using System;
using System.Collections.Generic;
using UnityEngine;
using StartFailure = AnomalyDebugViewBuilder.StartFailure;

/// <summary>
/// 개폐 변칙 사건 시스템 - 발생, 시간 갱신과 복구 결과 관리
/// </summary>
public class OpenCloseAnomalySystem : AnomalyEventSystem
{
    List<OpenCloseOccurrence> _occurrences = new( );     //진행 및 사후 복구 대기 목록

    /// <summary>
    /// 디버그 화면의 사건 종류
    /// </summary>
    public override string DebugName => "개폐";
    /// <summary>
    /// 씬에서 설정을 조회할 사건 종류
    /// </summary>
    public override AnomalyEventType EventType => AnomalyEventType.OpenClose;
    /// <summary>
    /// 현재 관리 중인 발생 건 수
    /// </summary>
    public override int DebugOccurrenceCount => _occurrences.Count;


    private void Update ()
    {
        for ( int i = _occurrences.Count - 1; i >= 0; i-- )
        {
            //결과 콜백에서 관리자가 종료돼 목록이 정리될 수 있음
            if ( i >= _occurrences.Count )
                continue;

            OpenCloseOccurrence occurrence = _occurrences [ i ];

            if ( IsTargetValid( occurrence ) == false )
            {
                CancelOccurrence( occurrence );
                continue;
            }

            //실패 이후에도 사후 복구 시간 진행
            occurrence.UpdateElapsedTime( Time.timeAsDouble );

            //실패 알림 중 정리된 발생 건은 다시 종료하지 않음
            if ( _occurrences.Contains( occurrence ) )
                occurrence.Event.TryExpire( );
        }
    }


    private void OnDisable ()
    {
        while ( _occurrences.Count > 0 )
        {
            OpenCloseOccurrence occurrence =
                _occurrences [ _occurrences.Count - 1 ];

            CancelOccurrence( occurrence );
        }
    }


    #region ----- 발생 -----

    /// <summary>
    /// 발생 건별 개폐와 무효화 및 상태 알림 연결
    /// </summary>
    /// <param name="occurrence">연결할 발생 건</param>
    void Subscribe ( OpenCloseOccurrence occurrence )
    {
        //해제할 때 같은 델리게이트를 사용하도록 참조 보관
        occurrence.OpenHandler =
            isOpen => HandleOpenAttempt( occurrence, isOpen );

        occurrence.InvalidatedHandler =
            () => CancelOccurrence( occurrence );

        occurrence.StatusHandler =
            ( previous, current ) =>
                HandleStatusChanged( occurrence, previous, current );

        occurrence.OpenClose.OnOpenChanged += occurrence.OpenHandler;
        occurrence.Target.OnInvalidated += occurrence.InvalidatedHandler;
        occurrence.Event.OnStatusChanged += occurrence.StatusHandler;
    }

    /// <summary>
    /// 지정한 대상에 개폐 변칙 사건 발생
    /// </summary>
    /// <param name="target">사건 대상</param>
    /// <param name="showLog">발생 실패 로그 출력 여부</param>
    /// <param name="startedEvent">발생한 사건</param>
    /// <returns>사건 발생 성공 여부</returns>
    bool TryStart (
        AnomalyTarget target, bool showLog, out AnomalyEvent startedEvent )
    {
        startedEvent = null;

        if ( isActiveAndEnabled == false )
        {
            ReportStartFailure(
                "개폐", target, StartFailure.PresenterInactive, showLog, this );
            return false;
        }

        if ( target == null || target.isActiveAndEnabled == false )
        {
            ReportStartFailure(
                "개폐", target, StartFailure.TargetUnavailable, showLog, this );
            return false;
        }

        if ( target.NormalLocked == true )
        {
            ReportStartFailure(
                "개폐", target, StartFailure.NormalLocked, showLog, this );
            return false;
        }

        if ( TryGetEventTimes(
            target, out AnomalyEventTimes times ) == false )
            return false;

        OpenCloseController openClose =
            target.GetComponent<OpenCloseController>( );

        if ( openClose == null || openClose.CanInteract == false )
        {
            ReportStartFailure(
                "개폐", target, StartFailure.OpenCloseUnavailable, showLog, this );
            return false;
        }

        ItemCarry item = target.GetComponent<ItemCarry>( );

        //배치 정상 데이터 대신 실제 보유 상태로 다른 적의 보유 대상 제외
        if ( item != null && item.CurrentType == HoldType.World )
        {
            ReportStartFailure(
                "개폐", target, StartFailure.ItemHeld, showLog, this, item.CurrentType );
            return false;
        }

        //발생 직전 개폐 상태를 이번 사건의 복구 기준으로 확보
        bool isOpenNormal = openClose.IsOpen;

        //열린 대상은 닫고 닫힌 대상은 열기
        bool isOpenAnomaly = !isOpenNormal;

        OpenCloseAnomalyEvent nextEvent = new(
            isOpenNormal, times );

        OpenCloseOccurrence occurrence = new(
            target, openClose, nextEvent, Time.timeAsDouble );

        //강제 개폐 도중 발생하는 알림과 무효화도 추적
        _occurrences.Add( occurrence );
        Subscribe( occurrence );
        target.SetNormalLocked( true );

        if ( openClose.TrySetOpen( isOpenAnomaly ) == false )
        {
            CancelOccurrence( occurrence );
            ReportStartFailure(
                "개폐", target, StartFailure.StateChangeFailed, showLog, this );
            return false;
        }

        //동기 알림 처리 중 발생 건이 정리됐는지 확인
        if ( _occurrences.Contains( occurrence ) == false )
        {
            ReportStartFailure(
                "개폐", target, StartFailure.PreparationCancelled, showLog, this );
            return false;
        }

        if ( IsTargetValid( occurrence ) == false ||
             openClose.IsOpen != isOpenAnomaly )
        {
            CancelOccurrence( occurrence );
            ReportStartFailure(
                "개폐", target, StartFailure.InvalidAfterStateChange, showLog, this );
            return false;
        }

        //실제 개폐 완료 시점부터 제한시간 계산
        occurrence.LastTickTime = Time.timeAsDouble;

        if ( nextEvent.TryStart( ) == false )
        {
            CancelOccurrence( occurrence );
            ReportStartFailure(
                "개폐", target, StartFailure.StartFailed, showLog, this );
            return false;
        }

        startedEvent = nextEvent;
        return true;
    }

    /// <summary>
    /// 개폐 변칙 사건 시작
    /// </summary>
    /// <param name="startedEvent">발생한 사건</param>
    /// <returns>사건 발생 성공 여부</returns>
    public override bool TryStart ( out AnomalyEvent startedEvent )
    {
        startedEvent = null;

        if ( isActiveAndEnabled == false )
            return false;

        //발생 처리 중 대상이 비활성화돼도
        //등록 목록 순회가 깨지지 않게 복사
        List<AnomalyTarget> candidates = new( AnomalyRegister.Targets );

        foreach ( AnomalyTarget target in candidates )
        {
            //발생 가능한 첫 대상을 선택하고 후보 제외 로그는 생략
            if ( TryStart( target, false, out startedEvent ) == true )
                return true;
        }

        return false;
    }


    /// <summary>
    /// 선정한 대상에 개폐 사건 시작
    /// </summary>
    /// <param name="target">발생 대상</param>
    /// <param name="startedEvent">발생한 사건</param>
    /// <returns>실제 시작 여부</returns>
    protected override bool TryStartTarget (
        AnomalyTarget target, out AnomalyEvent startedEvent )
    {
        return TryStart( target, false, out startedEvent );
    }


    /// <summary>
    /// 개폐 가능한 대상 확인
    /// </summary>
    /// <param name="target">발생 대상</param>
    /// <param name="times">발생 시간 설정</param>
    /// <returns>개폐 후보 여부</returns>
    protected override bool CanStartTarget (
        AnomalyTarget target, AnomalyEventTimes times )
    {
        OpenCloseController openClose = target.GetComponent<OpenCloseController>( );
        ItemCarry item = target.GetComponent<ItemCarry>( );

        return target.NormalLocked == false &&
            openClose != null && openClose.CanInteract == true &&
            ( item == null || item.CurrentType != HoldType.World );
    }

    #endregion

    #region ----- 개폐/시간 갱신 -----

    /// <summary>
    /// 개폐 완료에 따른 정상 상태 복구 판정
    /// </summary>
    /// <param name="occurrence">개폐한 대상의 발생 건</param>
    /// <param name="isOpen">개폐 완료 후 열림 여부</param>
    void HandleOpenAttempt (
        OpenCloseOccurrence occurrence, bool isOpen )
    {
        if ( _occurrences.Contains( occurrence ) == false )
            return;

        //사건을 시작하기 위한 강제 개폐 알림은
        //플레이어의 복구 결과가 아니므로 제외
        if ( occurrence.Event.RequiresNormalLock == false )
            return;

        if ( IsTargetValid( occurrence ) == false )
        {
            CancelOccurrence( occurrence );
            return;
        }

        //제한시간 경계에서는 실패 판정을 먼저 반영
        occurrence.UpdateElapsedTime( Time.timeAsDouble );

        //시간 갱신의 결과 콜백에서 취소되거나 대상이 바뀔 수 있음
        if ( _occurrences.Contains( occurrence ) == false )
            return;

        if ( IsTargetValid( occurrence ) == false )
        {
            CancelOccurrence( occurrence );
            return;
        }

        //같은 알림의 다른 콜백에서
        //개폐 상태가 바뀔 수 있으므로 현재 상태와 다시 비교
        if ( occurrence.OpenClose.IsOpen != isOpen )
            return;

        occurrence.Event.TryRestore( isOpen );
    }

    #endregion

    #region ----- 종료 -----

    /// <summary>
    /// 발생 건의 구독과 잠금 정리
    /// </summary>
    /// <param name="occurrence">정리할 발생 건</param>
    void ReleaseOccurrence ( OpenCloseOccurrence occurrence )
    {
        //이미 정리한 발생 건은 다시 잠금을 변경하지 않음
        if ( _occurrences.Remove( occurrence ) == false )
            return;

        if ( occurrence.OpenClose != null )
            occurrence.OpenClose.OnOpenChanged -= occurrence.OpenHandler;

        //대상이 존재한다면 무효화 구독과 잠금 정리
        if ( occurrence.Target != null )
        {
            occurrence.ReleaseTarget( );
        }

        occurrence.Event.OnStatusChanged -= occurrence.StatusHandler;
        //만료와 취소에서는 현재 개폐 상태를 유지
    }

    /// <summary>
    /// 준비/진행 중 사건 취소
    /// </summary>
    /// <param name="occurrence">취소할 발생 건</param>
    void CancelOccurrence ( OpenCloseOccurrence occurrence )
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
        OpenCloseOccurrence occurrence,
        AnomalyStatus previous, AnomalyStatus current )
    {
        RecordDebugStatus( occurrence );

        //종료된 발생 건은 외부 결과 전달 전에 정리
        if ( occurrence.Event.RequiresNormalLock == false )
            ReleaseOccurrence( occurrence );


        //공통 결과 전달 함수에서 시작 상태 제외
        Result( occurrence.Event, current );
    }

    #endregion

    #region ----- 상태 검사 -----

    /// <summary>
    /// 발생 대상과 관리 상태의 유효성 확인
    /// </summary>
    /// <param name="occurrence">확인할 발생 건</param>
    /// <returns>사건 유지 가능 여부</returns>
    bool IsTargetValid ( OpenCloseOccurrence occurrence )
    {
        return isActiveAndEnabled == true &&
               occurrence.Target != null &&
               occurrence.Target.isActiveAndEnabled == true &&
               occurrence.Target.NormalLocked == true &&
               occurrence.OpenClose != null &&
               occurrence.OpenClose.CanInteract == true;
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
        return TryStart( target, false, out startedEvent );
    }


    #endregion

}
