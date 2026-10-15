using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 경로 이동 모듈 - 경유점 이동, 도착과 재계산 처리
/// </summary>
public class RouteNavigator
{
    CharacterMover _mover;     //실제 이동 모듈
    RouteBuilder _builder;     //경로 구성 모듈
    /// <summary>
    /// 순서대로 이동할 경유점과 노드 정보
    /// </summary>
    List<RoutePointData> _points = new ( );

    bool _usesRequiredNodes;		//선정한 전체 입구 도주 경로의 순서 유지 여부

    float _moveSpeed;     //이동 속력
    float _rotateSpeed;     //회전 속력
    float _arriveDist;     //에이전트 정지 거리
    float _arriveTolerance;     //정지 위치 오차 허용 거리
    float _retryInterval;     //재계산 대기 시간
    float _timeLimit;     //전체 이동 제한시간
    float _stuckTimeLimit;     //진행 없이 기다릴 최대 시간

    Vector3 _destination;     //요청받은 최종 목적지
    int _pointIndex;     //현재 경유점 번호

    bool _usesConnectedNodes;		//연결된 두 노드 이동 여부
    RoutePointData _firstPoint;		//처음 선정한 첫 경유점
    RoutePointData _destinationPoint;		//처음 선정한 도주 완료 경유점
    bool _firstPointPassed;		//첫 경유점 통과 여부
    bool _savedAutoBraking;		//경유점 이동 전 자동 감속 설정
    bool _hasBrakingOverride;		//자동 감속 복구 필요 여부
    Vector3 _previousPosition;		//직전 이동 갱신 위치

    float _elapsed;     //전체 경과 시간
    float _stuckElapsed;     //진행이 없는 경과 시간
    float _bestRemaining;     //현재 구간에서 가장 짧았던 남은 거리
    float _retryRemaining;     //재계산까지 남은 시간
    bool _waitingForRetry;     //재계산 대기 여부

    RouteStatusType _status;     //현재 이동 상태
    string _failureReason = string.Empty;     //최근 실패 사유

    const float ProgressDistance = 0.01f;     //이동 진행을 인정할 최소 거리

    /// <summary>
    /// 현재 이동 상태
    /// </summary>
    public RouteStatusType Status => _status;

    /// <summary>
    /// 최근 실패 사유
    /// </summary>
    public string FailureReason => _failureReason;

    /// <summary>
    /// 연결된 두 노드 이동 여부
    /// </summary>
    public bool UsesConnectedNodes => _usesConnectedNodes;

    /// <summary>
    /// 선정한 전체 입구 도주 경로의 순서를 유지하는지 여부
    /// </summary>
    public bool UsesRequiredNodes => _usesRequiredNodes;

    /// <summary>
    /// 현재 경유점을 정지 없이 통과하는지 여부
    /// </summary>
    bool PassThrough => CheckPassThroughPoint( );


    /// <summary>
    /// 이동 기능과 설정 연결
    /// </summary>
    /// <param name="mover">초기화된 이동 모듈</param>
    /// <param name="builder">경로 구성 모듈</param>
    /// <param name="moveSpeed">이동 속력</param>
    /// <param name="rotateSpeed">회전 속력</param>
    /// <param name="arriveDist">에이전트 정지 거리</param>
    /// <param name="retryInterval">경로 재계산 대기 시간</param>
    /// <param name="timeLimit">전체 이동 제한시간</param>
    /// <param name="stuckTimeLimit">이동 정체 허용 시간</param>
    /// <param name="arriveTolerance">정지 위치 오차 허용 거리</param>
    public RouteNavigator (
        CharacterMover mover , RouteBuilder builder ,
        float moveSpeed , float rotateSpeed , float arriveDist ,
        float retryInterval , float timeLimit , float stuckTimeLimit ,
        float arriveTolerance = 0.05f )
    {
        _mover = mover;
        _builder = builder;
        _moveSpeed = moveSpeed;
        _rotateSpeed = rotateSpeed;
        _arriveDist = arriveDist;
        _arriveTolerance = arriveTolerance;
        _retryInterval = retryInterval;
        _timeLimit = timeLimit;
        _stuckTimeLimit = stuckTimeLimit;
    }


    #region ----- 종료/재시도 -----

    /// <summary>
    /// 경유점 이동 전 자동 감속 설정 복구
    /// </summary>
    void RestoreAutoBraking ()
    {
        if ( _hasBrakingOverride == false )
            return;

        //적과 에이전트가 함께 파괴된 경우에는 복구 대상이 없음
        if ( _mover != null )
            _mover.AutoBraking = _savedAutoBraking;
        _hasBrakingOverride = false;
    }

    /// <summary>
    /// 활성 에이전트의 이동 정지
    /// </summary>
    void StopMovement ( )
    {
        //적 비활성화 이후 취소돼도 에이전트에 잘못된 명령을 보내지 않음
        if ( _mover != null && _mover.CanNavigate )
            _mover.Stop ( );
    }

    /// <summary>
    /// 실패 사유를 기록하고 이동 종료
    /// </summary>
    /// <param name="reason">실패 설명</param>
    void Fail ( string reason )
    {
        _failureReason = reason;
        _status = RouteStatusType.Failed;
        _waitingForRetry = false;
        StopMovement ( );
        RestoreAutoBraking( );
    }

    /// <summary>
    /// 현재 경로 이동 취소
    /// </summary>
    public void Cancel ( )
    {
        RestoreAutoBraking( );
        //이미 종료된 이동에서 다른 기능의 새 이동까지 정지시키지 않음
        if ( _status == RouteStatusType.Moving )
            StopMovement ( );

        _status = RouteStatusType.Cancelled;
        _failureReason = string.Empty;
        _waitingForRetry = false;
        _points.Clear ( );
        _pointIndex = 0;
        _usesRequiredNodes = false;
        _usesConnectedNodes = false;
        _firstPoint = default;
        _destinationPoint = default;
        _firstPointPassed = false;
    }

    /// <summary>
    /// 이동을 멈추고 경로 재계산 예약
    /// </summary>
    /// <param name="reason">재계산이 필요한 사유</param>
    void ScheduleRetry ( string reason )
    {
        //이동 중 그래프가 제거된 경우처럼 재시도로 복구할 수 없는 설정 오류는 종료
        if ( _builder.CheckSettings ( out string settingsReason ) == false )
        {
            Fail ( settingsReason );
            return;
        }

        _failureReason = reason;
        _waitingForRetry = true;
        _retryRemaining = _retryInterval;
        StopMovement ( );

        //재계산 대기도 전체 이동 제한시간에 포함
    }

    #endregion

    #region ----- 경로 요청 -----

    /// <summary>
    /// 현재 경유점으로 이동 요청
    /// </summary>
    void RequestPoint ( )
    {
        while ( _pointIndex < _points.Count )
        {
            //새 구간으로 이동하기 전에 원본 노드와 연결부터 확인
            if ( _builder.CheckSegment ( _points , _pointIndex ) == false )
            {
                ScheduleRetry ( "경유점 노드 또는 연결이 변경됐습니다." );
                return;
            }

            //좌표가 같은 구간도 연결은 검사하되 중복 이동 명령은 생략
            if ( _pointIndex == 0 ||
                 _points [ _pointIndex ].Position.Equals (
                     _points [ _pointIndex - 1 ].Position ) == false )
                break;

            _pointIndex++;
        }

        if ( _pointIndex >= _points.Count )
        {
            //입구 도주도 최종 입구를 목록에 포함하므로 목록 끝에서만 완료
            //중간 노드 통과는 다음 번호로 진행하며 전체 경로를 다시 구성하지 않음
            _status = RouteStatusType.Arrived;
            _failureReason = string.Empty;
            StopMovement ( );
            RestoreAutoBraking( );
#if UNITY_EDITOR
            if ( _usesConnectedNodes )
                PlayDebugHistory.Record( "납치 B 도착", _mover.GetComponent<Enemy>( ),
                    $"{GetDebugDetails( )} / 도착 오차: {Vector3.Distance( _mover.Position, _destinationPoint.Position ):F3}" );
            if ( _usesRequiredNodes == true )
                PlayDebugHistory.Record( "입구 도주 도착", _mover.GetComponent<Enemy>( ),
                    GetDebugDetails( ) );
#endif
            return;
        }

        _stuckElapsed = 0f;
        _bestRemaining = float.PositiveInfinity;

        bool passThrough = PassThrough;
        if ( _usesConnectedNodes == true || _usesRequiredNodes == true )
            _mover.AutoBraking = passThrough == false;
        _previousPosition = _mover.Position;

        if ( _mover.MoveTo (
            _points [ _pointIndex ].Position , _moveSpeed , passThrough ? 0f : _arriveDist ) == false )
        {
            ScheduleRetry ( "경유점 이동 요청에 실패했습니다." );
        }
    }

    /// <summary>
    /// 일반 경로 재구성 또는 선정한 입구 도주 구간 재요청
    /// </summary>
    void RebuildRoute ( )
    {
        _waitingForRetry = false;

        if ( _usesRequiredNodes == true )
        {
            //입구 도주의 재시도는 다음 미통과 지점까지 이동 가능한지만 확인
            //선정한 전체 목록, 통과 번호와 전체 경과 시간은 유지
            if ( _builder.CheckMoveSegment( _mover.Position, _points, _pointIndex ) == false )
            {
                if ( _builder.CanRetry == true )
                    ScheduleRetry( _builder.FailureReason );
                else
                    Fail( _builder.FailureReason );
                return;
            }

            RequestPoint( );
            return;
        }

        //일반 이동과 빈손 두 노드 도주는 기존 경로 구성 계약을 유지
        bool built;
        if ( _usesConnectedNodes == true )
        {
            //두 노드 요청은 처음 선정한 순서와 통과 상태 유지
            built = _builder.TryBuildConnectedNodes(
                _mover.Position, _firstPoint, _destinationPoint,
                _firstPointPassed, _points );
        }
        else
            built = _builder.TryBuild( _mover.Position, _destination, _points );

        if ( built == false )
        {
            if ( _builder.CanRetry )
                ScheduleRetry ( _builder.FailureReason );
            else
                Fail ( _builder.FailureReason );
            return;
        }

        if ( _points.Count == 0 )
        {
            Fail ( "경로 구성 결과에 이동할 경유점이 없습니다." );
            return;
        }

        _pointIndex = 0;
        //경로를 다시 얻어도 최근 실패 사유는 도착 또는 취소까지 보관
        RequestPoint ( );

        //새 경로를 얻어도 전체 경과 시간은 유지
    }

    /// <summary>
    /// 지정한 목적지로 경로 이동 시작
    /// </summary>
    /// <param name="destination">최종 목적지의 월드 위치</param>
    public void Begin ( Vector3 destination )
    {
        Cancel ( );

        _destination = destination;
        _elapsed = 0f;
        _failureReason = string.Empty;
        _status = RouteStatusType.Moving;

        if ( CheckSettings ( ) == false )
        {
            Fail ( "경로 이동 기능과 설정값을 확인해 주세요." );
            return;
        }

        RebuildRoute ( );
    }

    /// <summary>
    /// 전체 입구 도주 경로를 선정하고 첫 경유점 이동 시작
    /// </summary>
    /// <param name="destination">최종 입구 위치</param>
    public void BeginEntranceDetour ( Vector3 destination )
    {
        Cancel( );
        _destination = destination;
        _elapsed = 0f;
        _status = RouteStatusType.Moving;
        _usesRequiredNodes = true;

        if ( CheckSettings( ) == false )
        {
            Fail( "경로 이동 기능과 설정값을 확인해 주세요." );
            return;
        }
        if ( _builder.TrySelectEntranceDetour(
            _mover.Position, destination, _arriveDist + _arriveTolerance,
            _points ) == false )
        {
            Fail( _builder.FailureReason );
            return;
        }
        BeginSelectedRoute( );
    }

    /// <summary>
    /// 최소 이동 거리를 만족하는 도주 경로로 이동 시작
    /// </summary>
    /// <param name="destination">최종 목적지</param>
    /// <param name="minimumDistance">최소 이동 거리</param>
    public void BeginDistanceDetour ( Vector3 destination, float minimumDistance )
    {
        Cancel( );
        _destination = destination;
        _elapsed = 0f;
        _status = RouteStatusType.Moving;
        _usesRequiredNodes = true;

        if ( CheckSettings( ) == false ||
             IsFinite( minimumDistance ) == false || minimumDistance <= 0f )
        {
            Fail( "도주 이동 기능과 최소 거리 설정을 확인해 주세요." );
            return;
        }

        //속력과 제한시간으로 이동하기에도 너무 긴 후보 제외
        if ( _builder.TrySelectDistanceDetour(
            _mover.Position, destination, minimumDistance,
            _moveSpeed * _timeLimit, _points ) == false )
        {
            Fail( _builder.FailureReason );
            return;
        }

        BeginSelectedRoute( );
    }

    /// <summary>
    /// 선정한 전체 경로를 보관하고 첫 이동 요청
    /// </summary>
    void BeginSelectedRoute ()
    {
        //재시도에서도 전체 목록과 다음 미통과 지점 번호 유지
        _pointIndex = 0;
        _destination = _points [ _points.Count - 1 ].Position;
        _savedAutoBraking = _mover.AutoBraking;
        _hasBrakingOverride = true;
        RequestPoint( );
#if UNITY_EDITOR
        string route = string.Empty;
        foreach ( RoutePointData point in _points )
        {
            string pointName = point.HasNode == true ? point.Node.name : "입구";
            route += $"{pointName} {point.Position} → ";
        }
        PlayDebugHistory.Record( "입구 도주 경로 선정", _mover.GetComponent<Enemy>( ),
            $"{route} / {_builder.DebugSelection} / {GetDebugDetails( )}" );
#endif
    }

    /// <summary>
    /// 가까운 첫 노드를 거쳐 연결된 도주 완료 노드로 이동 시작
    /// </summary>
    public void BeginConnectedNodes ()
    {
        Cancel( );
        _elapsed = 0f;
        _status = RouteStatusType.Moving;
        _usesConnectedNodes = true;

        if ( CheckSettings( ) == false )
        {
            Fail( "경로 이동 기능과 설정값을 확인해 주세요." );
            return;
        }

        if ( _builder.TrySelectConnectedNodes(
            _mover.Position, _mover.Heading, _arriveDist + _arriveTolerance,
            out _firstPoint, out _destinationPoint ) == false )
        {
            Fail( _builder.FailureReason );
            return;
        }

        //선정 과정에서 검사한 경로를 사용하고 첫 요청에서 반복 계산하지 않음
        _savedAutoBraking = _mover.AutoBraking;
        _hasBrakingOverride = true;
        _points.Add( _firstPoint );
        _points.Add( _destinationPoint );
        _pointIndex = 0;
        RequestPoint( );
#if UNITY_EDITOR
        PlayDebugHistory.Record( "납치 도주 선정", _mover.GetComponent<Enemy>( ),
            $"{_builder.DebugSelection} / {GetDebugDetails( )}" );
#endif
    }

    #endregion

    #region ----- 이동 갱신 -----

    /// <summary>
    /// 현재 위치 또는 직전 이동 구간의 중간 경유점 통과 여부 확인
    /// </summary>
    /// <param name="point">중간 경유점 위치</param>
    /// <param name="distance">통과 허용 거리</param>
    /// <returns>통과 범위에 들어왔거나 지나갔는지 여부</returns>
    bool CheckPassThrough ( Vector3 point, float distance )
    {
        Vector3 movement = _mover.Position - _previousPosition;
        float lengthSqr = movement.sqrMagnitude;
        //프레임 사이에 통과 범위를 지나친 경우도 인정
        float progress = lengthSqr > 0f
            ? Mathf.Clamp01( Vector3.Dot( point - _previousPosition, movement ) / lengthSqr )
            : 0f;
        Vector3 closest = _previousPosition + movement * progress;
        return ( point - closest ).sqrMagnitude <= distance * distance;
    }

    /// <summary>
    /// 다음 경유점 선택 또는 최종 도착 처리
    /// </summary>
    void AdvancePoint ( )
    {
        bool passedFirst = _usesConnectedNodes && _firstPointPassed == false &&
            _points [ _pointIndex ].Node == _firstPoint.Node;
        if ( passedFirst )
            _firstPointPassed = true;

#if UNITY_EDITOR
        if ( _usesRequiredNodes == true )
        {
            RoutePointData point = _points [ _pointIndex ];
            string pointName = point.HasNode == true ? point.Node.name : "입구";
            PlayDebugHistory.Record( "입구 도주 경유점 통과", _mover.GetComponent<Enemy>( ),
                $"통과 지점: {pointName} / {GetDebugDetails( )}" );
        }
#endif
        //입구 도주도 현재 번호만 증가시켜 다음 선정 지점으로 진행
        _pointIndex++;
#if UNITY_EDITOR
        if ( passedFirst )
            PlayDebugHistory.Record( "납치 A 통과", _mover.GetComponent<Enemy>( ),
                $"{GetDebugDetails( )} / 다음 목적지: {_destinationPoint.Position}" );
#endif
        //다음 구간 검사와 최종 도착 처리는 이동 요청에서 함께 수행
        RequestPoint ( );
    }

    /// <summary>
    /// 현재 경로의 이동 진행 확인
    /// </summary>
    /// <param name="deltaTime">경과 시간</param>
    void UpdateProgress ( float deltaTime )
    {
        //이동 도중의 노드 제거와 연결 변경도 감지하며 NavMesh 재탐색은 하지 않음
        if ( _builder.CheckSegment ( _points , _pointIndex ) == false )
        {
            ScheduleRetry ( "경유점 노드 또는 연결이 변경됐습니다." );
            return;
        }

        _stuckElapsed += deltaTime;

        //경로 계산이 끝나지 않는 경우도 무한정 기다리지 않음
        if ( _mover.PathPending )
        {
            if ( _stuckElapsed >= _stuckTimeLimit )
                ScheduleRetry ( "경로 계산 대기 시간이 초과됐습니다." );

            return;
        }

        if ( _mover.IsPathStale )
        {
            ScheduleRetry ( "현재 이동 경로가 무효화됐습니다." );
            return;
        }

        float positionDistance = Vector3.Distance (
            _mover.Position , _points [ _pointIndex ].Position );

        if ( _mover.HasPath == false )
        {
            //경로가 없으면 잔여 거리를 신뢰할 수 없으므로 완전 경로를 다시 확인
            //벽 너머의 가까운 지점을 도착으로 처리하지 않음
            float arriveDistance = _arriveDist + _arriveTolerance;
            if ( positionDistance <= arriveDistance &&
                 _builder.CheckArrival ( _mover.Position ,
                     _points [ _pointIndex ].Position , arriveDistance ) )
                AdvancePoint ( );
            else
                ScheduleRetry ( "이동 경로가 사라졌습니다." );

            return;
        }

        //부분 경로는 목적지와 가까워도 도착으로 처리하지 않음
        if ( _mover.PathStatus != NavMeshPathStatus.PathComplete )
        {
            ScheduleRetry ( "목적지까지 완전한 이동 경로가 없습니다." );
            return;
        }

        float remaining = _mover.RemainingDistance;

        //완전 경로를 확인한 뒤 중간 경유점은 정지하지 않고 통과 범위로 판단
        //잔여 경로도 비교하여 벽 너머의 가까운 위치를 통과로 처리하지 않음
        if ( PassThrough &&
             remaining <= _arriveDist + _arriveTolerance +
                 Vector3.Distance( _previousPosition, _mover.Position ) &&
             CheckPassThrough( _points [ _pointIndex ].Position, _arriveDist + _arriveTolerance ) )
        {
            AdvancePoint( );
            return;
        }

        //경로 잔여 거리와 실제 위치를 함께 확인
        if ( CheckArrival ( remaining , positionDistance ) )
        {
            AdvancePoint ( );
            return;
        }

        //작은 흔들림 대신 누적된 거리 감소로 진행 여부 판단
        if ( remaining < _bestRemaining - ProgressDistance )
        {
            _bestRemaining = remaining;
            _stuckElapsed = 0f;
        }

        if ( _stuckElapsed >= _stuckTimeLimit )
        {
            ScheduleRetry ( "이동이 정체되어 경로를 다시 계산합니다." );
            return;
        }

        _mover.RotateAlongMovement ( _rotateSpeed );
    }

    /// <summary>
    /// 경로 이동과 재계산 대기 갱신
    /// </summary>
    /// <param name="deltaTime">경과 시간</param>
    /// <returns>현재 이동 상태</returns>
    public RouteStatusType UpdateMove ( float deltaTime )
    {
        if ( _status != RouteStatusType.Moving )
            return _status;

        if ( _mover == null || _mover.CanNavigate == false )
        {
            Fail ( "이동 에이전트를 사용할 수 없습니다." );
            return _status;
        }

        if ( IsFinite ( deltaTime ) == false || deltaTime < 0f )
        {
            Fail ( "경과 시간이 유효하지 않습니다." );
            return _status;
        }

        _elapsed += deltaTime;

        if ( _elapsed >= _timeLimit )
        {
            //재시도 원인을 시간 초과 문구로 덮어쓰지 않음
            string reason = "전체 경로 이동 제한시간이 초과됐습니다.";
            if ( string.IsNullOrEmpty ( _failureReason ) == false )
                reason += $" 최근 재시도 사유: {_failureReason}";

            Fail ( reason );
            return _status;
        }

        if ( _waitingForRetry )
        {
            _retryRemaining -= deltaTime;

            if ( _retryRemaining <= 0f )
                RebuildRoute ( );

            return _status;
        }

        UpdateProgress ( deltaTime );
        _previousPosition = _mover.Position;
        return _status;
    }

    #endregion

    #region ----- 검사 -----

    /// <summary>
    /// 유한한 수인지 확인
    /// </summary>
    /// <param name="value">검사할 값</param>
    /// <returns>유효한 수 여부</returns>
    bool IsFinite ( float value )
    {
        return float.IsNaN ( value ) == false &&
               float.IsInfinity ( value ) == false;
    }

    /// <summary>
    /// 양수 설정값 확인
    /// </summary>
    /// <param name="value">검사할 값</param>
    /// <returns>유한한 양수 여부</returns>
    bool IsPositive ( float value )
    {
        return IsFinite ( value ) && value > 0f;
    }

    /// <summary>
    /// 잔여 거리와 실제 위치로 도착 확인
    /// </summary>
    /// <param name="remaining">완전 경로의 잔여 거리</param>
    /// <param name="positionDistance">경유점까지의 실제 거리</param>
    /// <returns>두 거리가 도착 허용 범위 안인지 여부</returns>
    bool CheckArrival ( float remaining , float positionDistance )
    {
        if ( IsFinite ( remaining ) == false || remaining < 0f ||
             IsFinite ( positionDistance ) == false )
            return false;

        float arriveDistance = _arriveDist + _arriveTolerance;
        return remaining <= arriveDistance && positionDistance <= arriveDistance;
    }

    /// <summary>
    /// 이동 기능과 설정 확인
    /// </summary>
    /// <returns>이동 시작 가능 여부</returns>
    bool CheckSettings ( )
    {
        if ( _mover == null || _mover.CanNavigate == false ||
             _builder == null )
            return false;

        if ( IsPositive ( _moveSpeed ) == false ||
             IsPositive ( _arriveDist ) == false ||
             IsPositive ( _retryInterval ) == false ||
             IsPositive ( _timeLimit ) == false ||
             IsPositive ( _stuckTimeLimit ) == false )
            return false;

        if ( IsFinite ( _arriveTolerance ) == false || _arriveTolerance < 0f ||
             IsFinite ( _arriveDist + _arriveTolerance ) == false )
            return false;

        return IsFinite ( _rotateSpeed ) && _rotateSpeed >= 0f;
    }

    /// <summary>
    /// 현재 구간이 최종 도착 전 통과할 경유점인지 확인
    /// </summary>
    /// <returns>정지 없이 통과할 경유점 여부</returns>
    bool CheckPassThroughPoint ()
    {
        return ( _usesConnectedNodes == true && _firstPointPassed == false ) ||
            ( _usesRequiredNodes == true && _pointIndex < _points.Count - 1 );
    }

    #endregion
#if UNITY_EDITOR
    /// <summary>
    /// 선택한 도주 경유점과 이동 결과 조회
    /// </summary>
    /// <returns>이동 방식, 목적지와 진행 상태</returns>
    public string GetDebugDetails ()
    {
        string route = _usesConnectedNodes == true
            ? $"A: {(_firstPoint.Node != null ? _firstPoint.Node.name : "없음")} {_firstPoint.Position} / " +
              $"B: {(_destinationPoint.Node != null ? _destinationPoint.Node.name : "없음")} {_destinationPoint.Position} / A 통과: {_firstPointPassed}"
            : $"지정 목적지: {_destination}";
        if ( _usesRequiredNodes == true )
        {
            string next = "완료";
            if ( _pointIndex < _points.Count )
            {
                RoutePointData point = _points [ _pointIndex ];
                next = point.HasNode == false ? "입구"
                    : ( point.Node != null ? point.Node.name : "제거된 노드" );
            }
            route = $"입구 도주 / 다음: {next} / 통과: {_pointIndex}/{_points.Count} / 입구: {_destination}";
        }
        string movement = _mover.CanNavigate == true
            ? $"속도: {_mover.Velocity} / 남은 경로: {_mover.RemainingDistance:F2} / 계산 중: {_mover.PathPending}"
            : "이동 에이전트 비활성";
        return $"{route} / 실제 위치: {_mover.Position} / {movement} / " +
            $"상태: {_status} / 통과 경유점: {_pointIndex}/{_points.Count} / " +
            $"경과: {_elapsed:F2}/{_timeLimit:F2}초 / 실패 사유: {_failureReason}";
    }
#endif

}
