using System.Collections.Generic;
using UnityEngine;
using System;
using Random = UnityEngine.Random;

/// <summary>
/// 교체 변칙 사건 시스템 - 대상 선정, 교체와 배치 결과 관리
/// </summary>
public class SwapAnomalySystem : AnomalyEventSystem
{
    [Header( "----- 교체 -----" )]
    [SerializeField] SwapSettings _swapSettings = new( );     //표면과 공간 검사 설정

    ItemSwapper _swap;     //사건과 독립적으로 교체를 수행하는 실행 모듈
    List<SwapOccurrence> _occurrences = new( );     //관리 중인 발생 건
    bool _isStarting;     //발생 처리 중 재진입 방지
    bool _startCancelled;     //교체 적용 중 발생한 취소 요청

    /// <summary>
    /// 디버그 화면의 사건 이름
    /// </summary>
    public override string DebugName => "교체";

    /// <summary>
    /// 시간 설정을 조회할 사건 종류
    /// </summary>
    public override AnomalyEventType EventType => AnomalyEventType.Swap;

    /// <summary>
    /// 관리 중인 발생 건 수
    /// </summary>
    public override int DebugOccurrenceCount => _occurrences.Count;


    private void Awake ()
    {
        //별도 컴포넌트 연결 없이 검사 설정으로 교체 모듈 생성
        _swap = new ItemSwapper( _swapSettings );
    }

    private void Update ()
    {
        //결과 알림에서 목록이 변경돼도 현재 순회 대상은 유지
        SwapOccurrence [ ] snapshot = _occurrences.ToArray( );

        foreach ( SwapOccurrence occurrence in snapshot )
        {
            if ( _occurrences.Contains( occurrence ) == false )
                continue;

            if ( IsOccurrenceValid( occurrence ) == false )
            {
                CancelOccurrence( occurrence );
                continue;
            }

            occurrence.UpdateElapsedTime( Time.timeAsDouble );

            //실패 알림에서 사건이 취소될 수 있으므로 다시 확인
            if ( _occurrences.Contains( occurrence ) )
                occurrence.Event.TryExpire( );
        }
    }

    private void OnDisable ()
    {
        //적용 중인 Ready 사건은 교체 함수가 반환된 뒤 정리
        //취소가 보류될 수 있으므로 while 대신 복사한 목록을 순회
        SwapOccurrence [ ] snapshot = _occurrences.ToArray( );

        foreach ( SwapOccurrence occurrence in snapshot )
            CancelOccurrence( occurrence );
    }


    #region ----- 시간/종료 -----

    /// <summary>
    /// 대상의 구독과 잠금을 해제하고 정상 기준 갱신 요청
    /// </summary>
    /// <param name="target">정리할 사건 대상</param>
    /// <param name="item">대상의 보유 기능</param>
    /// <param name="placementHandler">해제할 배치 콜백</param>
    /// <param name="invalidatedHandler">해제할 무효화 콜백</param>
    void ReleaseTarget (
        AnomalyTarget target, ItemCarry item,
        Action<PlacementTarget> placementHandler,
        Action invalidatedHandler )
    {
        if ( target == null )
            return;

        target.OnPlacementAttempt -= placementHandler;
        target.OnInvalidated -= invalidatedHandler;

        //잠금 해제 전에 갱신 대기를 등록하여 오래된 기준으로 재발생 방지
        //적 보유와 비활성 대상은 현재 상태를 정상 기준으로 채택하지 않음
        if ( target.isActiveAndEnabled &&
             item != null && item.isActiveAndEnabled &&
             item.CurrentType != HoldType.World )
        {
            target.RequestNormalStateRefresh( );
        }

        target.SetNormalLocked( false );
    }

    /// <summary>
    /// 발생 건을 제거하고 두 대상의 구독과 잠금 정리
    /// </summary>
    /// <param name="occurrence">정리할 발생 건</param>
    void ReleaseOccurrence ( SwapOccurrence occurrence )
    {
        if ( _occurrences.Remove( occurrence ) == false )
            return;

        occurrence.Event.OnStatusChanged -= occurrence.StatusHandler;

        ReleaseTarget(
            occurrence.TargetA, occurrence.ItemA,
            occurrence.PlacementHandlerA, occurrence.InvalidatedHandler );

        ReleaseTarget(
            occurrence.TargetB, occurrence.ItemB,
            occurrence.PlacementHandlerB, occurrence.InvalidatedHandlerB );
    }

    /// <summary>
    /// 준비 또는 진행 중인 교체 변칙 사건 취소
    /// </summary>
    /// <param name="occurrence">취소할 발생 건</param>
    void CancelOccurrence ( SwapOccurrence occurrence )
    {
        if ( _occurrences.Contains( occurrence ) == false )
            return;

        if ( occurrence.Event.Status == AnomalyStatus.Ready )
        {
            //교체 모듈이 적용 또는 실패 복구 중일 수 있으므로 취소 요청만 기록
            //TryStart의 finally에서 실행이 끝난 뒤 두 대상의 잠금 해제
            _startCancelled = true;
            return;
        }

        if ( occurrence.Event.TryCancel( ) == false )
            ReleaseOccurrence( occurrence );
    }

    /// <summary>
    /// 상태 변경에 따른 종료 정리와 사건 결과 전달
    /// </summary>
    /// <param name="occurrence">상태가 변경된 발생 건</param>
    /// <param name="previous">이전 상태</param>
    /// <param name="current">현재 상태</param>
    void HandleStatusChanged (
        SwapOccurrence occurrence,
        AnomalyStatus previous, AnomalyStatus current )
    {
        RecordDebugStatus( occurrence );

        //Failed에서는 두 대상의 잠금과 배치 알림을 유지
        if ( occurrence.Event.RequiresNormalLock == false )
            ReleaseOccurrence( occurrence );


        //두 물건이 참여해도 결과는 하나의 사건으로 전달
        Result( occurrence.Event, current );
    }

    #endregion

    #region ----- 배치 -----

    /// <summary>
    /// 실제 배치 결과를 해당 대상의 완료 판정에 전달
    /// </summary>
    /// <param name="occurrence">배치한 대상이 속한 발생 건</param>
    /// <param name="targetIndex">첫 번째 대상은 0, 두 번째 대상은 1</param>
    /// <param name="placement">실제로 배치한 대상</param>
    void HandlePlacement (
        SwapOccurrence occurrence, int targetIndex,
        PlacementTarget placement )
    {
        if ( _occurrences.Contains( occurrence ) == false ||
             occurrence.Event.RequiresNormalLock == false )
            return;

        if ( IsOccurrenceValid( occurrence ) == false )
        {
            CancelOccurrence( occurrence );
            return;
        }

        //배치 완료 시점까지 시간을 반영하여 시간 초과를 먼저 판정
        occurrence.UpdateElapsedTime( Time.timeAsDouble );

        if ( _occurrences.Contains( occurrence ) == false )
            return;

        //실패 결과를 받은 외부 코드가 대상을 변경했을 수 있음
        if ( IsOccurrenceValid( occurrence ) == false )
        {
            CancelOccurrence( occurrence );
            return;
        }

        ItemCarry item = targetIndex == 0
            ? occurrence.ItemA : occurrence.ItemB;

        PlacementTarget original = targetIndex == 0
            ? occurrence.OriginalPlacementA
            : occurrence.OriginalPlacementB;

        //좌표가 아닌 발생 당시 배치 대상과 비교
        //1/2에서는 모델의 완료 기록만 바뀌며 전체 결과는 발행되지 않음
        occurrence.Event.TryPlace(
            targetIndex, placement != null && placement == original,
            item.IsHold );
    }

    /// <summary>
    /// 두 대상의 배치와 무효화, 사건 상태 알림 연결
    /// </summary>
    /// <param name="occurrence">알림을 연결할 발생 건</param>
    void Subscribe ( SwapOccurrence occurrence )
    {
        occurrence.PlacementHandlerA =
            placement => HandlePlacement( occurrence, 0, placement );

        occurrence.PlacementHandlerB =
            placement => HandlePlacement( occurrence, 1, placement );

        occurrence.InvalidatedHandler =
            () => CancelOccurrence( occurrence );

        occurrence.InvalidatedHandlerB =
            () => CancelOccurrence( occurrence );

        occurrence.StatusHandler =
            ( previous, current ) =>
                HandleStatusChanged( occurrence, previous, current );

        occurrence.TargetA.OnPlacementAttempt += occurrence.PlacementHandlerA;
        occurrence.TargetB.OnPlacementAttempt += occurrence.PlacementHandlerB;
        occurrence.TargetA.OnInvalidated += occurrence.InvalidatedHandler;
        occurrence.TargetB.OnInvalidated += occurrence.InvalidatedHandlerB;
        occurrence.Event.OnStatusChanged += occurrence.StatusHandler;
    }

    #endregion

    #region ----- 발생 -----

    /// <summary>
    /// 발생 실패 사유를 공통 디버그 요청에 전달
    /// </summary>
    /// <param name="target">실패한 요청의 대표 대상</param>
    /// <param name="code">실패 구분 코드</param>
    /// <param name="reason">실패 설명</param>
    /// <returns>발생 실패</returns>
    bool Fail ( AnomalyTarget target, string code, string reason )
    {
        ReportStartFailure(
            target, new AnomalyStartFailure( "Swap", code, reason ), false );

        return false;
    }

    /// <summary>
    /// 지정한 두 대상을 잠그고 교체한 뒤 사건 시작
    /// </summary>
    /// <param name="targetA">첫 번째 사건 대상</param>
    /// <param name="targetB">두 번째 사건 대상</param>
    /// <param name="startedEvent">시작한 사건</param>
    /// <returns>교체 변칙 사건 발생 성공 여부</returns>
    public bool TryStart (
        AnomalyTarget targetA, AnomalyTarget targetB,
        out AnomalyEvent startedEvent )
    {
        startedEvent = null;

        if ( _isStarting )
            return Fail( targetA, "Busy", "다른 교체 발생 요청을 처리 중입니다." );

        if ( CheckStartPair( targetA, targetB, out string reason ) == false )
            return Fail( targetA, "InvalidPair", reason );

        if ( TryGetEventTimes( targetA, out AnomalyEventTimes times ) == false )
            return false;

        _isStarting = true;
        _startCancelled = false;
        SwapOccurrence occurrence = null;

        try
        {
            ItemCarry itemA = targetA.GetComponent<ItemCarry>( );
            ItemCarry itemB = targetB.GetComponent<ItemCarry>( );

            PlacementTarget placementA = targetA.NormalState.PlacementTarget;
            PlacementTarget placementB = targetB.NormalState.PlacementTarget;

            SwapAnomalyEvent nextEvent = new( times );
            occurrence = new SwapOccurrence(
                targetA, itemA, placementA,
                targetB, itemB, placementB, nextEvent );

            //교체 중 정상 기준 갱신과 다른 사건 발생을 먼저 차단
            targetA.SetNormalLocked( true );
            targetB.SetNormalLocked( true );

            _occurrences.Add( occurrence );
            Subscribe( occurrence );

            //일반 교체 기능이 위치와 회전 저장, 공간 검사, 적용과 실패 복구 담당
            if ( _swap.TrySwap(
                itemA, placementA, itemB, placementB, out reason ) == false )
                return Fail( targetA, "SwapFailed", reason );

            if ( _startCancelled || IsOccurrenceValid( occurrence ) == false )
            {
                return Fail(
                    targetA, "StartCancelled",
                    "교체 처리 중 대상 또는 관리자가 무효화되었습니다." );
            }

            //실제 교체에 사용한 시간은 사건 제한 시간에서 제외
            occurrence.LastTickTime = Time.timeAsDouble;

            if ( nextEvent.TryStart( ) == false )
                return Fail( targetA, "StartFailed", "교체 사건을 시작하지 못했습니다." );

            startedEvent = nextEvent;
            return true;
        }
        finally
        {
            //중간 실패와 보류된 취소도 이 지점에서 정리하여
            //두 대상의 잠금이 남지 않게 처리
            if ( startedEvent == null && occurrence != null )
                ReleaseOccurrence( occurrence );

            _isStarting = false;
            _startCancelled = false;
        }
    }

    /// <summary>
    /// 등록된 대상을 무작위 순서로 확인하여 교체 변칙 사건 시작
    /// </summary>
    /// <param name="startedEvent">시작한 사건</param>
    /// <returns>발생 가능한 두 대상을 찾았는지 여부</returns>
    public override bool TryStart ( out AnomalyEvent startedEvent )
    {
        startedEvent = null;

        if ( isActiveAndEnabled == false || _isStarting )
            return false;

        List<AnomalyTarget> candidates = new( AnomalyRegister.Targets );

        //이미 사용할 수 없는 대상은 조합 검사 전에 제외
        candidates.RemoveAll( target =>
            {
                if ( CheckStartTarget( target, out string reason ) )
                    return false;

                Fail( target, "CandidateExcluded", reason );
                return true;
            } );

        for ( int i = candidates.Count - 1; i > 0; i-- )
        {
            int randomIndex = Random.Range( 0, i + 1 );
            AnomalyTarget temporary = candidates [ i ];

            candidates [ i ] = candidates [ randomIndex ];
            candidates [ randomIndex ] = temporary;
        }

        //같은 쌍을 A/B만 바꾸어 다시 검사하지 않음
        for ( int i = 0; i < candidates.Count - 1; i++ )
        {
            for ( int j = i + 1; j < candidates.Count; j++ )
            {
                if ( isActiveAndEnabled == false )
                    return false;

                if ( TryStart(
                    candidates [ i ], candidates [ j ], out startedEvent ) )
                    return true;
            }
        }

        return false;
    }


    /// <summary>
    /// 요청 방의 서로 다른 두 대상에 교체 사건 시작
    /// </summary>
    /// <param name="room">요청한 방</param>
    /// <param name="startedEvent">발생한 사건</param>
    /// <returns>실제 시작 여부</returns>
    public override bool TryStart (
        Room room, out AnomalyEvent startedEvent )
    {
        startedEvent = null;

        if ( isActiveAndEnabled == false || _isStarting == true ||
             TryReadEventTimes( out _ ) == false )
            return false;

        AnomalyTarget selectedA = null;
        AnomalyTarget selectedB = null;
        int pairCount = 0;

        foreach ( AnomalyTarget first in AnomalyRegister.Targets )
        {
            if ( IsInRoom( first, room ) == false )
                continue;

            foreach ( AnomalyTarget second in AnomalyRegister.Targets )
            {
                if ( IsInRoom( second, room ) == false ||
                     CheckStartPair( first, second, out _ ) == false ||
                     first.GetInstanceID( ) >= second.GetInstanceID( ) )
                    continue;

                //A/B를 바꾼 같은 쌍은 제외하고 균등하게 하나 선정
                pairCount++;

                if ( Random.Range( 0, pairCount ) == 0 )
                {
                    selectedA = first;
                    selectedB = second;
                }
            }
        }

        if ( selectedA == null ||
             IsInRoom( selectedA, room ) == false ||
             IsInRoom( selectedB, room ) == false ||
             CheckStartPair( selectedA, selectedB, out _ ) == false )
            return false;

        return TryStart( selectedA, selectedB, out startedEvent );
    }


    /// <summary>
    /// 요청 방의 유효한 교체 쌍 확인
    /// </summary>
    /// <param name="room">요청한 방</param>
    /// <returns>교체 후보 존재 여부</returns>
    public override bool CanStart ( Room room )
    {
        if ( isActiveAndEnabled == false || _isStarting == true ||
             TryReadEventTimes( out _ ) == false )
            return false;

        foreach ( AnomalyTarget first in AnomalyRegister.Targets )
        {
            if ( IsInRoom( first, room ) == false )
                continue;

            foreach ( AnomalyTarget second in AnomalyRegister.Targets )
            {
                if ( first != second &&
                     IsInRoom( second, room ) == true &&
                     CheckStartPair( first, second, out _ ) == true )
                    return true;
            }
        }

        return false;
    }

    #endregion

    #region ----- 상태 검사 -----

    /// <summary>
    /// 사건을 새로 적용할 대상의 정상 기준과 보유 상태 확인
    /// </summary>
    /// <param name="target">검사할 사건 대상</param>
    /// <param name="reason">사용할 수 없는 사유</param>
    /// <returns>발생 후보로 사용할 수 있는지 여부</returns>
    bool CheckStartTarget ( AnomalyTarget target, out string reason )
    {
        reason = string.Empty;

        if ( target == null || target.isActiveAndEnabled == false )
        {
            reason = "활성 사건 대상이 필요합니다.";
            return false;
        }

        if ( target.NormalState == null || target.NormalLocked )
        {
            reason = "정상 기준이 있고 잠금과 기준 갱신 대기가 없는 대상이 필요합니다.";
            return false;
        }

        PlacementTarget placement = target.NormalState.PlacementTarget;

        if ( placement == null || placement.isActiveAndEnabled == false )
        {
            reason = "정상 기준에 활성 배치 대상이 필요합니다.";
            return false;
        }

        ItemCarry item = target.GetComponent<ItemCarry>( );

        if ( item == null || item.isActiveAndEnabled == false || item.IsHold )
        {
            reason = "아무도 들고 있지 않은 활성 운반 기능이 필요합니다.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 교체 관리자와 서로 다른 두 발생 후보 확인
    /// </summary>
    /// <param name="targetA">첫 번째 대상</param>
    /// <param name="targetB">두 번째 대상</param>
    /// <param name="reason">발생할 수 없는 사유</param>
    /// <returns>교체 발생 시도 가능 여부</returns>
    bool CheckStartPair (
        AnomalyTarget targetA, AnomalyTarget targetB, out string reason )
    {
        reason = string.Empty;

        if ( isActiveAndEnabled == false || _swap == null )
        {
            reason = "활성 사건 관리자와 초기화된 교체 모듈이 필요합니다.";
            return false;
        }

        if ( CheckStartTarget( targetA, out reason ) == false ||
             CheckStartTarget( targetB, out reason ) == false )
            return false;

        if ( targetA == targetB ||
             targetA.NormalState.PlacementTarget ==
             targetB.NormalState.PlacementTarget )
        {
            reason = "서로 다른 배치 대상에 놓인 두 물건이 필요합니다.";
            return false;
        }

        //실제 지지 표면과 충돌체, 부모 관계는 일반 교체 기능에서 검사
        return true;
    }

    /// <summary>
    /// 사건 대상과 원래 배치 대상의 유지 가능 여부 확인
    /// </summary>
    /// <param name="target">사건 대상</param>
    /// <param name="item">대상의 보유 기능</param>
    /// <param name="originalPlacement">발생 당시 원래 배치 대상</param>
    /// <returns>사건을 유지할 수 있는지 여부</returns>
    bool IsTargetValid (
        AnomalyTarget target, ItemCarry item,
        PlacementTarget originalPlacement )
    {
        if ( target == null || target.isActiveAndEnabled == false ||
             target.NormalState == null || target.NormalLocked == false )
            return false;

        //플레이어 보유는 해결 과정이므로 허용하고 적 보유만 제외
        if ( item == null || item.isActiveAndEnabled == false ||
             item.CurrentType == HoldType.World )
            return false;

        if ( originalPlacement == null ||
             originalPlacement.isActiveAndEnabled == false )
            return false;

        Collider surface = originalPlacement.SurfaceCollider;

        return surface != null && surface.enabled &&
               surface.gameObject.activeInHierarchy && surface.isTrigger == false;
    }

    /// <summary>
    /// 관리자와 두 사건 대상의 유지 가능 여부 확인
    /// </summary>
    /// <param name="occurrence">검사할 발생 건</param>
    /// <returns>사건 유지 가능 여부</returns>
    bool IsOccurrenceValid ( SwapOccurrence occurrence )
    {
        return isActiveAndEnabled &&
               IsTargetValid(
                   occurrence.TargetA, occurrence.ItemA,
                   occurrence.OriginalPlacementA ) &&
               IsTargetValid(
                   occurrence.TargetB, occurrence.ItemB,
                   occurrence.OriginalPlacementB );
    }

    #endregion

    #region ----- 디버그 -----

    /// <summary>
    /// 지정한 두 대상으로 교체 변칙 사건 요청
    /// </summary>
    /// <param name="target">디버그 창에서 선택한 대상</param>
    /// <param name="secondTarget">교환 사건의 두 번째 대상</param>
    /// <param name="startedEvent">시작한 사건</param>
    /// <returns>지정 대상 발생 성공 여부</returns>
    protected override bool TryStartDebugManual (
        AnomalyTarget target, AnomalyTarget secondTarget,
        out AnomalyEvent startedEvent )
    {
        return TryStart( target, secondTarget, out startedEvent );
    }

    /// <summary>
    /// 관리 중인 교체 발생 건 조회
    /// </summary>
    /// <param name="index">발생 건 번호</param>
    /// <returns>디버그 조회 대상</returns>
    public override IAnomalyDebugOccurrence GetDebugOccurrence ( int index )
    {
        return _occurrences [ index ];
    }

    #endregion

}
