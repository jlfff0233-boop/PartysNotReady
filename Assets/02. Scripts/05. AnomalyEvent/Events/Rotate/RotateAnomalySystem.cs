using System.Collections.Generic;
using UnityEngine;
using StartFailure = AnomalyDebugViewBuilder.StartFailure;

/// <summary>
/// 회전 변칙 사건 시스템 - 발생, 시간 갱신과 복구 결과 관리
/// </summary>
public class RotateAnomalySystem : AnomalyEventSystem
{
    [Header( "----- 발생 회전 설정 -----" )]
    [SerializeField] float _minAngle = 60f;     //최소 발생 각도
    [SerializeField] float _maxAngle = 270f;     //최대 발생 각도

    List<RotateOccurrence> _occurrences = new( );     //진행 및 사후 복구 대기 목록

    /// <summary>
    /// 디버그 화면의 사건 종류
    /// </summary>
    public override string DebugName => "회전";
    /// <summary>
    /// 씬에서 설정을 조회할 사건 종류
    /// </summary>
    public override AnomalyEventType EventType => AnomalyEventType.Rotate;
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

            RotateOccurrence occurrence = _occurrences [ i ];

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
        //모든 사건 정리
        while ( _occurrences.Count > 0 )
        {
            RotateOccurrence occurrence =
                _occurrences [ _occurrences.Count - 1 ];

            CancelOccurrence( occurrence );
        }
    }


    #region ----- 발생 -----

    /// <summary>
    /// 발생 건별 회전과 무효화 및 상태 알림 연결
    /// </summary>
    /// <param name="occurrence">연결할 발생 건</param>
    void Subscribe ( RotateOccurrence occurrence )
    {
        //해제할 때 같은 델리게이트를 사용하도록 참조 보관
        occurrence.RotateHandler =
            angle => HandleRotateAttempt( occurrence, angle );

        occurrence.InvalidatedHandler =
            () => CancelOccurrence( occurrence );

        occurrence.StatusHandler =
            ( previous, current ) =>
                HandleStatusChanged( occurrence, previous, current );

        occurrence.Rotate.OnAngleChanged += occurrence.RotateHandler;
        occurrence.Target.OnInvalidated += occurrence.InvalidatedHandler;
        occurrence.Event.OnStatusChanged += occurrence.StatusHandler;
    }

    /// <summary>
    /// 지정한 대상에 회전 변칙 사건 발생
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
                "회전", target, StartFailure.PresenterInactive, showLog, this );
            return false;
        }

        if ( target == null || target.isActiveAndEnabled == false )
        {
            ReportStartFailure(
                "회전", target, StartFailure.TargetUnavailable, showLog, this );
            return false;
        }

        if ( target.NormalLocked == true )
        {
            ReportStartFailure(
                "회전", target, StartFailure.NormalLocked, showLog, this );
            return false;
        }

        if ( TryGetEventTimes(
            target, out AnomalyEventTimes times ) == false )
            return false;

        //발생 각도는 초기 회전을 제외한 유한한 범위
        if ( float.IsNaN( _minAngle ) == true ||
             float.IsInfinity( _minAngle ) == true ||
             float.IsNaN( _maxAngle ) == true ||
             float.IsInfinity( _maxAngle ) == true ||
             _minAngle <= 0f ||
             _maxAngle < _minAngle ||
             _maxAngle >= 360f )
        {
            ReportStartFailure(
                "회전", target, StartFailure.InvalidRotationRange, showLog, this );
            return false;
        }

        RotatePresenter rotate =
            target.GetComponent<RotatePresenter>( );

        if ( rotate == null || rotate.CanRotate == false )
        {
            ReportStartFailure(
                "회전", target, StartFailure.RotateUnavailable, showLog, this );
            return false;
        }

        ItemCarry item = target.GetComponent<ItemCarry>( );

        //운반 가능한 대상은 보유 중 발생 차단
        if ( item != null && item.IsHold == true )
        {
            ReportStartFailure( "회전", target,
                StartFailure.ItemHeld, showLog, this, item.CurrentType );
            return false;
        }

        float angle = Random.Range( _minAngle, _maxAngle );

        RotateAnomalyEvent nextEvent = new( times );

        RotateOccurrence occurrence = new(
            target, rotate, nextEvent, Time.timeAsDouble );

        //강제 회전 도중 발생하는 알림과 무효화도 추적
        _occurrences.Add( occurrence );
        Subscribe( occurrence );
        target.SetNormalLocked( true );

        //이전 사건의 기울기가 발생 각도와 같아도 새 사건은 시작 가능
        if ( rotate.Angle != angle && rotate.TrySetAngle( angle ) == false )
        {
            CancelOccurrence( occurrence );
            ReportStartFailure( "회전", target,
                StartFailure.RotationFailed, showLog, this );
            return false;
        }

        //동기 알림 처리 중 발생 건이 정리됐는지 확인
        if ( _occurrences.Contains( occurrence ) == false )
        {
            ReportStartFailure( "회전", target,
                StartFailure.RotationPreparationCancelled, showLog, this );
            return false;
        }

        if ( IsTargetValid( occurrence ) == false ||
             rotate.Angle != angle )
        {
            CancelOccurrence( occurrence );
            ReportStartFailure( "회전", target,
                StartFailure.InvalidAfterRotation, showLog, this );
            return false;
        }

        //실제 회전 완료 시점부터 제한시간 계산
        occurrence.LastTickTime = Time.timeAsDouble;

        if ( nextEvent.TryStart( ) == false )
        {
            CancelOccurrence( occurrence );
            ReportStartFailure( "회전", target,
                StartFailure.StartFailed, showLog, this );
            return false;
        }

        startedEvent = nextEvent;
        return true;
    }

    /// <summary>
    /// 회전 변칙 사건 시작
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
    /// 선정한 대상에 회전 사건 시작
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
    /// 회전 가능한 대상과 각도 설정 확인
    /// </summary>
    /// <param name="target">발생 대상</param>
    /// <param name="times">발생 시간 설정</param>
    /// <returns>회전 후보 여부</returns>
    protected override bool CanStartTarget (
        AnomalyTarget target, AnomalyEventTimes times )
    {
        RotatePresenter rotate = target.GetComponent<RotatePresenter>( );
        ItemCarry item = target.GetComponent<ItemCarry>( );

        return target.NormalLocked == false &&
            rotate != null && rotate.CanRotate == true &&
            ( item == null || item.IsHold == false ) &&
            float.IsNaN( _minAngle ) == false &&
            float.IsInfinity( _minAngle ) == false &&
            float.IsNaN( _maxAngle ) == false &&
            float.IsInfinity( _maxAngle ) == false &&
            _minAngle > 0f && _maxAngle >= _minAngle &&
            _maxAngle < 360f;
    }

    #endregion

    #region ----- 회전/시간 갱신 -----

    /// <summary>
    /// 회전 완료에 따른 정상 상태 복구 판정
    /// </summary>
    /// <param name="occurrence">회전한 대상의 발생 건</param>
    /// <param name="angle">회전 완료 후 회전 각도</param>
    void HandleRotateAttempt (
        RotateOccurrence occurrence, float angle )
    {
        if ( _occurrences.Contains( occurrence ) == false )
            return;

        //사건을 시작하기 위한 강제 회전 알림은
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
        //각도가 바뀔 수 있으므로 현재 각도와 다시 비교
        if ( occurrence.Rotate.Angle != angle )
            return;

        occurrence.Event.TryRestore( angle );
    }

    #endregion

    #region ----- 종료 -----

    /// <summary>
    /// 발생 건의 구독과 잠금 정리
    /// </summary>
    /// <param name="occurrence">정리할 발생 건</param>
    void ReleaseOccurrence ( RotateOccurrence occurrence )
    {
        //이미 정리한 발생 건은 다시 잠금을 변경하지 않음
        if ( _occurrences.Remove( occurrence ) == false )
            return;

        if ( occurrence.Rotate != null )
        {
            //해결, 사후 처리 만료, 취소 이후에는 다시 집기 입력 허용
            occurrence.Rotate.SetInteractionAllowed( false );
            occurrence.Rotate.OnAngleChanged -= occurrence.RotateHandler;
        }

        //대상이 존재한다면 무효화 구독과 잠금 정리
        if ( occurrence.Target != null )
        {
            occurrence.ReleaseTarget( );
        }

        occurrence.Event.OnStatusChanged -= occurrence.StatusHandler;
        //만료와 취소는 현재 기울기를 유지하며 초기 회전 기준은 변경하지 않음
    }

    /// <summary>
    /// 시작 전 또는 진행 중인 사건 취소
    /// </summary>
    /// <param name="occurrence">취소할 발생 건</param>
    void CancelOccurrence ( RotateOccurrence occurrence )
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
        RotateOccurrence occurrence,
        AnomalyStatus previous, AnomalyStatus current )
    {
        RecordDebugStatus( occurrence );

        //회전 사건 진행과 사후 복구 중에만 플레이어 회전 입력 허용
        if ( occurrence.Rotate != null )
            occurrence.Rotate.SetInteractionAllowed( occurrence.Event.RequiresNormalLock );

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
    bool IsTargetValid ( RotateOccurrence occurrence )
    {
        return isActiveAndEnabled == true &&
               occurrence.Target != null &&
               occurrence.Target.isActiveAndEnabled == true &&
               occurrence.Target.NormalLocked == true &&
               occurrence.Rotate != null &&
               occurrence.Rotate.IsReady == true;
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
