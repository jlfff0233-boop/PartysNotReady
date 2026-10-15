using UnityEngine;

#region ----- 추적 -----

/// <summary>
/// 추적 및 진로 방해 상태
/// </summary>
public class BothersomeChaseState : IEnemyState
{
    EnemyBothersome _enemy;     //진로 방해 적
    float _frontDist;     //유지 거리
    float _deliveryDist;     //전달 대기 거리
    float _resumeDist;     //추적 재개 거리

    float _speed;     //이동 속력
    float _repathInterval;     //목적지 갱신 간격

    Vector3 _forward = Vector3.forward;     //시선 방향
    float _repathTimer;     //목적지 갱신 시간
    bool _isWaiting;     //공 전달 대기 여부

    /// <summary>
    /// 상태 종류
    /// </summary>
    public virtual EnemyStateType StateType => EnemyStateType.Chase;

    /// <summary>
    /// 공 전달 가능 여부
    /// </summary>
    public bool CanDeliver => CheckCanDeliver( );


    /// <summary>
    /// 추적 설정 초기화
    /// </summary>
    /// <param name="enemy">에너미</param>
    /// <param name="frontDist">유지 거리</param>
    /// <param name="deliveryDist">전달 대기 거리</param>
    /// <param name="resumeDist">추적 재개 거리</param>
    /// <param name="speed">이동 속력</param>
    /// <param name="repathInterval">목적지 갱신 간격</param>
    public BothersomeChaseState (
        EnemyBothersome enemy,
        float frontDist, float deliveryDist,
        float resumeDist, float speed, float repathInterval )
    {
        _enemy = enemy;
        _frontDist = frontDist;
        _deliveryDist = deliveryDist;
        _resumeDist = resumeDist;
        _speed = speed;
        _repathInterval = repathInterval;
    }

    /// <summary>
    /// 추적 시작
    /// </summary>
    public void Enter ()
    {
        //초기화
        _repathTimer = 0f;
        _isWaiting = false;
        _enemy.Stop( );
    }

    /// <summary>
    /// 추적 및 공 전달 대기
    /// </summary>
    public void Update ()
    {
        //시선 방향 수평으로 유지
        Vector3 forward = _enemy.PlayerForward;
        forward.y = 0f;

        //수평 방향이 사실상 없다면 이전 방향 유지
        if ( forward.sqrMagnitude >= 0.0001f )
            _forward = forward.normalized;

        //플레이어까지의 방향과 거리 계산
        Vector3 toPlayer = _enemy.PlayerPosition - _enemy.Position;
        float playerDist = toPlayer.magnitude;

        //전달 대기 거리보다 넓은 재개 거리로 잦은 정지/이동 전환 방지
        //플레이어가 멀어지거나 공을 받았다면 추적 재개
        if ( _isWaiting == true &&
            ( _enemy.HasBall == false || playerDist > _resumeDist ) )
        {
            _isWaiting = false;
            _repathTimer = 0f;
        }

        //정면 여부 확인
        Vector3 fromPlayer = -toPlayer;
        fromPlayer.y = 0f;

        //공을 가지고 플레이어 정면에 도착하면 멈춰서 전달
        if ( _isWaiting == false && _enemy.HasBall == true &&
            playerDist <= _deliveryDist &&
            Vector3.Dot( fromPlayer, _forward ) > 0f )
        {
            _isWaiting = true;
            _enemy.Stop( );

            //전달 대기에 진입한 순간에만 요구음 재생
            _enemy.PlayRequest( );
        }

        if ( _isWaiting == false )
        {
            //지정 간격마다 목적지 갱신
            _repathTimer -= Time.deltaTime;

            if ( _repathTimer <= 0f )
            {
                //플레이어 시선 앞쪽의 유지 거리만큼 목표 위치 설정
                Vector3 destination =
                    _enemy.PlayerPosition + _forward * _frontDist;

                //이동
                _enemy.MoveTo( destination, _speed, 0.1f );
                _repathTimer = _repathInterval;
            }
        }

        //이동 방향과 별개로 플레이어를 마주 봄
        toPlayer.y = 0f;

        if ( toPlayer.sqrMagnitude >= 0.0001f )
            _enemy.Rotate(
                Quaternion.LookRotation( toPlayer.normalized, Vector3.up ) );
    }

    /// <summary>
    /// 추적 종료
    /// </summary>
    public void Exit ()
    {
        _enemy.Stop( );
    }


    /// <summary>
    /// 전달 대기 상태와 플레이어까지의 거리 확인
    /// </summary>
    /// <returns>공 전달 가능 여부</returns>
    bool CheckCanDeliver ()
    {
        return _isWaiting &&
               Vector3.Distance( _enemy.Position, _enemy.PlayerPosition ) <= _resumeDist;
    }


}

#endregion

#region ----- 공 회수/전달 -----

/// <summary>
/// 던져진 공 회수 상태
/// </summary>
public class BothersomeFetchState : IEnemyState
{
    EnemyBothersome _enemy;
    float _speed;     //회수 속력
    float _fetchDist;     //회수 거리

    float _recoverDelay;     //복구 대기 시간
    float _progressDist;     //이동 인정 거리
    float _repathInterval;     //목적지 갱신 간격

    Vector3 _progressPos;     //이동 진행 위치
    float _stuckTimer;     //이동 대기 타이머
    float _repathTimer;     //목적지 갱신 시간

    /// <summary>
    /// 상태 종류
    /// </summary>
    public EnemyStateType StateType => EnemyStateType.Fetch;


    /// <summary>
    /// 공 회수 설정 초기화
    /// </summary>
    /// <param name="enemy">상태 소유자</param>
    /// <param name="speed">회수 속력</param>
    /// <param name="fetchDist">보유 위치 기준 회수 거리</param>
    /// <param name="recoverDelay">이동 정체 시 복구 대기 시간</param>
    /// <param name="progressDist">이동 진행 인정 거리</param>
    /// <param name="repathInterval">목적지 갱신 간격</param>
    public BothersomeFetchState (
        EnemyBothersome enemy,
        float speed, float fetchDist,
        float recoverDelay, float progressDist, float repathInterval )
    {
        _enemy = enemy;
        _speed = speed;
        _fetchDist = fetchDist;
        _recoverDelay = recoverDelay;
        _progressDist = progressDist;
        _repathInterval = repathInterval;
    }

    /// <summary>
    /// 공 회수 시작
    /// </summary>
    public void Enter ()
    {
        _progressPos = _enemy.Position;
        _stuckTimer = 0f;
        _repathTimer = 0f;
        _enemy.Stop( );
    }

    /// <summary>
    /// 공 추적, 회수 및 이동 정체 확인
    /// </summary>
    public void Update ()
    {
        //공의 현재 위치를 기준으로 회수 거리 확인
        Vector3 ballPosition = _enemy.BallPosition;

        float ballDist = Vector3.Distance(
            _enemy.HoldPosition, ballPosition );

        //공 회수 전에 범위 이탈 여부 기록
        _enemy.CheckRecoveryRange( ballDist );

        //높이를 포함해 실제 보유 위치에서 닿을 수 있을 때 회수
        if ( ballDist <= _fetchDist )
        {
            _enemy.TakeBall( );
            return;
        }

        //지정 간격마다 목적지 갱신
        _repathTimer -= Time.deltaTime;

        if ( _repathTimer <= 0f )
        {
            _enemy.MoveTo( ballPosition, _speed, 0.05f );
            _repathTimer = _repathInterval;
        }

        //공 회수 중에는 실제 이동 방향을 바라봄
        Vector3 direction = _enemy.Velocity;
        direction.y = 0f;

        if ( direction.sqrMagnitude >= 0.0001f )
        {
            _enemy.Rotate(
                Quaternion.LookRotation( direction.normalized, Vector3.up ) );
        }

        //공과의 거리가 아니라 적이 실제로 움직인 거리로 정체 판단
        if ( Vector3.Distance( _progressPos, _enemy.Position ) >= _progressDist )
        {
            //이동이 진행됐다면 대기 시간 초기화
            _progressPos = _enemy.Position;
            _stuckTimer = 0f;
        }
        else
        {
            //이동이 진행되지 않은 시간만 누적
            _stuckTimer += Time.deltaTime;
        }

        //대기 시간이 기준을 넘으면 공 놀이를 취소하고 공 복구
        if ( _stuckTimer >= _recoverDelay )
        {
            _enemy.RecoverBall( );
            return;
        }
    }

    /// <summary>
    /// 공 회수 종료
    /// </summary>
    public void Exit ()
    {
        _enemy.Stop( );
    }
}

/// <summary>
/// 공 전달 상태 - 추적과 전달 대기 재사용
/// </summary>
public class BothersomeReturnState : BothersomeChaseState
{
    /// <summary>
    /// 상태 종류
    /// </summary>
    public override EnemyStateType StateType => EnemyStateType.Return;


    /// <summary>
    /// 공 전달 설정 초기화
    /// </summary>
    /// <param name="enemy">상태 소유자</param>
    /// <param name="frontDist">정면 유지 거리</param>
    /// <param name="deliveryDist">전달 대기 거리</param>
    /// <param name="resumeDist">추적 재개 거리</param>
    /// <param name="speed">이동 속력</param>
    /// <param name="repathInterval">목적지 갱신 간격</param>
    public BothersomeReturnState (
        EnemyBothersome enemy,
        float frontDist, float deliveryDist,
        float resumeDist, float speed, float repathInterval )
        : base( enemy, frontDist, deliveryDist, resumeDist, speed, repathInterval )
    {
        //이동과 전달 대기는 추적 상태를 재사용하고 상태 종류만 구분
    }
}

#endregion

#region ----- 퇴장 -----

/// <summary>
/// 퇴장 상태
/// </summary>
public class BothersomeEscapeState : IEnemyState
{
    readonly EnemyBothersome _enemy;
    Vector3 _position;     //이번 퇴장 위치
    bool _hasDestination;     //목적지 확보 여부
    float _elapsed;     //퇴장 경과 시간
    readonly float _speed;     //퇴장 속력
    readonly float _repathInterval;     //목적지 갱신 간격

    const float StopDist = 0.2f;     //정지 거리
    bool _moveRequested;             //이동 요청 성공 여부

    float _repathTimer;     //목적지 갱신 시간

    /// <summary>
    /// 상태 종류
    /// </summary>
    public EnemyStateType StateType => EnemyStateType.Escape;


    /// <summary>
    /// 퇴장 설정 초기화
    /// </summary>
    /// <param name="enemy">상태 소유자</param>
    /// <param name="speed">퇴장 속력</param>
    /// <param name="repathInterval">목적지 갱신 간격</param>
    public BothersomeEscapeState (
        EnemyBothersome enemy,
        float speed, float repathInterval )
    {
        _enemy = enemy;
        _speed = speed;
        _repathInterval = repathInterval;
    }

    /// <summary>
    /// 퇴장 시작
    /// </summary>
    public void Enter ()
    {
        _enemy.Stop( );

        _hasDestination = _enemy.TryGetExitPosition( out _position );
        _elapsed = 0f;
        _moveRequested = _hasDestination == true &&
            _enemy.MoveToExit( _position, _speed, StopDist );
        _repathTimer = _repathInterval;
    }

    /// <summary>
    /// 출구 이동 및 도착 확인
    /// </summary>
    public void Update ()
    {
        _elapsed += Time.deltaTime;
        if ( _hasDestination == false ||
             _enemy.TryGetExitPosition( out _ ) == false ||
             _elapsed >= _enemy.ExitTimeLimit )
        {
            _enemy.EndFailedExit( );
            return;
        }

        //완전한 경로의 남은 거리로 도착 확인
        if ( _moveRequested == true && _enemy.HasArrived( StopDist ) == true )
        {
            _enemy.CompleteEscape( );
            return;
        }

        //지정 간격마다 목적지 갱신
        _repathTimer -= Time.deltaTime;

        if ( _repathTimer <= 0f )
        {
            _moveRequested = _enemy.MoveToExit(
                _position, _speed, StopDist );
            _repathTimer = _repathInterval;
        }

        //퇴장 중에는 실제 이동 방향을 수평으로 바라봄
        Vector3 direction = _enemy.Velocity;
        direction.y = 0f;

        if ( direction.sqrMagnitude >= 0.0001f )
        {
            _enemy.Rotate(
                Quaternion.LookRotation( direction.normalized, Vector3.up ) );
        }
    }

    /// <summary>
    /// 퇴장 이동 종료
    /// </summary>
    public void Exit ()
    {
        _enemy.Stop( );
    }
}

#endregion
