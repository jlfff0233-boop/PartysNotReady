using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 이동 변칙 사건 시스템 - 발생, 시간 갱신과 재배치 결과 관리
/// </summary>
public class DisplaceAnomalySystem : AnomalyEventSystem
{
    [Header( "----- 사건 설정 -----" )]
    [SerializeField] ItemDisplacementSettings _map = new( );     //이동 위치 선정과 배치 검사 설정

    List<DisplaceOccurrence> _occurrences = new( );     //진행 또는 사후 복구 대기 중인 사건

    /// <summary>
    /// 디버그 화면의 사건 종류
    /// </summary>
    public override string DebugName => "이동";
    /// <summary>
    /// 씬에서 설정을 조회할 사건 종류
    /// </summary>
    public override AnomalyEventType EventType => AnomalyEventType.Displace;

    /// <summary>
    /// 현재 관리 중인 발생 건 수
    /// </summary>
    public override int DebugOccurrenceCount => _occurrences.Count;


    private void Update ()
    {
        //결과 콜백에서 목록이 변경돼도 현재 순회 대상은 유지
        DisplaceOccurrence [ ] snapshot = _occurrences.ToArray( );

        foreach ( DisplaceOccurrence occurrence in snapshot )
        {
            if ( _occurrences.Contains( occurrence ) == false )
                continue;

            if ( IsTargetValid( occurrence ) == false )
            {
                CancelOccurrence( occurrence );
                continue;
            }

            //실패 이후에도 사후 처리 시간 진행
            occurrence.UpdateElapsedTime( Time.timeAsDouble );

            if ( _occurrences.Contains( occurrence ) == true )
                occurrence.Event.TryExpire( );
        }
    }

    private void OnDisable ()
    {
        while ( _occurrences.Count > 0 )
            CancelOccurrence( _occurrences [ _occurrences.Count - 1 ] );
    }


    #region ----- 발생 -----

    /// <summary>
    /// 배치와 대상 무효화 및 사건 상태 알림 연결
    /// </summary>
    /// <param name="occurrence">연결할 발생 건</param>
    void Subscribe ( DisplaceOccurrence occurrence )
    {
        occurrence.PlacementHandler =
            placement => HandlePlacement( occurrence, placement );

        occurrence.InvalidatedHandler =
            () => CancelOccurrence( occurrence );

        occurrence.StatusHandler =
            ( previous, current ) => HandleStatusChanged( occurrence, previous, current );

        occurrence.Target.OnPlacementAttempt += occurrence.PlacementHandler;
        occurrence.Target.OnInvalidated += occurrence.InvalidatedHandler;
        occurrence.Event.OnStatusChanged += occurrence.StatusHandler;
    }

    /// <summary>
    /// 이동 변칙 사건 시작
    /// </summary>
    /// <param name="startedEvent">발생한 사건</param>
    /// <returns>발생 성공 여부</returns>
    public override bool TryStart ( out AnomalyEvent startedEvent )
    {
        startedEvent = null;

        if ( isActiveAndEnabled == false )
            return false;

        List<AnomalyTarget> candidates = new( AnomalyRegister.Targets );

        foreach ( AnomalyTarget target in candidates )
        {
            if ( TryStart( target, out startedEvent ) == true )
                return true;
        }

        return false;
    }


    /// <summary>
    /// 선정한 대상에 이동 사건 시작
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
    /// 지정 대상에 이동을 적용하고 사건 시작
    /// </summary>
    /// <param name="target">사건 대상</param>
    /// <param name="startedEvent">발생한 사건</param>
    /// <returns>발생 성공 여부</returns>
    bool TryStart (
        AnomalyTarget target, out AnomalyEvent startedEvent )
    {
        startedEvent = null;

        if ( isActiveAndEnabled == false )
            return Fail( target,
                "Inactive", "사건 관리자가 비활성 상태입니다." );

        if ( target == null || target.isActiveAndEnabled == false )
            return Fail( target,
                "TargetUnavailable", "활성 사건 대상이 필요합니다." );

        if ( TryGetEventTimes(
            target, out AnomalyEventTimes times ) == false )
            return false;

        if ( DisplaceAnomalyEvent.CanStart(
            target.NormalState != null, target.NormalLocked,
            times.TimeLimit, out string reason ) == false )
            return Fail( target, "StartCondition", reason );

        ItemCarry item = target.GetComponent<ItemCarry>( );

        if ( item == null )
            return Fail( target, "CarryMissing", "운반 기능이 필요합니다." );

        DisplaceAnomalyEvent nextEvent = new( times );
        DisplaceOccurrence occurrence = new( target, item, nextEvent );

        //이동 중 발생하는 무효화 알림을 받도록
        //먼저 등록/구독하고 정상 기준 갱신 잠금
        _occurrences.Add( occurrence );
        Subscribe( occurrence );
        target.SetNormalLocked( true );

        //보유 상태와 목적지는 기존 이동 기능에서 판단
        if ( item.TryDisplace( _map, out reason ) == false )
        {
            CancelOccurrence( occurrence );
            return Fail( target, "MoveFailed", reason );
        }

        if ( _occurrences.Contains( occurrence ) == false )
            return Fail( target,
                "PreparationCancelled", "발생 준비가 취소됐습니다." );

        if ( IsTargetValid( occurrence ) == false )
        {
            CancelOccurrence( occurrence );
            return Fail( target,
                "TargetInvalidated", "이동 후 대상이 무효화됐습니다." );
        }

        //이동 결과를 기록하고 실제 이동 완료 시점부터 제한시간 계산
        occurrence.MovedPosition = item.transform.position;
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
    /// 이동 가능한 대상 확인
    /// </summary>
    /// <param name="target">발생 대상</param>
    /// <param name="times">발생 시간 설정</param>
    /// <returns>이동 후보 여부</returns>
    protected override bool CanStartTarget (
        AnomalyTarget target, AnomalyEventTimes times )
    {
        return target.GetComponent<ItemCarry>( ) != null &&
            DisplaceAnomalyEvent.CanStart(
                target.NormalState != null, target.NormalLocked,
                times.TimeLimit, out _ ) == true;
    }

    #endregion

    #region ----- 배치/시간 갱신 -----

    /// <summary>
    /// 실제 배치 완료를 사건 모델에 전달
    /// </summary>
    /// <param name="occurrence">배치한 발생 건</param>
    /// <param name="placement">실제 배치 대상</param>
    void HandlePlacement (
        DisplaceOccurrence occurrence, PlacementTarget placement )
    {
        if ( _occurrences.Contains( occurrence ) == false ||
             occurrence.Event.RequiresNormalLock == false )
            return;

        if ( IsTargetValid( occurrence ) == false )
        {
            CancelOccurrence( occurrence );
            return;
        }

        //배치 완료 시점까지 시간을 반영해 시간 초과를 먼저 처리
        occurrence.UpdateElapsedTime( Time.timeAsDouble );

        //실패 결과 콜백에서 사건이 취소되거나 대상이 바뀔 수 있음
        if ( _occurrences.Contains( occurrence ) == false )
            return;

        if ( IsTargetValid( occurrence ) == false )
        {
            CancelOccurrence( occurrence );
            return;
        }

        //원래 위치가 아니어도 허용된 표면에 내려놓으면 해결/복구 판정
        occurrence.Event.TryPlace(
            placement != null, occurrence.Item.IsHold );
    }

    #endregion

    #region ----- 종료 -----

    /// <summary>
    /// 발생 건의 구독과 정상 기준 잠금 해제
    /// </summary>
    /// <param name="occurrence">정리할 발생 건</param>
    void ReleaseOccurrence ( DisplaceOccurrence occurrence )
    {
        if ( _occurrences.Remove( occurrence ) == false )
            return;

        occurrence.Event.OnStatusChanged -= occurrence.StatusHandler;

        if ( occurrence.Target != null )
        {
            occurrence.Target.OnPlacementAttempt -= occurrence.PlacementHandler;
            //만료된 위치의 정상 기준 갱신 요청 후 잠금 해제
            occurrence.ReleaseTarget( occurrence.Event.Status == AnomalyStatus.Expired );
        }

        //해결과 복구의 정상 기준 갱신은 AnomalyTarget.HandlePlaced가 담당
        //취소에서는 위치를 복원하거나 정상 기준을 새로 저장하지 않음
    }

    /// <summary>
    /// 준비 또는 진행 중인 발생 건 취소
    /// </summary>
    /// <param name="occurrence">취소할 발생 건</param>
    void CancelOccurrence ( DisplaceOccurrence occurrence )
    {
        CancelManagedOccurrence(
            _occurrences, occurrence, occurrence.Event, ReleaseOccurrence );
    }

    /// <summary>
    /// 사건 상태 변경에 따른 정리와 결과 전달
    /// </summary>
    /// <param name="occurrence">상태가 변경된 발생 건</param>
    /// <param name="previous">이전 상태</param>
    /// <param name="current">현재 상태</param>
    void HandleStatusChanged (
        DisplaceOccurrence occurrence,
        AnomalyStatus previous, AnomalyStatus current )
    {
        RecordDebugStatus( occurrence );

        if ( occurrence.Event.RequiresNormalLock == false )
            ReleaseOccurrence( occurrence );


        Result( occurrence.Event, current );
    }

    #endregion

    #region ----- 상태 검사 -----

    /// <summary>
    /// 저장된 대상 참조와 사건 관리 상태 확인
    /// </summary>
    /// <param name="occurrence">검사할 발생 건</param>
    /// <returns>발생 건 유지 가능 여부</returns>
    bool IsTargetValid ( DisplaceOccurrence occurrence )
    {
        //플레이어 보유는 복구 과정이므로 IsHold로 취소하지 않음
        return isActiveAndEnabled == true &&
               occurrence.Target != null &&
               occurrence.Target.isActiveAndEnabled == true &&
               occurrence.Target.NormalState != null &&
               occurrence.Target.NormalLocked == true &&
               occurrence.Item != null &&
               occurrence.Item.isActiveAndEnabled == true;
    }

    /// <summary>
    /// 실제 발생 실패 사유를 공통 디버그 요청에 전달
    /// </summary>
    /// <param name="target">발생 대상</param>
    /// <param name="code">실패 코드</param>
    /// <param name="message">실패 설명</param>
    /// <returns>발생 실패</returns>
    bool Fail ( AnomalyTarget target, string code, string message )
    {
        ReportStartFailure(
            target, new AnomalyStartFailure(
                "Displacement", code, message ), false );

        return false;
    }

    #endregion

    #region ----- 디버그 -----

    /// <summary>
    /// 발생 건 읽기 전용 조회
    /// </summary>
    /// <param name="index">발생 건 인덱스</param>
    /// <returns>디버그 조회 대상</returns>
    public override IAnomalyDebugOccurrence GetDebugOccurrence ( int index )
    {
        return _occurrences [ index ];
    }

    /// <summary>
    /// 지정 대상 디버그 요청 연결
    /// </summary>
    /// <param name="target">디버그 창에서 선택한 대상</param>
    /// <param name="secondTarget">교환 사건의 두 번째 대상</param>
    /// <param name="startedEvent">발생한 사건</param>
    /// <returns>발생 성공 여부</returns>
    protected override bool TryStartDebugManual (
        AnomalyTarget target, AnomalyTarget secondTarget,
        out AnomalyEvent startedEvent )
    {
        return TryStart( target, out startedEvent );
    }


    #endregion

}
