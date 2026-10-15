using System;
using System.Collections.Generic;
using UnityEngine;
using DebugView = AnomalyDebugViewBuilder;
using StartFailure = AnomalyDebugViewBuilder.StartFailure;

/// <summary>
/// 낙하 변칙 사건 시스템 - 발생, 시간 갱신과 재배치 결과 관리
/// </summary>
public class DropAnomalySystem : AnomalyEventSystem
{
    [Header( "----- 사건 설정 -----" )]
    [SerializeField, Min( 0f )] float _pushImpulse;       //윗면 아이템 밀기 충격량
    [SerializeField, Min( 0f )] float _supportGap;     //지지 표면 허용 간격


    List<DropOccurrence> _occurrences = new( );     //진행 또는 사후 복구 대기 중인 발생 건

    /// <summary>
    /// 디버그 화면의 사건 종류
    /// </summary>
    public override string DebugName => "낙하";
    /// <summary>
    /// 씬에서 설정을 조회할 사건 종류
    /// </summary>
    public override AnomalyEventType EventType => AnomalyEventType.Drop;
    /// <summary>
    /// 현재 관리 중인 발생 건 수
    /// </summary>
    public override int DebugOccurrenceCount => _occurrences.Count;


    private void Update ()
    {
        //종료 결과 콜백에서 목록이 바뀌어도 같은 발생 건을 중복 처리하지 않음
        DropOccurrence [ ] snapshot = _occurrences.ToArray( );
        foreach ( DropOccurrence occurrence in snapshot )
        {
            if ( _occurrences.Contains( occurrence ) == false )
                continue;

            if ( IsTargetValid( occurrence ) == false )
            {
                occurrence.Event.TryCancel( );
                continue;
            }

            occurrence.UpdateElapsedTime( Time.timeAsDouble );
            if ( _occurrences.Contains( occurrence ) == true )
                occurrence.Event.TryExpire( );
        }
    }

    private void OnDisable ()
    {
        while ( _occurrences.Count > 0 )
        {
            DropOccurrence occurrence = _occurrences [ _occurrences.Count - 1 ];

            occurrence.Event.TryCancel( );
            ReleaseOccurrence( occurrence );
        }
    }


    #region ----- 발생 -----

    /// <summary>
    /// 지정한 대상의 현재 배치 표면에서 낙하 시작
    /// </summary>
    /// <param name="target">사건을 발생시킬 대상</param>
    /// <param name="showLog">실패 원인 출력 여부</param>
    /// <param name="startedEvent">발생한 사건</param>
    /// <returns>발생 성공 여부</returns>
    bool TryStart (
        AnomalyTarget target, bool showLog, out AnomalyEvent startedEvent )
    {
        startedEvent = null;

        if ( isActiveAndEnabled == false )
        {
            ReportStartFailure(
                "낙하", target, StartFailure.PresenterInactive, showLog, this );
            return false;
        }

        if ( target == null )
        {
            ReportStartFailure(
                "낙하", target, StartFailure.TargetMissing, showLog, this );
            return false;
        }

        if ( target.isActiveAndEnabled == false )
        {
            ReportStartFailure(
                "낙하", target, StartFailure.TargetInactive, showLog, this );
            return false;
        }

        ItemCarry item = target.GetComponent<ItemCarry>( );

        if ( item == null )
        {
            ReportStartFailure(
                "낙하", target, StartFailure.CarryMissing, showLog, this );
            return false;
        }

        if ( item.isActiveAndEnabled == false )
        {
            ReportStartFailure(
                "낙하", target, StartFailure.CarryInactive, showLog, this );
            return false;
        }

        if ( item.IsHold == true )
        {
            ReportStartFailure(
                "낙하", target, StartFailure.ItemHeld, showLog, this, item.CurrentType );
            return false;
        }

        if ( target.NormalLocked == true )
        {
            ReportStartFailure(
                "낙하", target, StartFailure.NormalLocked, showLog, this );
            return false;
        }

        if ( TryGetEventTimes(
            target, out AnomalyEventTimes times ) == false )
            return false;

        ItemWorldController itemView = target.GetComponent<ItemWorldController>( );
        if ( itemView == null )
        {
            ReportStartFailure( target,
                DebugView.CreateFeatureFailure( "낙하", "ViewMissing" ), showLog );
            return false;
        }

        //바닥 접촉은 과거 배치 정보와 별개로 먼저 판정
        bool hasGroundSupport = itemView.TryGetSupport(
            Vector3.down, _supportGap, out RaycastHit groundHit, false );
        if ( hasGroundSupport == true &&
             groundHit.collider.gameObject.layer == LayerMask.NameToLayer( "Floor" ) )
        {
            ReportStartFailure(
                "낙하", target, StartFailure.OnFloor, showLog, this );
            return false;
        }

        //바닥 접촉을 제외한 뒤 정상 기준에 저장된 표면과 실제 지지 표면 비교
        AnomalyTargetState normal = target.NormalState;

        if ( normal == null )
        {
            ReportStartFailure(
                "낙하", target, StartFailure.PlacementNormalMissing, showLog, this );
            return false;
        }

        if ( normal.PlacementTarget == null )
        {
            ReportStartFailure(
                "낙하", target, StartFailure.PlacementMissing, showLog, this );
            return false;
        }

        if ( normal.PlacementTarget.isActiveAndEnabled == false )
        {
            ReportStartFailure(
                "낙하", target, StartFailure.PlacementInactive, showLog, this, normal.PlacementTarget );
            return false;
        }

        Collider surface = normal.PlacementTarget.SurfaceCollider;

        if ( surface == null )
        {
            ReportStartFailure(
                "낙하", target, StartFailure.SurfaceMissing, showLog, this );
            return false;
        }

        if ( surface.enabled == false || surface.gameObject.activeInHierarchy == false )
        {
            ReportStartFailure(
                "낙하", target, StartFailure.SurfaceInactive, showLog, this, surface );
            return false;
        }

        if ( surface.isTrigger == true )
        {
            ReportStartFailure(
                "낙하", target, StartFailure.SurfaceTrigger, showLog, this, surface );
            return false;
        }

        bool isWall =
            surface.gameObject.layer == LayerMask.NameToLayer( "Wall" );
        bool isFloor =
            surface.gameObject.layer == LayerMask.NameToLayer( "Floor" );

        //현재 외형의 전체 영역을 기준으로 접촉 방향과 밀기 방향 계산
        Bounds itemBounds = itemView.GetBounds( );

        //윗면은 아래쪽, 벽은 아이템에서 가까운 벽면 방향 검사
        Vector3 checkDirection = isWall == true
            ? surface.ClosestPoint( itemBounds.center ) - itemBounds.center
            : Vector3.down;

        RaycastHit hit = groundHit;
        bool hasSupport = isWall == true
            ? itemView.TryGetSupport( checkDirection, _supportGap, out hit, showLog )
            : hasGroundSupport;
        if ( hasSupport == false )
        {
            ReportStartFailure( target,
                DebugView.CreateFeatureFailure( "낙하", "SupportMissing" ), showLog );
            return false;
        }

        if ( hit.collider != surface )
        {
            ReportStartFailure( target,
                DebugView.CreateFeatureFailure( "낙하", "SupportMismatch" ), showLog );
            return false;
        }

        ItemDropEvent nextEvent = new( times );

        if ( nextEvent.CanDrop( isFloor, isWall, hit.normal ) == false )
        {
            ReportStartFailure(
                "낙하", target,
                isFloor == true ? StartFailure.OnFloor : StartFailure.SurfaceNotUp,
                showLog, this, Vector3.Dot( hit.normal, Vector3.up ) );
            return false;
        }

        //벽은 충격 없이 고정만 해제하고 윗면에서는 가까운 가장자리로 밀기
        Vector3 impulse = Vector3.zero;

        if ( isWall == false )
        {
            if ( _pushImpulse <= 0f )
            {
                ReportStartFailure(
                    "낙하", target, StartFailure.InvalidImpulse, showLog, this, _pushImpulse );
                return false;
            }

            Vector3 direction = nextEvent.GetPushDirection(
                surface.bounds, itemBounds );
            impulse = direction * _pushImpulse;
        }

        //물리 변화 전에 정상 배치 갱신 잠금
        target.SetNormalLocked( true );

        //기존 고정 해제와 충격량 적용 재사용
        if ( item.TryPush( impulse ) == false )
        {
            target.SetNormalLocked( false );
            ReportStartFailure(
                "낙하", target, StartFailure.PushFailed, showLog, this );
            return false;
        }

        //발생 시점의 대상과 사건 참조를 함께 보관
        DropOccurrence occurrence = new(
            target, item, nextEvent, Time.timeAsDouble );

        //대상 이벤트에 발생 건을 연결하고 구독 해제용 콜백 보관
        occurrence.PlacementHandler =
            placementTarget => HandlePlacementAttempt( occurrence, placementTarget );
        occurrence.InvalidatedHandler =
            () => HandleTargetInvalidated( occurrence );
        occurrence.StatusHandler =
            ( prev, current ) => HandleStatusChanged( occurrence, prev, current );

        target.OnPlacementAttempt += occurrence.PlacementHandler;
        target.OnInvalidated += occurrence.InvalidatedHandler;
        nextEvent.OnStatusChanged += occurrence.StatusHandler;

        _occurrences.Add( occurrence );

        //새로 생성한 Ready 상태의 사건 시작
        nextEvent.TryStart( );
        startedEvent = nextEvent;
        return true;
    }

    /// <summary>
    /// 자동 등록된 대상 중 발생 가능한 낙하 변칙 사건 하나 시작
    /// </summary>
    /// <param name="startedEvent">발생한 사건</param>
    /// <returns>발생 성공 여부</returns>
    public override bool TryStart ( out AnomalyEvent startedEvent )
    {
        startedEvent = null;

        if ( isActiveAndEnabled == false )
            return false;

        foreach ( AnomalyTarget target in AnomalyRegister.Targets )
        {
            //후보 제외 로그는 생략하고 발생 가능한 대상 하나 선택
            if ( TryStart( target, false, out startedEvent ) == true )
                return true;
        }

        return false;
    }


    /// <summary>
    /// 선정한 대상에 낙하 사건 시작
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
    /// 낙하 가능한 지지 표면 확인
    /// </summary>
    /// <param name="target">발생 대상</param>
    /// <param name="times">발생 시간 설정</param>
    /// <returns>낙하 후보 여부</returns>
    protected override bool CanStartTarget (
        AnomalyTarget target, AnomalyEventTimes times )
    {
        ItemCarry item = target.GetComponent<ItemCarry>( );
        ItemWorldController view = target.GetComponent<ItemWorldController>( );
        AnomalyTargetState normal = target.NormalState;
        PlacementTarget placement = normal?.PlacementTarget;
        Collider surface = placement == null
            ? null : placement.SurfaceCollider;

        if ( item == null || item.isActiveAndEnabled == false ||
             item.IsHold == true || view == null ||
             target.NormalLocked == true ||
             placement == null || placement.isActiveAndEnabled == false ||
             surface == null || surface.enabled == false ||
             surface.gameObject.activeInHierarchy == false ||
             surface.isTrigger == true )
            return false;

        bool isWall =
            surface.gameObject.layer == LayerMask.NameToLayer( "Wall" );
        bool isFloor =
            surface.gameObject.layer == LayerMask.NameToLayer( "Floor" );

        Bounds bounds = view.GetBounds( );
        Vector3 direction = isWall
            ? surface.ClosestPoint( bounds.center ) - bounds.center
            : Vector3.down;

        if ( view.TryGetSupport(
            direction, _supportGap, out RaycastHit hit, false ) == false ||
             hit.collider != surface ||
             isFloor == true ||
             ( isWall == false && _pushImpulse <= 0f ) )
            return false;

        return new ItemDropEvent( times ).CanDrop(
            isFloor, isWall, hit.normal );
    }

    #endregion

    #region ----- 배치/시간 갱신 -----

    /// <summary>
    /// 실제 배치 완료에 따른 해결 판정
    /// </summary>
    /// <param name="occurrence">배치한 아이템의 발생 건</param>
    /// <param name="placementTarget">실제 배치 대상</param>
    void HandlePlacementAttempt (
        DropOccurrence occurrence, PlacementTarget placementTarget )
    {
        if ( _occurrences.Contains( occurrence ) == false ||
             occurrence.Event.RequiresNormalLock == false )
            return;

        if ( IsTargetValid( occurrence ) == false )
        {
            occurrence.Event.TryCancel( );
            return;
        }

        //입력 시점까지 경과 시간을 먼저 반영
        occurrence.UpdateElapsedTime( Time.timeAsDouble );

        //실패 결과 콜백에서 종료되거나 대상이 바뀔 수 있음
        if ( _occurrences.Contains( occurrence ) == false )
            return;
        if ( IsTargetValid( occurrence ) == false )
        {
            occurrence.Event.TryCancel( );
            return;
        }

        bool isPlaced =
            placementTarget != null &&
            placementTarget.isActiveAndEnabled == true &&
            occurrence.Item.IsHold == false;

        //낙하 전 표면과 같을 필요는 없으며 허용된 대상에 직접 배치하면 해결/복구
        occurrence.Event.TryPlace( isPlaced );
    }

    #endregion

    #region ----- 종료 -----

    /// <summary>
    /// 발생 건의 목록, 구독과 잠금 해제
    /// </summary>
    /// <param name="occurrence">정리할 발생 건</param>
    void ReleaseOccurrence ( DropOccurrence occurrence )
    {
        if ( _occurrences.Remove( occurrence ) == false )
            return;

        if ( occurrence.Target != null )
        {
            occurrence.Target.OnPlacementAttempt -= occurrence.PlacementHandler;
            occurrence.ReleaseTarget( );
        }

        occurrence.Event.OnStatusChanged -= occurrence.StatusHandler;
        //만료돼도 낙하 위치를 정상 기준으로 저장하거나 물리 상태를 변경하지 않음
    }

    /// <summary>
    /// 대상 무효화 시 해당 사건 취소
    /// </summary>
    /// <param name="occurrence">취소할 발생 건</param>
    void HandleTargetInvalidated ( DropOccurrence occurrence )
    {
        occurrence.Event.TryCancel( );
    }

    /// <summary>
    /// 사건 상태에 따른 잠금과 결과 처리
    /// </summary>
    /// <param name="occurrence">상태가 변경된 발생 건</param>
    /// <param name="prev">이전 상태</param>
    /// <param name="current">현재 상태</param>
    void HandleStatusChanged (
        DropOccurrence occurrence, AnomalyStatus prev, AnomalyStatus current )
    {
        RecordDebugStatus( occurrence );

        //외부 결과 전달 전에 정리, 낙하 전 배치 기준은 유지
        if ( occurrence.Event.RequiresNormalLock == false )
            ReleaseOccurrence( occurrence );


        //공통 결과 전달 함수에서 시작 상태 제외
        Result( occurrence.Event, current );
    }

    #endregion

    #region ----- 상태 검사 -----

    /// <summary>
    /// 대상이 사건을 계속 유지할 수 있는지 확인
    /// </summary>
    /// <param name="occurrence">검사할 발생 건</param>
    /// <returns>대상 유효 여부</returns>
    bool IsTargetValid ( DropOccurrence occurrence )
    {
        return occurrence.Target != null &&
               occurrence.Target.isActiveAndEnabled == true &&
               occurrence.Item != null &&
               occurrence.Item.isActiveAndEnabled == true &&
               occurrence.Item.CurrentType != HoldType.World;
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
