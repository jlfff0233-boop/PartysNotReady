using UnityEngine;

using System.Collections.Generic;

#region ----- 이동 -----

/// <summary>
/// 아이템 요구 적 이동 상태
/// </summary>
public class DemandMoveState : IEnemyState
{
    EnemyDemand _enemy;
    EnemyStateType _stateType;   //상태 타입

    Vector3 _position;          //이번 이동 목적지
    bool _hasDestination;          //목적지 확보 여부
    float _elapsed;          //이번 이동 경과 시간
    bool _moveRequested;        //이동 요청 성공 여부
    float _retryTime;           //이동 대기 시간

    /// <summary>
    /// 상태 타입
    /// </summary>
    public EnemyStateType StateType => _stateType;

    /// <summary>
    /// 이동 상태 초기화
    /// </summary>
    /// <param name="enemy">아이템 요구 적</param>
    /// <param name="stateType">상태 타입</param>
    public DemandMoveState (
        EnemyDemand enemy, EnemyStateType stateType )
    {
        _enemy = enemy;
        _stateType = stateType;
    }

    /// <summary>
    /// 이동 시작
    /// </summary>
    public void Enter ()
    {
        _hasDestination = _enemy.TryGetMovePosition( _stateType, out _position );
        _moveRequested = _hasDestination == true && _enemy.MoveTo( _position );
        _elapsed = 0f;
        _retryTime = 0.5f;
    }

    /// <summary>
    /// 이동 상태 종료
    /// </summary>
    public void Exit ()
    {
        _enemy.Stop( );
    }

    /// <summary>
    /// 이동 상태 갱신
    /// </summary>
    public void Update ()
    {
        _elapsed += Time.deltaTime;
        if ( _hasDestination == false ||
             _enemy.TryGetMovePosition( _stateType, out _ ) == false ||
             _elapsed >= _enemy.MoveTimeLimit )
        {
            _enemy.EndFailedMove( );
            return;
        }

        //도착 후 다음 동작 진행
        if ( _moveRequested == true && _enemy.HasArrived( ) == true )
        {
            if ( _stateType == EnemyStateType.Enter )
                _enemy.ChangeState( EnemyStateType.Normal );
            else
                _enemy.CompleteEscape( );

            return;
        }

        //일정 간격으로 경로 재요청
        _retryTime -= Time.deltaTime;

        if ( _retryTime <= 0f )
        {
            _moveRequested = _enemy.MoveTo( _position );
            _retryTime = 0.5f;
        }

        //수평 이동 방향으로 회전
        _enemy.RotateAlongMovement( );
    }
}

#endregion

#region ----- 대기/섭취 -----

/// <summary>
/// 아이템 요구 적 음식 대기 상태
/// </summary>
public class DemandWaitState : IEnemyState
{
    EnemyDemand _enemy;

    /// <summary>
    /// 상태 타입
    /// </summary>
    public EnemyStateType StateType => EnemyStateType.Normal;

    /// <summary>
    /// 음식 대기 상태 초기화
    /// </summary>
    /// <param name="enemy">아이템 요구 적</param>
    public DemandWaitState ( EnemyDemand enemy )
    {
        _enemy = enemy;
    }

    /// <summary>
    /// 음식 대기 시작
    /// </summary>
    public void Enter ()
    {
        _enemy.Stop( );
        _enemy.SetFoodGuide( true );
    }

    /// <summary>
    /// 음식 대기 종료
    /// </summary>
    public void Exit ()
    {
        _enemy.SetFoodGuide( false );
    }

    /// <summary>
    /// 음식 대기 갱신
    /// </summary>
    public void Update ()
    {
        //음식 수락 시 프레젠터에서 상태 변경
    }
}

/// <summary>
/// 아이템 요구 적 먹기 상태
/// </summary>
public class DemandEatState : IEnemyState
{
    EnemyDemand _enemy;      //아이템 요구 적
    float _elapsedTime;     //먹기 경과 시간

    /// <summary>
    /// 상태 타입
    /// </summary>
    public EnemyStateType StateType => EnemyStateType.Eat;

    /// <summary>
    /// 먹기 상태 초기화
    /// </summary>
    /// <param name="enemy">아이템 요구 적</param>
    public DemandEatState ( EnemyDemand enemy )
    {
        _enemy = enemy;
    }

    /// <summary>
    /// 먹기 시작
    /// </summary>
    public void Enter ()
    {
        _elapsedTime = 0f;
        _enemy.BeginEat( );
    }

    /// <summary>
    /// 먹기 종료
    /// </summary>
    public void Exit ()
    {
        _enemy.EndEat( );
    }

    /// <summary>
    /// 먹기 갱신
    /// </summary>
    public void Update ()
    {
        _elapsedTime += Time.deltaTime;

        //먹기 시간이 지나면 섭취 결과 반영
        if ( _elapsedTime >= _enemy.EatTime )
            _enemy.CompleteEat( );
    }
}

#endregion

#region ----- 난동 -----

/// <summary>
/// 아이템 요구 적 난동 상태
/// </summary>
public class DemandMadState : IEnemyState
{
    /// <summary>
    /// 단계
    /// </summary>
    enum Phase
    {
        Approach,       //접근
        Turn,       //회전
        Wait,       //대기
        Attack      //공격
    }

    EnemyDemand _enemy;     //아이템 요구 적
    ItemCarry _target;     //자동 선택한 난동 대상
    Vector3 _targetPosition;     //선택 당시 아이템 위치
    ulong _targetRentVersion;     //선택 당시 풀 대여 회차
    Vector3 _position;     //이번 접근 위치
    Quaternion _rotation;     //이번 공격 방향
    bool _hasDestination;     //목적지 확보 여부
    int _selectionCount;     //이번 난동의 위치 선택 횟수
    float _approachElapsed;     //재선택을 포함한 접근과 회전 경과 시간

    /// <summary>
    /// 이번 난동의 재선택 제외 아이템
    /// </summary>
    HashSet<ItemCarry> _excluded = new( );

    const int SelectionLimit = 3;     //최초 선택을 포함한 최대 선택 횟수
    Phase _phase;     //현재 난동 단계

    float _timer;     //현재 단계의 경과 시간
    float _repathTimer;     //경로 재요청 대기 시간
    int _completedCount;     //완료한 공격 횟수
    bool _moveRequested;     //이동 요청 성공 여부

    /// <summary>
    /// 상태 종류
    /// </summary>
    public EnemyStateType StateType => EnemyStateType.Mad;

    /// <summary>
    /// 난동 상태의 적 연결
    /// </summary>
    /// <param name="enemy">아이템 요구 적</param>
    public DemandMadState ( EnemyDemand enemy )
    {
        _enemy = enemy;
    }

    /// <summary>
    /// 난동 단계와 공격 횟수 초기화
    /// </summary>
    public void Enter ()
    {
        //초기화
        _enemy.EndAttack( );
        _enemy.Stop( );

        _excluded.Clear( );
        _selectionCount = 0;
        _approachElapsed = 0f;
        _hasDestination = SelectPosition( );
        _phase = Phase.Approach;
        _timer = 0f;
        _repathTimer = 0f;
        _completedCount = 0;
        _moveRequested = false;

        //상태 전환은 Update에서 처리
    }

    /// <summary>
    /// 현재 난동 단계 진행
    /// </summary>
    public void Update ()
    {
        if ( _hasDestination == false )
        {
            Escape( );
            return;
        }

        //진행 중인 공격은 끝내고 다음 접근 전에만 재선택
        if ( _phase != Phase.Attack &&
             ( _enemy.IsAttackTargetValid( _target, _targetPosition ) == false ||
               ( _target != null && _target.PoolRentVersion != _targetRentVersion ) ) )
        {
            if ( ReselectPosition( ) == false )
            {
                Escape( );
                return;
            }
        }

        //재선택과 단계 전환으로 접근 제한시간을 연장하지 않음
        if ( _phase == Phase.Approach || _phase == Phase.Turn )
        {
            _approachElapsed += Time.deltaTime;
            if ( _approachElapsed >= _enemy.ApproachTimeout )
            {
                Escape( );
                return;
            }
        }

        switch ( _phase )
        {
            case Phase.Approach:
                UpdateApproach( );
                break;

            case Phase.Turn:
                UpdateTurn( );
                break;

            case Phase.Wait:
                UpdateWait( );
                break;

            case Phase.Attack:
                UpdateAttack( );
                break;
        }
    }

    /// <summary>
    /// 제한 횟수 안에서 난동 위치 선택
    /// </summary>
    /// <returns>위치 선택 성공 여부</returns>
    bool SelectPosition ()
    {
        if ( _selectionCount >= SelectionLimit )
            return false;

        _selectionCount++;
        bool selected = _enemy.TryGetAttackPosition(
            _excluded, out _target, out _position, out _rotation );
        _targetPosition = _target != null ? _target.transform.position : default;
        _targetRentVersion = _target != null ? _target.PoolRentVersion : 0;
        return selected;
    }

    /// <summary>
    /// 이전 대상을 제외하고 접근 위치 재선택
    /// </summary>
    /// <returns>다음 위치 선택 성공 여부</returns>
    bool ReselectPosition ()
    {
        //직접 지정한 위치는 자동 대상 선택으로 대체하지 않음
        if ( ReferenceEquals( _target, null ) == true )
            return false;

        _excluded.Add( _target );
        _enemy.Stop( );
        _hasDestination = SelectPosition( );
        _phase = Phase.Approach;
        _timer = 0f;
        _repathTimer = 0f;
        _moveRequested = false;
        return _hasDestination;
    }

    /// <summary>
    /// 난동 위치로 접근
    /// </summary>
    void UpdateApproach ()
    {
        _timer += Time.deltaTime;

        if ( _moveRequested == true && _enemy.HasFailedMove( ) == true )
        {
            if ( ReselectPosition( ) == false )
                Escape( );
            return;
        }

        if ( _moveRequested == true && _enemy.HasArrived( ) == true )
        {
            _enemy.Stop( );
            _phase = Phase.Turn;
            _timer = 0f;
            return;
        }

        _repathTimer -= Time.deltaTime;

        if ( _repathTimer <= 0f )
        {
            _moveRequested = _enemy.MoveTo( _position );
            _repathTimer = 0.5f;
        }

        _enemy.RotateAlongMovement( );
    }

    /// <summary>
    /// 공격 방향으로 회전
    /// </summary>
    void UpdateTurn ()
    {
        _timer += Time.deltaTime;

        _enemy.Rotate( _rotation );

        //공격 방향에 충분히 가까워지면 대기 단계로 전환
        if ( Quaternion.Angle( _enemy.transform.rotation, _rotation ) <= 3f )
        {
            _phase = Phase.Wait;
            _timer = 0f;
        }
    }

    /// <summary>
    /// 공격 대기 후 명중 검사 시작
    /// </summary>
    void UpdateWait ()
    {
        _timer += Time.deltaTime;

        if ( _timer < _enemy.AttackDelay )
            return;

        _enemy.BeginAttack( );
        _phase = Phase.Attack;
        _timer = 0f;
    }

    /// <summary>
    /// 명중 검사와 공격 횟수 갱신
    /// </summary>
    void UpdateAttack ()
    {
        //공격 구간 동안 반복 탐지하며 같은 물건의 중복 명중은 공격 기능에서 제외
        _enemy.CheckAttack( );
        _timer += Time.deltaTime;

        if ( _timer < _enemy.AttackDuration )
            return;

        _enemy.EndAttack( );
        _completedCount++;

        if ( _completedCount >= _enemy.AttackCount )
        {
            Escape( );
            return;
        }

        _phase = Phase.Wait;
        _timer = 0f;
    }

    /// <summary>
    /// 퇴장 상태로 전환
    /// </summary>
    void Escape ()
    {
        _enemy.ChangeState( EnemyStateType.Escape );
    }

    /// <summary>
    /// 난동 공격과 이동 종료
    /// </summary>
    public void Exit ()
    {
        _enemy.EndAttack( );
        _enemy.Stop( );
    }
}

#endregion

#region ----- 납치 -----

/// <summary>
/// 아이템 요구 적 납치 접근 상태
/// </summary>
public class DemandKidnapChaseState : IEnemyState
{
    EnemyDemand _enemy;

    /// <summary>
    /// 상태 종류
    /// </summary>
    public EnemyStateType StateType => EnemyStateType.Chase;

    /// <summary>
    /// 납치 접근 상태 생성
    /// </summary>
    /// <param name="enemy">아이템 요구 적</param>
    public DemandKidnapChaseState ( EnemyDemand enemy )
    {
        _enemy = enemy;
    }

    /// <summary>
    /// 납치 표적 접근 시작
    /// </summary>
    public void Enter ()
    {
        _enemy.BeginKidnapChase( );
    }

    /// <summary>
    /// 접근 결과에 따라 포획 공격 또는 퇴장
    /// </summary>
    public void Update ()
    {
        ApproachStatus result =
            _enemy.UpdateKidnapChase( Time.deltaTime );

        if ( result == ApproachStatus.Arrived )
            _enemy.ChangeState( EnemyStateType.Attack );
        else if ( result == ApproachStatus.Failed )
            _enemy.ChangeState( EnemyStateType.Escape );
    }

    /// <summary>
    /// 접근 종료
    /// </summary>
    public void Exit ()
    {
        _enemy.StopKidnapChase( );
    }
}

/// <summary>
/// 아이템 요구 적 납치 공격 상태 - 준비 후 명중과 포획 시도
/// </summary>
public class DemandKidnapAttackState : IEnemyState
{
    EnemyDemand _enemy;     //포획 공격을 실행할 곰
    float _elapsedTime;     //공격 준비 경과시간
    bool _isAttacking;      //공격 재생 요청 여부

    /// <summary>
    /// 상태 종류
    /// </summary>
    public EnemyStateType StateType => EnemyStateType.Attack;

    /// <summary>
    /// 납치 공격을 실행할 적 연결
    /// </summary>
    /// <param name="enemy">아이템 요구 적</param>
    public DemandKidnapAttackState ( EnemyDemand enemy )
    {
        _enemy = enemy;
    }

    /// <summary>
    /// 공격 준비 시작
    /// </summary>
    public void Enter ()
    {
        _elapsedTime = 0f;
        _isAttacking = false;
    }

    /// <summary>
    /// 준비 후 한 번만 재생을 요청하고 손을 내리는 동작까지 대기
    /// </summary>
    public void Update ()
    {
        if ( _isAttacking == false )
        {
            if ( _enemy.IsKidnapTargetValid( ) == false )
            {
                _enemy.ChangeState( EnemyStateType.Escape );
                return;
            }

            _enemy.LookAtKidnapTarget( );
            _elapsedTime += Time.deltaTime;
            if ( _elapsedTime < _enemy.AttackDelay ||
                 _enemy.CanBeginKidnapAttack == false )
                return;

            _isAttacking = true;
            _enemy.BeginKidnapAttack( );
            return;
        }

        //명중 여부만으로 퇴장하지 않고 실제 클립 복귀가 끝난 뒤 이동
        if ( _enemy.IsKidnapAttackFinished == true )
            _enemy.ChangeState( EnemyStateType.Escape );
    }

    /// <summary>
    /// 공격 대기를 정리하고 운반 대상은 도주로 유지
    /// </summary>
    public void Exit ()
    {
        _enemy.EndKidnapAttack( );
    }
}

#endregion
