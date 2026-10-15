using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// 공격 적 - 변칙 사건 해결 누적, 추적과 공격 중재
/// </summary>
public partial class EnemyHarm : Enemy, IAnomalyEventOwner
{
    [Header ( "----- 설정 데이터 -----" )]
    [SerializeField] EnemyHarmData _data;		//공격 적 설정

    [Header ( "----- 컴포넌트 -----" )]
    [SerializeField] CharacterMover _mover;		//캐릭터 이동
    [SerializeField] AttackAnimatorHandler _animatorHandler;		//이동과 공격 애니메이션
    [SerializeField] CharacterAttacker _attacker;       //캐릭터 명중 검사
    [SerializeField] Transform _holdPoint;      //아이템 보유 지점
    [SerializeField] TargetSearcher _searchView;        //가까운 플레이어 탐색
    [SerializeField] HideActivationController _hideView;		//잠복 뷰

    [Header ( "----- 사운드 -----" )]
    [SerializeField] HarmSound _enemySound;		//적 사운드
    [SerializeField] SoundPlayer _soundPlayer;		//적 전용 사운드 재생

    Transform _escapePoint;		//도주 후 숨을 위치
    bool _hasEscapePoint;		//초기화 시 개별 도주 지점 지정 여부
    RouteGraph _routeGraph;		//경로 그래프
    EnemySceneController _sceneController;		//현재 방의 도주 위치 제공
    IngredientStockManager _stock;		//공급원 재고 조회와 획득

    bool _collectRequested;		//수집 요청
    bool _isPressureApproved;		//압력 승인 여부
    bool _hasHit;		//이번 공격 명중 여부

    Collider [ ] _bodyColliders;		//아이템과 충돌을 제외할 몸체
    Room _collectRoom;		//사건을 요청한 방
    IAttackTarget _target;		//이번 공격 대상

    HarmRetaliationModel _model;		//저지 횟수와 공격 전환 판정
    AnomalyLinker _anomalyLinker;		//자신의 사건 결과 연결
    MultiItemHolder _holder;		//확보한 아이템 보유
    ItemCollector _collector;		//아이템 선택과 수집
    EnemyRoomActivity _roomActivity;		//잠복 후 활동 위치 준비
    RouteNavigator _escapeNavigator;		//도주 경로 이동
    HideController _hider;		//잠복 시간과 표시 연결
    Approacher _chaser;		//플레이어 접근

    /// <summary>
    /// 자신의 사건 결과 연결
    /// </summary>
    public AnomalyLinker EventLinker => _anomalyLinker;

    /// <summary>
    /// 자동 발생 대상 종류
    /// </summary>
    public override EnemySpawnType SpawnType => EnemySpawnType.Harm;

    /// <summary>
    /// 최초 진입 상태
    /// </summary>
    protected override EnemyStateType InitialState => EnemyStateType.Hide;

    /// <summary>
    /// 수집 전환 요청 여부
    /// </summary>
    public bool HasCollectionRequest => _collectRequested;

    /// <summary>
    /// 공격 전환 가능 여부
    /// </summary>
    public bool CanAttack => _model.CanAttack;

    /// <summary>
    /// 공격 명중 이벤트(명중 대상)
    /// </summary>
    public event Action<IAttackTarget> OnHit;

    #region ----- 초기화 -----

    /// <summary>
    /// 모델과 공통 모듈 생성 및 상태 등록
    /// </summary>
    protected override void Init ()
    {
        base.Init( );

        _mover.Init( _animatorHandler );

        //초기화된 이동 모듈과 전용 사운드 연결
        if ( _enemySound != null )
            _enemySound.Init( _soundPlayer );

        //아이템을 보유하기 전에 몸체 콜라이더만 저장
        _bodyColliders = GetComponentsInChildren<Collider>( true );

        _model = new HarmRetaliationModel(
            _data.RequiredCount, _data.RetaliationChance );


        _anomalyLinker = new AnomalyLinker( );

        _hider = new HideController( _hideView );
        _roomActivity = new EnemyRoomActivity(
            _mover, new EnemyRoomMover( _mover, _hideView, _data.SampleDist ),
            _hider );

        _chaser = new Approacher(
            _mover, _data.ChaseSpeed, _data.RotateSpeed,
            _data.AttackDist, _data.SampleDist,
            _data.RefreshInterval, _data.ChaseTimeLimit,
            _data.AttackHeight, _data.ArriveTolerance );

        //실제 이동 모듈의 검사기를 사용하여 에이전트 조건 일치
        RouteBuilder routeBuilder = new(
            _routeGraph, _mover.PathChecker,
            _data.SampleDist, _data.EscapeConnectDist );

        _escapeNavigator = new RouteNavigator(
            _mover, routeBuilder,
            _data.EscapeSpeed, _data.RotateSpeed, _data.EscapeArriveDist,
            _data.EscapeRetryInterval, _data.EscapeTimeLimit,
            _data.EscapeStuckTimeLimit, _data.EscapeArriveTolerance );

        _states [ ( int ) EnemyStateType.Hide ] =
            new HarmHideState( this, _data.EventInterval );

        _states [ ( int ) EnemyStateType.Chase ] =
            new HarmChaseState( this );

        _states [ ( int ) EnemyStateType.Attack ] =
            new HarmAttackState( this, _data.AttackDelay, _data.AttackDuration );

        _states [ ( int ) EnemyStateType.Escape ] =
            new HarmEscapeState( this );

        _states [ ( int ) EnemyStateType.Collect ] =
            new HarmCollectState( this );
    }

    /// <summary>
    /// 씬 배치 참조 연결과 적 초기화
    /// </summary>
    /// <param name="settings">배치 설정</param>
    /// <param name="routeGraph">공용 경로</param>
    /// <param name="sceneController">도주 위치 제공</param>
    /// <param name="stock">공급원 재고 중재자</param>
    /// <returns>초기화 성공 여부</returns>
    public bool Init (
        HarmSceneSettings settings, RouteGraph routeGraph,
        EnemySceneController sceneController, IngredientStockManager stock )
    {
        if ( routeGraph == null )
        {
            Debug.LogError( "[적 초기화 실패] " +
                $"{name}: 경로 그래프를 연결해 주세요.", this );
            return false;
        }

        _escapePoint = settings.EscapePoint;
        //미지정과 실행 중 지정 지점의 파괴를 구분
        _hasEscapePoint = _escapePoint != null;
        _sceneController = sceneController;
        _stock = stock;
        _routeGraph = routeGraph;
        return base.Init( settings.StartRoom );
    }

    private void LateUpdate ()
    {
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

        //비활성화 때 종료한 보유 모듈과 수집 정보를 새로 생성
        _holder = new MultiItemHolder( _data.MaxCapacity );
        _collector = new ItemCollector(
            _mover, _data, _holder, _holdPoint, _bodyColliders, _stock );

        _collectRequested = false;
        _collectRoom = null;

        _roomActivity.Reset( );

        _anomalyLinker.OnResult += HandleAnomalyResult;
        _attacker.OnCharacterHit += HandleCharacterHit;
    }


    /// <summary>
    /// 이벤트 구독 해제와 활동 정리
    /// </summary>
    protected override void Deactivate ()
    {
        //비활성화 시 이전 경로와 재계산 대기 취소
        _escapeNavigator?.Cancel( );

        _anomalyLinker.Stop( );
        _anomalyLinker.OnResult -= HandleAnomalyResult;
        _attacker.OnCharacterHit -= HandleCharacterHit;

        _collectRequested = false;
        _collectRoom = null;

        _roomActivity.Reset( );

        //최초 승인과 방별 저지 기록은 유지
        _model.EndActivity( );

        //확보 콜백이 이전 수집 결과를 적용하지 않도록 먼저 종료
        _collector?.Stop( );
        _collector = null;

        //확보 중 비활성화된 경우에도 임시 보유와 기존 보유를 함께 정리
        _holder?.Dispose( );
        _holder = null;

        _attacker.SetTarget( null );
        //활동 종료 시 이동을 다시 켜지 않는다. 재등장 준비에서 유효한 위치를 확보한 뒤 복구한다.
        _hideView.Hide( );

        //비활성화된 에이전트에 이동 명령을 보내지 않고 참조 정리
        _target = null;

        //이전 재생과 이동 거리 기록 정리
        if ( _enemySound != null )
            _enemySound.ResetSounds( );
    }

    #endregion


    #region ----- 핸들 -----

    /// <summary>
    /// 사건 결과에 따른 수집 요청과 저지 기록
    /// </summary>
    /// <param name="occurrence">결과가 발생한 사건</param>
    /// <param name="status">사건 결과 상태</param>
    void HandleAnomalyResult (
        AnomalyEvent occurrence, AnomalyStatus status )
    {
        //자신이 발생시킨 사건의 잠복 중 결과만 행동에 반영
        if ( isActiveAndEnabled == false ||
             _currentState == null ||
             _currentState.StateType != EnemyStateType.Hide )
            return;

        //플레이어 기준 실패는 공격 적의 변칙 성공
        if ( status == AnomalyStatus.Failed )
        {
            //결과 콜백 안에서 상태를 바꾸지 않고 잠복 상태에서 전환
            _collectRequested = true;
            _anomalyLinker.Stop( );

            return;
        }

        if ( _model.TryRegisterResult( occurrence, status ) == false )
            return;


        if ( CanAttack == false )
            return;

        //공격 전환 기준을 채우면 추가 사건 요청 중단
        _anomalyLinker.Stop( );
    }

    /// <summary>
    /// 실제 명중 대상에 스턴 적용과 결과 전달
    /// </summary>
    /// <param name="target">명중한 캐릭터</param>
    void HandleCharacterHit ( IAttackTarget target )
    {
        _hasHit = true;

        //한 번 명중하면 남은 공격 지점 검사 중단
        _attacker.EndAttack( );

        if ( target is IStunTarget stunTarget )
        {
            stunTarget.TakeStun( _data.StunDuration );
        }

        //공격 명중 이벤트 발행
        OnHit?.Invoke( target );
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

        _model.Begin( room );
        _anomalyLinker.Begin( );

        return true;
    }

    /// <summary>
    /// 잠복 상태에서 다른 방 이동 예약
    /// </summary>
    void RequestRoomMove ()
    {
        _anomalyLinker.Stop( );
        _collectRequested = false;
        _collectRoom = null;
        _target = null;
        _roomActivity.RequestMove( );

        //Hide 재진입에 따른 일시적인 표시 방지
        _roomActivity.BeginWait( _data.HideDuration );
    }

    /// <summary>
    /// 보복 또는 방 이동 선택
    /// </summary>
    void SelectRetaliation ()
    {
        //저지 기준에 도달한 경우에만
        //플레이어 탐색과 보복 추첨
        bool hasTarget = TryFindTarget( );

        if ( _model.TrySelectAction(
            hasTarget, Random.value,
            out EnemyRoomActionType action ) == false )
            return;


        _anomalyLinker.Stop( );

        if ( action == EnemyRoomActionType.Retaliate )
        {
            ChangeState( EnemyStateType.Chase );
            return;
        }

        //플레이어가 없거나
        //이동으로 결정됐다면 잠복 중 방 이동
        RequestRoomMove( );
    }

    /// <summary>
    /// 현재 방의 다음 행동 결정
    /// </summary>
    public void UpdateActivity ()
    {
        if ( _isPressureApproved == false || CanRunActivity( ) == false ||
             _collectRequested == true ||
             _anomalyLinker.HasActiveEvent == true )
            return;

        //물건 유무보다 먼저 새 저지 기준을 처리
        if ( CanAttack == true )
        {
            SelectRetaliation( );
            return;
        }

        //저지 기준에 도달하지 않았고
        //훔칠 물건도 없으면 방 이동
        if ( _collector.HasTarget( _model.CurrentRoom ) == false )
        {
            RequestRoomMove( );
            return;
        }
    }


    /// <summary>
    /// 실제 사건 시작 반영
    /// </summary>
    /// <param name="room">사건이 발생한 방</param>
    public void HandleEventStarted ( Room room )
    {
        _collectRoom = room;
        _isPressureApproved = true;
    }


    /// <summary>
    /// 잠복 중 방 활동 가능 여부 확인
    /// </summary>
    /// <returns>활동 가능 여부</returns>
    bool CanRunActivity ()
    {
        if ( isActiveAndEnabled == false ||
             _currentState == null ||
             _currentState.StateType != EnemyStateType.Hide )
            return false;

        if ( _roomActivity.CanRun == false )
            return false;

        Room room = _model.CurrentRoom;
        return room != null && room.isActiveAndEnabled;
    }

    /// <summary>
    /// 요청 방에서 사건 시작 가능 여부
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
        else if ( CanAttack == true || _collectRequested == true )
        {
            reason = "공격 또는 수집 전환 대기 중입니다.";
        }
        else if ( _collector.HasTarget( room ) == false )
        {
            reason = "수집 가능한 물건이 없습니다.";
        }
        else if ( _anomalyLinker.CanPrepare == false )
        {
            reason = "사건 연결 중이거나 활동이 중단됐습니다.";
        }
        return reason.Length == 0;
    }

    #endregion

    #region ----- 잠복 -----

    /// <summary>
    /// 잠복 시작
    /// </summary>
    public void Hide ()
    {
        _anomalyLinker.Stop( );
        _attacker.SetTarget( null );
        _mover.Stop( );

        _target = null;

        //이전 활동의 이동 요청은 유지하고
        //새 활동 시작 여부만 초기화
        _roomActivity.BeginWait( _data.HideDuration );
    }

    /// <summary>
    /// 잠복 대기와 방 활동 시작
    /// </summary>
    /// <param name="deltaTime">경과 시간</param>
    /// <returns>활동 가능 여부</returns>
    public bool UpdateHide ( float deltaTime )
    {
        _hider.Update( deltaTime );

        if ( _hider.IsReady == false )
            return false;

        //새 방 선택이나 현재 방 복귀까지 끝나야 행동 시작
        return _roomActivity.IsReady || BeginRoomActivity( );
    }

    /// <summary>
    /// 외형 및 이동 에이전트 복구
    /// </summary>
    public void Show ()
    {
        _hider.Show( );
    }


    #endregion

    #region ----- 수집 -----

    /// <summary>
    /// 사건이 발생한 방에서 수집 시작
    /// </summary>
    public void BeginCollect ()
    {
        _collectRequested = false;
        _anomalyLinker.Stop( );
        _attacker.SetTarget( null );
        _target = null;

        //외형과 몸체 충돌은 숨긴 채 수집 이동만 복구
        _hider.RestoreMovement( );

        if ( _collector.Begin( _collectRoom ) == false )
        {
            Debug.LogWarning(
                $"[공격 적] {name} 수집 시작 실패 / " +
                $"방, 보유 지점과 설정을 확인해 주세요.", this );
        }

        //실제로 수집이 시작된 경우에만 반복음 재생
        if ( _enemySound != null )
            _enemySound.SetCollecting(
                CanRun == true && _collector != null &&
                _collector.IsCollecting == true );
    }

    /// <summary>
    /// 아이템 수집 갱신
    /// </summary>
    /// <param name="deltaTime">경과 시간</param>
    /// <returns>수집 진행 여부</returns>
    public bool UpdateCollect ( float deltaTime )
    {
        _collector?.UpdateCollect( deltaTime );

        //집기 알림에서 적이 비활성화되면
        //수집 모듈이 정리될 수 있음
        return _collector != null && _collector.IsCollecting;
    }

    /// <summary>
    /// 수집 접근 종료
    /// </summary>
    public void EndCollect ()
    {
        //수집 종료 시 반복음 정리
        if ( _enemySound != null )
            _enemySound.SetCollecting( false );

        _collector?.Stop( );
        _collectRoom = null;
    }

    /// <summary>
    /// 현재 보유한 아이템의 렌더러와 충돌 복구
    /// </summary>
    void ShowHeldItems ()
    {
        if ( _holder == null )
            return;

        List<ItemCarry> items = new( );
        _holder.GetItems( items );

        foreach ( ItemCarry item in items )
            item.SetHeldHidden( false );
    }

    #endregion

    #region ----- 추적/공격 -----

    /// <summary>
    /// 가까운 공격 가능 플레이어 선정
    /// </summary>
    /// <returns>목표 선정 성공 여부</returns>
    public bool TryFindTarget ()
    {
        bool isFound = _searchView.TryFindTarget( _data.SearchDist, out _target );


        return isFound;
    }

    /// <summary>
    /// 선정한 플레이어에게 접근 시작
    /// </summary>
    public void BeginChase ()
    {
        Show( );

        _chaser.Begin( _target.TargetTransform );
    }

    /// <summary>
    /// 플레이어 거리 이탈 확인과 접근 갱신
    /// </summary>
    /// <param name="deltaTime">경과 시간</param>
    /// <returns>접근 결과</returns>
    public ApproachStatus UpdateChase ( float deltaTime )
    {
        if ( IsTargetValid( ) == false )
        {
            return ApproachStatus.Failed;
        }

        //추격 중단 거리는 높이를 제외한 수평 거리로 계산
        Vector3 offset = _target.TargetTransform.position - _mover.Position;
        offset.y = 0f;

        float targetDist = offset.magnitude;

        //거리 이탈 시 기존 상태 흐름을 통해 도주
        if ( _model.StopChase( targetDist, _data.LoseTargetDist ) == true )
        {
            return ApproachStatus.Failed;
        }

        ApproachStatus current = _chaser.UpdateMove( deltaTime );


        return current;
    }

    /// <summary>
    /// 플레이어 접근 중단
    /// </summary>
    public void StopChase ()
    {
        _chaser.Stop( );
    }

    /// <summary>
    /// 공격 목표 및 명중 기록 초기화
    /// </summary>
    public void PrepareAttack ()
    {
        _hasHit = false;
        _attacker.SetTarget( _target );
    }

    /// <summary>
    /// 공격 준비 중 목표 방향으로 회전
    /// </summary>
    public void LookAtTarget ()
    {
        Vector3 direction = _target.TargetTransform.position - _mover.Position;
        direction.y = 0f;

        if ( direction.sqrMagnitude <= 0.0001f )
            return;

        _mover.Rotate( Quaternion.LookRotation( direction ), _data.RotateSpeed );
    }


    /// <summary>
    /// 공격 명중 검사 시작
    /// </summary>
    public void BeginAttack ()
    {
        _attacker.BeginAttack( );

        if ( _animatorHandler != null )
            _animatorHandler.PlayAttack( );

        //한 차례 공격 시작음 재생
        if ( _enemySound != null )
            _enemySound.PlayAttack( );
    }

    /// <summary>
    /// 공격 범위 내 목표 명중 확인
    /// </summary>
    /// <returns>명중 여부</returns>
    public bool CheckAttack ()
    {
        _attacker.CheckHit( );
        return _hasHit;
    }

    /// <summary>
    /// 공격 종료와 목표 연결 해제
    /// </summary>
    public void EndAttack ()
    {
        _attacker.SetTarget( null );
    }

    /// <summary>
    /// 선정한 플레이어가 현재 공격 가능한지 확인
    /// </summary>
    /// <returns>목표 유효 여부</returns>
    public bool IsTargetValid ()
    {
        if ( _target == null )
            return false;

        Transform targetTransform = _target.TargetTransform;

        return targetTransform != null &&
               _target.CanTakeHit == true;
    }

    #endregion

    #region ----- 도주 -----

    /// <summary>
    /// 사건 요청과 목표를 정리하고 도주 시작
    /// </summary>
    /// <returns>도주 이동 상태</returns>
    public RouteStatusType BeginEscape ()
    {
        Show( );
        ShowHeldItems( );

        //도주 중에는 사건 결과를 받지 않고
        //다음 잠복 주기에서 다시 연결
        _anomalyLinker.Stop( );
        _target = null;

        StopEscape( );

        if ( TryGetEscapePosition( out Vector3 position, out string reason ) == false )
        {
            Debug.LogWarning(
                $"[공격 적 도주 실패] {name} / " +
                $"{reason}", this );

            return RouteStatusType.Failed;
        }

        if ( _hasEscapePoint == true )
        {
            //개별 지점은 기존 일반 경로로 이동
            _escapeNavigator.Begin( position );
        }
        else
        {
            //충분한 이동 거리를 확보한 경로로 최종 입구까지 이동
            _escapeNavigator.BeginDistanceDetour( position, _data.EscapeMinDistance );
        }

#if UNITY_EDITOR
        RecordDebugEscape( position );
#endif

        //설정 오류 등 시작 단계의 실패는 여기서 한 번만 기록
        if ( _escapeNavigator.Status == RouteStatusType.Failed )
        {
            Debug.LogWarning(
                $"[공격 적 도주 실패] {name} / " +
                $"{_escapeNavigator.FailureReason}", this );
        }

        return _escapeNavigator.Status;
    }

    /// <summary>
    /// 도주 경로 이동 갱신
    /// </summary>
    /// <param name="deltaTime">경과 시간</param>
    /// <returns>도주 이동 상태</returns>
    public RouteStatusType UpdateEscape ( float deltaTime )
    {
        if ( TryGetEscapePosition( out _, out string reason ) == false )
        {
            StopEscape( );
            Debug.LogWarning(
                $"[공격 적 도주 실패] {name} / " +
                $"{reason}", this );
            return RouteStatusType.Failed;
        }

        RouteStatusType previous = _escapeNavigator.Status;
        RouteStatusType current = _escapeNavigator.UpdateMove( deltaTime );

        //이동 실패로 바뀐 순간만 기록
        if ( previous == RouteStatusType.Moving &&
             current == RouteStatusType.Failed )
        {
            Debug.LogWarning(
                $"[공격 적 도주 실패] {name} / " +
                $"{_escapeNavigator.FailureReason}", this );
        }

        return current;
    }

    /// <summary>
    /// 개별 도주 지점 또는 공용 입구 위치 조회
    /// </summary>
    /// <param name="position">도주 목적지</param>
    /// <param name="reason">조회 실패 사유</param>
    /// <returns>목적지 조회 성공 여부</returns>
    bool TryGetEscapePosition ( out Vector3 position, out string reason )
    {
        position = default;
        reason = string.Empty;

        if ( _hasEscapePoint == true )
        {
            //지정 지점이 무효가 되어도 다른 목적지로 바꾸지 않음
            if ( _escapePoint == null )
            {
                reason = "직접 지정한 도주 지점이 파괴되었거나 유효하지 않습니다.";
                return false;
            }

            if ( _escapePoint.gameObject.activeInHierarchy == false )
            {
                reason = $"직접 지정한 도주 지점 {_escapePoint.name}이 비활성 상태입니다.";
                return false;
            }

            position = _escapePoint.position;
            return true;
        }

        //개별 지점이 없으면 공용 입구까지 도주
        return _sceneController.TryGetEntrance( out position, out reason );
    }

    /// <summary>
    /// 도주 이동과 재계산 대기 취소
    /// </summary>
    public void StopEscape ()
    {
        _escapeNavigator.Cancel( );
    }

    /// <summary>
    /// 도주 완료 후 보유 아이템 손실 처리
    /// </summary>
    /// <returns>도주 종료 처리 완료 여부</returns>
    public bool CompleteEscape ()
    {
        if ( isActiveAndEnabled == false ||
             _currentState == null ||
             _currentState.StateType != EnemyStateType.Escape ||
             HasArrivedEscape( ) == false )
            return false;

        MultiItemHolder holder = _holder;
        if ( holder == null )
            return false;

        //손실 처리 중 보유 목록이 변경되므로 별도 목록으로 순회
        List<ItemCarry> items = new( );
        holder.GetItems( items );

        foreach ( ItemCarry item in items )
        {
            //앞선 아이템의 알림에서
            //적이 종료되거나 다시 활성화됐으면 중단
            if ( isActiveAndEnabled == false ||
                 ReferenceEquals( holder, _holder ) == false )
                return false;

            //목록 조회 이후 회수된 물건도 손실 대상에서 제외
            if ( holder.IsHolding( item ) == false )
                continue;

            if ( item.TryGetComponent( out ItemLoss loss ) &&
                 loss.TryLose( _holdPoint ) == true )
                continue;

            //손실 기능이 제거되거나 비활성화된 경우
            //물건을 임의로 삭제하지 않음
            Debug.LogWarning(
                $"[공격 적] {name} 아이템 손실 처리 실패: {item.name}", this );
        }

        if ( isActiveAndEnabled == false ||
             ReferenceEquals( holder, _holder ) == false )
            return false;

        //처리하지 못한 물건은 월드에 내려놓고
        //잠복 시 함께 숨겨지는 상황 방지
        if ( holder.TryReleaseAll( ) == false )
            return false;

        //보유 해제 알림에서 적이 종료되거나
        //새 보유 모듈로 교체됐으면 중단
        if ( isActiveAndEnabled == false ||
             ReferenceEquals( holder, _holder ) == false )
            return false;

        //활동 완료 시 한 번만 추첨
        float chance = _data.MoveRoomChance;
        if ( chance >= 1f || ( chance > 0f && Random.value < chance ) )
            _roomActivity.RequestMove( );
        else
            _roomActivity.RequestReturn( );
        return true;
    }

    /// <summary>
    /// 도주 실패 처리
    /// </summary>
    /// <returns>잠복 전환 가능 여부</returns>
    public bool EndFailedEscape ()
    {
        if ( isActiveAndEnabled == false ||
             _currentState == null ||
             _currentState.StateType != EnemyStateType.Escape )
            return false;

        IEnemyState state = _currentState;
        MultiItemHolder holder = _holder;

        if ( holder == null )
            return false;

        //출구에 도착하지 못했으므로 손실 처리 없이 바닥에 내려놓음
        //해제하지 못한 물건이 있으면 아직 잠복하지 않음
        if ( holder.TryReleaseAll( ) == false )
            return false;

        //보유 해제 알림에서 상태나 보유 모듈이 바뀌었으면 후속 처리 중단
        if ( isActiveAndEnabled == false ||
             _currentState != state ||
             ReferenceEquals( holder, _holder ) == false )
            return false;

        //성공 시 방 이동 추첨과 구분하여 현재 활동 방으로 복귀
        _roomActivity.RequestReturn( );
        return true;
    }

    /// <summary>
    /// 최종 도주 지점 도착 여부 확인
    /// </summary>
    /// <returns>도착 여부</returns>
    public bool HasArrivedEscape ()
    {
        return _escapeNavigator.Status == RouteStatusType.Arrived;
    }

    #endregion

}
