using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

/// <summary>
/// 변칙 사건 시스템 - 발생 요청과 결과 전달
/// </summary>
public abstract class AnomalyEventSystem : MonoBehaviour
{
    AnomalyEventSettings _eventSettings;     //씬에서 전달한 해당 사건 설정
    AnomalyDebugHistory _debugHistory = new( );     //세션별 종료와 실패 기록
    AnomalyStartResult _requestResult;     //현재 요청의 실패 정보

    bool _debugRequest;     //현재 인스펙터 요청 처리 여부
    bool _traceRequest;     //자동 요청 실패 수집 여부


    /// <summary>
    /// 실패 사유 목록
    /// </summary>
    List<string> _traceFailures = new( );
    /// <summary>
    /// 방별 변칙 후보 목록
    /// </summary>
    List<AnomalyTarget> _roomCandidates = new( );

    #region ----- 프로퍼티/이벤트 -----

    /// <summary>
    /// 최근 자동 요청 실패 설명
    /// </summary>
    public string StartFailureSummary => _traceFailures.Count == 0
        ? "등록 후보 없음 또는 실패 상세 미제공"
        : string.Join( " / ", _traceFailures );

    /// <summary>
    /// 최근 디버그 요청에서 보고된 실패 횟수
    /// </summary>
    public int DebugFailureCount => _debugHistory.FailureCount;

    /// <summary>
    /// 보관한 실제 실패 설명 수
    /// </summary>
    public int DebugFailureDetailCount => _debugHistory.FailureDetailCount;

    /// <summary>
    /// 사건 종류 표시 이름
    /// </summary>
    public abstract string DebugName { get; }
    /// <summary>
    /// 씬에서 설정을 조회할 사건 종류
    /// </summary>
    public abstract AnomalyEventType EventType { get; }
    /// <summary>
    /// 읽을 수 있는 발생 건 수
    /// </summary>
    public abstract int DebugOccurrenceCount { get; }
    /// <summary>
    /// 최근 종료 결과 수
    /// </summary>
    public int RecentResultCount => _debugHistory.RecentResultCount;

    /// <summary>
    /// 사건 발생 알림 이벤트(발생한 사건)
    /// </summary>
    public event Action<AnomalyEvent> OnStarted;

    /// <summary>
    /// 사건 결과 알림(변칙 사건, 진행 상태)
    /// </summary>
    public event Action<AnomalyEvent, AnomalyStatus> OnResult;

    #endregion


    /// <summary>
    /// 사건 시간 설정 연결
    /// </summary>
    /// <param name="settings">조회한 사건 설정</param>
    public void Init ( AnomalyEventSettings settings )
    {
        //진행 중인 사건은 발생 시
        //복사한 시간을 사용하므로 새 요청에만 반영
        _eventSettings = settings;
    }

    /// <summary>
    /// 설정 실패 사유 전달
    /// </summary>
    /// <param name="target">발생 대상</param>
    /// <param name="times">복사할 시간 값</param>
    /// <returns>유효한 설정 조회 여부</returns>
    protected bool TryGetEventTimes (
        AnomalyTarget target, out AnomalyEventTimes times )
    {
        times = default;
        string reason;

        if ( _eventSettings == null )
            reason = "PlayScene에서 사건 설정을 전달받지 못했습니다.";
        else if ( _eventSettings.TryGetTimes( out times, out reason ) == true )
            return true;

        ReportStartFailure( target,
            new AnomalyStartFailure( EventType.ToString( ), "InvalidSettings", reason ), true );
        return false;
    }


    #region ----- 발생 -----

    /// <summary>
    /// 기존 발생 조건을 유지하며 자동 요청 실패 수집
    /// </summary>
    /// <param name="startedEvent">발생한 사건</param>
    /// <returns>발생 성공 여부</returns>
    public bool TryStartTraced ( out AnomalyEvent startedEvent )
    {
        _traceFailures.Clear( );
        _traceRequest = true;

        try
        {
            bool started = TryStart( out startedEvent );

            if ( started == true && startedEvent != null )
                OnStarted?.Invoke( startedEvent );

            return started;
        }
        finally
        {
            //예외 이후에도 진단 요청 상태는 해제
            _traceRequest = false;
        }
    }

    /// <summary>
    /// 사건 발생 및 실패 사유 수집
    /// </summary>
    /// <param name="room">요청한 방</param>
    /// <param name="startedEvent">발생한 사건</param>
    /// <returns>실제 발생 여부</returns>
    public bool TryStartTraced (
        Room room, out AnomalyEvent startedEvent )
    {
        _traceFailures.Clear( );
        _traceRequest = true;

        try
        {
            bool started = TryStart( room, out startedEvent );

            if ( started == true && startedEvent != null )
                OnStarted?.Invoke( startedEvent );

            return started;
        }
        finally
        {
            _traceRequest = false;
        }
    }


    /// <summary>
    /// 변칙 사건 시작
    /// </summary>
    /// <param name="room">요청한 방</param>
    /// <param name="startedEvent">발생한 사건</param>
    /// <returns>실제 시작 여부</returns>
    public virtual bool TryStart (
        Room room, out AnomalyEvent startedEvent )
    {
        startedEvent = null;
        _roomCandidates.Clear( );

        if ( isActiveAndEnabled == false ||
             TryReadEventTimes( out AnomalyEventTimes times ) == false )
            return false;

        foreach ( AnomalyTarget target in AnomalyRegister.Targets )
        {
            if ( IsInRoom( target, room ) == true &&
                 CanStartTarget( target, times ) == true )
                _roomCandidates.Add( target );
        }

        if ( _roomCandidates.Count == 0 )
            return false;

        AnomalyTarget selected =
            _roomCandidates [ Random.Range( 0, _roomCandidates.Count ) ];

        //선정 뒤 대상 상태가 바뀌었다면 다른 대상을 재시도하지 않음
        if ( IsInRoom( selected, room ) == false ||
             CanStartTarget( selected, times ) == false )
            return false;

        return TryStartTarget( selected, out startedEvent );
    }

    /// <summary>
    /// 실제 검사에서 결정된 실패를 요청 결과에 기록
    /// </summary>
    /// <param name="target">발생을 시도한 대상</param>
    /// <param name="failure">검사에서 반환한 실패 정보</param>
    /// <param name="showLog">기존 호출 호환용 출력 옵션</param>
    protected void ReportStartFailure (
        AnomalyTarget target, AnomalyStartFailure failure, bool showLog )
    {
        if ( _traceRequest == true )
            _traceFailures.Add( $"{target}: {failure.Code} - {failure.Message}" );

        if ( _debugRequest == true )
        {
            _requestResult = new AnomalyStartResult( false, null, target, failure );
            _debugHistory.RecordFailure( target, failure );
        }
    }

    /// <summary>
    /// 기능별 실제 실패 지점의 결과 전달
    /// </summary>
    /// <param name="target">발생을 시도한 대상</param>
    /// <param name="code">기능별 실패 코드</param>
    /// <returns>발생 실패</returns>
    protected bool RejectStart ( AnomalyTarget target, string code )
    {
        if ( _debugRequest == true || _traceRequest == true )
            ReportStartFailure( target,
                AnomalyDebugViewBuilder.CreateFeatureFailure( DebugName, code ), false );

        return false;
    }

    /// <summary>
    /// 기존 실패 코드를 공통 요청 결과로 변환
    /// </summary>
    /// <typeparam name="T">추가 실패 정보 형식</typeparam>
    /// <param name="eventName">실패한 사건 이름</param>
    /// <param name="target">발생을 시도한 대상</param>
    /// <param name="reason">공통 실패 코드</param>
    /// <param name="showLog">기존 호출 호환용 출력 옵션</param>
    /// <param name="context">기존 호출 호환용 참조</param>
    /// <param name="detail">실패 설명에 포함할 추가 정보</param>
    protected void ReportStartFailure<T> (
        string eventName,
        AnomalyTarget target,
        AnomalyDebugViewBuilder.StartFailure reason,
        bool showLog,
        Object context, T detail )
    {
        if ( _debugRequest == false && _traceRequest == false )
            return;

        ReportStartFailure(
            target, AnomalyDebugViewBuilder
            .CreateFailure( eventName, reason, detail ), showLog );
    }

    /// <summary>
    /// 추가 정보 없는 기존 실패 코드 전달
    /// </summary>
    /// <param name="eventName">실패한 사건 이름</param>
    /// <param name="target">발생을 시도한 대상</param>
    /// <param name="reason">공통 실패 코드</param>
    /// <param name="showLog">기존 호출 호환용 출력 옵션</param>
    /// <param name="context">기존 호출 호환용 참조</param>
    protected void ReportStartFailure (
        string eventName, AnomalyTarget target,
        AnomalyDebugViewBuilder.StartFailure reason, bool showLog,
        Object context )
    {
        ReportStartFailure<string>( eventName, target, reason, showLog, context, null );
    }

    /// <summary>
    /// 관리 중인 사건을 취소하고 상태 전환이 없으면 직접 정리
    /// </summary>
    /// <typeparam name="TOccurrence">관리할 발생 건 형식</typeparam>
    /// <param name="occurrences">현재 관리 목록</param>
    /// <param name="occurrence">취소할 발생 건</param>
    /// <param name="anomalyEvent">취소할 사건 모델</param>
    /// <param name="release">기능별 구독과 잠금을 해제할 함수</param>
    protected void CancelManagedOccurrence<TOccurrence> (
        List<TOccurrence> occurrences, TOccurrence occurrence,
        AnomalyEvent anomalyEvent, Action<TOccurrence> release )
    {
        if ( occurrences.Contains( occurrence ) == false )
            return;

        //진행 중이면 상태 변경 알림에서 정리
        //준비 중이거나 종료된 건은 직접 정리
        if ( anomalyEvent.TryCancel( ) == false )
            release( occurrence );
    }


    /// <summary>
    /// 요청 방의 유효 대상 존재 여부 확인
    /// </summary>
    /// <param name="room">요청한 방</param>
    /// <returns>후보 존재 여부</returns>
    public virtual bool CanStart ( Room room )
    {
        if ( isActiveAndEnabled == false ||
             TryReadEventTimes( out AnomalyEventTimes times ) == false )
            return false;

        foreach ( AnomalyTarget target in AnomalyRegister.Targets )
        {
            if ( IsInRoom( target, room ) == true &&
                 CanStartTarget( target, times ) == true )
                return true;
        }

        return false;
    }

    #endregion

    #region ----- 사건 -----

    /// <summary>
    /// 지정한 대상에 사건 발생 요청
    /// </summary>
    /// <param name="target">선택한 대상</param>
    /// <param name="secondTarget">교환 사건의 두 번째 대상</param>
    /// <param name="startedEvent">발생한 사건</param>
    /// <returns>사건 발생 성공 여부</returns>
    protected abstract bool TryStartDebugManual (
        AnomalyTarget target, AnomalyTarget secondTarget,
        out AnomalyEvent startedEvent );

    /// <summary>
    /// 변칙 사건 발생
    /// </summary>
    /// <param name="startedEvent">발생한 사건</param>
    /// <returns>사건 발생 성공 여부</returns>
    public abstract bool TryStart ( out AnomalyEvent startedEvent );


    /// <summary>
    /// 발생 설정을 상태 변경 없이 조회
    /// </summary>
    /// <param name="times">발생 시간 설정</param>
    /// <returns>유효한 설정 여부</returns>
    protected bool TryReadEventTimes ( out AnomalyEventTimes times )
    {
        times = default;

        return _eventSettings != null &&
            _eventSettings.TryGetTimes( out times, out _ ) == true;
    }


    /// <summary>
    /// 사건별 대상 시작 요청
    /// </summary>
    /// <param name="target">발생 대상</param>
    /// <param name="startedEvent">발생한 사건</param>
    /// <returns>실제 시작 여부</returns>
    protected virtual bool TryStartTarget (
        AnomalyTarget target, out AnomalyEvent startedEvent )
    {
        startedEvent = null;
        return false;
    }

    /// <summary>
    /// 실제 발생 함수를 호출하고 상세 결과 반환
    /// </summary>
    /// <param name="registered">등록 후보에서 선택할지 여부</param>
    /// <param name="target">디버그 창에서 선택한 대상</param>
    /// <param name="secondTarget">교환 사건의 두 번째 대상</param>
    /// <returns>발생 성공 또는 실패 정보</returns>
    public AnomalyStartResult RequestDebugStart (
        bool registered, AnomalyTarget target = null,
        AnomalyTarget secondTarget = null )
    {
        if ( Application.isPlaying == false || isActiveAndEnabled == false )
            return new AnomalyStartResult( false, null, null,
                new AnomalyStartFailure(
                    "Common", "Inactive", "플레이 중인 활성 Presenter에서 실행할 수 있습니다." ) );

        if ( _debugRequest == true )
            return new AnomalyStartResult( false, null, null,
                new AnomalyStartFailure( "Common", "Busy", "발생 요청을 처리 중입니다." ) );

        _debugHistory.BeginRequest( );
        _debugRequest = true;
        _requestResult = new AnomalyStartResult( false, null, null,
            new AnomalyStartFailure( "Common", "StartFailed", "사건을 시작하지 못했습니다." ) );

        try
        {
            AnomalyEvent startedEvent;
            bool success = registered
                ? TryStart( out startedEvent )
                : TryStartDebugManual( target, secondTarget, out startedEvent );

            if ( success == true )
            {
                if ( startedEvent != null )
                    OnStarted?.Invoke( startedEvent );

                AnomalyTarget selectedTarget = null;
                for ( int i = 0; i < DebugOccurrenceCount; i++ )
                {
                    IAnomalyDebugOccurrence occurrence = GetDebugOccurrence( i );
                    if ( occurrence.IsEvent( startedEvent ) == false )
                        continue;

                    selectedTarget = occurrence.Target;
                    break;
                }
                return new AnomalyStartResult( true, startedEvent, selectedTarget, default );
            }

            return registered
                ? new AnomalyStartResult( false, null, null,
                    new AnomalyStartFailure( "Common", "NoCandidate",
                        _debugHistory.FailureCount > 0
                            ? $"발생 가능한 후보가 없습니다. 실제 검사 실패 {_debugHistory.FailureCount}건을 확인해 주세요."
                            : "발생 가능한 후보가 없습니다. 보고된 검사 실패가 없어 등록 대상과 기능 구성을 확인해야 합니다." ) )
                : _requestResult;
        }
        finally
        {
            //중간 반환이나 예외 이후에도 다음 요청을 받을 수 있도록 임시 상태 해제
            _debugRequest = false;
            _requestResult = default;
        }
    }

    /// <summary>
    /// 사건 결과 알림
    /// </summary>
    /// <param name="occurrence">결과가 발생한 사건</param>
    /// <param name="status">사건 결과 상태</param>
    protected void Result (
        AnomalyEvent occurrence, AnomalyStatus status )
    {
        //발생 준비와 시작은 결과에서 제외
        if ( status == AnomalyStatus.Ready ||
             status == AnomalyStatus.Active )
            return;

#if UNITY_EDITOR
        PlayDebugHistory.Record( "사건", this, status.ToString( ) );
#endif
        OnResult?.Invoke( occurrence, status );
    }


    /// <summary>
    /// 요청 방에 속한 활성 대상인지 확인
    /// </summary>
    /// <param name="target">확인할 대상</param>
    /// <param name="room">요청한 방</param>
    /// <returns>같은 방의 활성 대상 여부</returns>
    protected bool IsInRoom ( AnomalyTarget target, Room room )
    {
        return target != null &&
            target.isActiveAndEnabled == true &&
            room != null &&
            room.isActiveAndEnabled == true &&
            RoomRegister.TryGetRoom(
                target.transform.position, out Room current ) == true &&
            current == room;
    }

    /// <summary>
    /// 사건별 대상 시작 조건 조회
    /// </summary>
    /// <param name="target">발생 대상</param>
    /// <param name="times">발생 시간 설정</param>
    /// <returns>시작 가능한 대상 여부</returns>
    protected virtual bool CanStartTarget (
        AnomalyTarget target, AnomalyEventTimes times )
    {
        return false;
    }

    #endregion

    #region ----- 설정 검사 -----

    /// <summary>
    /// 상태를 변경하지 않고 사건 시간 설정 확인
    /// </summary>
    /// <param name="reason">설정 누락/유효하지 않은 사유</param>
    /// <returns>유효한 시간 설정 존재 여부</returns>
    public bool CheckDebugSettings ( out string reason )
    {
        if ( _eventSettings == null )
        {
            reason = "PlayScene에서 사건 설정을 전달받지 못했습니다.";
            return false;
        }

        return _eventSettings.TryGetTimes( out _, out reason );
    }

    #endregion

    #region ----- 디버그 조회/기록 -----

    /// <summary>
    /// 발생 건의 디버그 정보 조회
    /// </summary>
    /// <param name="index">조회할 발생 건 번호</param>
    /// <returns>발생 건의 읽기 및 디버그 실행 기능</returns>
    public abstract IAnomalyDebugOccurrence GetDebugOccurrence ( int index );

    /// <summary>
    /// 최근 요청에서 실제 검사 함수가 반환한 실패 설명 조회
    /// </summary>
    /// <param name="index">보관한 설명 번호</param>
    public string GetDebugFailure ( int index )
    {
        return _debugHistory.GetDebugFailure( index );
    }

    /// <summary>
    /// 최근 종료 결과 읽기
    /// </summary>
    /// <param name="index">조회할 종료 기록 번호</param>
    public AnomalyDebugRecord GetRecentResult ( int index )
    {
        return _debugHistory.GetRecentResult( index );
    }

    /// <summary>
    /// 최근 종료 기록만 지우기
    /// </summary>
    public void ClearRecentResults ()
    {
        _debugHistory.ClearRecentResults( );
    }

    /// <summary>
    /// 사건 정리 전에 종료 결과를 값으로 보관
    /// </summary>
    /// <param name="occurrence">종료 상태를 기록할 발생 건</param>
    protected void RecordDebugStatus ( IAnomalyDebugOccurrence occurrence )
    {
        _debugHistory.RecordStatus( DebugName, occurrence );
    }

    #endregion

}
