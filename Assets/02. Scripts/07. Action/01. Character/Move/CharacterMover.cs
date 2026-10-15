using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 캐릭터 이동 모듈 - 이동과 회전 적용, 경로 정보 조회
/// </summary>
[RequireComponent ( typeof ( NavMeshAgent ) )]
public class CharacterMover : MonoBehaviour
{
    AnimatorHandler _animatorHandler;		//동작 애니메이션

    protected NavMeshAgent _agent;     //이동 에이전트
    RoutePathChecker _pathChecker;     //현재 에이전트 조건의 NavMesh 검사

    /// <summary>
    /// 이동 위치와 경로에 공통으로 사용하는 검사기
    /// </summary>
    public RoutePathChecker PathChecker => _pathChecker;

    /// <summary>
    /// 현재 월드 위치
    /// </summary>
    public Vector3 Position => transform.position;

    /// <summary>
    /// 현재 월드 이동 속도 벡터
    /// </summary>
    public Vector3 Velocity => _agent.velocity;

    /// <summary>
    /// 이동 중에는 속도 방향, 정지 중에는 바라보는 방향
    /// </summary>
    public Vector3 Heading => _agent.velocity.sqrMagnitude > 0f
        ? _agent.velocity.normalized : transform.forward;

    /// <summary>
    /// 목적지 근처 자동 감속 여부
    /// </summary>
    public bool AutoBraking
    {
        get => _agent.autoBraking;
        set => _agent.autoBraking = value;
    }


    /// <summary>
    /// 경로 계산 중 여부
    /// </summary>
    public bool PathPending => _agent.pathPending;

    /// <summary>
    /// 경로 보유 여부
    /// </summary>
    public bool HasPath => _agent.hasPath;

    /// <summary>
    /// 현재 경로 상태
    /// </summary>
    public NavMeshPathStatus PathStatus => _agent.pathStatus;

    /// <summary>
    /// 현재 경로의 남은 거리
    /// </summary>
    public float RemainingDistance => _agent.remainingDistance;

    /// <summary>
    /// 이동 명령을 받을 수 있는 상태
    /// </summary>
    public bool CanNavigate => CheckCanNavigate ( );

    /// <summary>
    /// 현재 경로의 재계산 필요 여부
    /// </summary>
    public bool IsPathStale => _agent.isPathStale;

    /// <summary>
    /// 이동 컴포넌트 초기화
    /// </summary>
    public virtual void Init ( )
    {
        _agent = GetComponent<NavMeshAgent> ( );
        _pathChecker = new RoutePathChecker ( _agent );

        _agent.updateRotation = false;
    }

    /// <summary>
    /// 애니메이터 연결과 이동 초기화
    /// </summary>
    /// <param name="animatorHandler">동작 애니메이션</param>
    public void Init ( AnimatorHandler animatorHandler )
    {
        _animatorHandler = animatorHandler;
        Init ( );
    }

    private void Update ( )
    {
        UpdateMoveAnimation ( );
    }

    /// <summary>
    /// 이동 모듈 비활성화 시 이동 표시 정리
    /// </summary>
    protected virtual void OnDisable ( )
    {
        if ( _animatorHandler != null )
            _animatorHandler.SetMoveSpeed ( 0f );
    }


    #region ----- 이동/회전 -----

    /// <summary>
    /// 이동 설정 적용, 목적지 경로 계산 요청
    /// </summary>
    /// <param name="position">목적지</param>
    /// <param name="speed">이동 속력</param>
    /// <param name="stopDistance">목적지 정지 거리</param>
    /// <returns>경로 계산 요청 성공 여부</returns>
    public bool MoveTo (
        Vector3 position , float speed , float stopDistance )
    {
        if ( CanNavigate == false )
            return false;

        _agent.speed = speed;
        _agent.stoppingDistance = stopDistance;

        //경로 계산을 요청
        if ( _agent.SetDestination ( position ) == false )
            return false;

        //정지 상태였다면 이동 재개
        _agent.isStopped = false;
        return true;
    }

    /// <summary>
    /// 이동 정지 및 기존 경로 해제
    /// </summary>
    public void Stop ( )
    {
        if ( _animatorHandler != null )
            _animatorHandler.SetMoveSpeed ( 0f );

        if ( CanNavigate == false )
            return;

        _agent.isStopped = true;
        _agent.ResetPath ( );
    }

    /// <summary>
    /// 실제 수평 이동 속력으로 이동 표시 갱신
    /// </summary>
    void UpdateMoveAnimation ( )
    {
        if ( _animatorHandler == null )
            return;

        //초기화 전이나 에이전트 이동이 불가능한 상태에서는 정지 표시
        if ( CanNavigate == false )
        {
            _animatorHandler.SetMoveSpeed ( 0f );
            return;
        }

        if ( _agent.isStopped == true )
        {
            _animatorHandler.SetMoveSpeed ( 0f );
            return;
        }

        Vector3 moveVelocity = Velocity;		//실제 이동 속도
        moveVelocity.y = 0f;

        _animatorHandler.SetMoveSpeed ( moveVelocity.magnitude );
    }

    /// <summary>
    /// 지정 회전값 적용
    /// </summary>
    /// <param name="rotation">목표 회전값</param>
    /// <param name="rotateSpeed">초당 회전 각도</param>
    public void Rotate (
        Quaternion rotation , float rotateSpeed )
    {
        //초당 회전 각도에 프레임 시간을 곱해 점진적으로 회전
        transform.rotation = Quaternion.RotateTowards (
            transform.rotation , rotation , rotateSpeed * Time.deltaTime );
    }

    /// <summary>
    /// 실제 이동 방향으로 회전
    /// </summary>
    /// <param name="rotateSpeed">초당 회전 각도</param>
    public void RotateAlongMovement ( float rotateSpeed )
    {
        if ( CanNavigate == false )
            return;

        Vector3 direction = Velocity;
        direction.y = 0f;

        //정지 중의 작은 속도 변화에는 회전하지 않음
        if ( direction.sqrMagnitude <= 0.0001f )
            return;

        Rotate ( Quaternion.LookRotation ( direction ) , rotateSpeed );
    }

    /// <summary>
    /// 이동 에이전트가 꺼진 상태에서 재배치
    /// </summary>
    /// <param name="position">검사를 마친 월드 위치</param>
    /// <param name="rotation">적용할 월드 회전</param>
    /// <returns>재배치 성공 여부</returns>
    public bool TryRelocate (
        Vector3 position , Quaternion rotation )
    {
        if ( isActiveAndEnabled == false ||
             _agent == null || _agent.enabled == true )
            return false;

        //에이전트가 비활성화된 상태에서 Transform에 적용
        //이동 중인 에이전트를 임의로 끄거나 현재 경로를 변경하지 않음
        transform.SetPositionAndRotation ( position , rotation );
        return true;
    }

    #endregion

    /// <summary>
    /// 지정 위치 주변의 NavMesh 위치 조회
    /// </summary>
    /// <param name="position">조회할 월드 위치</param>
    /// <param name="searchDistance">주변 탐색 거리</param>
    /// <param name="movePosition">조회한 월드 위치, 실패하면 기본값</param>
    /// <returns>위치 조회 성공 여부</returns>
    public bool TryGetMovePosition (
        Vector3 position ,
        float searchDistance ,
        out Vector3 movePosition )
    {
        movePosition = default;
        if ( _pathChecker == null )
            return false;

        //에이전트가 꺼져 있어도 같은 조건으로 위치 조회 가능
        return _pathChecker.TryGetPosition ( position , searchDistance , out movePosition );
    }

    /// <summary>
    /// 등장 지점의 NavMesh 위치로 이동과 회전 초기화
    /// </summary>
    /// <param name="point">등장 지점</param>
    /// <param name="searchDistance">NavMesh 위치 탐색 거리</param>
    /// <returns>초기화 성공 여부</returns>
    public bool TryResetPosition ( Transform point, float searchDistance )
    {
        if ( isActiveAndEnabled == false ||
             point == null || point.gameObject.activeInHierarchy == false ||
             _agent == null || _agent.isActiveAndEnabled == false )
            return false;

        //같은 에이전트 종류와 이동 가능 영역에서 등장 위치 확보
        if ( TryGetMovePosition(
            point.position, searchDistance, out Vector3 position ) == false )
            return false;

        //활성 에이전트의 위치도 함께 변경
        if ( _agent.Warp( position ) == false )
            return false;

        Stop( );
        _agent.velocity = Vector3.zero;
        transform.rotation = point.rotation;
        return true;
    }

    /// <summary>
    /// 이동 에이전트의 활성 상태 확인
    /// </summary>
    /// <returns>이동 명령 가능 여부</returns>
    bool CheckCanNavigate ( )
    {
        if ( isActiveAndEnabled == false ||
             _agent == null || _agent.isActiveAndEnabled == false )
            return false;

        return _agent.isOnNavMesh;
    }

#if UNITY_EDITOR
    /// <summary>
    /// 공격 중 실제 이동과 에이전트 정지 상태 조회
    /// </summary>
    /// <returns>실제 위치, 이동 속도와 정지 상태</returns>
    public string GetAttackDebugDetails ()
    {
        return $"적 위치: {Position} / Agent 위치: {_agent.nextPosition} / 속도: {Velocity} / " +
            $"희망 속도: {_agent.desiredVelocity} / 정지: {_agent.isStopped}";
    }
#endif

}
