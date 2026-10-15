using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// NPC 중재 - 행동 상태 전환, 안전 상태와 포획 및 구조 처리
/// </summary>
[RequireComponent( typeof( NpcMover ), typeof( Capturable ) )]
public class Npc : MonoBehaviour, ICaptureTarget, IAttackTarget
{
    [Header( "----- 설정 데이터 -----" )]
    [SerializeField] NpcData _data;		//일반 행동 설정

    [Header( "----- 컴포넌트 -----" )]
    [SerializeField] NpcMover _mover;		//이동 모듈
    [SerializeField] NpcAnimatorHandler _animatorHandler;		//이동과 피포획 애니메이션
    [SerializeField] CaptureEffect _captureEffect;		//NPC 납치 자세 연출
    [SerializeField] NpcAppearance _appearance;		//무늬 적용, 미사용 시 비워 둠

    [Header( "----- 사운드 -----" )]
    [SerializeField] NpcSound _npcSound;		//NPC 사운드
    [SerializeField] SoundPlayer _soundPlayer;		//NPC 전용 사운드 재생

    bool _isLocalReady;		//프리팹 내부 컴포넌트 준비 여부
    bool _isReady;		//씬 공용 설정 준비 여부
    bool _isRunning;		//플레이 시작 여부
    bool _isEndingCapture;		//포획 결과 전달 중
    int _captureRound;		//성공한 포획 회차

#if UNITY_EDITOR
    bool _isAiTest;		//AI 실행 제외 테스트 여부
#endif

    Capturable _capturable;		//포획, 구조

    NpcState _currentState;		//현재 행동 상태, null이면 실행 중인 상태 없음
    NpcNormalBehaviour _normalBehaviour;		//Normal 내부 행동 판단
    NpcSafetyModel _model = new( );		//NPC 안전 상태

    /// <summary>
    /// Npc 행동 딕셔너리(상태 타입, 상태)
    /// </summary>
    Dictionary<NpcStateType, NpcState> _states = new( );

    /// <summary>
    /// 대상 트랜스폼
    /// </summary>
    public Transform TargetTransform => transform;

    /// <summary>
    /// 현재 안전 상태
    /// </summary>
    public NpcSafeStatusType Status => _model.Status;

    /// <summary>
    /// 게임에 남아 있는지 여부
    /// </summary>
    public bool IsRemaining => _model.IsRemaining;

    /// <summary>
    /// 피격 가능 여부
    /// </summary>
    public bool CanTakeHit => CanCapture;

    /// <summary>
    /// 포획 가능 여부
    /// </summary>
    public bool CanCapture => CheckCanCapture( );

    /// <summary>
    /// 구조 가능 여부
    /// </summary>
    public bool CanRescue => CheckCanRescue( );

    /// <summary>
    /// 구조 진행 비율
    /// </summary>
    public float RescueProgressRatio =>
        CanRescue == true ? _capturable.RescueProgressRatio : 0f;

    /// <summary>
    /// 현재 행동 상태
    /// </summary>
    public NpcStateType? CurrentState =>
        _currentState?.StateType;

    /// <summary>
    /// 안전 상태 변경 이벤트(이전 상태, 현재 상태)
    /// </summary>
    public event Action<NpcSafeStatusType, NpcSafeStatusType> OnStatusChanged;

    /// <summary>
    /// NPC 포획 시작 이벤트(NPC, 포획 회차)
    /// </summary>
    public event Action<Npc, int> OnCaptureStarted;

    /// <summary>
    /// 포획 종료 이벤트(포획 결과)
    /// </summary>
    public event Action<CaptureResult> OnCaptureEnded;

    /// <summary>
    /// 포획 해제 요청 이벤트(포획 결과)
    /// </summary>
    public event Action<CaptureResult> OnReleaseRequested;

    /// <summary>
    /// NPC 포획 결과 이벤트(NPC, 포획 회차, 결과)
    /// </summary>
    public event Action<Npc, int, CaptureResult> OnCaptureResult;


    /// <summary>
    /// 프리팹 내부 컴포넌트와 상태 준비
    /// </summary>
    void InitLocalComponents ()
    {
        if ( _isLocalReady == true )
            return;

        _mover.Init( _animatorHandler );

        if ( _captureEffect != null )
            _captureEffect.Init( );

        //NPC 상태와 실제 이동을 사운드 담당에 연결
        if ( _npcSound != null )
            _npcSound.Init( this, _mover, _soundPlayer );

        _mover.OnCaptureInterrupted += HandleCaptureInterrupted;

        NpcState normal = new NpcNormalState( this );
        NpcState excited = new NpcExcitedState( );
        NpcState captured = new NpcCapturedState( );

        _states.Add( normal.StateType, normal );
        _states.Add( excited.StateType, excited );
        _states.Add( captured.StateType, captured );


        _capturable = GetComponent<Capturable>( );

        _capturable.Init( this );
        _capturable.OnReleaseRequested += HandleReleaseRequested;

        NpcRegister.Register( this );
        _isLocalReady = true;
    }

    /// <summary>
    /// 씬 공용 설정으로 방 이동과 무늬 준비
    /// </summary>
    /// <param name="partyRoom">기본 거점인 파티룸</param>
    /// <param name="routeGraph">공용 방 이동 경로 그래프</param>
    /// <param name="appearanceMap">무늬 후보 목록, 외형 담당 미사용 시 null 허용</param>
    /// <returns>NPC 준비 성공 여부</returns>
    public bool Init ( Room partyRoom, RouteGraph routeGraph, MaterialMap appearanceMap )
    {
        if ( _isReady == true )
            return true;

        //비활성 NPC도 Awake 실행 순서와 무관하게 준비
        InitLocalComponents( );

#if UNITY_EDITOR
        if ( _isAiTest == false )
#endif
        {
            if ( _data == null || partyRoom == null || routeGraph == null )
            {
                Debug.LogError( $"[NPC 초기화 실패] {name}: 행동 데이터, 파티룸과 공용 경로 그래프를 확인해 주세요.", this );
                return false;
            }

            _mover.InitNormalMove( _data, routeGraph );
            _normalBehaviour = new NpcNormalBehaviour( _mover, _data, partyRoom );
        }

        if ( _appearance != null && _appearance.Init( appearanceMap ) == false )
            return false;

        _isReady = true;
        return true;
    }

    private void Awake ()
    {
        InitLocalComponents( );
    }

    private void Update ()
    {
        _currentState?.Update( );
    }

    private void OnEnable ()
    {
        //NPC 컴포넌트 재활성화 시 이전 재생과 이동 기록 정리
        if ( _npcSound != null )
            _npcSound.ResetSounds( );

        //최초 활성화와 재활성화 모두 현재 안전 상태를 기준으로 시작
        SyncState( );
    }

    private void OnDisable ()
    {
        if ( _captureEffect != null )
            _captureEffect.ResetEffect( );

        //NPC 컴포넌트만 비활성화되어도 사운드 정리
        if ( _npcSound != null )
            _npcSound.ResetSounds( );

        StopState( );

        if ( Status != NpcSafeStatusType.Captured )
            return;

        //포획자 역호출 없이 관계 정리
        FinishRelease( Vector3.zero, CaptureResult.Cancelled );
    }

    private void OnDestroy ()
    {
        if ( _capturable != null )
            _capturable.OnReleaseRequested -= HandleReleaseRequested;

        if ( _mover != null )
            _mover.OnCaptureInterrupted -= HandleCaptureInterrupted;

        NpcRegister.Unregister( this );
    }


    /// <summary>
    /// 준비된 NPC의 행동 시작
    /// </summary>
    public void Begin ()
    {
        if ( _isReady == false || _isRunning == true )
            return;

        _isRunning = true;
        SyncState( );
    }


    #region ----- 행동 상태 -----

    /// <summary>
    /// 현재 행동 종료
    /// </summary>
    void StopState ()
    {
        NpcState previous = _currentState;
        _currentState = null;
        previous?.Exit( );
    }

    /// <summary>
    /// 기존 행동을 종료하고 새 행동 시작
    /// </summary>
    /// <param name="stateType">진입할 행동 상태</param>
    void ChangeState ( NpcStateType stateType )
    {
        if ( CurrentState == stateType )
            return;

#if UNITY_EDITOR
        PlayDebugHistory.Record( "NPC", this, $"{CurrentState?.ToString( ) ?? "미시작"} → {stateType}" );
#endif
        StopState( );

        _currentState = _states [ stateType ];
        _currentState.Enter( );
    }

    /// <summary>
    /// 안전 상태에 맞춰 행동 상태 동기화
    /// </summary>
    void SyncState ()
    {
#if UNITY_EDITOR
        if ( _isAiTest == true )
        {
            StopState( );
            return;
        }
#endif

        //씬 설정 준비와 플레이 시작 전에는 행동을 실행하지 않음
        if ( _isReady == false || _isRunning == false ||
             isActiveAndEnabled == false || IsRemaining == false )
        {
            StopState( );
            return;
        }

        //음악 반응을 연결하기 전에는 정상과 포획 행동만 사용
        NpcStateType next = Status == NpcSafeStatusType.Captured
            ? NpcStateType.Captured
            : NpcStateType.Normal;

        ChangeState( next );
    }

    /// <summary>
    /// 일반 행동 시작 준비
    /// </summary>
    public void BeginNormal ()
    {
#if UNITY_EDITOR
        if ( _isAiTest == true )
            return;
#endif

        _normalBehaviour.Begin( );
    }

    /// <summary>
    /// 일반 행동 갱신
    /// </summary>
    /// <param name="deltaTime">경과 시간</param>
    public void UpdateNormal ( float deltaTime )
    {
#if UNITY_EDITOR
        if ( _isAiTest == true )
            return;
#endif

        if ( isActiveAndEnabled == false ||
             CurrentState != NpcStateType.Normal ||
             Status != NpcSafeStatusType.Normal )
            return;

        _normalBehaviour.Update( deltaTime );
    }

    /// <summary>
    /// 일반 행동과 진행 중인 이동 종료
    /// </summary>
    public void EndNormal ()
    {
#if UNITY_EDITOR
        if ( _isAiTest == true )
            return;
#endif

        _normalBehaviour.End( );
    }

    #endregion

    #region ----- 포획/구조 -----

    /// <summary>
    /// 포획 수락과 따라가기 시작
    /// </summary>
    /// <param name="owner">포획자</param>
    /// <param name="holdPoint">보유 지점</param>
    /// <returns>포획 성공 여부</returns>
    public bool TryCapture ( CaptureController owner, Transform holdPoint )
    {
        if ( CanCapture == false || _capturable.TryBegin( owner ) == false )
            return false;

        //이전 상태 저장
        NpcSafeStatusType prevStatus = Status;

        //상태 변환 실패 시 종료
        if ( _model.TryCapture( ) == false )
        {
            _capturable.End( );
            return false;
        }

        //이동 적용에 실패한 요청은 포획 회차로 발행하지 않음
        if ( _mover.TryBeginCapture( holdPoint ) == false )
        {
            _model.TryRelease( );
            _capturable.End( );
            return false;
        }

        _captureRound++;

        //포획 성공 이후 행동 상태 반영
        SyncState( );

        if ( _animatorHandler != null )
            _animatorHandler.SetCaptured( true );

        if ( _captureEffect != null )
            _captureEffect.SetCaptured( true );

        OnCaptureStarted?.Invoke( this, _captureRound );
        //시작 콜백에서 취소된 회차는 성공으로 반환하지 않음
        if ( IsCapturedBy( owner ) == false )
            return false;

        NotifyStatusChanged( prevStatus );
        return IsCapturedBy( owner );
    }

    /// <summary>
    /// 구조 시도
    /// </summary>
    /// <param name="progressAmount">입력 진행량</param>
    /// <returns>목표 진행도 도달 여부</returns>
    public bool TryRescue ( float progressAmount = 1f )
    {
        if ( CanRescue == false )
            return false;

        return _capturable.TryRescue( progressAmount );
    }


    /// <summary>
    /// 포획 해제와 낙하 시작
    /// </summary>
    /// <param name="impulse">해제 충격량</param>
    /// <param name="result">종료 결과</param>
    /// <returns>해제 성공 여부</returns>
    bool FinishRelease ( Vector3 impulse, CaptureResult result )
    {
        if ( _isEndingCapture == true )
            return false;

        //종료 알림이 모두 끝나기 전에는 새 포획 차단
        _isEndingCapture = true;
        try
        {
            //이전 상태 저장
            NpcSafeStatusType prevStatus = Status;

            //포획 해제 실패 시
            if ( _model.TryRelease( ) == false )
                return false;

            //포획 관계를 정리하고 낙하를 시작한 뒤 종료 알림 전달
            _capturable.End( );
            _mover.EndCapture( impulse );

            if ( _animatorHandler != null )
                _animatorHandler.SetCaptured( false );

            if ( _captureEffect != null )
                _captureEffect.ResetEffect( );

            //Normal로 전환하되 실제 행동은 착지 후 허용
            SyncState( );

            //포획 종료 이벤트 발행
            OnCaptureResult?.Invoke( this, _captureRound, result );
            OnCaptureEnded?.Invoke( result );
            NotifyStatusChanged( prevStatus );
            return true;
        }
        finally
        {
            _isEndingCapture = false;
        }
    }

    /// <summary>
    /// 포획 해제
    /// </summary>
    /// <param name="owner">포획자</param>
    /// <param name="impulse">해제 충격량</param>
    /// <param name="result">종료 결과</param>
    /// <returns>해제 성공 여부</returns>
    public bool TryRelease (
        CaptureController owner, Vector3 impulse, CaptureResult result )
    {
        //포획자가 없거나 다르면
        if ( IsCapturedBy( owner ) == false )
            return false;

        //결과가 구조 또는 취소가 아니면 종료
        if ( result != CaptureResult.Rescued &&
             result != CaptureResult.Cancelled )
            return false;

        //보유 지점 소실과 구조 입력이 겹쳐도 구조 성공으로 확정하지 않음
        if ( _mover.CanFollow == false )
        {
            result = CaptureResult.Cancelled;
            impulse = Vector3.zero;
        }

        return FinishRelease( impulse, result );
    }

    /// <summary>
    /// 납치 완료 시도
    /// </summary>
    /// <param name="owner">완료를 요청한 포획자</param>
    /// <returns>납치 완료 성공 여부</returns>
    public bool TryCompleteKidnap ( CaptureController owner )
    {
        if ( _isEndingCapture == true || IsCapturedBy( owner ) == false )
            return false;

        if ( _mover.CanFollow == false )
        {
            FinishRelease( Vector3.zero, CaptureResult.Cancelled );
            return false;
        }

        //납치 확정과 종료 알림을 한 회차로 처리
        _isEndingCapture = true;
        try
        {
            NpcSafeStatusType prevStatus = Status;

            if ( _model.TryCompleteKidnap( ) == false )
                return false;

            //게임 이탈 확정 후 행동 중단
            SyncState( );

            _capturable.End( );

            if ( _animatorHandler != null )
                _animatorHandler.SetCaptured( false );

            if ( _captureEffect != null )
                _captureEffect.ResetEffect( );

            //이탈 상태로 전환한 뒤 비활성화하여 OnDisable의 포획 취소 방지
            _mover.Hide( );

            //포획 종료 이벤트 발행
            OnCaptureResult?.Invoke( this, _captureRound, CaptureResult.Kidnapped );
            OnCaptureEnded?.Invoke( CaptureResult.Kidnapped );
            NotifyStatusChanged( prevStatus );
            return true;
        }
        finally
        {
            _isEndingCapture = false;
        }
    }


    /// <summary>
    /// 포획자 확인
    /// </summary>
    /// <param name="owner">확인할 포획자</param>
    /// <returns>포획자 여부</returns>
    public bool IsCapturedBy ( CaptureController owner )
    {
        return Status == NpcSafeStatusType.Captured &&
            _capturable.IsOwner( owner );
    }

    #endregion

    #region ----- 상태 알림 -----

    /// <summary>
    /// 따라가기 중단에 따른 포획 취소
    /// </summary>
    void HandleCaptureInterrupted ()
    {
        FinishRelease( Vector3.zero, CaptureResult.Cancelled );
    }

    /// <summary>
    /// 포획 해제 요청 처리
    /// </summary>
    /// <param name="result">요청 결과</param>
    void HandleReleaseRequested ( CaptureResult result )
    {
        if ( Status != NpcSafeStatusType.Captured )
            return;

        //관계가 끊긴 취소는 포획자의 응답을 기다리지 않음
        if ( result == CaptureResult.Cancelled )
            FinishRelease( Vector3.zero, result );
        else
            OnReleaseRequested?.Invoke( result );
    }

    /// <summary>
    /// 상태 변경 로그와 알림 전달
    /// </summary>
    /// <param name="previous">이전 상태</param>
    void NotifyStatusChanged ( NpcSafeStatusType previous )
    {
        OnStatusChanged?.Invoke( previous, Status );
    }

    #endregion

    #region ----- 상태 검사 -----

    /// <summary>
    /// NPC 활성 상태와 포획 및 낙하 상태 확인
    /// </summary>
    /// <returns>포획 가능 여부</returns>
    bool CheckCanCapture ()
    {
#if UNITY_EDITOR
        if ( _isAiTest == true )
            return false;
#endif

        return _isReady == true && _isRunning == true &&
               isActiveAndEnabled == true && _isEndingCapture == false &&
               _capturable.isActiveAndEnabled == true &&
               _model.CanCapture == true &&
               _mover.CanBeginCapture == true;
    }

    /// <summary>
    /// NPC 포획 상태와 구조 가능 여부 확인
    /// </summary>
    /// <returns>구조 가능 여부</returns>
    bool CheckCanRescue ()
    {
#if UNITY_EDITOR
        if ( _isAiTest == true )
            return false;
#endif

        return _isReady == true && _isRunning == true && isActiveAndEnabled == true &&
               Status == NpcSafeStatusType.Captured &&
               _capturable.CanRescue == true;
    }

    #endregion


    /// <summary>
    /// 플레이 중 납치 완료 확인
    /// </summary>
    [ContextMenu( "Test Complete Kidnap" )]
    void TestCompleteKidnap ()
    {
        if ( Application.isPlaying == false )
            return;

        Debug.Log(
            $"[NPC 테스트] 납치 완료: {TryCompleteKidnap( _capturable.Owner )}", this );
    }

    /// <summary>
    /// 플레이 중 구조 입력 한 번 확인
    /// </summary>
    [ContextMenu( "Test Rescue Press" )]
    void TestRescuePress ()
    {
        if ( Application.isPlaying == false )
            return;

        TryRescue( );
    }


#if UNITY_EDITOR
    /// <summary>
    /// 초기화 전 AI 실행 제외 테스트 지정
    /// </summary>
    public void PrepareAiTest ()
    {
        _isAiTest = true;
    }
#endif

#if UNITY_EDITOR
    /// <summary>
    /// NPC 행동과 이동 상세 조회
    /// </summary>
    /// <param name="details">표시 항목</param>
    public void GetDebugDetails ( List<AnomalyDebugDetail> details )
    {
        if ( _normalBehaviour != null )
            _normalBehaviour.GetDebugDetails( details );
        if ( _mover != null )
            _mover.GetDebugDetails( details );
    }
#endif

}
