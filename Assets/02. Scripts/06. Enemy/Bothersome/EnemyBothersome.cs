using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 진로 방해 적 - 이동, 진로 방해, 공 회수 및 전달
/// </summary>
[RequireComponent( typeof( CharacterMover ) )]
public partial class EnemyBothersome : Enemy, IAnomalySpawnTarget
{
    [Header( "----- 설정 데이터 -----" )]
    [SerializeField] EnemyBothersomeData _data;		//공 놀이와 이동 설정

    [Header( "----- 컴포넌트 -----" )]
    [SerializeField] CharacterMover _mover;		//캐릭터 이동
    [SerializeField] HideActivationController _activation;		//등장 표시와 충돌 제어
    [SerializeField] AnimatorHandler _animatorHandler;		//동작 애니메이션

    [Header( "----- 사운드 -----" )]
    [SerializeField] BothersomeSound _enemySound;		//적 사운드
    [SerializeField] SoundPlayer _soundPlayer;		//적 전용 사운드 재생

    [Header( "----- 대상 -----" )]
    [SerializeField] Transform _holdPoint;		//공 보유 위치
    [SerializeField] Collider [ ] _bodyColliders;		//몸통 콜라이더

    [Header( "----- 퇴장 -----" )]
    [SerializeField, Min( 0.01f )] float _exitTimeLimit = 30f;		//퇴장 이동 제한시간

    Transform _player;		//따라다닐 플레이어
    Transform _lookDir;		//시선 방향
    ItemThrow _ball;		//놀이에 사용할 공
    Transform _exitPoint;		//퇴장 위치
    Transform _spawnPoint;		//사건 시작 시 등장 지점
    EnemySceneController _sceneController;		//공용 퇴장 위치 제공

    FetchPlayModel _playModel;      //놀이 진행 모델

    #region ----- 프로퍼티 -----

    /// <summary>
    /// 자동 발생 대상 종류
    /// </summary>
    public override EnemySpawnType SpawnType => EnemySpawnType.Bothersome;

    /// <summary>
    /// 최초 상태
    /// </summary>
    protected override EnemyStateType InitialState => EnemyStateType.Chase;

    /// <summary>
    /// 현재 위치
    /// </summary>
    public Vector3 Position => _mover.Position;

    /// <summary>
    /// 현재 이동 속도
    /// </summary>
    public Vector3 Velocity => _mover.Velocity;

    /// <summary>
    /// 플레이어 위치
    /// </summary>
    public Vector3 PlayerPosition => _player.position;

    /// <summary>
    /// 시선 방향
    /// </summary>
    public Vector3 PlayerForward => _lookDir.forward;

    /// <summary>
    /// 공 위치
    /// </summary>
    public Vector3 BallPosition => _ball.transform.position;

    /// <summary>
    /// 공 보유 위치
    /// </summary>
    public Vector3 HoldPosition => _holdPoint.position;

    /// <summary>
    /// 공 보유 여부
    /// </summary>
    public bool HasBall => _ball.CurrentType == HoldType.World;

    /// <summary>
    /// 놀이 완료 횟수
    /// </summary>
    public int CompletedCount => _playModel.CurrentCount;

    /// <summary>
    /// 퇴장 이동 제한시간
    /// </summary>
    public float ExitTimeLimit => _exitTimeLimit;

    /// <summary>
    /// 초기 행동 상태 자동 진입 여부
    /// </summary>
    protected override bool AutoStart => false;

    #endregion

    #region ----- 초기화 -----

    /// <summary>
    /// 이동 모듈, 놀이 모델, 상태 초기화
    /// </summary>
    protected override void Init ()
    {
        base.Init( );

        _mover.Init( _animatorHandler );

        //초기화된 이동 모듈과 전용 사운드 연결
        if ( _enemySound != null )
            _enemySound.Init( _soundPlayer );

        _playModel = new FetchPlayModel( _data.ReqCount );

        //정면 방해와 전달은 같은 추적 설정 사용
        _states [ ( int ) EnemyStateType.Chase ] =
            new BothersomeChaseState(
                this, _data.FrontDist, _data.DeliveryDist,
                _data.ResumeDist, _data.FollowSpeed, _data.RepathInterval );

        //공 회수와 이동 정체 시 복구 설정
        _states [ ( int ) EnemyStateType.Fetch ] =
            new BothersomeFetchState(
                this, _data.FetchSpeed, _data.FetchDist,
                _data.RecoverDelay, _data.ProgressDist, _data.RepathInterval );

        //회수한 공 전달
        _states [ ( int ) EnemyStateType.Return ] =
            new BothersomeReturnState(
                this, _data.FrontDist, _data.DeliveryDist,
                _data.ResumeDist, _data.FollowSpeed, _data.RepathInterval );

        //놀이 완료 후 퇴장
        _states [ ( int ) EnemyStateType.Escape ] =
            new BothersomeEscapeState(
                this, _data.FollowSpeed, _data.RepathInterval );
    }

    /// <summary>
    /// 씬 배치 참조 연결과 적 초기화
    /// </summary>
    /// <param name="settings">배치 설정</param>
    /// <param name="player">추적 대상</param>
    /// <param name="lookDir">시선 방향</param>
    /// <param name="sceneController">공용 퇴장 위치 제공</param>
    /// <returns>초기화 성공 여부</returns>
    public bool Init (
        BothersomeSceneSettings settings, Transform player, Transform lookDir,
        EnemySceneController sceneController )
    {
        _sceneController = sceneController;
        _player = player;
        _lookDir = lookDir;
        _ball = settings.Ball;
        _exitPoint = settings.ExitPoint;
        _spawnPoint = settings.SpawnPoint;
        return base.Init( settings.StartRoom );
    }

    private void LateUpdate ()
    {
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

        //투척과 실제 보유 변경을 기준으로 놀이 진행
        _ball.OnThrown += HandleThrown;
        _ball.OnHoldChanged += HandleHoldChanged;

        //초기화와 재등장 대기 중에는 표시와 충돌, Agent 숨김
        if ( _currentState == null )
            _activation.Hide( );
    }

    /// <summary>
    /// 이벤트 구독 해제와 활동 정리
    /// </summary>
    protected override void Deactivate ()
    {
        _ball.OnThrown -= HandleThrown;
        _ball.OnHoldChanged -= HandleHoldChanged;

        //이전 재생과 이동 거리 기록 정리
        if ( _enemySound != null )
            _enemySound.ResetSounds( );

        //종료된 등장의 몸체와 이동 기능 숨김
        _mover.Stop( );
        _activation.Hide( );
    }

    /// <summary>
    /// 새 등장 놀이 준비
    /// </summary>
    void PrepareAppearance ()
    {
        //등장 시에만 초기화
        _mover.Stop( );
        _playModel.Reset( );
    }

    #endregion


    #region ----- 핸들 -----

    /// <summary>
    /// 실제 투척에 따른 공 놀이 시작
    /// </summary>
    void HandleThrown ()
    {
        if ( _currentState == null ||
            _currentState.StateType == EnemyStateType.Escape )
            return;

        _playModel.BeginRound( );
        ChangeState( EnemyStateType.Fetch );
    }

    /// <summary>
    /// 공 보유 변경에 따른 회수, 전달 처리
    /// </summary>
    /// <param name="previous">이전 보유 방식</param>
    /// <param name="current">현재 보유 방식</param>
    void HandleHoldChanged ( HoldType previous, HoldType current )
    {
        if ( _currentState == null ||
            _currentState.StateType == EnemyStateType.Escape )
            return;

        //회수 요청이 실제로 성공했을 때만 회수 완료 기록
        if ( previous == HoldType.None &&
            current == HoldType.World &&
            _currentState.StateType == EnemyStateType.Fetch )
        {
            _playModel.MarkRecovered( );

            //마지막 회차는 전달을 기다리지 않고 정상 회수 시 공을 가진 채 퇴장
            if ( _playModel.TryCompleteFinalRound( ) == true )
            {
                //마지막 회차가 실제로 완료된 경우 만족음 재생
                if ( _enemySound != null )
                    _enemySound.PlaySatisfied( );

                ChangeState( EnemyStateType.Escape );
                return;
            }

            //앞선 회차는 플레이어에게 공 전달
            ChangeState( EnemyStateType.Return );
            return;
        }

        if ( current != HoldType.View )
            return;

        //플레이어에게 돌아와 전달 대기 중인 공만 정상 전달로 인정
        if ( previous == HoldType.World &&
            _currentState is BothersomeReturnState returnState &&
            returnState.CanDeliver == true )
        {
            if ( _playModel.TryCompleteRound( ) == true )
            {
                //실제 완료로 인정된 회차에만 만족음 재생
                if ( _enemySound != null )
                    _enemySound.PlaySatisfied( );
            }
        }
        else
        {
            //최초 전달 또는 회수 도중 플레이어가 직접 집은 경우
            _playModel.CancelRound( );
        }

        ChangeState( EnemyStateType.Chase );
    }

    #endregion

    #region ----- 이동/퇴장 -----

    /// <summary>
    /// 목표 주변 NavMesh로 이동 요청
    /// </summary>
    /// <param name="position">목표 위치</param>
    /// <param name="speed">이동 속력</param>
    /// <param name="stopDistance">정지 거리</param>
    /// <returns>이동 요청 성공 여부</returns>
    public bool MoveTo (
        Vector3 position, float speed, float stopDistance )
    {
        if ( _mover.TryGetMovePosition(
            position, _data.SearchDistance, out Vector3 movePosition ) == false )
        {
            _mover.Stop( );
            return false;
        }

        return _mover.MoveTo( movePosition, speed, stopDistance );
    }

    /// <summary>
    /// 퇴장 지점 이동
    /// </summary>
    /// <param name="position">퇴장 위치</param>
    /// <param name="speed">이동 속력</param>
    /// <param name="stopDist">정지 거리</param>
    /// <returns>이동 요청 성공 여부</returns>
    public bool MoveToExit (
        Vector3 position, float speed, float stopDist )
    {
        //퇴장 위치는 주변 위치 조회 없이 직접 경로 요청
        return _mover.MoveTo( position, speed, stopDist );
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
    /// 직접 지정 또는 공용 퇴장 위치 조회
    /// </summary>
    /// <param name="position">퇴장 위치</param>
    /// <returns>조회 성공 여부</returns>
    public bool TryGetExitPosition ( out Vector3 position )
    {
        return _sceneController.TryGetExit( _exitPoint, out position );
    }

    /// <summary>
    /// 퇴장 실패 시 공을 해제하고 등장 종료
    /// </summary>
    public void EndFailedExit ()
    {
        Debug.LogWarning( $"[방해 적 퇴장 실패] {name}: 이번 등장을 종료합니다.", this );
        Stop( );
        bool hasBall = _ball != null && _ball.CurrentType == HoldType.World &&
            _ball.transform.IsChildOf( _holdPoint ) == true;

        //공 해제 알림에서 추적 상태로 돌아가지 않도록 활동부터 정리
        ResetAppearance( );
        if ( hasBall == true && _ball != null && _ball.TryRelease( ) == false )
            Debug.LogWarning( $"[방해 적] {name}: 퇴장 실패 후 공 해제 실패", this );
    }

    /// <summary>
    /// 퇴장 이동을 종료하고 다음 등장 대기로 복귀
    /// </summary>
    public void CompleteEscape ()
    {
        _mover.Stop( );
        ResetAppearance( );
    }

    /// <summary>
    /// 목적지 도착 여부
    /// </summary>
    /// <param name="stopDist">정지 거리</param>
    /// <returns>도착 여부</returns>
    public bool HasArrived ( float stopDist )
    {
        //경로 계산 중이거나 완전한 경로가 아니면 도착 불가
        if ( _mover.CanNavigate == false ||
             _mover.PathPending == true ||
             _mover.HasPath == false ||
             _mover.PathStatus != NavMeshPathStatus.PathComplete )
            return false;

        return _mover.RemainingDistance <= stopDist;
    }

    #endregion

    #region ----- 공 놀이 -----

    /// <summary>
    /// 공 놀이 요구음 재생
    /// </summary>
    public void PlayRequest ()
    {
        if ( _enemySound != null )
            _enemySound.PlayRequest( );
    }

    /// <summary>
    /// 공 회수 범위 이탈 기록
    /// </summary>
    /// <param name="ballDist">보유 위치와 공 사이 거리</param>
    public void CheckRecoveryRange ( float ballDist )
    {
        _playModel.CheckRecoveryRange( ballDist, _data.FetchDist );
    }

    /// <summary>
    /// 공을 보유 위치로 회수
    /// </summary>
    public void TakeBall ()
    {
        //실패한 집기를 회수 완료로 취급하지 않음
        _ball.TryPickUp( _holdPoint, HoldType.World, _bodyColliders );
    }

    /// <summary>
    /// 실패한 공 놀이 취소 및 공 복구
    /// </summary>
    public void RecoverBall ()
    {
        _playModel.CancelRound( );

        //회수 알림이 놀이 완료로 집계되지 않도록 상태부터 변경
        ChangeState( EnemyStateType.Chase );
        TakeBall( );
    }

    #endregion

    #region ----- 발생 -----


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
            _mover, _spawnPoint, _data.SearchDistance ) == false )
        {
            reason = "등장 지점의 연결과 NavMesh를 확인해 주세요.";
            Debug.LogWarning(
                $"[방해 적 등장 실패] {name}: 등장 지점의 연결과 NavMesh를 확인해 주세요.",
                this );
            return false;
        }

        PrepareAppearance( );
        _activation.Show( );
        ChangeState( InitialState );
        TakeBall( );
        if ( _currentState == null )
            reason = "등장 상태에 진입하지 못했습니다.";
        return _currentState != null;
    }


    /// <summary>
    /// 등장 위치를 준비한 뒤 추적과 공 놀이 시작
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
