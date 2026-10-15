using System;
using System.Collections.Generic;

/// <summary>
/// 변칙 사건 연결 - 시작 결과와 적의 사건 구독 관리
/// </summary>
public class AnomalyLinker
{
    bool _isRunning;        //사건 연결과 결과 수신 허용 여부
    bool _isStarting;       //발생 처리 중 중복 요청 방지
    int _requestId;		//시작 요청 번호

    AnomalyEventSystem _activePresenter;     //현재 사건의 중재자
    AnomalyEvent _activeEvent;      //발생한 사건

    /// <summary>
    /// 시작 호출 중 수신한 사건과 결과
    /// </summary>
    List<(AnomalyEvent occurrence, AnomalyStatus status)> _pendingResults = new( );

    /// <summary>
    /// 새 사건 연결 가능 여부
    /// </summary>
    public bool CanPrepare =>
        _isRunning == true && _isStarting == false && HasActiveEvent == false;

    /// <summary>
    /// 처리 중인 사건 존재 여부
    /// </summary>
    public bool HasActiveEvent => _activeEvent != null;

#if UNITY_EDITOR
    /// <summary>
    /// 연결된 사건 조회
    /// </summary>
    public AnomalyEvent DebugEvent => _activeEvent;

    /// <summary>
    /// 연결된 사건 중재자 조회
    /// </summary>
    public AnomalyEventSystem DebugPresenter => _activePresenter;
#endif

    /// <summary>
    /// 변칙 사건 결과 이벤트(변칙 사건, 진행 상태)
    /// </summary>
    public event Action<AnomalyEvent, AnomalyStatus> OnResult;


    #region ----- 사건 연결 -----

    /// <summary>
    /// 추적 중인 사건의 결과 전달
    /// </summary>
    /// <param name="occurrence">결과가 발생한 사건</param>
    /// <param name="status">사건 결과</param>
    void HandleResult (
        AnomalyEvent occurrence, AnomalyStatus status )
    {
        if ( _isRunning == false ||
             status == AnomalyStatus.Ready ||
             status == AnomalyStatus.Active )
            return;

        //시작 결과를 받기 전에는 사건 식별 후 전달
        if ( _isStarting == true )
        {
            _pendingResults.Add( (occurrence, status) );
            return;
        }

        if ( ReferenceEquals( occurrence, _activeEvent ) == false )
            return;

#if UNITY_EDITOR
        PlayDebugHistory.Record( "사건 연결", _activePresenter, $"적에게 결과 전달: {status}" );
#endif
        ClearActiveEvent( );
        OnResult?.Invoke( occurrence, status );
    }

    /// <summary>
    /// 새 활동 주기의 사건 연결 시작
    /// </summary>
    public void Begin ()
    {
        ClearActiveEvent( );

        _isRunning = true;
    }

    /// <summary>
    /// 사건 연결과 결과 수신 중단
    /// </summary>
    public void Stop ()
    {
        //이미 발생한 사건은 유지하고 결과 구독만 중단
        _isRunning = false;
        ClearActiveEvent( );
    }

    /// <summary>
    /// 현재 사건의 구독과 참조 정리
    /// </summary>
    void ClearActiveEvent ()
    {
        if ( _activePresenter != null )
            _activePresenter.OnResult -= HandleResult;

        _activeEvent = null;
        _activePresenter = null;
        _isStarting = false;
        _pendingResults.Clear( );
        _requestId++;
    }


    /// <summary>
    /// 사건 시작 전 결과 구독
    /// </summary>
    /// <param name="presenter">선정한 사건 프레젠터</param>
    /// <param name="requestId">시작 요청 번호</param>
    /// <returns>연결 준비 여부</returns>
    public bool TryPrepareStart ( 
        AnomalyEventSystem presenter, out int requestId )
    {
        requestId = 0;

        if ( CanPrepare == false || presenter == null )
            return false;

        _requestId++;
        requestId = _requestId;
        _isStarting = true;

        _activePresenter = presenter;
        _activePresenter.OnResult += HandleResult;

        return true;
    }

    /// <summary>
    /// 시작한 사건 확정과 대기 결과 전달
    /// </summary>
    /// <param name="requestId">시작 요청 번호</param>
    /// <param name="occurrence">실제 시작한 사건</param>
    /// <param name="onStarted">적의 시작 성공 반영</param>
    public void CompleteStart (
        int requestId, AnomalyEvent occurrence, Action onStarted )
    {
        if ( _isRunning == false || _isStarting == false ||
             requestId != _requestId || occurrence == null )
            return;

        _activeEvent = occurrence;
        onStarted( );

        //시작 반영 중 종료된 활동은 다시 연결하지 않음
        if ( _isRunning == false || _isStarting == false || requestId != _requestId )
            return;

        AnomalyStatus? pending = null;		//현재 사건의 대기 결과

        foreach ( var result in _pendingResults )
        {
            if ( ReferenceEquals( result.occurrence, occurrence ) == true )
            {
                pending = result.status;
                break;
            }
        }

        _pendingResults.Clear( );
        _isStarting = false;

        if ( pending.HasValue == true )
            HandleResult( occurrence, pending.Value );
    }

    /// <summary>
    /// 완료되지 않은 시작 구독 해제
    /// </summary>
    /// <param name="requestId">시작 요청 번호</param>
    public void CancelStart ( int requestId )
    {
        if ( _isStarting == true && requestId == _requestId )
            ClearActiveEvent( );
    }

    #endregion

}
