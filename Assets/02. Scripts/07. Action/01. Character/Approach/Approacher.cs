using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 목표 접근 모듈 - 목적지 갱신과 이동 결과 판정
/// </summary>
public class Approacher
{
    float _moveSpeed;		//이동 속력
    float _rotateSpeed;		//회전 속력
    float _arriveDist;		//도착 허용 거리
    float _sampleDist;		//NavMesh 위치 탐색 거리
    float _refreshInterval;		//목적지 갱신 간격
    float _refreshElapsed;		//목적지 갱신 경과 시간
    float _maxHeightDiff;		//허용 높이 차이
    Vector3 _movePosition;		//최근 NavMesh 목적지

    CharacterMover _mover;		//이동 모듈
    Transform _target;		//접근 목표

    ApproachModel _model;		//접근 판정 모델


#if UNITY_EDITOR
    float _debugRemaining;		//최근 판정에 사용한 남은 경로 거리
    bool _debugPathPending;		//최근 판정의 경로 계산 상태
    bool _debugHasPath;		//최근 판정의 경로 보유 상태
    NavMeshPathStatus _debugPathStatus;		//최근 판정의 경로 종류
    float _debugArriveTolerance;		//접근 정지 위치 오차
#endif


    /// <summary>
    /// 접근 상태
    /// </summary>
    public ApproachStatus Status => _model.Status;

    /// <summary>
    /// 접근 설정, 이동 모듈 연결
    /// </summary>
    /// <param name="mover">초기화된 이동 모듈</param>
    /// <param name="moveSpeed">이동 속력</param>
    /// <param name="rotateSpeed">회전 속력</param>
    /// <param name="arriveDist">도착 허용 거리</param>
    /// <param name="sampleDist">NavMesh 위치 탐색 거리</param>
    /// <param name="refreshInterval">목적지 갱신 간격(초)</param>
    /// <param name="timeLimit">접근 제한 시간(초)</param>
    /// <param name="maxHeightDiff">허용 높이 차이, 음수면 직선 거리 사용</param>
    /// <param name="arriveTolerance">정지 위치 오차 허용 거리</param>
    public Approacher (
        CharacterMover mover, float moveSpeed, float rotateSpeed,
        float arriveDist, float sampleDist,
        float refreshInterval, float timeLimit,
        float maxHeightDiff = -1f, float arriveTolerance = 0f )
    {
        _mover = mover;
        _moveSpeed = moveSpeed;
        _rotateSpeed = rotateSpeed;
        _arriveDist = arriveDist;
        _sampleDist = sampleDist;
        _refreshInterval = refreshInterval;
        _maxHeightDiff = maxHeightDiff;

#if UNITY_EDITOR
        _debugArriveTolerance = arriveTolerance;
#endif

        _model = new ApproachModel( timeLimit, arriveDist, arriveTolerance );
    }


    #region ----- 접근 -----

    /// <summary>
    /// 목표 주변 NavMesh로 이동 요청
    /// </summary>
    /// <returns>이동 요청 성공 여부</returns>
    bool RequestMove ()
    {
        //목표가 NavMesh 밖에 있어도 주변 위치로 접근
        if ( _mover.TryGetMovePosition(
            _target.position, _sampleDist, out Vector3 position ) == false )
        {
            _model.Fail( $"목표 주변 NavMesh 조회 실패 / 목표: {_target.name}" );
            return false;
        }

        _movePosition = position;
#if UNITY_EDITOR
        //새 요청 실패 시에도 이전 접근 판정값을 표시하지 않음
        _debugRemaining = float.PositiveInfinity;
        _debugPathPending = false;
        _debugHasPath = false;
        _debugPathStatus = NavMeshPathStatus.PathInvalid;
#endif
        if ( _mover.MoveTo( position, _moveSpeed, _arriveDist ) == false )
        {
            _model.Fail( $"목적지 이동 요청 실패 / 목표: {_target.name}" );
            return false;
        }

#if UNITY_EDITOR
        _debugRemaining = _mover.RemainingDistance;
        _debugPathPending = _mover.PathPending;
        _debugHasPath = _mover.HasPath;
        _debugPathStatus = _mover.PathStatus;
#endif
        return true;
    }

    /// <summary>
    /// 목표 접근 시작
    /// </summary>
    /// <param name="target">접근할 목표</param>
    public void Begin ( Transform target )
    {
        //이전 경로를 이어가지 않도록 먼저 정지
        _mover.Stop( );
        _target = target;
        _refreshElapsed = 0f;
        _model.Begin( );

        if ( _target == null || _target.gameObject.activeInHierarchy == false )
        {
            _model.Fail( "목표가 없거나 비활성화되어 있음" );
            LogFailure( );
            return;
        }


        if ( RequestMove( ) == false )
            LogFailure( );
    }

    /// <summary>
    /// 접근 중단과 목표 정리
    /// </summary>
    public void Stop ()
    {
        _mover.Stop( );
        _target = null;
        _model.Stop( );
    }

    /// <summary>
    /// 목표 접근 진행과 도착/실패 판정
    /// </summary>
    /// <param name="deltaTime">경과 시간(초)</param>
    /// <returns>접근 상태</returns>
    public ApproachStatus UpdateMove ( float deltaTime )
    {
        if ( _model.Status != ApproachStatus.Moving )
            return _model.Status;

        if ( _target == null || _target.gameObject.activeInHierarchy == false )
        {
            _model.Fail( "이동 중 목표가 사라지거나 비활성화됨" );
            LogFailure( );
            _mover.Stop( );
            return _model.Status;
        }

        //이동 기능을 사용할 수 없으면 경로 조회 전에 실패
        if ( _mover.CanNavigate == false )
        {
            _model.Fail( "이동 중 이동 기능을 사용할 수 없음" );
            LogFailure( );
            _mover.Stop( );
            return _model.Status;
        }

        Vector3 offset = _target.position - _mover.Position;
        float heightDiff = Mathf.Abs( offset.y );
        float targetDist = offset.magnitude;

        //높이 제한을 지정하면 수평 거리로 판정
        //제한 초과 시 도착 차단
        if ( _maxHeightDiff >= 0f )
        {
            offset.y = 0f;
            targetDist = heightDiff <= _maxHeightDiff
                ? offset.magnitude
                : float.PositiveInfinity;
        }

        float remainingDist = _mover.RemainingDistance;
        bool pathPending = _mover.PathPending;
        bool hasPath = _mover.HasPath;

#if UNITY_EDITOR
        //도착과 실패로 이동을 정지하기 전에 판정값 보관
        _debugRemaining = remainingDist;
        _debugPathPending = pathPending;
        _debugHasPath = hasPath;
        _debugPathStatus = _mover.PathStatus;
#endif

        _model.UpdateMove(
            deltaTime,
            pathPending,
            hasPath,
            _mover.PathStatus == NavMeshPathStatus.PathComplete,
            remainingDist,
            targetDist );

        if ( _model.Status != ApproachStatus.Moving )
        {
            if ( _model.Status == ApproachStatus.Failed )
            {
                LogFailure( );
            }

            _mover.Stop( );
            return _model.Status;
        }

        _refreshElapsed += deltaTime;

        //경로 계산을 중복 요청하지 않도록 완료 후 갱신
        if ( _mover.PathPending == false &&
             _refreshElapsed >= _refreshInterval )
        {
            _refreshElapsed = 0f;

            if ( RequestMove( ) == false )
            {
                LogFailure( );
                _mover.Stop( );
                return _model.Status;
            }
        }

        _mover.RotateAlongMovement( _rotateSpeed );
        return _model.Status;
    }

    #endregion

    /// <summary>
    /// 접근 목표와 실패 이유 출력
    /// </summary>
    void LogFailure ()
    {
        string targetName = _target == null ? "없음" : _target.name;
        string targetPosition = _target == null ? "없음" : _target.position.ToString( );

        Debug.Log(
            $"[접근 실패] {_mover.name} / 목표: {targetName} / {_model.FailureReason} / " +
            $"적 위치: {_mover.Position} / 목표 위치: {targetPosition} / " +
            $"최근 NavMesh 목적지: {_movePosition} / 이동 가능: {_mover.CanNavigate}",
            _mover );
    }

#if UNITY_EDITOR
    /// <summary>
    /// 정지 전 경로 상태와 현재 목표 거리 조회
    /// </summary>
    /// <returns>접근 판정에 사용한 거리와 경로 상태</returns>
    public string GetDebugDetails ()
    {
        Vector3 offset = _target != null ? _target.position - _mover.Position : Vector3.zero;
        float height = Mathf.Abs( offset.y );		//목표와의 높이 차이
        float distance = offset.magnitude;		//목표와의 직선 거리
        if ( _maxHeightDiff >= 0f )
        {
            offset.y = 0f;
            distance = offset.magnitude;
        }

        return $"목표: {(_target != null ? _target.name : "없음")} / 상태: {_model.Status} / " +
            $"목표 거리: {distance:F2} / 도착 허용: {_arriveDist + _debugArriveTolerance:F2} / " +
            $"높이 차: {height:F2} / 허용 높이: {_maxHeightDiff:F2} / " +
            $"남은 경로: {_debugRemaining:F2} / 계산 중: {_debugPathPending} / " +
            $"경로 보유: {_debugHasPath} / 경로 종류: {_debugPathStatus} / " +
            $"적 위치: {_mover.Position} / 목표 위치: {(_target != null ? _target.position.ToString( ) : "없음")} / " +
            $"NavMesh 목적지: {_movePosition} / 실패 사유: {_model.FailureReason}";
    }
#endif

}
