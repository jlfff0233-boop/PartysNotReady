using UnityEngine;

/// <summary>
/// 납치 적 - NPC와 플레이어 선정, 포획과 도주 연결
/// </summary>
public partial class EnemyKidnap : Enemy, IAnomalyEventOwner
{
    [Header( "----- 컴포넌트 -----" )]
    [SerializeField] CharacterMover _mover;		//캐릭터 이동
    [SerializeField] AttackAnimatorHandler _animatorHandler;		//이동과 공격 애니메이션
    [SerializeField] TargetSearcher _searchView;		//포획 가능 대상 탐색
    [SerializeField] CharacterAttacker _attacker;		//캐릭터 명중 검사
    [SerializeField] CaptureController _capturer;		//포획과 해제 중재
    [SerializeField] HideActivationController _hideView;		//잠복 뷰

    [Header( "----- 사운드 -----" )]
    [SerializeField] KidnapSound _enemySound;		//적 사운드
    [SerializeField] SoundPlayer _soundPlayer;		//적 전용 사운드 재생

    [Header( "----- 설정 -----" )]
    [SerializeField] EnemyKidnapData _data;		//납치 적 설정
    [SerializeField] float _attackAnimationStartTimeout = 2f;     //공격 상태 진입 제한시간

    RouteGraph _routeGraph;		//도주에 사용할 경로 그래프
    EnemySceneController _sceneController;		//공용 납치 완료 위치 제공

    bool _isPressureApproved;		//같은 게임의 최초 Pressure 승인 여부
    bool _isKidnapEscape;		//포획 성공 후 입구로 도주하는 시도

    IAttackTarget _target;		//선정한 NPC 또는 플레이어

    CaptureAttack _captureAttack;     //클립 시점에 따른 명중과 포획 장착

    KidnapModel _model;		//납치 허용과 표적 선정 판정
    AnomalyLinker _anomalyLinker;		//변칙 사건 연결
    HideController _hider;		//잠복과 출현 중재
    Approacher _chaser;		//선정한 대상에게 접근
    RouteNavigator _escapeNavigator;		//도주 경로 이동
    EnemyRoomActivity _roomActivity;		//잠복 후 활동 위치 준비

    /// <summary>
    /// 자신의 사건 결과 연결
    /// </summary>
    public AnomalyLinker EventLinker => _anomalyLinker;

    /// <summary>
    /// 자동 발생 대상 종류
    /// </summary>
    public override EnemySpawnType SpawnType => EnemySpawnType.Kidnap;

    /// <summary>
    /// 최초 상태
    /// </summary>
    protected override EnemyStateType InitialState => EnemyStateType.Hide;

    /// <summary>
    /// 포획 대상 보유 여부
    /// </summary>
    public bool HasCapturedTarget => _capturer.HasTarget;

    /// <summary>
    /// 납치 시도 가능 여부
    /// </summary>
    public bool CanKidnap => _model.CanKidnap;

    /// <summary>
    /// 보복 가능 여부
    /// </summary>
    public bool ShouldRetaliate => _model.HasReachedRescueLimit;

    /// <summary>
    /// 공격 애니메이션 재생 여부
    /// </summary>
    public bool IsAttackPlaying => _animatorHandler.IsAttackPlaying;

    /// <summary>
    /// 현재 Animator 상태의 공격 진입 여부
    /// </summary>
    public bool HasEnteredAttack => _animatorHandler.HasEnteredAttack;

    /// <summary>
    /// 이전 동작이 복귀하여 포획 공격을 시작할 수 있는지 여부
    /// </summary>
    public bool CanBeginCaptureAttack => _captureAttack.CanBegin;

    /// <summary>
    /// 포획 공격 동작 종료 여부
    /// </summary>
    public bool IsCaptureAttackFinished => _captureAttack.IsFinished;

    /// <summary>
    /// 이번 공격의 실제 포획 요청 여부
    /// </summary>
    public bool CaptureAttempted => _captureAttack.Attempted;
#if UNITY_EDITOR
    /// <summary>
    /// 포획 공격의 현재 진행 단계
    /// </summary>
    public string CaptureAttackPhase => _captureAttack.DebugPhase;
#endif

    #region ----- 초기화 -----

    /// <summary>
    /// 공통 모듈과 상태 생성
    /// </summary>
    protected override void Init ()
    {
        base.Init( );
        _mover.Init( _animatorHandler );

        //초기화된 이동 모듈과 전용 사운드 연결
        if ( _enemySound != null )
            _enemySound.Init( _soundPlayer );

        _captureAttack = new CaptureAttack(
            _animatorHandler, _attacker, _capturer,
            this, UpdateKidnapAnimation, _attackAnimationStartTimeout );

        _model = new KidnapModel(
            _data.ChaseTimeLimit, _data.RescueLimit, _data.NpcTargetChance,
            _data.RetaliationChance );

        _anomalyLinker = new AnomalyLinker( );
        _hider = new HideController( _hideView );

        _roomActivity = new EnemyRoomActivity(
            _mover, new EnemyRoomMover(
                _mover, _hideView, _data.SampleDist ), _hider );

        _chaser = new Approacher(
            _mover, _data.ChaseSpeed, _data.RotateSpeed,
            _data.AttackDist, _data.SampleDist,
            _data.RefreshInterval, _data.ChaseTimeLimit,
            _data.AttackHeight, _data.ArriveTolerance );

        //이동 모듈의 검사기를 공유하여 실제 에이전트 조건으로 경로 탐색
        RouteBuilder routeBuilder = new(
            _routeGraph, _mover.PathChecker,
            _data.SampleDist, _data.EscapeConnectDist );

        _escapeNavigator = new RouteNavigator(
            _mover, routeBuilder,
            _data.EscapeSpeed, _data.RotateSpeed, _data.EscapeArriveDist,
            _data.EscapeRetryInterval, _data.EscapeTimeLimit,
            _data.EscapeStuckTimeLimit, _data.EscapeArriveTolerance );

        _states [ ( int ) EnemyStateType.Hide ] =
            new KidnapHideState( this, _data.SearchInterval );

        _states [ ( int ) EnemyStateType.Chase ] =
            new KidnapChaseState( this );

        _states [ ( int ) EnemyStateType.Attack ] =
            new KidnapAttackState(
                this, _data.AttackDelay );

        _states [ ( int ) EnemyStateType.Escape ] =
            new KidnapEscapeState( this );
    }

    /// <summary>
    /// 씬 배치 참조 연결과 적 초기화
    /// </summary>
    /// <param name="settings">배치 설정</param>
    /// <param name="routeGraph">공용 경로</param>
    /// <param name="sceneController">납치 완료 위치 제공</param>
    /// <returns>초기화 성공 여부</returns>
    public bool Init (
        KidnapSceneSettings settings, RouteGraph routeGraph,
        EnemySceneController sceneController )
    {
        if ( routeGraph == null )
        {
            Debug.LogError( "[적 초기화 실패] " +
                $"{name}: 경로 그래프를 연결해 주세요.", this );
            return false;
        }

        _sceneController = sceneController;
        _routeGraph = routeGraph;

        return base.Init( settings.StartRoom );
    }

    private void LateUpdate ()
    {
        //Animator가 반영한 손 자세에서 판정/장착/종료를 함께 처리
        if ( CanRun == true )
            _captureAttack.LateUpdateAttack( Time.deltaTime );

        //잠복 중 이동은 발소리로 노출하지 않음
        if ( _enemySound != null )
        {
            _enemySound.UpdateMovement(
                _mover, Time.deltaTime,
                CanRun == true &&
                _currentState != null &&
                _hideView.IsHidden == false );
        }
    }


    /// <summary>
    /// 이벤트 구독과 활성 모듈 준비
    /// </summary>
    protected override void Activate ()
    {
        //이전 재생과 이동 거리 기록 정리
        if ( _enemySound != null )
            _enemySound.ResetSounds( );

        _roomActivity.Reset( );

        _anomalyLinker.OnResult += HandleAnomalyResult;
        _capturer.OnResult += HandleCaptureResult;
        UpdateKidnapAnimation( );
    }


    /// <summary>
    /// 이벤트 구독 해제와 활동 정리
    /// </summary>
    protected override void Deactivate ()
    {
        _anomalyLinker.Stop( );
        _anomalyLinker.OnResult -= HandleAnomalyResult;

        _captureAttack.End( );
        //취소 해제 결과가 행동 처리로 돌아오지 않도록 구독 해제
        _capturer.OnResult -= HandleCaptureResult;

        _attacker.SetTarget( null );
        _capturer.TryRelease( CaptureResult.Cancelled );
        UpdateKidnapAnimation( );
        _model.EndPlayerAttempt( );
        _model.EndActivity( );
        _roomActivity.Reset( );

        //재활성화 시 이전 도주 경로와 재계산 대기를 이어가지 않도록 취소
        StopEscape( );
        //종료 중에는 숨김과 이동 정지를 유지한다. 이동 복구는 다음 등장 위치 준비에서 처리한다.
        _hideView.Hide( );

        _target = null;

        //이전 재생과 이동 거리 기록 정리
        if ( _enemySound != null )
            _enemySound.ResetSounds( );
    }

    #endregion


    #region ----- 핸들 -----

    /// <summary>
    /// 사건 결과에 따른 납치 허용과 재대기
    /// </summary>
    /// <param name="occurrence">결과가 발생한 사건</param>
    /// <param name="status">사건 결과</param>
    void HandleAnomalyResult (
        AnomalyEvent occurrence, AnomalyStatus status )
    {
        if ( isActiveAndEnabled == false ||
             CurrentState == null ||
             CurrentState.StateType != EnemyStateType.Hide )
            return;

        _model.ApplyAnomalyResult( status );


        if ( CanKidnap == true )
        {
            //실패 후에는 새 사건 없이 표적 선정으로 진행
            _anomalyLinker.Stop( );
            return;
        }

        //해결/취소 시 출현하지 않고 다음 발생까지 대기
        _hider.Begin( _data.HideDuration );
    }

    /// <summary>
    /// 납치 운반 표시와 포획 사운드 갱신
    /// </summary>
    void UpdateKidnapAnimation ()
    {
        bool isKidnapping = isActiveAndEnabled == true &&
            _capturer.HasTarget == true;		//실제 대상 운반 여부

        //포획 성공과 해제 상태를 사운드 모듈에 전달
        if ( _enemySound != null )
            _enemySound.SetKidnap( isKidnapping );
    }

    /// <summary>
    /// 포획 결과에 따른 저지 기록과 시도 종료
    /// </summary>
    /// <param name="target">포획 대상</param>
    /// <param name="result">결과</param>
    void HandleCaptureResult (
        ICaptureTarget target, CaptureResult result )
    {
        UpdateKidnapAnimation( );

#if UNITY_EDITOR
        PlayDebugHistory.Record( "포획 결과", this,
            $"{EnemyDebugUtility.Name( target as Component )} / {result}" );
#endif
        //NPC 구조만 집계
        if ( target is Npc && result == CaptureResult.Rescued )
            _model.RecordRescue( );

        //탈출, 시간 초과, 납치 완료, 취소 모두 플레이어 시도 종료
        if ( target is Player )
        {
            //납치에 실패한 경우 도주 후 반드시 다른 방으로 이동
            if ( result != CaptureResult.Kidnapped )
                _roomActivity.RequestMove( );

            _model.EndPlayerAttempt( );
        }
    }

    #endregion

    #region ----- 방 활동 -----

    /// <summary>
    /// 잠복 위치를 정하고 방 활동 시작
    /// </summary>
    /// <returns>활동 시작 여부</returns>
    bool BeginRoomActivity ()
    {
        if ( _roomActivity.TryPrepare(
            _model.CurrentRoom, _data.HideDuration, out Room room ) == false )
            return false;

        _model.BeginActivity( room );
        _anomalyLinker.Begin( );

        return true;
    }

    /// <summary>
    /// 잠복 중 다른 방 이동 요청
    /// </summary>
    void RequestRoomMove ()
    {
        _anomalyLinker.Stop( );
        _model.BeginHide( );
        _target = null;
        _roomActivity.RequestMove( );

        //Hide 재진입에 따른 일시적인 표시 방지
        _roomActivity.BeginWait( _data.HideDuration );
    }

    /// <summary>
    /// 플레이어 납치/방 이동 선택
    /// </summary>
    void SelectRoomAction ()
    {
        if ( _model.TrySelectAction(
            true, Random.value, out EnemyRoomActionType action ) == false )
            return;

        if ( action == EnemyRoomActionType.MoveRoom )
        {
            RequestRoomMove( );
            return;
        }

        //플레이어를 발견하면 접근과 공격을 거쳐 포획
        _model.BeginPlayerAttempt( );
        _target = null;

        if ( _searchView.TryFindTarget(
            _data.SearchDist, out IAttackTarget target, IsPlayerCandidate ) == true )
        {
            _target = target;
            ChangeState( EnemyStateType.Chase );
            return;
        }

        //플레이어가 없으면 추가 추첨 없이 도주 후 방 이동
        _roomActivity.RequestMove( );
        ChangeState( EnemyStateType.Escape );
    }

    /// <summary>
    /// 방 상황에 따른 납치/보복/이동 선택
    /// </summary>
    public void UpdateActivity ()
    {
        if ( _isPressureApproved == false || CanRunActivity( ) == false || _anomalyLinker.HasActiveEvent == true )
            return;

        //진행 중인 사건이 끝난 뒤 저지 기준과 대상 유무 확인
        if ( ShouldRetaliate == true )
        {
            SelectRoomAction( );
            return;
        }

        if ( _searchView.TryFindTarget(
            _data.SearchDist, out _, IsRoomCaptureCandidate ) == false )
        {
            SelectRoomAction( );
            return;
        }

        if ( CanKidnap == false )
            return;

        if ( TryFindTarget( ) == true )
            ChangeState( EnemyStateType.Chase );
        else if ( _roomActivity.IsMoveRequested == true )
            ChangeState( EnemyStateType.Escape );
    }


    /// <summary>
    /// 실제 사건 시작 반영
    /// </summary>
    /// <param name="room">사건이 발생한 방</param>
    public void HandleEventStarted ( Room room )
    {
        _isPressureApproved = true;
    }


    /// <summary>
    /// 잠복 중 방 활동 가능 여부 확인
    /// </summary>
    /// <returns>활동 가능 여부</returns>
    bool CanRunActivity ()
    {
        if ( isActiveAndEnabled == false ||
             CurrentState == null ||
             CurrentState.StateType != EnemyStateType.Hide ||
             _roomActivity.CanRun == false )
            return false;

        Room room = _model.CurrentRoom;
        return room != null && room.isActiveAndEnabled == true;
    }

    /// <summary>
    /// 사건 시작 가능 여부 확인
    /// </summary>
    /// <param name="room">요청한 방</param>
    /// <returns>시작 가능 여부</returns>
    public bool CanStart ( Room room )
    {
        return CheckStart( room, out _ );
    }

    /// <summary>
    /// 사건 발생 조건과 거절 사유 확인
    /// </summary>
    /// <param name="room">요청 방</param>
    /// <param name="reason">거절 사유</param>
    /// <returns>시작 가능 여부</returns>
    public bool CheckStart ( Room room, out string reason )
    {
        reason = string.Empty;

        if ( CanRun == false )
        {
            reason = "플레이 시작 전이거나 활동이 중단됐습니다.";
        }
        else if ( _isPressureApproved == false && IsStartRoom( room ) == false )
        {
            reason = "최초 승인 방과 다릅니다.";
        }
        else if ( CanRunActivity( ) == false )
        {
            reason = "잠복 대기 또는 방 활동 준비 중입니다.";
        }
        else if ( _model.CurrentRoom != room )
        {
            reason = "현재 활동 방과 다릅니다.";
        }
        else if ( CanKidnap == true )
        {
            reason = "사건 실패 후 납치 진행을 기다립니다.";
        }
        else if ( ShouldRetaliate == true )
        {
            reason = "구조 저지 기준에 도달했습니다.";
        }
        else if ( _anomalyLinker.CanPrepare == false )
        {
            reason = "사건 연결 중이거나 활동이 중단됐습니다.";
        }
        return reason.Length == 0;
    }

    #endregion

    #region ----- 표적 선정 -----

    /// <summary>
    /// 잠복 시작
    /// </summary>
    public void Hide ()
    {
        //재잠복 시 이전 연결과 납치 허용 상태 초기화
        _anomalyLinker.Stop( );
        _model.BeginHide( );

        _target = null;
        _attacker.SetTarget( null );
        _mover.Stop( );
        _roomActivity.BeginWait( _data.HideDuration );
    }

    /// <summary>
    /// 잠복 대기 갱신
    /// </summary>
    /// <param name="deltaTime">경과시간</param>
    /// <returns>탐색 시작 가능 여부</returns>
    public bool UpdateHide ( float deltaTime )
    {
        _hider.Update( deltaTime );
        if ( _hider.IsReady == false )
            return false;

        return _roomActivity.IsReady == true || BeginRoomActivity( );
    }

    /// <summary>
    /// 변칙 사건 연결 종료 및 나타나기
    /// </summary>
    public void Show ()
    {
        _anomalyLinker.Stop( );
        _hider.Show( );
    }


    /// <summary>
    /// 포획 가능 대상 선정
    /// </summary>
    /// <returns>선정 성공 여부</returns>
    public bool TryFindTarget ()
    {
        _target = null;

        //보복은 플레이어로 고정하고 일반 납치는 확률로 종류 선정
        if ( _model.SelectedAction == EnemyRoomActionType.Retaliate )
            _model.BeginPlayerAttempt( );
        else
            _model.SelectTargetType( Random.value );

        if ( _searchView.TryFindTarget(
            _data.SearchDist, out IAttackTarget target, IsCaptureCandidate ) == false )
        {
            //플레이어 탐색 실패 시 도주 후 다른 방으로 이동
            if ( _model.IsPlayerAttempt == true )
                _roomActivity.RequestMove( );

            _model.EndPlayerAttempt( );
            return false;
        }

        _target = target;

        return true;
    }

    /// <summary>
    /// 후보가 현재 활동 중인 방에 있는지 확인
    /// </summary>
    /// <param name="candidate">탐색 후보</param>
    /// <returns>현재 방 소속 여부</returns>
    bool IsInCurrentRoom ( IAttackTarget candidate )
    {
        if ( candidate == null || _model.CurrentRoom == null )
            return false;

        Transform target = candidate.TargetTransform;
        if ( target == null )
            return false;

        return RoomRegister.TryGetRoom( target.position, out Room room ) &&
            room == _model.CurrentRoom;
    }

    /// <summary>
    /// 현재 방의 포획 가능 대상 확인
    /// </summary>
    /// <param name="candidate">탐색 후보</param>
    /// <returns>포획 가능 여부</returns>
    bool IsRoomCaptureCandidate ( IAttackTarget candidate )
    {
        bool canCapture;

        if ( candidate is Player player )
            canCapture = player != null && player.CanCapture == true;
        else if ( candidate is Npc npc )
            canCapture = npc != null && npc.CanCapture == true;
        else
            return false;

        return canCapture == true && IsInCurrentRoom( candidate );
    }

    /// <summary>
    /// 보복 대상으로 포획 가능한 플레이어 확인
    /// </summary>
    /// <param name="candidate">탐색 후보</param>
    /// <returns>포획 가능 여부</returns>
    bool IsPlayerCandidate ( IAttackTarget candidate )
    {
        return candidate is Player && IsRoomCaptureCandidate( candidate );
    }

    /// <summary>
    /// 포획 가능 후보 확인
    /// </summary>
    /// <param name="candidate">공격 대상 후보</param>
    /// <returns>이번 탐색의 후보 여부</returns>
    bool IsCaptureCandidate ( IAttackTarget candidate )
    {
        if ( IsRoomCaptureCandidate( candidate ) == false )
            return false;

        //이번에 선정한 종류만 표적으로 허용
        return _model.IsPlayerTarget == true
            ? candidate is Player : candidate is Npc;
    }

    #endregion

    #region ----- 추격/포획 -----

    /// <summary>
    /// 새로운 접근 시도 시작
    /// </summary>
    public void BeginChase ()
    {
        _model.Begin( );
        _chaser.Begin( _target.TargetTransform );
#if UNITY_EDITOR
        _debugApproachElapsed = 0f;
        RecordDebugApproach( "시작" );
#endif
    }

    /// <summary>
    /// 표적 재탐색과 접근 갱신
    /// </summary>
    /// <param name="deltaTime">경과시간</param>
    /// <returns>접근 결과</returns>
    public ApproachStatus UpdateChase ( float deltaTime )
    {
        //대상이 바뀌어도 접근 시간은 계속 누적
        _model.Tick( deltaTime );

        if ( _model.IsChaseExpired == true )
        {
#if UNITY_EDITOR
            RecordDebugApproach( "상위 접근 제한시간 초과" );
#endif
            return ApproachStatus.Failed;
        }

        if ( IsTargetValid( ) == false )
        {
            _chaser.Stop( );

            //플레이어 시도 실패를 다른 대상 재선정으로 넘기지 않음
            if ( _model.IsPlayerAttempt == true )
            {
#if UNITY_EDITOR
                RecordDebugApproach( "플레이어 목표 무효 / 접근 종료" );
#endif
                return ApproachStatus.Failed;
            }

            if ( TryFindTarget( ) == false )
            {
#if UNITY_EDITOR
                RecordDebugApproach( "재탐색할 포획 대상 없음" );
#endif
                return ApproachStatus.Failed;
            }

            //표적 경로만 재시작하고 전체 제한시간은 유지
            _chaser.Begin( _target.TargetTransform );
#if UNITY_EDITOR
            RecordDebugApproach( "표적 재선정" );
#endif
        }

        ApproachStatus status = _chaser.UpdateMove( deltaTime );
#if UNITY_EDITOR
        UpdateDebugApproach( deltaTime, status );
#endif
        return status;
    }

    /// <summary>
    /// 접근 종료
    /// </summary>
    public void StopChase ()
    {
        _chaser.Stop( );
    }

    /// <summary>
    /// 공격 준비
    /// </summary>
    public void PrepareAttack ()
    {
        //준비 단계만 기록하며 실제 범위 검사는 AttackHit 요청에서 실행
#if UNITY_EDITOR
        RecordDebugAttack( "공격 준비" );
#endif
    }

    /// <summary>
    /// 목표 방향으로 회전
    /// </summary>
    public void LookAtTarget ()
    {
        if ( IsTargetValid( ) == false )
            return;

        Vector3 direction = _target.TargetTransform.position - _mover.Position;
        direction.y = 0f;

        if ( direction.sqrMagnitude <= 0.0001f )
            return;

        _mover.Rotate(
            Quaternion.LookRotation( direction ), _data.RotateSpeed );
    }

    /// <summary>
    /// 준비가 끝난 목표의 클립 이벤트 수신과 공격 재생 시작
    /// </summary>
    public void BeginAttack ()
    {
        _captureAttack.Begin( _target );
    }

    /// <summary>
    /// 공격 종료와 대기 요청 정리
    /// </summary>
    public void EndAttack ()
    {
        //실제 포획은 유지하여 다음 도주 상태에서 운반
        _captureAttack.End( );
    }

    /// <summary>
    /// 현재 표적의 포획 가능 여부 확인
    /// </summary>
    /// <returns>표적 유효 여부</returns>
    public bool IsTargetValid ()
    {
        return IsCaptureCandidate( _target );
    }

    #endregion

    #region ----- 도주/종료 -----

    /// <summary>
    /// 도주 이동과 재계산 대기 취소
    /// </summary>
    public void StopEscape ()
    {
#if UNITY_EDITOR
        if ( _escapeNavigator.Status != RouteStatusType.Idle &&
             _escapeNavigator.Status != RouteStatusType.Cancelled )
            PlayDebugHistory.Record( "납치 도주 종료", this,
                $"{_escapeNavigator.GetDebugDetails( )} / 실제 포획: {_capturer.HasTarget}" );
#endif
        _escapeNavigator.Cancel( );
    }

    /// <summary>
    /// 포획 성공 도주의 자동 우회 노드와 공용 입구 연결
    /// </summary>
    /// <returns>입구 도주 시작 결과</returns>
    RouteStatusType BeginKidnapEscape ()
    {
        if ( _sceneController.TryGetEntrance(
            out Vector3 entrance, out string reason ) == false )
        {
            Debug.LogWarning( $"[납치 적 도주 실패] {name} / {reason}", this );
            return RouteStatusType.Failed;
        }
        _escapeNavigator.BeginEntranceDetour( entrance );
        return _escapeNavigator.Status;
    }

    /// <summary>
    /// 표적을 정리하고 도주 경로 이동 시작
    /// </summary>
    /// <returns>도주 이동 상태</returns>
    public RouteStatusType BeginEscape ()
    {
        _anomalyLinker.Stop( );

        //플레이어 접근 또는 공격이 실패했다면 시도 종료
        //포획 성공 상태라면 실제 포획 결과가 나올 때 종료
        if ( _capturer.HasTarget == false )
        {
            if ( _model.IsPlayerAttempt == true )
                _roomActivity.RequestMove( );

            _model.EndPlayerAttempt( );
        }

        _target = null;
        StopEscape( );
        _isKidnapEscape = _capturer.HasTarget;

        RouteStatusType status;
        if ( _isKidnapEscape == true )
            status = BeginKidnapEscape( );
        else
        {
            //빈손 도주는 두 노드로 종료하고 납치 성공으로 처리하지 않음
            _escapeNavigator.BeginConnectedNodes( );
            status = _escapeNavigator.Status;
        }

#if UNITY_EDITOR
        _debugEscapeElapsed = 0f;
        PlayDebugHistory.Record( "납치 도주 시작", this,
            $"입구 도주: {_isKidnapEscape} / 출발 방: {_model.CurrentRoom} / 시작 결과: {status} / " +
            $"{_escapeNavigator.GetDebugDetails( )} / 실제 포획: {_capturer.HasTarget}" );
#endif

        //시작 단계의 설정 오류는 한 번만 기록
        if ( _escapeNavigator.Status == RouteStatusType.Failed )
        {
            Debug.LogWarning(
                $"[납치 적 도주 실패] {name} / {_escapeNavigator.FailureReason}", this );
        }

        return status;
    }

    /// <summary>
    /// 도주 경로 이동 갱신
    /// </summary>
    /// <param name="deltaTime">경과시간</param>
    /// <returns>도주 이동 상태</returns>
    public RouteStatusType UpdateEscape ( float deltaTime )
    {
        if ( _isKidnapEscape == true &&
             _sceneController.TryGetEntrance( out _, out string reason ) == false )
        {
            StopEscape( );
            Debug.LogWarning( $"[납치 적 도주 실패] {name} / {reason}", this );
            return RouteStatusType.Failed;
        }

        RouteStatusType previous = _escapeNavigator.Status;
        RouteStatusType current = _escapeNavigator.UpdateMove( deltaTime );
#if UNITY_EDITOR
        UpdateDebugEscape( deltaTime, current );
#endif

        //이동 실패로 바뀐 순간만 기록하여 같은 경고 반복 방지
        if ( previous == RouteStatusType.Moving &&
             current == RouteStatusType.Failed )
        {
            Debug.LogWarning(
                $"[납치 적 도주 실패] {name} / {_escapeNavigator.FailureReason}", this );
        }

        return current;
    }

    /// <summary>
    /// 포획 대상의 입구 도착 완료 처리
    /// </summary>
    /// <returns>대상별 납치 완료 성공 여부</returns>
    bool CompleteKidnapTarget ()
    {
        if ( _capturer.Target is Npc npc && npc != null )
            return npc.TryCompleteKidnap( _capturer );
        if ( _capturer.Target is Player player && player != null )
            return player.TryCompleteKidnap( _capturer );
        return false;
    }

    /// <summary>
    /// 도주 완료와 방 이동 결정
    /// </summary>
    /// <returns>도주 완료 처리 여부</returns>
    public bool CompleteEscape ()
    {
        if ( isActiveAndEnabled == false ||
             CurrentState == null ||
             CurrentState.StateType != EnemyStateType.Escape ||
             HasArrivedEscape( ) == false )
            return false;

        IEnemyState state = CurrentState;
        bool completed;

        //도주 중 대상이 탈출했다면 빈손 도주 완료
        if ( _capturer.HasTarget == false )
            completed = true;
        //필수 경유 노드 이후 입구까지 도착한 포획 성공 도주만 납치 완료
        else if ( _isKidnapEscape == true && _escapeNavigator.UsesRequiredNodes == true )
            completed = CompleteKidnapTarget( );
        else
            completed = false;

        //완료 알림에서 비활성화되거나 상태가 바뀌면 후속 처리 중단
        if ( completed == false || isActiveAndEnabled == false ||
             CurrentState != state )
            return false;

        //실패로 확정된 방 이동은 유지하고 나머지만 확률 적용
        if ( _roomActivity.IsMoveRequested == false )
        {
            float chance = _data.MoveRoomChance;
            if ( chance >= 1f || ( chance > 0f && Random.value < chance ) )
                _roomActivity.RequestMove( );
        }

        if ( _roomActivity.IsMoveRequested == false )
            _roomActivity.RequestReturn( );
        return true;
    }

    /// <summary>
    /// 도주 실패 시 포획 해제와 잠복 위치 결정
    /// </summary>
    /// <returns>잠복 전환 가능 여부</returns>
    public bool EndFailedEscape ()
    {
        if ( isActiveAndEnabled == false ||
             CurrentState == null ||
             CurrentState.StateType != EnemyStateType.Escape )
            return false;

        IEnemyState state = CurrentState;

        //출구 미도착은 납치 성공이나 NPC 구조로 집계하지 않음
        //해제에 실패하면 대상을 숨기지 않고 다음 갱신에서 재시도
        if ( _capturer.HasTarget == true &&
             _capturer.TryRelease( CaptureResult.Cancelled ) == false )
            return false;

        //해제 알림에서 상태가 바뀌었거나 새 포획이 생겼으면 중단
        if ( isActiveAndEnabled == false ||
             CurrentState != state ||
             _capturer.HasTarget == true )
            return false;

        _model.EndPlayerAttempt( );

        //플레이어 실패로 정해진 방 이동은 유지
        //그 외에는 현재 활동 방의 잠복 지점으로 복귀
        if ( _roomActivity.IsMoveRequested == false )
            _roomActivity.RequestReturn( );
        return true;
    }

    /// <summary>
    /// 도주 경로 도착 확인
    /// </summary>
    /// <returns>출구 도착 여부</returns>
    public bool HasArrivedEscape ()
    {
        return _escapeNavigator.Status == RouteStatusType.Arrived;
    }

    #endregion

}
