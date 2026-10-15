using UnityEngine;

#region ----- 잠복 -----

/// <summary>
/// 공격 적 잠복 상태 - 활동 대기와 결과에 따른 행동 전환
/// </summary>
public class HarmHideState : IEnemyState
{
    EnemyHarm _enemy;
    float _eventInterval;       //활동 판단과 목표 재탐색 간격
    float _elapsedTime;     //요청 대기 경과 시간

    /// <summary>
    /// 상태 종류
    /// </summary>
    public EnemyStateType StateType => EnemyStateType.Hide;

    /// <summary>
    /// 잠복 상태 초기화
    /// </summary>
    /// <param name="enemy">공격 적</param>
    /// <param name="eventInterval">활동 판단과 목표 재탐색 간격</param>
    public HarmHideState ( EnemyHarm enemy , float eventInterval )
    {
        _enemy = enemy;
        _eventInterval = eventInterval;
    }

    /// <summary>
    /// 잠복과 활동 대기 시작
    /// </summary>
    public void Enter ( )
    {
        _elapsedTime = 0f;

        _enemy.Hide ( );
    }

    /// <summary>
    /// 잠복 시간과 방 활동 갱신
    /// </summary>
    public void Update ( )
    {
        if ( _enemy.UpdateHide ( Time.deltaTime ) == false )
            return;

        //변칙 성공 후에는 다음 행동 판단보다 수집 전환을 먼저 처리
        if ( _enemy.HasCollectionRequest == true )
        {
            _enemy.ChangeState ( EnemyStateType.Collect );
            return;
        }

        _elapsedTime += Time.deltaTime;

        if ( _elapsedTime < _eventInterval )
            return;

        _elapsedTime = 0f;
        _enemy.UpdateActivity ( );
    }

    public void Exit ( )
    {

    }
}

#endregion

#region ----- 접근/공격 -----

/// <summary>
/// 공격 적 접근 상태 - 선정한 플레이어 추적
/// </summary>
public class HarmChaseState : IEnemyState
{
    EnemyHarm _enemy;

    /// <summary>
    /// 상태 종류
    /// </summary>
    public EnemyStateType StateType => EnemyStateType.Chase;

    /// <summary>
    /// 접근 상태 초기화
    /// </summary>
    /// <param name="enemy">공격 적</param>
    public HarmChaseState ( EnemyHarm enemy )
    {
        _enemy = enemy;
    }

    /// <summary>
    /// 플레이어 접근 시작
    /// </summary>
    public void Enter ( )
    {
        _enemy.BeginChase ( );
    }

    /// <summary>
    /// 접근 결과에 따른 공격 또는 도주 전환
    /// </summary>
    public void Update ( )
    {
        ApproachStatus status = _enemy.UpdateChase ( Time.deltaTime );

        if ( status == ApproachStatus.Arrived )
            _enemy.ChangeState ( EnemyStateType.Attack );
        else if ( status == ApproachStatus.Failed )
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
/// 공격 적 공격 상태 - 준비 후 한 차례 명중 검사
/// </summary>
public class HarmAttackState : IEnemyState
{
    EnemyHarm _enemy;
    float _attackDelay;     //공격 준비 시간
    float _attackDuration;      //명중 검사 지속 시간

    float _elapsedTime;     //현재 구간 경과 시간
    bool _isAttacking;      //명중 검사 구간 여부

    /// <summary>
    /// 상태 종류
    /// </summary>
    public EnemyStateType StateType => EnemyStateType.Attack;

    /// <summary>
    /// 공격 상태 초기화
    /// </summary>
    /// <param name="enemy">공격 적</param>
    /// <param name="attackDelay">공격 준비 시간</param>
    /// <param name="attackDuration">명중 검사 지속 시간</param>
    public HarmAttackState (
        EnemyHarm enemy , float attackDelay , float attackDuration )
    {
        _enemy = enemy;
        _attackDelay = attackDelay;
        _attackDuration = attackDuration;
    }

    /// <summary>
    /// 공격 준비 시작
    /// </summary>
    public void Enter ( )
    {
        _elapsedTime = 0f;
        _isAttacking = false;

        _enemy.PrepareAttack ( );
    }

    /// <summary>
    /// 공격 준비와 명중 검사 진행
    /// </summary>
    public void Update ( )
    {
        if ( _enemy.IsTargetValid ( ) == false )
        {
            _enemy.ChangeState ( EnemyStateType.Escape );
            return;
        }

        _elapsedTime += Time.deltaTime;

        if ( _isAttacking == false )
        {
            //준비 중에만 목표를 바라보고 공격 구간에서는 방향 유지
            _enemy.LookAtTarget ( );

            if ( _elapsedTime < _attackDelay )
                return;

            _elapsedTime = 0f;
            _isAttacking = true;
            _enemy.BeginAttack ( );
        }

        if ( _elapsedTime >= _attackDuration )
        {
            //명중하지 못해도 한 차례 시도 후 도주
            _enemy.ChangeState ( EnemyStateType.Escape );
            return;
        }

        //명중하면 남은 검사 시간을 기다리지 않고 도주
        if ( _enemy.CheckAttack ( ) == true )
            _enemy.ChangeState ( EnemyStateType.Escape );
    }

    /// <summary>
    /// 공격 검사 종료
    /// </summary>
    public void Exit ( )
    {
        _enemy.EndAttack ( );
    }
}

#endregion

#region ----- 수집 -----

/// <summary>
/// 공격 적 수집 상태 - 복수 아이템 확보 후 도주 전환
/// </summary>
public class HarmCollectState : IEnemyState
{
    EnemyHarm _enemy;

    /// <summary>
    /// 상태 종류
    /// </summary>
    public EnemyStateType StateType => EnemyStateType.Collect;


    /// <summary>
    /// 수집 상태 초기화
    /// </summary>
    /// <param name="enemy">공격 적</param>
    public HarmCollectState ( EnemyHarm enemy )
    {
        _enemy = enemy;
    }

    /// <summary>
    /// 아이템 수집 시작
    /// </summary>
    public void Enter ( )
    {
        _enemy.BeginCollect ( );
    }

    /// <summary>
    /// 수집 종료 후 도주 전환
    /// </summary>
    public void Update ( )
    {
        if ( _enemy.UpdateCollect ( Time.deltaTime ) == true )
            return;

        //집기 중 비활성화되거나 다른 상태로 바뀌었다면 전환하지 않음
        if ( _enemy.isActiveAndEnabled == false ||
             _enemy.CurrentState != this )
            return;

        //용량 부족, 시간 만료와 후보 없음 모두 확보한 만큼 들고 도주
        _enemy.ChangeState ( EnemyStateType.Escape );
    }

    /// <summary>
    /// 수집 이동 종료
    /// </summary>
    public void Exit ( )
    {
        _enemy.EndCollect ( );
    }
}

#endregion

#region ----- 도주 -----

/// <summary>
/// 공격 적 도주 상태 - 경로 이동과 보유 결과 처리
/// </summary>
public class HarmEscapeState : IEnemyState
{
    EnemyHarm _enemy;     //도주와 보유 결과 처리
    RouteStatusType _escapeStatus;     //현재 도주 이동 상태

    /// <summary>
    /// 상태 종류
    /// </summary>
    public EnemyStateType StateType => EnemyStateType.Escape;


    /// <summary>
    /// 공격 적 연결
    /// </summary>
    /// <param name="enemy">도주할 공격 적</param>
    public HarmEscapeState ( EnemyHarm enemy )
    {
        _enemy = enemy;
    }

    /// <summary>
    /// 도주 경로 이동 시작
    /// </summary>
    public void Enter ( )
    {
        _escapeStatus = _enemy.BeginEscape ( );
    }

    /// <summary>
    /// 도주 결과 처리 후 잠복 전환
    /// </summary>
    public void Update ( )
    {
        //재계산 대기와 회전도 Navigator에서 함께 처리
        if ( _escapeStatus == RouteStatusType.Moving )
            _escapeStatus = _enemy.UpdateEscape ( Time.deltaTime );

        if ( _enemy.isActiveAndEnabled == false ||
             _enemy.CurrentState != this )
            return;

        bool completed;

        switch ( _escapeStatus )
        {
            case RouteStatusType.Arrived:
                //실제 출구 도착 후에만 보유 아이템 손실 확정
                completed = _enemy.CompleteEscape ( );
                break;

            case RouteStatusType.Failed:
            case RouteStatusType.Cancelled:
                //물건을 모두 해제하기 전에는 잠복하지 않음
                completed = _enemy.EndFailedEscape ( );
                break;

            default:
                return;
        }

        //물건 처리 중 상태가 바뀌었으면 전환하지 않음
        if ( completed && _enemy.isActiveAndEnabled &&
             _enemy.CurrentState == this )
        {
            _enemy.ChangeState ( EnemyStateType.Hide );
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
