using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 변칙 사건 발생 정보 - 대상, 사건 모델과 알림 연결 보관
/// </summary>
/// <typeparam name="TEvent">발생 건에서 사용하는 사건 모델 형식</typeparam>
public abstract class AnomalyOccurrence<TEvent> :
    IAnomalyDebugOccurrence where TEvent : AnomalyEvent
{
    readonly string _debugId = Guid.NewGuid( ).ToString( "N" );     //발생 건 아이디
    string _targetName;     //파괴 후에도 표시할 이름

    AnomalyTarget _target;     //사건 대상
    TEvent _event;     //사건 모델
    double _lastTickTime;     //마지막 시간 갱신 시점

    /// <summary>
    /// 대상 무효화 콜백
    /// </summary>
    Action _invalidatedHandler;
    /// <summary>
    /// 상태 변경 콜백(이전 상태, 현재 상태)
    /// </summary>
    Action<AnomalyStatus, AnomalyStatus> _statusHandler;

    /// <summary>
    /// 발생 건별 디버그 아이디
    /// </summary>
    public string DebugId => _debugId;
    /// <summary>
    /// 생성 시 대상 이름
    /// </summary>
    public string TargetName => _targetName;
    /// <summary>
    /// 현재 사건 상태
    /// </summary>
    public AnomalyStatus Status => _event.Status;
    /// <summary>
    /// 실제 사후 복구 완료 방식
    /// </summary>
    public AnomalyRecoveryType RecoveryType => _event.RecoveryType;
    /// <summary>
    /// 남은 해결 시간
    /// </summary>
    public float RemainingTime => _event.RemainingTime;
    /// <summary>
    /// 실패 이후 남은 사후 복구 시간
    /// </summary>
    public float RecoveryRemainingTime => _event.RecoveryRemainingTime;
    /// <summary>
    /// 사후 처리 만료 대기 여부
    /// </summary>
    public bool RequiresAutomaticRecovery => _event.RequiresAutomaticRecovery;
    /// <summary>
    /// 관련 대상 수
    /// </summary>
    public virtual int RelatedTargetCount => 1;
    /// <summary>
    /// 기본 기능 테스트 수
    /// </summary>
    public virtual int DebugActionCount => 0;

    /// <summary>
    /// 사건 대상
    /// </summary>
    public AnomalyTarget Target => _target;

    /// <summary>
    /// 발생 건의 구체적인 사건 모델
    /// </summary>
    public TEvent Event => _event;

    /// <summary>
    /// 마지막 시간 갱신 시점
    /// </summary>
    public double LastTickTime
    {
        get => _lastTickTime;
        set => _lastTickTime = value;
    }

    /// <summary>
    /// 대상 무효화 알림 연결
    /// </summary>
    public Action InvalidatedHandler
    {
        get => _invalidatedHandler;
        set => _invalidatedHandler = value;
    }

    /// <summary>
    /// 사건 상태 변경 알림 연결
    /// </summary>
    public Action<AnomalyStatus, AnomalyStatus> StatusHandler
    {
        get => _statusHandler;
        set => _statusHandler = value;
    }

    /// <summary>
    /// 공통 대상과 사건 모델 및 시간 초기화
    /// </summary>
    /// <param name="target">사건 대상</param>
    /// <param name="anomalyEvent">사건 모델</param>
    /// <param name="lastTickTime">시간 갱신 시작 시점</param>
    protected AnomalyOccurrence (
        AnomalyTarget target, TEvent anomalyEvent, double lastTickTime )
    {
        _target = target;
        //대상이 파괴된 뒤에도 종료 기록에서 식별할 수 있도록 이름 보관
        _targetName = target != null ? target.name : "대상 없음";
        _event = anomalyEvent;
        _lastTickTime = lastTickTime;
    }

    #region ----- 시간/종료 -----

    /// <summary>
    /// 경과시간 반영
    /// </summary>
    /// <param name="now">현재 게임 시간</param>
    public void UpdateElapsedTime ( double now )
    {
        float elapsed = ( float ) ( now - _lastTickTime );

        //Tick의 결과 콜백 전에 기준 시각을 저장해
        //같은 시간의 중복 반영 방지
        _lastTickTime = now;
        _event.Tick( elapsed );
    }

    /// <summary>
    /// 대상 무효화 구독, 정상 기준 잠금 해제
    /// </summary>
    /// <param name="refreshNormal">정상 기준 갱신 요청 여부</param>
    public void ReleaseTarget ( bool refreshNormal = false )
    {
        if ( _target == null )
            return;

        _target.OnInvalidated -= _invalidatedHandler;

        if ( refreshNormal == true )
            _target.RequestNormalStateRefresh( );

        _target.SetNormalLocked( false );
    }

    #endregion

    #region ----- 관련 대상 조회 -----

    /// <summary>
    /// 대표 대상을 관련 대상으로 제공
    /// </summary>
    /// <param name="index">관련 대상 번호</param>
    /// <returns>관련 대상</returns>
    public virtual Object GetRelatedTarget ( int index )
    {
        //여러 대상을 사용하는 사건은
        //대상 수와 조회 함수를 함께 재정의
        return index == 0 ? _target : null;
    }

    /// <summary>
    /// 대표 대상의 현재 이름/보관한 이름 조회
    /// </summary>
    /// <param name="index">관련 대상 번호</param>
    public virtual string GetRelatedTargetName ( int index )
    {
        if ( index != 0 )
            return string.Empty;

        return _target != null ? _target.name : _targetName;
    }

    /// <summary>
    /// 사건 참조가 같은 발생 건인지 확인
    /// </summary>
    /// <param name="value">비교할 사건 모델</param>
    public bool IsEvent ( AnomalyEvent value )
    {
        return ReferenceEquals( _event, value );
    }

    #endregion

    #region ----- 디버그 -----

    /// <summary>
    /// 사건 시간과 복구 상태를 디버그 상세 목록에 추가
    /// </summary>
    /// <param name="details">상세 항목을 추가할 목록, 기존 항목은 유지</param>
    public virtual void GetDebugDetails ( List<AnomalyDebugDetail> details )
    {
        details.Add( new AnomalyDebugDetail( "사후 복구 남은 시간",
            Status == AnomalyStatus.Active || Status == AnomalyStatus.Ready
                ? $"시작 전 ({RecoveryRemainingTime:F2}초)"
                : $"{RecoveryRemainingTime:F2}초" ) );

        details.Add( new AnomalyDebugDetail( "만료 대기",
            _event.RequiresAutomaticRecovery.ToString( ) ) );

        details.Add( new AnomalyDebugDetail( "복구 완료 방식", _event.RecoveryType.ToString( ) ) );
    }

    /// <summary>
    /// 기본 기능 테스트는 제공하지 않음
    /// </summary>
    /// <param name="index">조회할 테스트 번호</param>
    /// <returns>미지원 사유를 포함한 테스트 정보</returns>
    public virtual AnomalyDebugAction GetDebugAction ( int index )
    {
        return new AnomalyDebugAction( 
            "지원하지 않는 동작", "기능 테스트가 없습니다." );
    }

    /// <summary>
    /// 기능별 테스트 실행, 기본 동작 없음
    /// </summary>
    /// <param name="index">실행할 테스트 번호</param>
    /// <returns>기본 구현은 항상 실패</returns>
    public virtual bool TryExecuteDebugAction ( int index )
    {
        return false;
    }

    #endregion

}
