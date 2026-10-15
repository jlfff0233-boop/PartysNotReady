using UnityEngine;

#region ----- 경유점 이동 -----

/// <summary>
/// 아이템 절도 적 이동 상태 - 경유점 진행과 차단 재시도
/// </summary>
public class StealRouteState : IEnemyState
{
    EnemyStateType _stateType;		//침입 또는 탈출
    int _pointIndex;		//현재 경유점 번호
    bool _wasBlocked;		//이동 차단 여부
    float _retryInterval;		//이동 재시도 간격
    float _failureTime;		//지속 차단 제한시간
    float _blockedElapsed;		//연속 차단 경과 시간
    float _retryRemaining;		//재시도까지 남은 시간
    bool _hasFailed;		//이동 실패 확정 여부
    Vector3 _risePosition;		//탈출 시작 시 상승 위치

    EnemySteal _enemy;		//아이템 절도 적
    Transform [ ] _points;		//이동 경유점

    /// <summary>
    /// 상태 타입
    /// </summary>
    public EnemyStateType StateType => _stateType;

    /// <summary>
    /// 경로 이동 상태 초기화
    /// </summary>
    /// <param name="enemy">아이템 절도 적</param>
    /// <param name="points">이동 경유점</param>
    /// <param name="stateType">침입 또는 탈출 상태</param>
    /// <param name="retryInterval">이동 재시도 간격</param>
    /// <param name="failureTime">지속 차단 제한시간</param>
    public StealRouteState (
        EnemySteal enemy, Transform [ ] points, EnemyStateType stateType,
        float retryInterval, float failureTime )
    {
        _enemy = enemy;
        _points = points;
        _stateType = stateType;
        _retryInterval = retryInterval;
        _failureTime = failureTime;
    }

    /// <summary>
    /// 경로 이동 시작
    /// </summary>
    public void Enter ()
    {
        //탈출은 먼저 위로 상승하여 배치면에서 벗어남
        _pointIndex = _stateType == EnemyStateType.Escape ? -1 : 0;
        _risePosition = _enemy.GetEscapeRisePosition( );
        _wasBlocked = false;
        _blockedElapsed = 0f;
        _retryRemaining = 0f;
        _hasFailed = false;
        _enemy.Stop( );
    }

    /// <summary>
    /// 경로 이동 종료
    /// </summary>
    public void Exit ()
    {
        _enemy.Stop( );
    }

    /// <summary>
    /// 경유점 이동과 재시도/실패 종료
    /// </summary>
    public void Update ()
    {
        if ( _hasFailed == true )
        {
            _enemy.EndFailedMove( );
            return;
        }

        //시작 검사 이후 소실된 경유점은 완료로 건너뛰지 않음
        if ( _pointIndex >= 0 && _points [ _pointIndex ] == null )
        {
            Fail( $"{_pointIndex + 1}번 경유점 소실" );
            return;
        }

        if ( _wasBlocked == true )
        {
            _blockedElapsed += Time.deltaTime;
            if ( _blockedElapsed >= _failureTime )
            {
                Fail( "이동 경로의 지속 차단" );
                return;
            }

            //일시 차단은 간격을 두고 재시도
            _retryRemaining -= Time.deltaTime;
            if ( _retryRemaining > 0f )
                return;
        }

        Vector3 destination = _pointIndex < 0
            ? _risePosition
            : _points [ _pointIndex ].position;

        //마지막 경유점은 종료 정리가 끝날 때까지 도착 여부 재확인
        if ( _enemy.HasArrived( destination ) == true )
        {
            _wasBlocked = false;
            _blockedElapsed = 0f;
            _retryRemaining = 0f;

            if ( _pointIndex < _points.Length - 1 )
                _pointIndex++;
            else if ( _stateType == EnemyStateType.Enter )
                _enemy.CompleteEnter( );
            else
                _enemy.CompleteEscape( );
            return;
        }

        bool moved = _enemy.MoveTo( destination );

        //같은 차단이 이어지는 동안 경고 중복 방지
        if ( moved == false && _wasBlocked == false )
        {
            Debug.LogWarning(
                $"아이템 절도 적 {_stateType} 경로 차단: " +
                $"{destination}", _enemy );
        }

        if ( moved == true )
        {
            _wasBlocked = false;
            _blockedElapsed = 0f;
            _retryRemaining = 0f;
            return;
        }

        _wasBlocked = true;
        _retryRemaining = _retryInterval;
    }

    /// <summary>
    /// 이동 실패 확정과 종료
    /// </summary>
    /// <param name="reason">실패 사유</param>
    void Fail ( string reason )
    {
        _hasFailed = true;
        _enemy.Stop( );
        Debug.LogWarning( $"[절도 이동 실패] {_enemy.name} / {_stateType}: {reason}", _enemy );
        _enemy.EndFailedMove( );
    }

}

#endregion

#region ----- 음식 탐색 -----

/// <summary>
/// 아이템 절도 적 탐색 상태 - 음식 탐색과 접근
/// </summary>
public class StealSearchState : IEnemyState
{
    EnemySteal _enemy;		//아이템 절도 적

    /// <summary>
    /// 상태 타입
    /// </summary>
    public EnemyStateType StateType => EnemyStateType.Normal;

    /// <summary>
    /// 음식 탐색 상태 초기화
    /// </summary>
    /// <param name="enemy">아이템 절도 적</param>
    public StealSearchState ( EnemySteal enemy )
    {
        _enemy = enemy;
    }

    /// <summary>
    /// 음식 탐색 시작
    /// </summary>
    public void Enter ()
    {
        _enemy.BeginSearch( );
    }

    /// <summary>
    /// 음식 탐색 종료
    /// </summary>
    public void Exit ()
    {
        _enemy.Stop( );
    }

    /// <summary>
    /// 음식 탐색 및 접근
    /// </summary>
    public void Update ()
    {
        _enemy.UpdateSearch( );
    }
}

#endregion
