using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 대체 변칙 사건 시스템 - 발생, 조사와 종료 결과 관리
/// </summary>
public class ReplaceAnomalySystem : AnomalyEventSystem
{
    /// <summary>
    /// 관리 중인 사건 리스트
    /// </summary>
    readonly List<ReplaceOccurrence> _occurrences = new( );
    /// <summary>
    /// 등록 대상 복사본 리스트
    /// </summary>
    readonly List<AnomalyTarget> _candidates = new( );

    /// <summary>
    /// 디버그 표시 이름
    /// </summary>
    public override string DebugName => "대체";

    /// <summary>
    /// 설정을 조회할 사건 종류
    /// </summary>
    public override AnomalyEventType EventType => AnomalyEventType.Replace;

    /// <summary>
    /// 관리 중인 발생 건 수
    /// </summary>
    public override int DebugOccurrenceCount => _occurrences.Count;


    private void Update ()
    {
        for ( int i = _occurrences.Count - 1; i >= 0; i-- )
        {
            //결과 콜백에서 다른 발생 건까지 제거된 경우 범위 재확인
            if ( i >= _occurrences.Count )
                continue;

            ReplaceOccurrence occurrence = _occurrences [ i ];

            if ( CheckChanging( occurrence ) == false )
            {
                CancelOccurrence( occurrence );
                continue;
            }

            UpdateOccurrence( occurrence );
        }
    }

    private void OnDisable ()
    {
        //마지막 발생 건부터 정리하여 목록 변경 중 누락 방지
        while ( _occurrences.Count > 0 )
            CancelOccurrence( _occurrences [ _occurrences.Count - 1 ] );
    }


    #region ----- 진행/조사 -----

    /// <summary>
    /// 경과 시간을 반영하고 사후 처리 만료 확인
    /// </summary>
    /// <param name="occurrence">갱신할 발생 건</param>
    void UpdateOccurrence ( ReplaceOccurrence occurrence )
    {
        occurrence.UpdateElapsedTime( Time.timeAsDouble );

        //실패 결과 콜백에서 취소되거나 대상이 변경됐을 수 있음
        if ( CheckManaged( occurrence ) == false )
            return;

        if ( CheckChanging( occurrence ) == false )
        {
            CancelOccurrence( occurrence );
            return;
        }

        if ( occurrence.Event.Status != AnomalyStatus.Failed ||
             occurrence.Event.RecoveryRemainingTime > 0f )
            return;

        //변경된 색/외형을 유지하고 조사를 종료한 뒤 사건 잠금 해제
        if ( occurrence.Replace.TryKeep( occurrence.TargetId ) == true )
            occurrence.Event.TryExpire( );
    }

    /// <summary>
    /// 조사 전에 사건 시간 갱신
    /// </summary>
    /// <param name="occurrence">조사를 요청한 발생 건</param>
    /// <param name="targetId">조사 요청의 ID</param>
    void HandleActionRequested (
        ReplaceOccurrence occurrence, int targetId )
    {
        if ( CheckManaged( occurrence ) == false ||
             occurrence.TargetId != targetId )
            return;

        if ( CheckChanging( occurrence ) == false )
        {
            CancelOccurrence( occurrence );
            return;
        }

        //복원 전에 시간을 반영하여
        //만료된 사건의 색/외형이 조사로 되돌아가지 않게 처리
        UpdateOccurrence( occurrence );
    }

    /// <summary>
    /// 조사 완료 알림으로 사건 해결 판정
    /// </summary>
    /// <param name="occurrence">복원된 대상의 발생 건</param>
    /// <param name="investigatedId">복원 완료 알림의 조사 ID</param>
    void HandleInvestigated (
        ReplaceOccurrence occurrence, int investigatedId )
    {
        if ( CheckManaged( occurrence ) == false ||
             occurrence.TargetId != investigatedId )
            return;

        //복원 완료 알림에서는 IsChanged가 이미 해제되므로
        //대상과 조사 ID로 같은 사건인지 확인
        if ( CheckTarget( occurrence ) == false ||
             occurrence.Replace.InvestigationId != occurrence.TargetId )
        {
            CancelOccurrence( occurrence );
            return;
        }

        //시간은 같은 조사 호출의 OnActionRequested에서 먼저 반영됨
        if ( occurrence.Event.TryRestore(
            occurrence.TargetId, investigatedId,
            occurrence.Replace.IsChanged ) == false )
        {
            if ( CheckManaged( occurrence ) == true )
                CancelOccurrence( occurrence );
        }
    }

    #endregion

    #region ----- 종료 -----

    /// <summary>
    /// 종료한 사건의 조사, 구독과 잠금 해제
    /// </summary>
    /// <param name="occurrence">정리할 발생 건</param>
    void ReleaseOccurrence ( ReplaceOccurrence occurrence )
    {
        if ( _occurrences.Remove( occurrence ) == false )
            return;

        occurrence.Event.OnStatusChanged -= occurrence.StatusHandler;

        if ( occurrence.Replace != null )
        {
            occurrence.Replace.OnActionRequested -= occurrence.ActionRequestedHandler;
            occurrence.Replace.OnInvestigated -= occurrence.InvestigatedHandler;

            //다른 ID의 새 대체는 건드리지 않음
            if ( occurrence.Replace.IsChanged == true &&
                 occurrence.Replace.InvestigationId == occurrence.TargetId )
                occurrence.Replace.TryKeep( occurrence.TargetId );
        }

        if ( occurrence.Target != null )
        {
            occurrence.ReleaseTarget( );
        }
    }

    /// <summary>
    /// 준비 중이거나 진행 중인 사건 취소
    /// </summary>
    /// <param name="occurrence">취소할 발생 건</param>
    void CancelOccurrence ( ReplaceOccurrence occurrence )
    {
        CancelManagedOccurrence(
            _occurrences, occurrence, occurrence.Event, ReleaseOccurrence );
    }

    /// <summary>
    /// 사건 상태를 기록하고 결과 전달
    /// </summary>
    /// <param name="occurrence">상태가 변경된 발생 건</param>
    /// <param name="previous">이전 상태</param>
    /// <param name="current">변경된 상태</param>
    void HandleStatusChanged (
        ReplaceOccurrence occurrence,
        AnomalyStatus previous, AnomalyStatus current )
    {
        RecordDebugStatus( occurrence );

        if ( occurrence.Event.RequiresNormalLock == false )
            ReleaseOccurrence( occurrence );


        Result( occurrence.Event, current );
    }

    #endregion

    #region ----- 발생 -----

    /// <summary>
    /// 사건에 필요한 알림 구독
    /// </summary>
    /// <param name="occurrence">연결할 발생 건</param>
    void Subscribe ( ReplaceOccurrence occurrence )
    {
        occurrence.ActionRequestedHandler =
            id => HandleActionRequested( occurrence, id );

        occurrence.InvestigatedHandler =
            id => HandleInvestigated( occurrence, id );

        occurrence.InvalidatedHandler =
            () => CancelOccurrence( occurrence );

        occurrence.StatusHandler =
            ( previous, current ) =>
                HandleStatusChanged( occurrence, previous, current );

        occurrence.Replace.OnActionRequested += occurrence.ActionRequestedHandler;
        occurrence.Replace.OnInvestigated += occurrence.InvestigatedHandler;
        occurrence.Target.OnInvalidated += occurrence.InvalidatedHandler;
        occurrence.Event.OnStatusChanged += occurrence.StatusHandler;
    }

    /// <summary>
    /// 지정 대상에 대체 변칙 사건 발생
    /// </summary>
    /// <param name="target">발생 대상</param>
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

        ReplaceController replace = target.GetComponent<ReplaceController>( );
        if ( replace == null )
            return Fail( target, "FeatureMissing", "대체 기능이 없습니다." );

        if ( TryGetEventTimes( target, out AnomalyEventTimes times ) == false )
            return false;

        ItemCarry item = target.GetComponent<ItemCarry>( );
        bool isHeld = item != null && item.IsHold;

        if ( ReplaceAnomalyEvent.CanStart(
            replace.CanChange, replace.IsChanged,
            isHeld, target.NormalLocked, times ) == false )
        {
            return Fail( target, "StartCondition",
                "유형별 가중치, 기능 설정, 기존 대체, 보유 또는 사건 잠금을 확인해 주세요." );
        }

        target.SetNormalLocked( true );

        if ( replace.TryChange( ) == false )
        {
            target.SetNormalLocked( false );
            return Fail( target, "ChangeFailed", replace.FailureReason );
        }

        //실제 변경이 성공한 뒤 발급된 ID로 발생 건 생성
        ReplaceAnomalyEvent nextEvent = new( times );
        ReplaceOccurrence occurrence = new(
            target, replace, replace.InvestigationId, nextEvent );

        _occurrences.Add( occurrence );
        Subscribe( occurrence );

        if ( CheckChanging( occurrence ) == false )
        {
            CancelOccurrence( occurrence );
            return Fail( target, "TargetInvalidated",
                "대체 적용 후 대상 또는 기능 상태가 변경됐습니다." );
        }

        occurrence.LastTickTime = Time.timeAsDouble;

        if ( nextEvent.TryStart( ) == false )
        {
            CancelOccurrence( occurrence );
            return Fail( target, "StartFailed", "대체 변칙 사건을 시작하지 못했습니다." );
        }

        //시작 알림 도중 취소 여부 확인
        if ( CheckManaged( occurrence ) == false )
            return Fail( target, "PreparationCancelled", "발생 준비가 취소됐습니다." );

        startedEvent = nextEvent;
        return true;
    }

    /// <summary>
    /// 등록 대상 중 대체 가능한 물건에 사건 발생
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
    /// 선정한 대상에 대체 사건 시작
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
    /// 대체 가능한 대상 확인
    /// </summary>
    /// <param name="target">발생 대상</param>
    /// <param name="times">발생 시간 설정</param>
    /// <returns>대체 후보 여부</returns>
    protected override bool CanStartTarget (
        AnomalyTarget target, AnomalyEventTimes times )
    {
        ReplaceController replace = target.GetComponent<ReplaceController>( );
        ItemCarry item = target.GetComponent<ItemCarry>( );

        return replace != null &&
            ReplaceAnomalyEvent.CanStart(
                replace.CanChange, replace.IsChanged,
                item != null && item.IsHold,
                target.NormalLocked, times ) == true;
    }

    #endregion

    #region ----- 상태 검사 -----

    /// <summary>
    /// 관리 중인 발생 건인지 확인
    /// </summary>
    /// <param name="occurrence">확인할 발생 건</param>
    /// <returns>관리 중인 발생 건 여부</returns>
    bool CheckManaged ( ReplaceOccurrence occurrence )
    {
        return _occurrences.Contains( occurrence );
    }

    /// <summary>
    /// 사건 대상과 대체 기능이 유효한지 확인
    /// </summary>
    /// <param name="occurrence">확인할 발생 건</param>
    /// <returns>유효한 사건 대상 여부</returns>
    bool CheckTarget ( ReplaceOccurrence occurrence )
    {
        return isActiveAndEnabled == true &&
               occurrence.Target != null &&
               occurrence.Target.isActiveAndEnabled == true &&
               occurrence.Target.NormalLocked == true &&
               occurrence.Replace != null &&
               occurrence.Replace.isActiveAndEnabled == true;
    }

    /// <summary>
    /// 발생 당시의 대체가 계속 진행 중인지 확인
    /// </summary>
    /// <param name="occurrence">확인할 발생 건</param>
    /// <returns>같은 대체의 진행 여부</returns>
    bool CheckChanging ( ReplaceOccurrence occurrence )
    {
        return CheckTarget( occurrence ) == true &&
               occurrence.Replace.IsChanged == true &&
               occurrence.Replace.InvestigationId == occurrence.TargetId;
    }

    /// <summary>
    /// 발생 실패 사유를 디버그에 전달
    /// </summary>
    /// <param name="target">발생 대상</param>
    /// <param name="code">실패 식별 코드</param>
    /// <param name="message">실패 설명</param>
    /// <returns>발생 실패</returns>
    bool Fail ( AnomalyTarget target, string code, string message )
    {
        ReportStartFailure( target,
            new AnomalyStartFailure( "Replace", code, message ), false );

        return false;
    }

    #endregion

    #region ----- 디버그 -----

    /// <summary>
    /// 지정한 대상에 대체 변칙 사건 발생
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
    /// 목록 순서로 발생 건 조회
    /// </summary>
    /// <param name="index">조회할 발생 건 순서</param>
    /// <returns>발생 건, 범위를 벗어나면 null</returns>
    public override IAnomalyDebugOccurrence GetDebugOccurrence ( int index )
    {
        return index >= 0 && index < _occurrences.Count
            ? _occurrences [ index ] : null;
    }

    #endregion

}
