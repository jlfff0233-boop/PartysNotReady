using System;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.AI;
using TMPro;
using Random = UnityEngine.Random;

/// <summary>
/// 아이템 요구 적 - 음식 섭취, 난동과 납치
/// </summary>
public partial class EnemyDemand : Enemy, IFoodReceiver, IAnomalySpawnTarget
{
    [Header( "----- 설정 데이터 -----" )]
    [SerializeField] EnemyDemandData _data;		//음식 요구와 행동 설정

    [Header( "----- 컴포넌트 -----" )]
    [SerializeField] CharacterMover _mover;		//캐릭터 이동
    [SerializeField] HideActivationController _activation;		//등장 표시와 충돌 제어
    [SerializeField] AttackAnimatorHandler _animatorHandler;		//이동과 공격 애니메이션
    [SerializeField] ProgressBarHud _hud;		//만족도 UI
    [SerializeField] ItemAttacker _attacker;		//물건 명중 검사
    [SerializeField] CharacterAttacker _kidnapAttacker;		//NPC 명중 검사
    [SerializeField] CaptureController _capturer;		//납치 표적 포획

    [Header( "----- 사운드 -----" )]
    [SerializeField] DemandSound _enemySound;		//적 사운드
    [SerializeField] SoundPlayer _soundPlayer;		//적 전용 사운드 재생

    [Header( "----- 연출 -----" )]
    [SerializeField] TMP_Text _foodGuide;		//음식 대기 중 요구 안내
    [SerializeField] FeedbackPlayer _feedback;		//행동 사운드와 파티클
    [SerializeField] DemandFeedbackProfile _feedbackProfile;		//곰 음원 설정
    [SerializeField] ParticleSystem _eatParticle;		//먹기 부스러기
    [SerializeField] ParticleSystem _madParticle;		//분노 파티클

    [Header( "----- 자동 목적지 -----" )]
    [SerializeField, Min( 0.01f )] float _destinationSampleDist = 0.5f;		//NavMesh 보정 범위
    [SerializeField, Min( 0.01f )] float _attackApproachDist = 1f;		//아이템 표면과 접근 위치의 거리
    [SerializeField, Min( 0.01f )] float _moveTimeLimit = 30f;		//대기 진입과 퇴장 이동 제한시간

    [Header( "----- 포획 공격 -----" )]
    [SerializeField] float _attackAnimationStartTimeout = 2f;     //공격 상태 진입 제한시간

    const int WaitPositionAttempts = 12;		//대기 위치 최대 탐색 횟수
    const float FloorHeightTolerance = 0.15f;		//바닥 충돌점과 NavMesh 높이 오차
    const float AttackTargetMoveTolerance = 0.25f;		//재접근이 필요한 아이템 이동 거리

    bool _isKidnapping;		//포획 성공음의 운반 시작 기록

    Transform _waitPoint;		//대기 지점
    Transform _spawnPoint;		//사건 시작 시 등장 지점
    Transform _exitPoint;		//퇴장 지점
    Transform _atkStandPoint;		//공격 시 설 위치
    EnemySceneController _sceneController;		//공용 목적지 제공
    DemandAttackPositionSelector _attackSelector;		//난동 접근 위치 선택
    Vector3 _waitPosition;		//이번 등장의 대기 위치
    Room _appearanceRoom;		//이번 등장 방

    FoodData _receivedFood;		//수락한 음식

    FoodDemandModel _model;		//아이템 요구 모델
    DemandRampage _rampage;		//난동 피해 처리
    CaptureAttack _captureAttack;     //클립 시점에 따른 포획 공격 진행
    DemandKidnap _kidnap;		//납치 대상 선정과 접근, 포획 처리


    /// <summary>
    /// 자동 발생 대상 종류
    /// </summary>
    public override EnemySpawnType SpawnType => EnemySpawnType.Demand;

    /// <summary>
    /// 초기 상태
    /// </summary>
    protected override EnemyStateType InitialState => EnemyStateType.Enter;

    /// <summary>
    /// 먹기 시간
    /// </summary>
    public float EatTime => _data.EatTime;

    /// <summary>
    /// 이동 속도
    /// </summary>
    public Vector3 Velocity => _mover.Velocity;

    /// <summary>
    /// 만족도 비율
    /// </summary>
    public float SatisfactionRatio => _model.SatisfactionRatio;

    /// <summary>
    /// 공격 지점
    /// </summary>
    public Transform AtkStandPoint => _atkStandPoint;

    /// <summary>
    /// 공격 횟수
    /// </summary>
    public int AttackCount => _data.AttackCount;

    /// <summary>
    /// 공격 전 대기 시간
    /// </summary>
    public float AttackDelay => _data.AttackDelay;

    /// <summary>
    /// 공격 지속 시간
    /// </summary>
    public float AttackDuration => _data.AttackDuration;

    /// <summary>
    /// 접근 제한 시간
    /// </summary>
    public float ApproachTimeout => _data.ApproachTimeout;

    /// <summary>
    /// 대기 진입과 퇴장 이동 제한시간
    /// </summary>
    public float MoveTimeLimit => _moveTimeLimit;

    /// <summary>
    /// 납치 대상 존재 여부
    /// </summary>
    public bool HasKidnapTarget =>
        _kidnap != null && _kidnap.HasTarget;

    /// <summary>
    /// 이번 공격의 포획 시도 여부
    /// </summary>
    public bool KidnapAttempted => _captureAttack != null && _captureAttack.Attempted;

    /// <summary>
    /// 이전 동작이 복귀하여 포획 공격을 시작할 수 있는지 여부
    /// </summary>
    public bool CanBeginKidnapAttack => _captureAttack.CanBegin;

    /// <summary>
    /// 포획 공격 동작 종료 여부
    /// </summary>
    public bool IsKidnapAttackFinished => _captureAttack.IsFinished;


    /// <summary>
    /// 초기 행동 상태 자동 진입 여부
    /// </summary>
    protected override bool AutoStart => false;

    /// <summary>
    /// 음식 수령 이벤트
    /// </summary>
    public event Action OnFoodReceived;

    /// <summary>
    /// 만족도 변경 이벤트(만족도 비율)
    /// </summary>
    public event Action<float> OnSatisfactionChanged;

    #region ----- 초기화 -----

    /// <summary>
    /// 이동, 음식 요구와 행동 상태 초기화
    /// </summary>
    protected override void Init ()
    {
        base.Init( );

        _mover.Init( _animatorHandler );

        //이전 사운드와 입자를 정리하고 운반 시작 기록 초기화
        _feedback.ResetFeedback( );
        _isKidnapping = false;

        //초기화된 이동 모듈과 전용 사운드 연결
        if ( _enemySound != null )
            _enemySound.Init( _soundPlayer );

        //음식 취향과 만족도 설정
        _model = new FoodDemandModel(
            _data.LikedFoods, _data.DislikedFoods, _data.MaxSatisfaction );

        //난동 시 재료 소실 상한
        _rampage = new DemandRampage( transform, _data.IngredientLossCount );
        _attackSelector = new DemandAttackPositionSelector(
            _mover, _attacker, _attackApproachDist, _destinationSampleDist );

        //납치 표적 접근 설정
        Approacher approacher = new Approacher(
            _mover, _data.MoveSpeed, _data.RotateSpeed,
            _data.KidnapDist, _data.KidnapCheckDist,
            _data.KidnapRefreshInterval, _data.ApproachTimeout,
            _data.KidnapHeight, _data.KidnapTolerance );

        _captureAttack = new CaptureAttack(
            _animatorHandler, _kidnapAttacker, _capturer,
            this, UpdateKidnapAnimation, _attackAnimationStartTimeout );

        _kidnap = new DemandKidnap(
            transform, _mover, approacher, _kidnapAttacker, _capturer, _captureAttack );

        _hud.SetProgress( _model.SatisfactionRatio );
        _hud.SetVisible( false );
        SetFoodGuide( false );

        _states [ ( int ) EnemyStateType.Enter ] =
            new DemandMoveState( this, EnemyStateType.Enter );

        _states [ ( int ) EnemyStateType.Normal ] = new DemandWaitState( this );

        _states [ ( int ) EnemyStateType.Eat ] = new DemandEatState( this );

        _states [ ( int ) EnemyStateType.Mad ] = new DemandMadState( this );

        _states [ ( int ) EnemyStateType.Escape ] =
            new DemandMoveState( this, EnemyStateType.Escape );

        _states [ ( int ) EnemyStateType.Chase ] = new DemandKidnapChaseState( this );

        _states [ ( int ) EnemyStateType.Attack ] = new DemandKidnapAttackState( this );
    }

    /// <summary>
    /// 씬 배치 참조 연결과 적 초기화
    /// </summary>
    /// <param name="settings">배치 설정</param>
    /// <param name="sceneController">공용 목적지 제공</param>
    /// <returns>초기화 성공 여부</returns>
    public bool Init (
        DemandSceneSettings settings, EnemySceneController sceneController )
    {
        _sceneController = sceneController;
        _spawnPoint = settings.SpawnPoint;
        _waitPoint = settings.WaitPoint;
        _exitPoint = settings.ExitPoint;
        _atkStandPoint = settings.AtkStandPoint;
        return base.Init( settings.StartRoom );
    }

    private void LateUpdate ()
    {
        //같은 프레임의 명중/장착 요청을 Animator 갱신 이후 순서대로 실행
        if ( CanRun == true )
            _captureAttack.LateUpdateAttack( Time.deltaTime );

        //실제 행동이 진행되는 동안 이동음 갱신
        if ( _enemySound != null )
        {
            _enemySound.UpdateMovement(
                _mover, Time.deltaTime, CanRun == true && _currentState != null );
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

        //새 활동 연결 전 이전 연출과 운반 기록 정리
        _feedback.ResetFeedback( );
        _isKidnapping = false;

        OnFoodReceived += HandleFoodReceived;
        OnSatisfactionChanged += _hud.SetProgress;

        _attacker.OnItemHit += HandleItemHit;

        _capturer.OnResult += HandleCaptureResult;
        UpdateKidnapAnimation( );

        //초기화와 재등장 대기 중에는 표시와 충돌, Agent 숨김
        if ( _currentState == null )
            _activation.Hide( );
    }

    /// <summary>
    /// 이벤트 구독 해제와 활동 정리
    /// </summary>
    protected override void Deactivate ()
    {
        EndEat( );

        //구독 해제 후 공격 표적과 진행 중인 포획 정리
        OnFoodReceived -= HandleFoodReceived;
        OnSatisfactionChanged -= _hud.SetProgress;

        _attacker.OnItemHit -= HandleItemHit;
        _attacker.EndAttack( );

        _capturer.OnResult -= HandleCaptureResult;
        _kidnap.Clear( );
        UpdateKidnapAnimation( );

        //이전 재생과 이동 거리 기록 정리
        if ( _enemySound != null )
            _enemySound.ResetSounds( );

        //모델을 숨기기 전에 단발음, 반복음과 입자를 모두 정리
        _feedback.ResetFeedback( );
        _isKidnapping = false;

        //종료된 등장의 UI와 몸체 표시 정리
        _hud.SetVisible( false );
        SetFoodGuide( false );
        _activation.Hide( );
    }

    /// <summary>
    /// 새 등장 음식 요구 준비
    /// </summary>
    void PrepareAppearance ()
    {
        //등장 전 상태 초기화
        _mover.Stop( );
        _attacker.EndAttack( );
        _kidnap.Clear( );
        UpdateKidnapAnimation( );
        _receivedFood = null;
        _model.Reset( );
        _hud.SetProgress( _model.SatisfactionRatio );
        _hud.SetVisible( false );
        SetFoodGuide( false );
    }

    #endregion


    #region ----- 핸들 -----

    /// <summary>
    /// 음식 수령 시 만족도 표시
    /// </summary>
    void HandleFoodReceived ()
    {
        _hud.SetProgress( _model.SatisfactionRatio );
        _hud.SetVisible( true );
    }

    /// <summary>
    /// 명중 아이템 피해 처리
    /// </summary>
    /// <param name="item">명중한 아이템</param>
    void HandleItemHit ( ItemCarry item )
    {
        //난동 중 명중만 피해 처리에 전달
        if ( CurrentState == null ||
            CurrentState.StateType != EnemyStateType.Mad )
            return;

        _rampage.ApplyHit( item, _data.PushPower, _data.PushUpPower );
    }

    /// <summary>
    /// 포획 종료 후 납치 운반 표시 갱신
    /// </summary>
    /// <param name="target">포획 대상</param>
    /// <param name="result">포획 결과</param>
    void HandleCaptureResult ( ICaptureTarget target, CaptureResult result )
    {
        UpdateKidnapAnimation( );
    }

    #endregion

    #region ----- 이동 -----

    /// <summary>
    /// 목적지 이동
    /// </summary>
    /// <param name="position">목적지</param>
    /// <returns>이동 요청 성공 여부</returns>
    public bool MoveTo ( Vector3 position )
    {
        return _mover.MoveTo( position, _data.MoveSpeed, _data.StopDist );
    }


    /// <summary>
    /// 이동 정지
    /// </summary>
    public void Stop ()
    {
        _mover.Stop( );
    }

    /// <summary>
    /// 회전 적용
    /// </summary>
    /// <param name="rotation">목표 회전값</param>
    public void Rotate ( Quaternion rotation )
    {
        _mover.Rotate( rotation, _data.RotateSpeed );
    }

    /// <summary>
    /// 실제 이동 방향으로 회전
    /// </summary>
    public void RotateAlongMovement ()
    {
        _mover.RotateAlongMovement( _data.RotateSpeed );
    }

    /// <summary>
    /// 이동 실패 시 포획을 취소하고 다음 등장 대기로 복귀
    /// </summary>
    public void EndFailedMove ()
    {
        Debug.LogWarning( $"[요구 적 이동 실패] {name}: 이번 등장을 종료합니다.", this );
        Stop( );

        //활동 정리에서 포획을 취소하며 납치 완료를 호출하지 않음
        ResetAppearance( );
    }

    /// <summary>
    /// 정상 퇴장 결과를 처리하고 다음 등장 대기로 복귀
    /// </summary>
    public void CompleteEscape ()
    {
        Stop( );

        //납치 완료 판정은 등장 정리 전에 유지
        if ( _kidnap.TryCompleteEscape( ) == false )
            return;

        ResetAppearance( );
    }


    #endregion

    #region ----- 섭취 -----

    /// <summary>
    /// 음식 대기 상태에서 요구 안내 표시 또는 해제
    /// </summary>
    /// <param name="visible">요구 안내 표시 여부</param>
    public void SetFoodGuide ( bool visible )
    {
        //안내 연결은 선택 사항이며 음식 전달 판정과 독립적으로 처리
        if ( _foodGuide == null )
            return;

        bool show = visible == true && isActiveAndEnabled == true;
        _foodGuide.text = show == true ? "먹을 것을 건네주세요." : string.Empty;
        _foodGuide.enabled = show;
    }

    /// <summary>
    /// 음식 수락
    /// </summary>
    /// <param name="data">음식 데이터</param>
    /// <returns>수락 성공 여부</returns>
    public bool TryReceive ( FoodData data )
    {
        if ( CanReceive( data ) == false )
            return false;

        //수락 시에는 음식만 저장하고 섭취 시간이 지난 뒤 만족도 반영
        _receivedFood = data;
        ChangeState( EnemyStateType.Eat );

        //음식 수령 이벤트 발행
        OnFoodReceived?.Invoke( );

        return true;
    }

    /// <summary>
    /// 이동을 멈추고 먹기 연출 시작
    /// </summary>
    public void BeginEat ()
    {
        Stop( );
        _feedback.Begin(
            FeedbackCue.Eat, _feedbackProfile.EatSound, _eatParticle );
    }

    /// <summary>
    /// 먹기 사운드와 파티클 종료
    /// </summary>
    public void EndEat ()
    {
        _feedback.End( FeedbackCue.Eat );
    }

    /// <summary>
    /// 섭취 완료와 만족도에 따른 행동 전환
    /// </summary>
    public void CompleteEat ()
    {
        DemandResult result = _model.ApplyFood( _receivedFood );
        _receivedFood = null;

        OnSatisfactionChanged?.Invoke( _model.SatisfactionRatio );

        //만족하면 퇴장, 분노하면 난동/납치 선택, 나머지는 다시 음식 대기
        switch ( result )
        {
            case DemandResult.Satisfied:
                //섭취 결과가 만족으로 확정된 경우 재생
                _feedback.Play( _feedbackProfile.SatisfiedSound );

                ChangeState( EnemyStateType.Escape );
                break;

            case DemandResult.Mad:
                //분노 소리와 파티클을 함께 재생
                _feedback.Play( _feedbackProfile.MadSound, _madParticle );
                BeginMadAction( );
                break;

            default:
                ChangeState( EnemyStateType.Normal );
                break;
        }
    }


    #endregion

    #region ----- 공격 -----

    /// <summary>
    /// 공격 시작
    /// </summary>
    public void BeginAttack ()
    {
        //새 공격마다 재료 소실 수량과 명중 기록 초기화
        _rampage.BeginAttack( );
        _attacker.BeginAttack( );

        if ( _animatorHandler != null )
            _animatorHandler.PlayAttack( );

        //한 차례 공격 시작음 재생
        _feedback.Play( _feedbackProfile.AttackSound );
    }

    /// <summary>
    /// 난동 공격 명중 검사
    /// </summary>
    public void CheckAttack ()
    {
        _attacker.CheckHit( );
    }

    /// <summary>
    /// 공격 판정 종료
    /// </summary>
    public void EndAttack ()
    {
        _attacker.EndAttack( );
    }

    #endregion

    #region ----- 납치 -----

    /// <summary>
    /// 실제 운반 시작 시 포획 성공음 재생
    /// </summary>
    void UpdateKidnapAnimation ()
    {
        bool isKidnapping = isActiveAndEnabled == true &&
            _capturer.HasTarget == true;		//실제 대상 운반 여부

        if ( _isKidnapping == isKidnapping )
            return;

        _isKidnapping = isKidnapping;

        //같은 대상을 운반하는 동안 반복 재생하지 않음
        if ( isKidnapping == true )
            _feedback.Play( _feedbackProfile.CaptureSound );
    }

    /// <summary>
    /// 난동/납치 선택과 시작
    /// </summary>
    void BeginMadAction ()
    {
        int remainingCount = NpcRegister.RemainingCount;
        bool kidnap = _model.ShouldKidnap(
            remainingCount, _data.KidnapChance, Random.value );


        if ( kidnap == false )
        {
            ChangeState( EnemyStateType.Mad );
            return;
        }

        if ( _kidnap.IsReady( ) == false )
        {
            Debug.LogError(
                "[아이템 요구 적 납치] " +
                "CharacterAttacker와 CaptureController 연결 및 " +
                "활성 상태를 확인해 주세요.", this );

            ChangeState( EnemyStateType.Escape );
            return;
        }

        //납치를 선택했지만 후보가 없으면 빈손 퇴장
        ChangeState( _kidnap.TrySelectTarget( )
            ? EnemyStateType.Chase
            : EnemyStateType.Escape );
    }

    /// <summary>
    /// 선정한 NPC 접근 시작
    /// </summary>
    public void BeginKidnapChase ()
    {
        _kidnap.BeginChase( );
    }

    /// <summary>
    /// NPC 접근 진행
    /// </summary>
    /// <param name="deltaTime">경과시간</param>
    /// <returns>접근 결과</returns>
    public ApproachStatus UpdateKidnapChase ( float deltaTime )
    {
        return _kidnap.UpdateChase( deltaTime );
    }

    /// <summary>
    /// NPC 접근 종료
    /// </summary>
    public void StopKidnapChase ()
    {
        _kidnap.StopChase( );
    }

    /// <summary>
    /// 포획 표적 방향으로 회전
    /// </summary>
    public void LookAtKidnapTarget ()
    {
        _kidnap.LookAtTarget( _data.RotateSpeed );
    }

    /// <summary>
    /// 준비가 끝난 표적의 포획 공격 재생 시작
    /// </summary>
    public void BeginKidnapAttack ()
    {
        _kidnap.BeginAttack( );
    }

    /// <summary>
    /// 포획 공격 종료와 표적 정리
    /// </summary>
    public void EndKidnapAttack ()
    {
        _kidnap.EndAttack( );
    }


    #endregion

    #region ----- 등장/목적지 -----

    /// <summary>
    /// 시작 조건과 거절 사유 조회
    /// </summary>
    /// <param name="room">요청 방</param>
    /// <param name="reason">거절 사유</param>
    /// <returns>시작 가능 여부</returns>
    public bool CheckStart ( Room room, out string reason )
    {
        return CheckAppearanceStart( room, out reason );
    }

    /// <summary>
    /// 실제 등장 준비 실패 사유를 포함하여 새 등장 시작
    /// </summary>
    /// <param name="room">요청 방</param>
    /// <param name="reason">시작 실패 사유</param>
    /// <returns>시작 성공 여부</returns>
    public bool TryStart ( Room room, out string reason )
    {
        if ( CheckStart( room, out reason ) == false )
            return false;

        //모델을 표시하기 전에 등장 위치와 Agent 준비
        if ( _activation.TryPreparePosition(
            _mover, _spawnPoint, _destinationSampleDist ) == false )
        {
            reason = "등장 지점의 연결과 NavMesh를 확인해 주세요.";
            Debug.LogWarning(
                $"[요구 적 등장 실패] {name}: 등장 지점의 연결과 NavMesh를 확인해 주세요.",
                this );
            return false;
        }

        if ( TrySelectWaitPosition( room, out _waitPosition ) == false )
        {
            _activation.Hide( );

            reason = "대기 위치 또는 연결 경로를 확보하지 못했습니다.";
            Debug.LogWarning(
                $"[요구 적 등장 실패] {name}: 대기 위치 또는 연결 경로를 확보하지 못했습니다.", this );
            return false;
        }

        _appearanceRoom = room;
        PrepareAppearance( );

        //등장 준비가 완료된 경우에만 표시
        _activation.Show( );
        ChangeState( InitialState );
        if ( _currentState == null )
            reason = "등장 상태에 진입하지 못했습니다.";
        return _currentState != null;
    }

    /// <summary>
    /// 지정 대기 지점 또는 방 안의 이동 가능한 위치 조회
    /// </summary>
    /// <param name="room">등장할 방</param>
    /// <param name="position">대기 위치</param>
    /// <returns>위치 선택 성공 여부</returns>
    bool TrySelectWaitPosition ( Room room, out Vector3 position )
    {
        position = default;
        RoutePathChecker checker = _mover.PathChecker;

        if ( ReferenceEquals( _waitPoint, null ) == false )
        {
            if ( _waitPoint == null || _waitPoint.gameObject.activeInHierarchy == false )
                return false;

            return checker.TryGetPosition(
                       _waitPoint.position, _destinationSampleDist, out position )
                && checker.TryGetDistance( _mover.Position, position, out _ );
        }

        for ( int i = 0; i < WaitPositionAttempts; i++ )
        {
            if ( room.TryGetRandomFloorProbe(
                out Vector3 origin, out float distance ) == false )
                return false;

            if ( Physics.Raycast(
                origin, Vector3.down, out RaycastHit hit, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore ) == false )
                continue;

            if ( Vector3.Dot( hit.normal, Vector3.up ) < 0.7f ||
                 room.Contains( hit.point ) == false )
                continue;

            if ( checker.TryGetPosition(
                hit.point, _destinationSampleDist, out Vector3 candidate ) == false )
                continue;

            //가구 위 충돌점이 아래 바닥 NavMesh로 보정되는 경우 제외
            if ( Mathf.Abs( candidate.y - hit.point.y ) > FloorHeightTolerance ||
                 room.Contains( candidate ) == false ||
                 checker.TryGetDistance( _mover.Position, candidate, out _ ) == false )
                continue;

            position = candidate;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 현재 이동 상태의 목적지 조회
    /// </summary>
    /// <param name="stateType">대기 진입 또는 퇴장 상태</param>
    /// <param name="position">이동 목적지</param>
    /// <returns>조회 성공 여부</returns>
    public bool TryGetMovePosition ( EnemyStateType stateType, out Vector3 position )
    {
        position = _waitPosition;
        if ( stateType == EnemyStateType.Enter )
            return _appearanceRoom != null && _appearanceRoom.isActiveAndEnabled == true &&
                ( ReferenceEquals( _waitPoint, null ) == true ||
                  ( _waitPoint != null && _waitPoint.gameObject.activeInHierarchy == true ) );

        return _sceneController.TryGetExit( _exitPoint, out position );
    }

    /// <summary>
    /// 직접 지정 또는 자동 난동 위치 조회
    /// </summary>
    /// <param name="excluded">재선택 제외 아이템</param>
    /// <param name="target">자동 선택한 아이템, 직접 지정이면 null</param>
    /// <param name="position">접근 위치</param>
    /// <param name="rotation">공격 방향</param>
    /// <returns>조회 성공 여부</returns>
    public bool TryGetAttackPosition (
        HashSet<ItemCarry> excluded, out ItemCarry target,
        out Vector3 position, out Quaternion rotation )
    {
        target = null;
        position = default;
        rotation = Quaternion.identity;

        if ( ReferenceEquals( _atkStandPoint, null ) == true )
            return _attackSelector.TrySelect(
                _appearanceRoom, excluded, out target, out position, out rotation );

        return _attackSelector.TrySelectAssigned(
            _atkStandPoint, _destinationSampleDist, out position, out rotation );
    }


    #endregion

    #region ----- 검사 -----

    /// <summary>
    /// 목적지 도착 여부
    /// </summary>
    /// <returns>도착 여부</returns>
    public bool HasArrived ()
    {
        //경로 계산 중이거나 완전한 경로가 아니면 도착 불가
        if ( _mover.CanNavigate == false ||
             _mover.PathPending == true ||
             _mover.HasPath == false ||
             _mover.PathStatus != NavMeshPathStatus.PathComplete )
            return false;

        return _mover.RemainingDistance <= _data.StopDist;
    }

    /// <summary>
    /// 요청한 이동의 경로 실패 여부 확인
    /// </summary>
    /// <returns>이동 불가 또는 계산이 끝난 경로의 실패 여부</returns>
    public bool HasFailedMove ()
    {
        return _mover.CanNavigate == false ||
            ( _mover.PathPending == false &&
              ( _mover.HasPath == false ||
                _mover.PathStatus != NavMeshPathStatus.PathComplete ) );
    }

    /// <summary>
    /// 음식 수락 가능 여부
    /// </summary>
    /// <param name="data">전달할 음식</param>
    /// <returns>수락 가능 여부</returns>
    public bool CanReceive ( FoodData data )
    {
        return isActiveAndEnabled == true &&
               CurrentState != null &&
               CurrentState.StateType == EnemyStateType.Normal;
    }

    /// <summary>
    /// 납치 표적의 포획 가능 여부
    /// </summary>
    /// <returns>포획 가능한 NPC 여부</returns>
    public bool IsKidnapTargetValid ()
    {
        return _kidnap != null && _kidnap.IsTargetValid( );
    }

    /// <summary>
    /// 이번 난동 대상 또는 직접 지정 지점의 유효성 확인
    /// </summary>
    /// <param name="target">선택한 아이템</param>
    /// <param name="selectedPosition">선택 당시 아이템 위치</param>
    /// <returns>기존 공격 위치 사용 가능 여부</returns>
    public bool IsAttackTargetValid ( ItemCarry target, Vector3 selectedPosition )
    {
        if ( ReferenceEquals( _atkStandPoint, null ) == false )
            return _atkStandPoint != null && _atkStandPoint.gameObject.activeInHierarchy == true;

        return target != null && target.isActiveAndEnabled == true &&
            target.IsHold == false && _appearanceRoom != null &&
            _appearanceRoom.Contains( target.transform.position ) == true &&
            ( target.transform.position - selectedPosition ).sqrMagnitude <=
                AttackTargetMoveTolerance * AttackTargetMoveTolerance;
    }

    /// <summary>
    /// 등장 위치와 대기 경로를 준비한 뒤 새 등장 시작
    /// </summary>
    /// <param name="room">요청한 방</param>
    /// <returns>시작 여부</returns>
    public bool TryStart ( Room room )
    {
        return TryStart( room, out _ );
    }

    /// <summary>
    /// 새 등장 가능 여부
    /// </summary>
    /// <param name="room">요청한 방</param>
    /// <returns>시작 가능 여부</returns>
    public bool CanStart ( Room room )
    {
        return CheckStart( room, out _ );
    }

    #endregion
}
