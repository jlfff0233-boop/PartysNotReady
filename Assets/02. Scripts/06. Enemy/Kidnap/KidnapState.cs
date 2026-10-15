using UnityEngine;

#region ----- 잠복 -----

/// <summary>
/// 납치 적 잠복 상태 - 활동 대기와 실패 후 표적 탐색
/// </summary>
public class KidnapHideState : IEnemyState
{
    EnemyKidnap _enemy;
    float _searchInterval;      //탐색 간격
    float _searchTime;      //다음 탐색까지 남은 시간

    /// <summary>
    /// 상태 종류
    /// </summary>
    public EnemyStateType StateType => EnemyStateType.Hide;

    /// <summary>
    /// 잠복 상태 생성
    /// </summary>
    /// <param name="enemy">납치 적</param>
    /// <param name="searchInterval">재탐색 간격</param>
    public KidnapHideState ( EnemyKidnap enemy , float searchInterval )
    {
        _enemy = enemy;
        _searchInterval = searchInterval;
    }

    /// <summary>
    /// 잠복 시작
    /// </summary>
    public void Enter ( )
    {
        _searchTime = 0f;
        _enemy.Hide ( );
    }

    /// <summary>
    /// 잠복 대기와 사건 실패 후 표적 탐색
    /// </summary>
    public void Update ( )
    {
        if ( _enemy.UpdateHide ( Time.deltaTime ) == false )
            return;

        _searchTime -= Time.deltaTime;

        //탐색 간격 동안 활동 판단과 표적 탐색 대기
        if ( _searchTime > 0f )
            return;

        _searchTime = _searchInterval;

        //방별 저지 횟수와 대상 유무에 따라 행동 갱신
        _enemy.UpdateActivity( );
    }

    /// <summary>
    /// 접근 전 외형과 이동 에이전트 복구
    /// </summary>
    public void Exit ( )
    {
        _enemy.Show ( );
    }
}

#endregion

#region ----- 접근/포획 -----

/// <summary>
/// 납치 적 접근 상태 - 표적 추적과 재탐색
/// </summary>
public class KidnapChaseState : IEnemyState
{
    EnemyKidnap _enemy;

    /// <summary>
    /// 상태 종류
    /// </summary>
    public EnemyStateType StateType => EnemyStateType.Chase;

    /// <summary>
    /// 접근 상태 생성
    /// </summary>
    /// <param name="enemy">납치 적</param>
    public KidnapChaseState ( EnemyKidnap enemy )
    {
        _enemy = enemy;
    }

    /// <summary>
    /// 접근 시작
    /// </summary>
    public void Enter ( )
    {
        _enemy.BeginChase ( );
    }

    /// <summary>
    /// 접근 결과에 따라 공격 또는 도주
    /// </summary>
    public void Update ( )
    {
        //접근 결과 갱신
        ApproachStatus result = _enemy.UpdateChase ( Time.deltaTime );

        //도착 시 공격 상태 전환
        if ( result == ApproachStatus.Arrived )
            _enemy.ChangeState ( EnemyStateType.Attack );
        //실패 시 도주
        else if ( result == ApproachStatus.Failed )
            _enemy.ChangeState ( EnemyStateType.Escape );
    }

    /// <summary>
    /// 접근 이동 종료
    /// </summary>
    public void Exit ( )
    {
        _enemy.StopChase ( );
    }
}

/// <summary>
/// 납치 적 공격 상태 - 준비, 명중 검사와 공격 동작 종료 대기
/// </summary>
public class KidnapAttackState : IEnemyState
{
    EnemyKidnap _enemy;     //포획 공격을 실행할 적
    float _attackDelay;     //공격 준비 시간
    float _elapsedTime;     //준비 경과시간
    bool _isAttacking;      //공격 재생 요청 여부

    /// <summary>
    /// 상태 종류
    /// </summary>
    public EnemyStateType StateType => EnemyStateType.Attack;

    /// <summary>
    /// 공격 준비 설정과 적 연결
    /// </summary>
    /// <param name="enemy">납치 적</param>
    /// <param name="attackDelay">공격 준비 시간</param>
    public KidnapAttackState ( EnemyKidnap enemy, float attackDelay )
    {
        _enemy = enemy;
        _attackDelay = attackDelay;
    }

    /// <summary>
    /// 공격 준비 시작
    /// </summary>
    public void Enter ()
    {
        _elapsedTime = 0f;
        _isAttacking = false;
        _enemy.PrepareAttack( );
    }

    /// <summary>
    /// 준비 후 한 번만 공격을 요청하고 동작 종료 뒤 도주
    /// </summary>
    public void Update ()
    {
        if ( _isAttacking == false )
        {
            //표적의 포획 가능 여부는 준비 중에만 확인
            //포획 성공 후 CanCapture가 false가 되는 것은 정상
            if ( _enemy.IsTargetValid( ) == false )
            {
                _enemy.ChangeState( EnemyStateType.Escape );
                return;
            }

            _enemy.LookAtTarget( );
            _elapsedTime += Time.deltaTime;
            if ( _elapsedTime < _attackDelay ||
                 _enemy.CanBeginCaptureAttack == false )
                return;

            _isAttacking = true;
            _enemy.BeginAttack( );
            return;
        }

        //LateUpdate가 해당 프레임의 명중/장착까지 처리한 뒤 완료를 발행
        if ( _enemy.IsCaptureAttackFinished == true )
            _enemy.ChangeState( EnemyStateType.Escape );
    }

    /// <summary>
    /// 공격 요청만 정리하고 실제 포획은 도주로 유지
    /// </summary>
    public void Exit ()
    {
        _enemy.EndAttack( );
    }
}

#endregion

#region ----- 도주 -----

/// <summary>
/// 납치 적 도주 상태 - 경로 이동과 포획 결과 처리
/// </summary>
public class KidnapEscapeState : IEnemyState
{
    EnemyKidnap _enemy;     //도주와 포획 결과 처리
    RouteStatusType _escapeStatus;     //현재 도주 이동 상태

    /// <summary>
    /// 상태 종류
    /// </summary>
    public EnemyStateType StateType => EnemyStateType.Escape;

    /// <summary>
    /// 납치 적 연결
    /// </summary>
    /// <param name="enemy">도주할 납치 적</param>
    public KidnapEscapeState ( EnemyKidnap enemy )
    {
        _enemy = enemy;
    }

    /// <summary>
    /// 도주 경로 이동 시작
    /// </summary>
    public void Enter ( )
    {
        _escapeStatus = _enemy.BeginEscape( );
    }

    /// <summary>
    /// 도주 결과 처리 후 잠복 전환
    /// </summary>
    public void Update ( )
    {
        //경로 재계산 대기와 이동 방향 회전도 Navigator에서 처리
        if ( _escapeStatus == RouteStatusType.Moving )
            _escapeStatus = _enemy.UpdateEscape( Time.deltaTime );

        if ( _enemy.isActiveAndEnabled == false ||
             _enemy.CurrentState != this )
            return;

        bool completed;

        switch ( _escapeStatus )
        {
            case RouteStatusType.Arrived:
                //실제 출구 도착 후에만 납치 완료 처리
                completed = _enemy.CompleteEscape( );
                break;

            case RouteStatusType.Failed:
            case RouteStatusType.Cancelled:
                //포획 대상 해제가 끝나기 전에는 잠복하지 않음
                completed = _enemy.EndFailedEscape( );
                break;

            default:
                return;
        }

        //결과 알림에서 바뀐 상태를 잠복으로 덮어쓰지 않음
        if ( completed == true &&
             _enemy.isActiveAndEnabled == true &&
             _enemy.CurrentState == this )
        {
            _enemy.ChangeState( EnemyStateType.Hide );
        }
    }

    /// <summary>
    /// 도주 이동 취소
    /// </summary>
    public void Exit ( )
    {
        _enemy.StopEscape ( );
    }
}

#endregion
