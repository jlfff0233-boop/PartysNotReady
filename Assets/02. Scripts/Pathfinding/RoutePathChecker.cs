using UnityEngine;
using UnityEngine.AI;
using System;

/// <summary>
/// 경로 검사 - NavMesh 위치 조회와 도달 가능 거리 계산
/// </summary>
public class RoutePathChecker
{
    NavMeshAgent _agent;     //검사 조건의 기준 에이전트
    NavMeshQueryFilter _filter;     //에이전트 종류와 이동 가능 영역
    NavMeshPath _path = new( );     //검사에 재사용할 경로
    Vector3 [ ] _corners = new Vector3 [ 32 ];     //재사용할 경로 지점 배열

    const float PositionTolerance = 0.01f;     //경로 양 끝의 위치 오차 허용값

    /// <summary>
    /// 최근 경로 검사 실패 사유
    /// </summary>
    public string FailureReason { get; private set; } = string.Empty;

    /// <summary>
    /// 최근 실패의 요청 좌표와 실제 경로 끝점
    /// </summary>
    public string FailureDetails { get; private set; } = string.Empty;


    /// <summary>
    /// 경로 검사 기준 에이전트 연결
    /// </summary>
    /// <param name="agent">실제 이동에 사용하는 에이전트</param>
    public RoutePathChecker ( NavMeshAgent agent )
    {
        _agent = agent;
    }

    /// <summary>
    /// 위치 조회에 사용할 에이전트 종류와 영역 반영
    /// </summary>
    /// <returns>조회 조건 설정 여부</returns>
    bool UpdatePositionFilter ()
    {
        if ( _agent == null )
            return false;

        //잠복 중 비활성 에이전트에서도 위치 조회는 필요
        //영역 비용은 위치 조회에 사용하지 않음
        _filter.agentTypeID = _agent.agentTypeID;
        _filter.areaMask = _agent.areaMask;
        return true;
    }

    /// <summary>
    /// 활성 에이전트의 경로 계산 조건 반영
    /// </summary>
    /// <returns>경로 계산 가능 여부</returns>
    bool UpdatePathFilter ()
    {
        //영역 비용 조회 전에 활성 상태와 NavMesh 배치 확인
        if ( _agent == null ||
             _agent.isActiveAndEnabled == false ||
             _agent.isOnNavMesh == false )
            return false;

        UpdatePositionFilter( );

        //실행 중 변경된 영역 비용도 실제 이동과 동일하게 반영
        for ( int i = 0; i < 32; i++ )
            _filter.SetAreaCost( i, _agent.GetAreaCost( i ) );

        return true;
    }


    #region ----- 위치 조회 -----

    /// <summary>
    /// 지정 위치 주변의 NavMesh 위치 조회
    /// </summary>
    /// <param name="position">조회할 월드 위치</param>
    /// <param name="searchDistance">주변 탐색 거리</param>
    /// <param name="movePosition">조회한 이동 위치</param>
    /// <returns>조회 성공 여부</returns>
    public bool TryGetPosition (
        Vector3 position, float searchDistance,
        out Vector3 movePosition )
    {
        movePosition = default;

        if ( IsValidPosition( position ) == false ||
             IsFinite( searchDistance ) == false ||
             searchDistance <= 0f )
            return false;

        if ( UpdatePositionFilter( ) == false )
            return false;

        //다른 에이전트 종류나 이동 금지 영역은 조회 대상에서 제외
        if ( NavMesh.SamplePosition(
            position, out NavMeshHit hit,
            searchDistance, _filter ) == false )
            return false;

        movePosition = hit.position;
        return true;
    }

    #endregion

    #region ----- 경로 거리 -----

    /// <summary>
    /// 경로 지점을 재사용 배열에 복사
    /// </summary>
    /// <returns>복사한 지점 수</returns>
    int ReadCorners ()
    {
        int count = _path.GetCornersNonAlloc( _corners );
        if ( count < _corners.Length )
            return count;

        //배열이 가득 차면 전체 결과로 용량을 늘려 긴 경로가 잘리지 않도록 함
        Vector3 [ ] corners = _path.corners;
        _corners = new Vector3 [ corners.Length + 1 ];
        Array.Copy( corners, _corners, corners.Length );
        return corners.Length;
    }

    /// <summary>
    /// 경로의 구간별 길이 합산
    /// </summary>
    /// <param name="corners">경로의 꺾이는 지점 목록</param>
    /// <param name="count">이번 경로의 지점 수</param>
    /// <returns>전체 경로 길이</returns>
    float CalculateDistance ( Vector3 [ ] corners, int count )
    {
        float distance = 0f;

        //벽을 무시한 직선거리 대신 실제 경로의 각 구간을 합산
        for ( int i = 1; i < count; i++ )
            distance += Vector3.Distance(
                corners [ i - 1 ], corners [ i ] );

        return distance;
    }

    /// <summary>
    /// 경로의 첫 구간 또는 마지막 구간의 수평 방향 조회
    /// </summary>
    /// <param name="count">경로 지점 수</param>
    /// <param name="atEnd">마지막 구간 조회 여부</param>
    /// <returns>수평 방향, 이동 구간이 없으면 영벡터</returns>
    Vector3 GetPathDirection ( int count, bool atEnd )
    {
        //중복 지점은 건너뛰고 실제 이동 구간 사용
        for ( int step = 1; step < count; step++ )
        {
            int index = atEnd ? count - step : step;
            Vector3 direction = _corners [ index ] - _corners [ index - 1 ];
            direction.y = 0f;
            if ( direction.sqrMagnitude > 0f )
                return direction.normalized;
        }
        return Vector3.zero;
    }

    /// <summary>
    /// 완전 경로의 길이와 출발 및 진입 방향 조회
    /// </summary>
    /// <param name="start">NavMesh에서 조회한 출발 위치</param>
    /// <param name="destination">NavMesh에서 조회한 도착 위치</param>
    /// <param name="distance">경로 길이, 실패하면 양의 무한대</param>
    /// <param name="departure">첫 구간의 수평 이동 방향</param>
    /// <param name="arrival">마지막 구간의 수평 이동 방향</param>
    /// <returns>완전 경로 조회 성공 여부</returns>
    public bool TryGetPathDetails (
        Vector3 start, Vector3 destination,
        out float distance, out Vector3 departure, out Vector3 arrival )
    {
        distance = float.PositiveInfinity;
        departure = Vector3.zero;
        arrival = Vector3.zero;
        FailureReason = string.Empty;
        FailureDetails = string.Empty;

        if ( IsValidPosition( start ) == false ||
             IsValidPosition( destination ) == false )
        {
            FailureReason = "경로 좌표 오류";
            return false;
        }

        if ( UpdatePathFilter( ) == false )
        {
            FailureReason = "에이전트 비활성 또는 NavMesh 미배치";
            return false;
        }

        //이전 검사 결과를 남기지 않고 현재 위치 쌍으로 다시 계산
        _path.ClearCorners( );

        if ( NavMesh.CalculatePath(
            start, destination, _filter, _path ) == false )
        {
            FailureReason = "NavMesh 경로 계산 실패";
            FailureDetails = DescribePath( start, destination, ReadCorners( ) );
            return false;
        }

        //불완전 경로도 실제 끝점을 조회하여 끊긴 위치를 확인할 수 있게 함
        int count = ReadCorners( );
        //일부 구간까지만 이동 가능한 경로는 연결 비용으로 사용하지 않음
        if ( _path.status != NavMeshPathStatus.PathComplete )
        {
            FailureReason = $"불완전 경로: {_path.status}";
            FailureDetails = DescribePath( start, destination, count );
            return false;
        }

        //배열 용량이 아닌 이번에 복사한 지점 수까지만 검사와 계산에 사용
        if ( CheckEndpoints( _corners, count, start, destination ) == false )
        {
            FailureReason = "경로 양 끝과 요청 위치 불일치";
            FailureDetails = DescribePath( start, destination, count );
            return false;
        }

        float calculatedDistance = CalculateDistance( _corners, count );
        if ( IsFinite( calculatedDistance ) == false )
        {
            FailureReason = "경로 길이 오류";
            return false;
        }

        distance = calculatedDistance;
        departure = GetPathDirection( count, atEnd: false );
        arrival = GetPathDirection( count, atEnd: true );
        return true;
    }

    /// <summary>
    /// 도달 가능한 경로 길이 조회
    /// </summary>
    /// <param name="start">NavMesh 출발 위치</param>
    /// <param name="destination">NavMesh 도착 위치</param>
    /// <param name="distance">경로 길이, 실패하면 양의 무한대</param>
    /// <returns>완전 경로 조회 성공 여부</returns>
    public bool TryGetDistance (
        Vector3 start, Vector3 destination, out float distance )
    {
        return TryGetPathDetails(
            start, destination, out distance, out _, out _ );
    }

    #endregion

    #region ----- 값 검사 -----

    /// <summary>
    /// 요청 위치와 실제 경로 양 끝의 차이 표시
    /// </summary>
    /// <param name="start">요청 출발 위치</param>
    /// <param name="destination">요청 도착 위치</param>
    /// <param name="count">실제 경로 지점 수</param>
    /// <returns>경로 상태와 끝점 오차</returns>
    string DescribePath ( Vector3 start, Vector3 destination, int count )
    {
        string details = $"요청 {start:F3} → {destination:F3} / {_path.status} / 지점 {count}";
        if ( count == 0 )
            return details;

        Vector3 first = _corners [ 0 ];
        Vector3 last = _corners [ count - 1 ];
        return $"{details} / 실제 {first:F3} → {last:F3} / " +
            $"출발 오차 {Vector3.Distance( first, start ):F4} / 도착 오차 {Vector3.Distance( last, destination ):F4}";
    }

    /// <summary>
    /// 유한한 수인지 확인
    /// </summary>
    /// <param name="value">검사할 값</param>
    /// <returns>유효한 수 여부</returns>
    bool IsFinite ( float value )
    {
        return float.IsNaN( value ) == false &&
               float.IsInfinity( value ) == false;
    }

    /// <summary>
    /// 위치 좌표의 유효성 확인
    /// </summary>
    /// <param name="position">검사할 위치</param>
    /// <returns>모든 좌표의 유효 여부</returns>
    bool IsValidPosition ( Vector3 position )
    {
        return IsFinite( position.x ) &&
               IsFinite( position.y ) &&
               IsFinite( position.z );
    }

    /// <summary>
    /// 경로 양 끝과 요청 위치 확인
    /// </summary>
    /// <param name="corners">계산된 경로 지점</param>
    /// <param name="count">이번 경로의 지점 수</param>
    /// <param name="start">요청한 출발 위치</param>
    /// <param name="destination">요청한 도착 위치</param>
    /// <returns>양 끝의 일치 여부</returns>
    bool CheckEndpoints (
        Vector3 [ ] corners, int count, Vector3 start, Vector3 destination )
    {
        if ( count == 0 )
            return false;

        float toleranceSqr = PositionTolerance * PositionTolerance;

        //NavMesh 내부 보정으로 다른 위치에 연결된 결과는 제외
        if ( ( corners [ 0 ] - start ).sqrMagnitude > toleranceSqr )
            return false;

        int lastIndex = count - 1;

        return ( corners [ lastIndex ] - destination ).sqrMagnitude
            <= toleranceSqr;
    }

    #endregion
}
