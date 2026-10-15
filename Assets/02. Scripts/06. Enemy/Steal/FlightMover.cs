using UnityEngine;

using System;

/// <summary>
/// 비행 이동 모듈 - 장애물 검사와 위치/회전 적용
/// </summary>
public class FlightMover : MonoBehaviour
{
    [Header( "----- 충돌 검사 -----" )]
    [SerializeField, Min( 0.01f )] float _flightRadius = 0.3f;		//몸체 검사 반경
    [SerializeField] LayerMask _obstacleMask;		//장애물 레이어

    Vector3 _moveDir;		//실제 이동 방향
    AnimatorHandler _animatorHandler;		//동작 애니메이션

    /// <summary>
    /// 현재 위치
    /// </summary>
    public Vector3 Position => transform.position;

    /// <summary>
    /// 실제 이동 방향
    /// </summary>
    public Vector3 MoveDir => _moveDir;

    /// <summary>
    /// 이동 적용 알림 이벤트(실제 이동량)
    /// </summary>
    public event Action<Vector3> OnMoveApplied;

    /// <summary>
    /// 이동 초기화 알림 이벤트
    /// </summary>
    public event Action OnMovementReset;

    /// <summary>
    /// 애니메이터 연결
    /// </summary>
    /// <param name="animatorHandler">동작 애니메이션</param>
    public void Init ( AnimatorHandler animatorHandler )
    {
        _animatorHandler = animatorHandler;
    }

    private void OnDisable ()
    {
        Stop( );
    }


    #region ----- 이동/회전 -----

    /// <summary>
    /// 이동 초기화
    /// </summary>
    public void Stop ()
    {
        _moveDir = Vector3.zero;

        if ( _animatorHandler != null )
            _animatorHandler.SetMoveSpeed( 0f );

        OnMovementReset?.Invoke( );
    }

    /// <summary>
    /// 장애물이 없는 구간으로 이동
    /// </summary>
    /// <param name="nextPosition">이번 이동 위치</param>
    /// <returns>이동 적용 여부</returns>
    public bool UpdateMove ( Vector3 nextPosition )
    {
        if ( isActiveAndEnabled == false )
            return false;

        Vector3 currentPosition = transform.position;

        if ( IsPathBlocked( currentPosition, nextPosition ) == true )
        {
            Stop( );
            return false;
        }

        Vector3 displacement = nextPosition - currentPosition;		//이번 이동 거리 벡터
        float moveSpeed = CalculateMoveSpeed( displacement, Time.deltaTime );		//실제 수평 이동 속력

        Move( nextPosition, displacement.normalized, moveSpeed );

        //위치 적용 후 실제 이동량 전달, 수직 이동도 포함
        OnMoveApplied?.Invoke( transform.position - currentPosition );
        return true;
    }

    /// <summary>
    /// 이동 적용
    /// </summary>
    /// <param name="position">적용할 위치</param>
    /// <param name="direction">실제 이동 방향</param>
    /// <param name="moveSpeed">실제 수평 이동 속력</param>
    void Move ( Vector3 position, Vector3 direction, float moveSpeed )
    {
        _moveDir = direction;
        transform.position = position;

        if ( _animatorHandler != null )
            _animatorHandler.SetMoveSpeed( moveSpeed );
    }

    /// <summary>
    /// 이동 속력 계산
    /// </summary>
    /// <param name="displacement">이번 이동 거리 벡터</param>
    /// <param name="deltaTime">이동에 사용한 경과 시간</param>
    /// <returns>수평 이동 속력, 경과 시간이 없으면 0</returns>
    float CalculateMoveSpeed ( Vector3 displacement, float deltaTime )
    {
        if ( deltaTime <= 0f )
            return 0f;

        displacement.y = 0f;
        return displacement.magnitude / deltaTime;
    }

    /// <summary>
    /// 목표 방향으로 회전
    /// </summary>
    /// <param name="rotation">목표 회전값</param>
    /// <param name="rotateSpeed">초당 회전 각도</param>
    public void Rotate ( Quaternion rotation, float rotateSpeed )
    {
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, rotation,
            rotateSpeed * Time.deltaTime );
    }

    /// <summary>
    /// 적 비활성화
    /// </summary>
    public void Hide ()
    {
        Stop( );
        gameObject.SetActive( false );
    }

    #endregion

    #region ----- 탐색/장애물 검사 -----

    /// <summary>
    /// 범위 내 콜라이더 조회
    /// </summary>
    /// <param name="searchDist">탐색 거리</param>
    /// <param name="itemMask">아이템 레이어</param>
    /// <returns>범위 내 콜라이더 목록</returns>
    public Collider [ ] FindItems ( float searchDist, LayerMask itemMask )
    {
        return Physics.OverlapSphere(
            Position, searchDist, itemMask,
            QueryTriggerInteraction.Ignore );
    }

    /// <summary>
    /// 비행 구간 장애물 검사
    /// </summary>
    /// <param name="start">시작 위치</param>
    /// <param name="end">도착 위치</param>
    /// <returns>장애물 여부</returns>
    public bool IsPathBlocked ( Vector3 start, Vector3 end )
    {
        //SphereCast가 놓칠 수 있는 시작 지점의 겹침을 먼저 확인
        if ( Physics.CheckSphere(
            start, _flightRadius, _obstacleMask,
            QueryTriggerInteraction.Ignore ) == true )
            return true;

        Vector3 direction = end - start;
        float dist = direction.magnitude;

        if ( dist <= 0.0001f )
            return false;

        return Physics.SphereCast(
            start, _flightRadius, direction / dist,
            out RaycastHit hit, dist, _obstacleMask,
            QueryTriggerInteraction.Ignore );
    }

    /// <summary>
    /// 음식 확보 구간 장애물 검사
    /// </summary>
    /// <param name="holdPosition">보유 지점 위치</param>
    /// <param name="foodPosition">음식 위치</param>
    /// <returns>장애물 여부</returns>
    public bool IsTakeBlocked (
        Vector3 holdPosition, Vector3 foodPosition )
    {
        return Physics.Linecast(
            holdPosition, foodPosition, _obstacleMask,
            QueryTriggerInteraction.Ignore );
    }

    #endregion

    /// <summary>
    /// 장애물과 겹치지 않는 등장 지점으로 위치와 회전 초기화
    /// </summary>
    /// <param name="point">등장 지점</param>
    /// <returns>초기화 성공 여부</returns>
    public bool TryResetPosition ( Transform point )
    {
        if ( isActiveAndEnabled == false ||
             point == null || point.gameObject.activeInHierarchy == false )
            return false;

        //같은 위치를 검사하면 몸체 반경의 장애물 겹침만 확인
        if ( IsPathBlocked( point.position, point.position ) == true )
            return false;

        transform.SetPositionAndRotation( point.position, point.rotation );
        Stop( );
        return true;
    }

    /// <summary>
    /// 비행 기능과 경유점 설정 검사
    /// </summary>
    /// <param name="points">이동 경유점</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>사용 가능 여부</returns>
    public bool CheckRoute ( Transform [ ] points, out string reason )
    {
        reason = string.Empty;

        if ( isActiveAndEnabled == false )
        {
            reason = "비행 이동 기능이 비활성 상태입니다.";
            return false;
        }

        if ( points == null || points.Length == 0 )
        {
            reason = "경유점이 없습니다.";
            return false;
        }

        for ( int i = 0; i < points.Length; i++ )
        {
            if ( points [ i ] != null )
                continue;

            reason = $"{i + 1}번 경유점이 없습니다.";
            return false;
        }

        return true;
    }


    private void OnDrawGizmosSelected ()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere( transform.position, _flightRadius );
    }

}
