using System;
using UnityEngine;

/// <summary>
/// 존재 제어 - 등장, 제거, 조사와 복구 연결
/// </summary>
[RequireComponent( typeof( PresenceDisplayController ) )]
public class PresenceController : MonoBehaviour, IInvestigatable
{
    [Header( "----- 초기 상태 -----" )]
    [SerializeField] bool _initiallyPresent;     //처음부터 정상 물건이 존재하는지 여부

    [Header( "----- 공간 검사 -----" )]
    [SerializeField] LayerMask _obstacleMask;     //등장을 막는 물체 레이어

    [Header( "----- 재충전 -----" )]
    [SerializeField, Min( 0f )] float _refillDuration = 3f;     //조사 후 복구 대기 시간
    [SerializeField, Min( 0.05f )] float _restoreCheckInterval = 0.25f;     //공간 재검사 간격

    PresenceModel _model;       //존재 상태와 판정
    ItemCarry _carry;     //보유 상태 조회
    PresenceDisplayController _view;     //대상 표시와 숨김
    string _spaceFailureReason = string.Empty;     //최근 공간 검사 실패 사유
    RefillTimer _refillModel = new( );     //재충전 시간과 완료 대기 상태
    int _restoreTargetId;     //재충전을 시작한 조사 ID
    float _restoreCheckRemaining;     //다음 공간 검사까지 남은 시간

    /// <summary>
    /// 연결된 아이템의 현재 보유 상태
    /// </summary>
    bool IsHeld => CheckHeld( );

    /// <summary>
    /// 최근 등장/복구 공간 검사 실패 사유
    /// </summary>
    public string SpaceFailureReason => _spaceFailureReason;

    /// <summary>
    /// 물건 존재 여부
    /// </summary>
    public bool IsPresent => CheckPresent( );

    /// <summary>
    /// 정상 복구 대기 여부
    /// </summary>
    public bool IsRestoring => CheckRestoring( );

    /// <summary>
    /// 재충전 완료 후 정상 복구 공간 대기 여부
    /// </summary>
    public bool IsWaitingForSpace => CheckWaitingForSpace( );

    /// <summary>
    /// 현재 조사 목적
    /// </summary>
    public PresenceInvestigationType InvestigationType =>
        _model != null
            ? _model.InvestigationType
            : PresenceInvestigationType.None;

    /// <summary>
    /// 현재 목적의 조사 가능 여부
    /// </summary>
    public bool CanInvestigate => CheckCanInvestigate( );

    /// <summary>
    /// 조사 행동 이름
    /// </summary>
    public string ActionName => "조사";

    /// <summary>
    /// 현재 조사 ID
    /// </summary>
    public int InvestigationId =>
        _model != null ? _model.CurTargetId : 0;

    /// <summary>
    /// 존재 기능 사용 가능 여부
    /// </summary>
    public bool IsReady => CheckReady( );

    /// <summary>
    /// 대상 표시 여부
    /// </summary>
    public bool IsVisible => CheckVisible( );

    /// <summary>
    /// 조사 제거 완료 이벤트(제거한 물건의 조사 ID)
    /// </summary>
    public event Action<int> OnInvestigated;

    /// <summary>
    /// 빈자리 조사 완료 이벤트(복구를 요청한 조사 ID)
    /// </summary>
    public event Action<int> OnRestoreRequested;

    /// <summary>
    /// 정상 복구 완료 이벤트(재충전을 시작한 조사 ID)
    /// </summary>
    public event Action<int> OnRestored;

    /// <summary>
    /// 조사/재충전 완료 이벤트(처리할 조사 ID)
    /// </summary>
    public event Action<int> OnActionRequested;


    private void Awake ()
    {
        _view = GetComponent<PresenceDisplayController>( );

        _carry = GetComponent<ItemCarry>( );

        if ( _view == null )
        {
            Debug.LogError( $"[{name}] PresenceDisplayController가 없습니다.", this );
            return;
        }

        //초기 설정과 실제 표시가 일치할 때만 사용 준비 완료
        if ( _view.Init( _initiallyPresent, out string reason ) == false )
        {
            Debug.LogError( $"[{name}] 존재 기능 초기화 실패: {reason}", this );
            return;
        }

        _model = new PresenceModel( _initiallyPresent );
    }

    private void OnEnable ()
    {
        //재활성화 시 진행 중인 빈자리 조사에만 탐지 영역 복원
        _view?.SetRestoreInvestigation(
            InvestigationType == PresenceInvestigationType.Restore &&
            CheckCanInvestigate( ) == true );
    }

    private void OnDisable ()
    {
        //기능이 꺼진 동안 빈자리 조사 입력 차단
        _view?.SetRestoreInvestigation( false );
    }

    private void Update ()
    {
        //비활성 동안은 재충전 진행을 멈추고 재활성화 후 이어서 처리
        if ( CheckReady( ) == false || _refillModel.IsRefilling == false )
            return;

        _refillModel.Tick( Time.deltaTime );
        if ( _refillModel.IsElapsed == false )
            return;

        //재충전 종료 후 공간 검사는 매 프레임 대신 지정 간격으로 재시도
        _restoreCheckRemaining -= Time.deltaTime;
        if ( _restoreCheckRemaining > 0f )
            return;

        _restoreCheckRemaining = _restoreCheckInterval;
        TryCompleteRestore( );
    }


    #region ----- 표시/숨김 -----

    /// <summary>
    /// 현재 외형 연결
    /// </summary>
    /// <param name="appearance">새로 표시할 외형</param>
    /// <param name="reason">연결 실패 사유</param>
    /// <returns>외형 연결 성공 여부</returns>
    public bool TrySetAppearance ( AppearanceComponents appearance, out string reason )
    {
        if ( CheckAppearanceBinding( appearance, false, out reason ) == false )
            return false;

        return _view.TrySetAppearance( appearance, out reason );
    }

    /// <summary>
    /// 이전 외형 참조 복구
    /// </summary>
    /// <param name="appearance">교체 직전 외형</param>
    /// <param name="reason">복구 실패 사유</param>
    public bool TryRestoreAppearance ( AppearanceComponents appearance, out string reason )
    {
        if ( CheckAppearanceBinding( appearance, true, out reason ) == false )
            return false;

        return _view.TryRestoreAppearance( appearance, out reason );
    }


    /// <summary>
    /// 지정한 조사 목적으로 물건 표시
    /// </summary>
    /// <param name="investigationType">등장 후 사용할 조사 목적</param>
    /// <returns>물건 표시 성공 여부</returns>
    public bool TryShow ( PresenceInvestigationType investigationType )
    {
        _spaceFailureReason = string.Empty;

        if ( isActiveAndEnabled == false ||
             _model == null || _view == null )
            return false;

        //표시 중이거나 준비되지 않았다면 공간 검사 생략
        if ( _model.CanShow(
            _view.IsReady, _view.IsVisible, true, IsHeld ) == false )
            return false;

        //공간 확인
        bool hasSpace = _view.IsSpaceClear( _obstacleMask, out _spaceFailureReason );

        //아이템 등장 가능 여부 확인
        if ( _model.CanShow(
            _view.IsReady, _view.IsVisible, hasSpace, IsHeld ) == false )
            return false;

        //표시 적용에 성공한 뒤 모델의 존재 상태 변경
        if ( _view.TrySetVisible( true ) == false )
            return false;

        _model.SetPresent( true, investigationType );

        //물건이 등장하면 이전 빈자리 조사 종료
        _view.SetRestoreInvestigation( false );
        return true;
    }

    /// <summary>
    /// 조사로 제거할 물건 표시
    /// </summary>
    /// <returns>물건 표시 성공 여부</returns>
    public bool TryShow ()
    {
        //인수를 생략한 등장은 조사로 제거하는 대상으로 설정
        return TryShow( PresenceInvestigationType.Remove );
    }

    /// <summary>
    /// 표시된 물건 숨기기
    /// </summary>
    public bool TryRemove ()
    {
        if ( isActiveAndEnabled == false ||
             _model == null || _view == null )
            return false;

        if ( _model.CanRemove(
            _view.IsReady, _view.IsVisible, IsHeld ) == false )
            return false;

        if ( _view.TrySetVisible( false ) == false )
            return false;

        //실제로 숨긴 뒤 모델에 제거 상태 반영
        _model.SetPresent( false );
        return true;
    }

    #endregion

    #region ----- 복구 -----

    /// <summary>
    /// 물건을 숨기고 빈자리 조사 시작
    /// </summary>
    /// <returns>물건 숨김과 조사 시작 성공 여부</returns>
    public bool TryRemoveForRestore ()
    {
        //조사 영역과 재충전 설정 실패로 물건만 사라지는 상황 방지
        if ( CheckRemoveForRestore( out _ ) == false )
            return false;

        if ( TryRemove( ) == false )
            return false;

        if ( _model.TryBeginRestoreInvestigation( ) == false )
            return false;

        _view.SetRestoreInvestigation( true );
        return true;
    }

    /// <summary>
    /// 복구 대기 표시와 재충전 시작
    /// </summary>
    /// <param name="expectedInvestigationId">조사 시작 시 저장한 ID</param>
    /// <returns>대기 표시와 재충전 시작 여부</returns>
    bool TryStartRestore ( int expectedInvestigationId )
    {
        if ( CheckRefillSettings( ) == false )
        {
            Debug.LogWarning( $"[{name}] 재충전 시간과 공간 검사 간격을 확인해 주세요.", this );
            return false;
        }

        if ( _refillModel.TryStart( _refillDuration ) == false )
            return false;

        if ( _view.TryBeginRestoreDisplay( out string reason ) == false )
        {
            _refillModel.Cancel( );
            Debug.LogWarning( $"[{name}] 복구 대기 표시 실패: {reason}", this );
            return false;
        }

        //대기 표시 후 조사를 종료
        //실패 시 숨김 상태와 타이머 복원
        if ( _model.TryCompleteRestoreInvestigation(
            expectedInvestigationId, _view.IsReady, _view.IsVisible, IsHeld ) == false )
        {
            _view.CancelRestoreDisplay( );
            _refillModel.Cancel( );
            return false;
        }

        _restoreTargetId = expectedInvestigationId;
        _restoreCheckRemaining = 0f;
        _spaceFailureReason = string.Empty;
        _view.SetRestoreInvestigation( false );
        OnRestoreRequested?.Invoke( expectedInvestigationId );
        return true;
    }

    /// <summary>
    /// 대기 시간과 공간 확인 후 복구
    /// </summary>
    /// <param name="skipDelay">남은 재충전 시간을 건너뛸지 여부</param>
    /// <returns>정상 복구 성공 여부</returns>
    bool TryCompleteRestore ( bool skipDelay = false )
    {
        int requestedId = _restoreTargetId;
        //사건의 시간 만료 처리를 먼저 요청한 뒤 같은 조사가 유지되는지 확인
        OnActionRequested?.Invoke( requestedId );

        //시간 만료 정리나 새 조사로 바뀌었으면 이전 복구 중단
        if ( CheckReady( ) == false || InvestigationId != requestedId )
            return false;

        if ( ( skipDelay == false && _refillModel.IsElapsed == false ) ||
             _model.CanRestore(
                 _restoreTargetId, _view.IsReady, _view.IsVisible, IsHeld ) == false )
            return false;

        //장애물 확인
        if ( _view.IsSpaceClear( _obstacleMask, out _spaceFailureReason ) == false )
            return false;

        if ( _view.TryCompleteRestoreDisplay( out _spaceFailureReason ) == false )
            return false;

        int restoredId = _restoreTargetId;
        if ( _model.TryCompleteRestore( restoredId ) == false )
            return false;

        _refillModel.Cancel( );
        _restoreCheckRemaining = 0f;
        _view.SetRestoreInvestigation( false );
        _spaceFailureReason = string.Empty;

        //표시와 물리, 모델을 모두 복원한 후 한 번만 완료 알림
        int completedId = InvestigationId;
        OnRestored?.Invoke( restoredId );
        //완료 알림에서 새 사건이 시작됐다면 이전 복구 결과로 사용하지 않음
        return CheckReady( ) && InvestigationId == completedId &&
               IsPresent && IsVisible && IsRestoring == false;
    }

    /// <summary>
    /// 대기 없이 복구 시도
    /// </summary>
    /// <param name="expectedInvestigationId">발생 당시 조사 ID</param>
    /// <returns>복구 완료 여부</returns>
    public bool TryRestoreImmediately ( int expectedInvestigationId )
    {
        _spaceFailureReason = string.Empty;
        if ( CheckReady( ) == false ||
             _model.CanRestoreImmediately( expectedInvestigationId, _view.IsReady, IsHeld ) == false )
        {
            _spaceFailureReason = "복구 대상 ID, 준비 상태 또는 보유 상태를 확인해 주세요.";
            return false;
        }

        //즉시 복구도 공간이 필요하며 실패하면 기존 조사와 대기 표시 유지
        if ( _view.IsSpaceClear( _obstacleMask, out _spaceFailureReason ) == false )
            return false;

        if ( _model.IsRestoring == false )
        {
            if ( _view.TryBeginRestoreDisplay( out _spaceFailureReason ) == false )
                return false;

            if ( _model.TryCompleteRestoreInvestigation(
                expectedInvestigationId, _view.IsReady, _view.IsVisible, IsHeld ) == false )
            {
                _view.CancelRestoreDisplay( );
                return false;
            }

            _restoreTargetId = expectedInvestigationId;
            _view.SetRestoreInvestigation( false );
        }

        //OnRestoreRequested는 실제 조사에만 발행
        return TryCompleteRestore( true );
    }

    /// <summary>
    /// 남은 복구 이어서 처리
    /// </summary>
    /// <param name="expectedInvestigationId">해제하는 사건의 조사 ID</param>
    public void ContinuePendingRestore ( int expectedInvestigationId )
    {
        if ( _model == null || _model.IsRestoring == false ||
             _model.CurTargetId != expectedInvestigationId || _refillModel.IsRefilling == true )
            return;

        //사건이 끝나도 대기 표시는 남을 수 있어 추가 대기 없이 공간 검사 재개
        _refillModel.TryStart( 0f );
        _restoreCheckRemaining = 0f;
    }

    #endregion

    #region ----- 조사 -----

    /// <summary>
    /// 조사와 복구 대기 종료
    /// </summary>
    /// <param name="expectedInvestigationId">종료할 사건의 조사 ID</param>
    /// <returns>종료 처리 여부</returns>
    public bool TryEndInvestigation ( int expectedInvestigationId )
    {
        if ( _model == null || _model.TryEndInvestigation( expectedInvestigationId ) == false )
            return false;

        _refillModel.Cancel( );
        _restoreTargetId = 0;
        _restoreCheckRemaining = 0f;
        _spaceFailureReason = string.Empty;
        _view?.SetRestoreInvestigation( false );
        _view?.CancelRestoreDisplay( );
        return true;
    }

    /// <summary>
    /// 조사 목적에 따라 물건 제거 또는 복구 요청
    /// </summary>
    /// <param name="expectedInvestigationId">조사 시작 시 저장한 ID</param>
    /// <returns>조사 완료 성공 여부</returns>
    public bool TryInvestigate ( int expectedInvestigationId )
    {
        //사건 처리 중 취소되거나 ID가 바뀔 수 있으므로 알림 후 상태 재확인
        OnActionRequested?.Invoke( expectedInvestigationId );

        if ( CheckInvestigationReady( ) == false )
            return false;

        //현재 물건과 조사 시작 때의 물건이 같은지 확인
        if ( _model.CanInvestigate(
            expectedInvestigationId, true, _view.IsVisible, IsHeld ) == false )
            return false;

        //빈자리 조사는 대기 표시와 재충전을 시작
        if ( InvestigationType == PresenceInvestigationType.Restore )
            return TryStartRestore( expectedInvestigationId );

        //제거 조사는 물건을 숨긴 뒤 완료 알림
        if ( TryRemove( ) == false )
            return false;

        //제거 처리 중 대상이 무효화되거나 새 물건이 등장하면 제외
        if ( isActiveAndEnabled == false ||
             InvestigationId != expectedInvestigationId ||
             IsPresent == true )
            return false;

        OnInvestigated?.Invoke( expectedInvestigationId );
        return true;
    }

    #endregion

    #region ----- 설정/상태 검사 -----

    /// <summary>
    /// 외형 연결 가능 여부 확인
    /// </summary>
    /// <param name="appearance">연결할 외형</param>
    /// <param name="isRollback">교체 실패 후 참조 복구 여부</param>
    /// <param name="reason">연결 불가 사유</param>
    public bool CheckAppearanceBinding (
        AppearanceComponents appearance, bool isRollback, out string reason )
    {
        reason = string.Empty;
        if ( isActiveAndEnabled == false || _view == null || _model == null ||
             _refillModel == null ||
             ( isRollback == false && ( CheckReady( ) == false || IsVisible == false ) ) ||
             IsPresent == false ||
             IsHeld == true || IsRestoring == true ||
             InvestigationType != PresenceInvestigationType.None ||
             _refillModel.IsRefilling == true )
        {
            reason = "미보유 상태의 정상 표시 물건만 외형을 변경할 수 있습니다. 조사와 복구를 먼저 종료해 주세요.";
            return false;
        }

        return _view.CheckAppearanceBinding( appearance, isRollback, out reason );
    }

    /// <summary>
    /// 복구용 제거에 필요한 상태와 설정 확인
    /// </summary>
    /// <param name="reason">제거할 수 없는 사유</param>
    /// <returns>복구용 제거 가능 여부</returns>
    public bool CheckRemoveForRestore ( out string reason )
    {
        reason = string.Empty;

        if ( CheckReady( ) == false )
            reason = "존재 기능의 초기화와 활성 상태를 확인해 주세요.";
        else if ( _view.IsRestoreReady == false )
            reason = "복구 조사 Collider 연결과 활성 상태를 확인해 주세요.";
        else if ( CheckRefillSettings( ) == false )
            reason = "재충전 시간과 공간 검사 간격을 확인해 주세요.";
        else if ( _obstacleMask.value == 0 )
            reason = "복구 공간 검사에 사용할 Obstacle Mask를 지정해 주세요.";
        else if ( _model.CanRemove( true, _view.IsVisible, IsHeld ) == false )
            reason = "표시된 정상 물건이 필요하며 보유 중이거나 복구 중이면 제거할 수 없습니다.";

        return string.IsNullOrEmpty( reason );
    }

    /// <summary>
    /// 재충전이 끝나고 정상 복구를 기다리는지 확인
    /// </summary>
    /// <returns>복구 공간 대기 여부</returns>
    bool CheckWaitingForSpace ()
    {
        return CheckRestoring( ) == true &&
               ( _refillModel.IsElapsed == true || _refillModel.IsRefilling == false );
    }

    /// <summary>
    /// 보유 상태 확인
    /// </summary>
    /// <returns>현재 보유 여부</returns>
    bool CheckHeld ()
    {
        return _carry != null && _carry.IsHold;
    }

    /// <summary>
    /// 물건 존재 여부 확인
    /// </summary>
    /// <returns>물건 존재 여부</returns>
    bool CheckPresent ()
    {
        return _model != null && _model.IsPresent;
    }

    /// <summary>
    /// 정상 복구 대기 여부 확인
    /// </summary>
    /// <returns>복구 대기 여부</returns>
    bool CheckRestoring ()
    {
        return _model != null && _model.IsRestoring;
    }

    /// <summary>
    /// 실제 표시 여부 확인
    /// </summary>
    /// <returns>물건 표시 여부</returns>
    bool CheckVisible ()
    {
        return _view != null && _view.IsVisible;
    }

    /// <summary>
    /// 재충전 시간과 공간 검사 간격 확인
    /// </summary>
    /// <returns>재충전 설정 유효 여부</returns>
    bool CheckRefillSettings ()
    {
        return float.IsNaN( _refillDuration ) == false &&
               float.IsInfinity( _refillDuration ) == false && _refillDuration >= 0f &&
               float.IsNaN( _restoreCheckInterval ) == false &&
               float.IsInfinity( _restoreCheckInterval ) == false && _restoreCheckInterval >= 0.05f;
    }

    /// <summary>
    /// 존재 기능 사용 준비 확인
    /// </summary>
    /// <returns>모델과 표시 기능 준비 여부</returns>
    bool CheckReady ()
    {
        return isActiveAndEnabled == true &&
               _model != null &&
               _view != null &&
               _view.IsReady;
    }

    /// <summary>
    /// 현재 조사 목적에 필요한 기능 준비 확인
    /// </summary>
    /// <returns>조사 기능 준비 여부</returns>
    bool CheckInvestigationReady ()
    {
        if ( CheckReady( ) == false )
            return false;

        if ( InvestigationType == PresenceInvestigationType.Restore )
            return _view.IsRestoreReady;

        return true;
    }

    /// <summary>
    /// 현재 상태에서 조사할 수 있는지 확인
    /// </summary>
    /// <returns>조사 가능 여부</returns>
    bool CheckCanInvestigate ()
    {
        if ( CheckInvestigationReady( ) == false )
            return false;

        return _model.CanInvestigate(
            _model.CurTargetId, true, _view.IsVisible, IsHeld );
    }

    #endregion

    #region ----- 테스트 -----

    /// <summary>
    /// 플레이 중 등장 테스트
    /// </summary>
    [ContextMenu( "Test Show" )]
    void TestShow ()
    {
        if ( Application.isPlaying == true )
            TryShow( );
    }

    /// <summary>
    /// 플레이 중 제거 테스트
    /// </summary>
    [ContextMenu( "Test Remove" )]
    void TestRemove ()
    {
        if ( Application.isPlaying == true )
            TryRemove( );
    }

    /// <summary>
    /// 플레이 중 물건 숨김과 빈자리 조사 시작 테스트
    /// </summary>
    [ContextMenu( "Test Remove For Restore" )]
    void TestRemoveForRestore ()
    {
        if ( Application.isPlaying == true )
            TryRemoveForRestore( );
    }

    #endregion

}
