using UnityEngine;

/// <summary>
/// 투척 모듈 - 보유 아이템 발사와 예상 궤적 표시
/// </summary>
public class Thrower : MonoBehaviour
{
    [Header( "----- 컴포넌트 -----" )]
    [SerializeField] LineRenderer _trajectory;      //궤적
    [SerializeField] Collider [ ] _ownerColliders;      //투척 직후 충돌을 제외할 플레이어 콜라이더

    [Header( "----- 던지기 -----" )]
    [SerializeField] float _throwSpeed;      //초기 속력
    [SerializeField] float _upwardPower;   //위쪽 방향 보정

    [Header( "----- 궤적 -----" )]
    [SerializeField] LayerMask _obstacleLayerMask;      //장애물 레이어마스크
    [SerializeField, Min( 2 )] int _pointCount;     //시작점을 포함한 최대 궤적 점 개수
    [SerializeField, Min( 0.01f )] float _timeStep;     //궤적 점 사이의 예상 경과시간, 초

    PlayerItemHolder _interact;     //아이템 보유 처리
    Camera _camera;        //조준 카메라

    /// <summary>
    /// 아이템 보유 처리과 조준 카메라 연결
    /// </summary>
    /// <param name="interact">아이템 보유 처리</param>
    /// <param name="camera">조준 카메라</param>
    public void Init ( PlayerItemHolder interact, Camera camera )
    {
        _interact = interact;
        _camera = camera;
    }

    private void Awake ()
    {
        //계산한 월드 좌표를 그대로 사용하고 투척 대상 확인 전에는 궤적을 숨김
        _trajectory.useWorldSpace = true;
        _trajectory.enabled = false;
    }

    private void LateUpdate ()
    {
        //카메라 이동을 마친 방향으로 예상 궤적 갱신
        UpdateTrajectory( );
    }

    private void OnDisable ()
    {
        //투척 기능이 꺼진 뒤 궤적만 남지 않도록 숨김
        _trajectory.enabled = false;
    }


    #region ----- 투척 -----


    /// <summary>
    /// 현재 보유 아이템의 투척 기능 조회
    /// </summary>
    /// <returns>투척 기능 또는 없음</returns>
    IThrowable GetThrowTarget ()
    {
        return _interact != null ? _interact.GetThrowTarget( ) : null;
    }


    /// <summary>
    /// 카메라 방향과 위쪽 보정을 반영한 초기 속도 계산
    /// </summary>
    /// <returns>초기 속도</returns>
    Vector3 GetThrowVelocity ()
    {
        Vector3 direction =
            ( _camera.transform.forward + Vector3.up * _upwardPower ).normalized;

        return direction * _throwSpeed;
    }

    /// <summary>
    /// 현재 보유 아이템 즉시 투척
    /// </summary>
    public void Throw ()
    {
        ItemHolder holder = _interact.ActiveHolder;
        ICarryable item = _interact.HoldItem;
        IThrowable target = GetThrowTarget( );

        if ( target == null || CanLaunch( target ) == false ) return;

        //입력 순간의 카메라 방향으로 실제 초기 속도 계산
        if ( _interact.TryThrow( holder, item, GetThrowVelocity( ), _ownerColliders ) == false )
            return;

        _trajectory.enabled = false;
    }


    /// <summary>
    /// 현재 보유 아이템의 투척 기능 지원 여부
    /// </summary>
    /// <returns>투척 기능 지원 여부</returns>
    public bool HasThrowTarget ()
    {
        return GetThrowTarget( ) != null;
    }

    #endregion

    #region ----- 궤적 -----

    /// <summary>
    /// 경과 시간에 따른 중력 궤적 위치 계산
    /// </summary>
    /// <param name="origin">월드 시작 위치</param>
    /// <param name="velocity">월드 방향의 초기 속도, 초당 월드 단위</param>
    /// <param name="time">투척 후 예상 경과시간, 초</param>
    /// <returns>예상 월드 위치</returns>
    Vector3 GetTrajectoryPosition (
        Vector3 origin, Vector3 velocity, float time )
    {
        //위치 = 시작점 + 초기 속도 이동량 + 중력 이동량
        return origin + velocity * time +
            Physics.gravity * ( 0.5f * time * time );
    }

    /// <summary>
    /// 첫 장애물 충돌까지 예상 궤적 표시
    /// </summary>
    void UpdateTrajectory ()
    {
        IThrowable target = GetThrowTarget( );

        if ( target == null || CanLaunch( target ) == false )
        {
            _trajectory.enabled = false;
            return;
        }

        Vector3 origin = target.ThrowPosition;
        Vector3 velocity = GetThrowVelocity( );
        Vector3 previous = origin;

        _trajectory.positionCount = _pointCount;
        _trajectory.SetPosition( 0, origin );

        for ( int i = 1; i < _pointCount; i++ )
        {
            //시작점에서의 총 경과시간으로 계산하여 구간별 누적 오차 방지
            Vector3 next = GetTrajectoryPosition( origin, velocity, i * _timeStep );

            Vector3 segment = next - previous;

            //점 사이를 아이템 반경으로 검사하여 가는 장애물을 건너뛰지 않도록 처리
            if ( Physics.SphereCast(
                previous, target.ThrowRadius, segment.normalized,
                out RaycastHit hit, segment.magnitude,
                _obstacleLayerMask,
                QueryTriggerInteraction.Ignore ) )
            {
                //표면 접촉점이 아닌 충돌 순간의 아이템 중심에서 궤적 종료
                Vector3 end =
                    previous + segment.normalized * hit.distance;

                _trajectory.SetPosition( i, end );
                _trajectory.positionCount = i + 1;
                break;
            }

            _trajectory.SetPosition( i, next );
            previous = next;
        }

        _trajectory.enabled = true;
    }

    #endregion

    #region ----- 발사 공간 검사 -----

    /// <summary>
    /// 카메라에서 보유 아이템까지 발사 공간 확인
    /// </summary>
    /// <param name="target">투척 대상</param>
    /// <returns>발사 공간 확보 여부</returns>
    bool CanLaunch ( IThrowable target )
    {
        Vector3 origin = _camera.transform.position;
        Vector3 offset = target.ThrowPosition - origin;

        //카메라 근처와 보유 위치가 장애물 내부인 경우 발사 금지
        if ( Physics.CheckSphere( origin, target.ThrowRadius,
                _obstacleLayerMask, QueryTriggerInteraction.Ignore ) ||
            Physics.CheckSphere( target.ThrowPosition, target.ThrowRadius,
                _obstacleLayerMask, QueryTriggerInteraction.Ignore ) )
            return false;

        //Overlay로 벽 너머에 표시된 위치에서 발사되는 상황 방지
        return Physics.SphereCast(
            origin, target.ThrowRadius, offset.normalized,
            out _, offset.magnitude, _obstacleLayerMask,
            QueryTriggerInteraction.Ignore ) == false;
    }

    #endregion

}
