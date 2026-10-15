using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 경로 구성 모듈 - 노드 검사와 탐색 결과 연결
/// </summary>
public class RouteBuilder
{
    RouteGraph _graph;     //씬의 노드와 연결 설정
    RoutePathChecker _checker;     //NavMesh 위치와 경로 검사
    float _sampleDist;     //노드 주변 NavMesh 탐색 거리
    float _connectDist;     //출발/도착 위치와 후보 노드 사이의 최대 경로 길이

    /// <summary>
    /// 그래프의 활성 노드
    /// </summary>
    List<RouteNode> _nodes = new( );
    /// <summary>
    /// 현재 노드의 연결 목록
    /// </summary>
    List<RouteNode> _connections = new( );
    /// <summary>
    /// 노드 인덱스 딕셔너리(노드, 인덱스)
    /// </summary>
    Dictionary<RouteNode, int> _indices = new( );
    /// <summary>
    /// 탐색 번호 순서의 원본 노드
    /// </summary>
    List<RouteNode> _nodeLookup = new( );
    /// <summary>
    /// A* 결과의 노드 순서
    /// </summary>
    List<int> _route = new( );     

    /// <summary>
    /// 입구 연결 검사에 재사용할 경유점 목록
    /// </summary>
    List<RoutePointData> _entranceRoute = new( );

    /// <summary>
    /// 경유 노드부터 최종 목적지까지의 노드 순서
    /// </summary>
    List<int> _detourReturnRoute = new( );

    RouteSearchData _data = new( );     //재사용할 탐색 데이터
    RoutePlanner _planner;     //재사용할 A* 계산기
    string _failureReason = string.Empty;     //최근 구성 실패 사유
    bool _canRetry;     //최근 구성 실패의 재시도 가능 여부
    string _endpointDetail = string.Empty;     //출발/도착 연결 제외 집계
    string _connectionDetail = string.Empty;		//노드 연결 집계와 대표 탈락 사유
    int _failureDetailCount;		//보관한 대표 탈락 사유 수

    /// <summary>
    /// 최근 노드 검사 수
    /// </summary>
    public string NodeSummary =>
        $"등록 칸: {_graph?.RegisteredCount ?? 0} / 활성 고유 노드: {_nodes.Count} / NavMesh 유효: {_indices.Count}";

    /// <summary>
    /// 최근 경로 구성 실패 사유
    /// </summary>
    public string FailureReason => _failureReason;

    /// <summary>
    /// 최근 구성 실패가 재시도 가능한지 여부
    /// </summary>
    public bool CanRetry => _canRetry;

#if UNITY_EDITOR
    /// <summary>
    /// 도주 경로 선정 근거와 거리
    /// </summary>
    public string DebugSelection { get; private set; } = string.Empty;
#endif


    /// <summary>
    /// 그래프와 경로 검사 연결
    /// </summary>
    /// <param name="graph">사용할 경로 그래프</param>
    /// <param name="checker">이동 조건을 설정한 경로 검사기</param>
    /// <param name="sampleDist">NavMesh 위치 탐색 거리</param>
    /// <param name="connectDist">후보 노드까지 허용할 최대 경로 길이</param>
    public RouteBuilder (
        RouteGraph graph, RoutePathChecker checker,
        float sampleDist, float connectDist )
    {
        _graph = graph;
        _checker = checker;
        _sampleDist = sampleDist;
        _connectDist = connectDist;
        _planner = new RoutePlanner( _data );
    }


    #region ----- 탐색 데이터 -----

    /// <summary>
    /// 이동 가능한 노드 위치 등록
    /// </summary>
    void AddNodes ()
    {
        _graph.GetNodes( _nodes );

        foreach ( RouteNode node in _nodes )
        {
            //NavMesh를 찾지 못한 노드는 이번 탐색에서 제외
            if ( _checker.TryGetPosition(
                node.Position, _sampleDist,
                out Vector3 position ) == false )
            {
                AddFailureDetail( $"{node.name}: 주변 NavMesh 조회 실패 / 위치 {node.Position:F3}" );
                continue;
            }

            int index = _data.AddNode( position );
            if ( index < 0 )
                continue;

            //경로 계산에는 원래 Transform 위치 대신 검사된 위치 사용
            _indices.Add( node, index );
            _nodeLookup.Add( node );
        }
    }

    /// <summary>
    /// 통행 가능한 단방향 연결 등록
    /// </summary>
    void AddConnections ()
    {
        int requested = 0;
        int accepted = 0;
        foreach ( RouteNode node in _nodes )
        {
            if ( _indices.TryGetValue( node, out int from ) == false )
                continue;

            _graph.GetConnections( node, _connections );
            Vector3 start = _data.GetPosition( from );

            foreach ( RouteNode connected in _connections )
            {
                requested++;
                //목적지 노드의 위치 검사도 통과한 연결만 사용
                if ( _indices.TryGetValue(
                    connected, out int to ) == false )
                {
                    AddFailureDetail( $"{node.name} → {connected.name}: 목적 노드 NavMesh 없음" );
                    continue;
                }

                Vector3 destination = _data.GetPosition( to );

                //A->B가 가능해도 B->A까지 가능하다고 가정하지 않음
                if ( _checker.TryGetDistance(
                    start, destination, out float distance ) == false )
                {
                    AddFailureDetail( $"{node.name} → {connected.name}: {_checker.FailureReason} / {_checker.FailureDetails}" );
                    continue;
                }

                _data.AddConnection( from, to, distance );
                accepted++;
            }
        }
        _connectionDetail = $"설정 연결 검사 {requested} / 통행 가능 {accepted} / 탈락 {requested - accepted}" +
            _connectionDetail;
    }

    /// <summary>
    /// 경로 구성 실패에 표시할 대표 탈락 사유 보관
    /// </summary>
    /// <param name="details">노드 또는 연결의 탈락 사유</param>
    void AddFailureDetail ( string details )
    {
        //전체 그래프를 매번 로그로 복사하지 않고 대표 사유만 제한하여 보관
        if ( _failureDetailCount >= 8 )
            return;
        _connectionDetail += $"\n{details}";
        _failureDetailCount++;
    }

    #endregion

    #region ----- 출발/도착 연결 -----

    /// <summary>
    /// 출발/도착 지점과 주변 후보 노드 연결
    /// </summary>
    /// <param name="endpoint">계산용 출발/도착 지점 번호</param>
    /// <param name="isStart">출발 지점에서 노드로 연결할지 여부</param>
    /// <returns>하나 이상 연결됐는지 여부</returns>
    bool ConnectEndpoint ( int endpoint, bool isStart )
    {
        bool connected = false;
        Vector3 position = _data.GetPosition( endpoint );
        int far = 0;
        int invalid = 0;
        int longPath = 0;
        string pathFailure = string.Empty;

        //씬에서 등록한 노드만 후보로 사용하며 임시 지점끼리는 연결하지 않음
        foreach ( int index in _indices.Values )
        {
            Vector3 nodePosition = _data.GetPosition( index );

            //직선거리부터 범위를 벗어나면 NavMesh 경로 계산 생략
            if ( Vector3.Distance( position, nodePosition ) > _connectDist )
            {
                far++;
                continue;
            }

            //출발은 현재 위치->노드, 도착은 노드->최종 목적지로 검사
            //단방향 통로가 있으므로 도착 검사를 반대로 수행하지 않음
            Vector3 from = isStart ? position : nodePosition;
            Vector3 to = isStart ? nodePosition : position;

            if ( _checker.TryGetDistance(
                from, to, out float distance ) == false )
            {
                invalid++;
                pathFailure = _checker.FailureReason;
                continue;
            }

            //가까워 보여도 벽을 우회하는 실제 경로가 길면 후보에서 제외
            if ( distance > _connectDist )
            {
                longPath++;
                continue;
            }

            int fromIndex = isStart ? endpoint : index;
            int toIndex = isStart ? index : endpoint;

            //가장 가까운 하나를 확정하지 않고 전체 비용 비교에 후보를 모두 포함
            if ( _data.AddConnection( fromIndex, toIndex, distance ) )
                connected = true;
        }

        _endpointDetail =
            $"허용 거리: {_connectDist} / 직선거리 초과: {far} / 경로 검사 실패: {invalid} / " +
            $"경로 길이 초과: {longPath} / 최근 검사 실패: {pathFailure} / {NodeSummary}";
        return connected;
    }

    #endregion

    #region ----- 경로 구성 -----

    /// <summary>
    /// 실패 사유 기록
    /// </summary>
    /// <param name="reason">실패 설명</param>
    /// <param name="canRetry">통행 상태가 바뀌면 재시도 가능한지 여부</param>
    /// <returns>항상 false</returns>
    bool Fail ( string reason, bool canRetry = false )
    {
        _failureReason = reason;
        _canRetry = canRetry;
        return false;
    }

    /// <summary>
    /// 출발 위치에서 목적지까지 이동 지점 구성
    /// </summary>
    /// <param name="start">현재 월드 위치</param>
    /// <param name="destination">최종 목적지의 월드 위치</param>
    /// <param name="results">이동 순서대로 정리한 경유점과 노드 정보</param>
    /// <returns>경로 구성 성공 여부</returns>
    public bool TryBuild (
        Vector3 start, Vector3 destination,
        List<RoutePointData> results )
    {
        results.Clear( );
        _indices.Clear( );
        _nodeLookup.Clear( );
        _route.Clear( );
        _data.Clear( );
        _nodes.Clear( );
        _failureReason = string.Empty;
        _canRetry = false;
#if UNITY_EDITOR
        DebugSelection = string.Empty;
#endif

        //설정과 입력 오류는 기다려도 해결되지 않으므로 재시도하지 않음
        if ( CheckSettings( out string reason ) == false )
            return Fail( reason );

        if ( CheckPosition( start ) == false || CheckPosition( destination ) == false )
            return Fail( "출발/도착 위치가 유효하지 않습니다." );

        //그래프와 노드는 실행 중 다시 활성화될 수 있음
        if ( _graph.isActiveAndEnabled == false )
            return Fail( "경로 그래프가 비활성화돼 있습니다.", canRetry: true );

        //출발과 도착 위치를 먼저 검사하여 불필요한 전체 연결 계산 방지
        if ( _checker.TryGetPosition(
            start, _sampleDist, out Vector3 startPosition ) == false )
            return Fail( "출발 위치 주변에서 NavMesh를 찾지 못했습니다.", canRetry: true );

        if ( _checker.TryGetPosition(
            destination, _sampleDist,
            out Vector3 destinationPosition ) == false )
            return Fail( "도착 위치 주변에서 NavMesh를 찾지 못했습니다.", canRetry: true );

        //이전 요청의 연결 비용을 재사용하지 않고 현재 통행 상태 반영
        _connectionDetail = string.Empty;
        _failureDetailCount = 0;
        AddNodes( );

        if ( _data.Count == 0 )
            return Fail( $"이동 가능한 경로 노드가 없습니다. / {NodeSummary}", canRetry: true );

        //임시 지점도 같은 탐색 데이터에 넣어 접근 거리까지 A* 비용에 포함
        //씬의 노드와 연결 설정은 변경하지 않음
        int startIndex = _data.AddNode( startPosition );
        int goalIndex = _data.AddNode( destinationPosition );

        if ( startIndex < 0 || goalIndex < 0 )
            return Fail( "출발/도착 위치가 유효하지 않습니다." );

        if ( ConnectEndpoint( startIndex, isStart: true ) == false )
            return Fail( $"출발 위치 연결 실패 / {_endpointDetail}", canRetry: true );

        string startDetail = _endpointDetail;
        if ( ConnectEndpoint( goalIndex, isStart: false ) == false )
            return Fail( $"최종 목적지 연결 실패 / {_endpointDetail}", canRetry: true );

        AddConnections( );

        //출발 접근 + 노드 사이 이동 + 목적지 접근의 총비용으로 경로 선택
        if ( _planner.TryFind( startIndex, goalIndex, _route ) == false )
            return Fail( $"출발 후보와 도착 후보 사이에 연결된 경로가 없습니다. / {NodeSummary}\n" +
                $"출발 후보: {startDetail}\n도착 후보: {_endpointDetail}\n{_connectionDetail}", canRetry: true );

        //현재 위치인 임시 출발점은 제외하고 첫 후보 노드부터 이동
        //마지막 임시 도착점은 최종 목적지이므로 이동 목록에 포함
        for ( int i = 1; i < _route.Count; i++ )
        {
            int index = _route [ i ];
            RouteNode node = index < _nodeLookup.Count
                ? _nodeLookup [ index ] : null;

            //같은 위치의 노드도 연결 검사를 위해 각각 보관
            //실제 이동 명령의 중복은 Navigator에서 생략
            results.Add( new RoutePointData( _data.GetPosition( index ), node ) );
        }
        return true;
    }

    /// <summary>
    /// 접근 가능한 첫 노드와 연결된 도주 완료 노드 선정
    /// </summary>
    /// <param name="start">도주 시작 위치</param>
    /// <param name="heading">이동 구간이 없는 경우 사용할 진행 방향</param>
    /// <param name="arriveDistance">도착 허용 거리</param>
    /// <param name="first">먼저 통과할 경유점</param>
    /// <param name="destination">도주 완료 경유점</param>
    /// <returns>두 경유점 선정 여부</returns>
    public bool TrySelectConnectedNodes (
        Vector3 start, Vector3 heading, float arriveDistance,
        out RoutePointData first, out RoutePointData destination )
    {
        first = default;
        destination = default;
        _failureReason = string.Empty;
        _canRetry = false;
#if UNITY_EDITOR
        DebugSelection = string.Empty;
#endif

        if ( CheckSettings( out string reason ) == false )
            return Fail( reason );

        if ( _checker.TryGetPosition(
            start, _sampleDist, out Vector3 startPosition ) == false )
            return Fail( "도주 시작 위치 주변에서 NavMesh를 찾지 못했습니다." );

        _graph.GetNodes( _nodes );
        float bestApproach = float.PositiveInfinity;		//첫 노드까지 가장 짧은 접근 경로
        float bestAlignment = -1f;		//진입과 출발 방향 일치도
        float bestEscapeDistance = float.PositiveInfinity;		//두 노드 사이 경로 길이
        bool found = false;		//유효한 두 노드 선정 여부
        heading.y = 0f;
        heading.Normalize( );

        foreach ( RouteNode candidate in _nodes )
        {
            if ( _checker.TryGetPosition(
                candidate.Position, _sampleDist, out Vector3 firstPosition ) == false )
                continue;

            if ( Vector3.Distance( startPosition, firstPosition ) > _connectDist )
                continue;

            if ( _checker.TryGetPathDetails(
                startPosition, firstPosition,
                out float approach, out _, out Vector3 incoming ) == false ||
                 approach > _connectDist || approach > bestApproach )
                continue;

            //출발부터 첫 노드에 있으면 현재 진행 방향 사용
            if ( incoming.sqrMagnitude == 0f )
                incoming = heading;

            _graph.GetConnections( candidate, _connections );
            foreach ( RouteNode connected in _connections )
            {
                if ( _checker.TryGetPosition(
                    connected.Position, _sampleDist,
                    out Vector3 destinationPosition ) == false )
                    continue;

                //같은 지점이나 출발 위치 근처에서 도주가 끝나는 조합 제외
                float separation = Vector3.Distance( startPosition, destinationPosition );
                if ( separation <= arriveDistance ||
                     Vector3.Distance( firstPosition, destinationPosition ) <= arriveDistance )
                    continue;

                if ( _checker.TryGetPathDetails(
                    firstPosition, destinationPosition,
                    out float escapeDistance, out Vector3 outgoing, out _ ) == false )
                    continue;

                //실제 경로의 마지막 진입 구간에서 앞쪽으로 이어지는 연결만 사용
                float alignment = Vector3.Dot( incoming, outgoing );
                if ( alignment <= 0f )
                    continue;

                //가까운 첫 노드 우선, 같은 접근 거리에서는 방향과 경로 길이 비교
                if ( approach == bestApproach )
                {
                    if ( alignment < bestAlignment ||
                         ( alignment == bestAlignment && escapeDistance >= bestEscapeDistance ) )
                        continue;
                }

                first = new RoutePointData( firstPosition, candidate );
                destination = new RoutePointData( destinationPosition, connected );
                bestApproach = approach;
                bestAlignment = alignment;
                bestEscapeDistance = escapeDistance;
                found = true;
#if UNITY_EDITOR
                DebugSelection = $"진입 방향: {incoming} / 출발 방향: {outgoing} / " +
                    $"방향 일치도: {alignment:F3} / A 접근 경로: {approach:F2} / A→B 경로: {escapeDistance:F2}";
#endif
            }
        }

        if ( found == false )
            return Fail( $"앞쪽 연결로 이동 가능한 두 노드가 없습니다. / 접근 허용: {_connectDist}" );

        return true;
    }

    /// <summary>
    /// 입구에서 멀어지는 두 노드와 전체 입구 복귀 경로 선정
    /// </summary>
    /// <param name="start">포획 후 도주 시작 위치</param>
    /// <param name="entrance">최종 입구 위치</param>
    /// <param name="arriveDistance">노드 통과 허용 거리</param>
    /// <param name="results">접근 노드부터 최종 입구까지 이동 순서</param>
    /// <returns>입구로 복귀 가능한 우회 노드 선정 여부</returns>
    public bool TrySelectEntranceDetour (
        Vector3 start, Vector3 entrance, float arriveDistance,
        List<RoutePointData> results )
    {
        results.Clear( );
        //기존 입구 경로 탐색으로 위치, 노드와 단방향 연결을 한 번 구성
        if ( TryBuild( start, entrance, _entranceRoute ) == false )
            return false;

        int startIndex = _nodeLookup.Count;
        int goalIndex = startIndex + 1;
        Vector3 startPosition = _data.GetPosition( startIndex );
        Vector3 entrancePosition = _data.GetPosition( goalIndex );
        Vector3 startAway = startPosition - entrancePosition;
        startAway.y = 0f;
        startAway.Normalize( );
        float startSeparation = Vector3.Distance( startPosition, entrancePosition );
        float bestApproach = float.PositiveInfinity;		//첫 노드까지 최단 접근 거리
        float bestAlignment = -1f;		//입구 반대 방향 일치도
        bool found = false;		//입구 복귀 가능한 우회 조합 여부

        foreach ( KeyValuePair<int, float> approach in _data.GetConnections( startIndex ) )
        {
            int from = approach.Key;
            if ( approach.Value > bestApproach )
                continue;
            Vector3 firstPosition = _data.GetPosition( from );
            Vector3 away = firstPosition - entrancePosition;
            away.y = 0f;
            away.Normalize( );

            //접근 구간부터 입구 쪽으로 향하는 후보는 제외
            if ( _checker.TryGetPathDetails( startPosition, firstPosition,
                out float distance, out Vector3 departure, out _ ) == false ||
                 ( distance > arriveDistance && startAway.sqrMagnitude > 0f &&
                   Vector3.Dot( departure, startAway ) <= 0f ) )
                continue;

            foreach ( KeyValuePair<int, float> connection in _data.GetConnections( from ) )
            {
                int to = connection.Key;
                if ( to >= _nodeLookup.Count )
                    continue;
                Vector3 secondPosition = _data.GetPosition( to );
                float separation = Vector3.Distance( secondPosition, entrancePosition );
                //입구에서 실제로 더 멀어지고 서로 구분되는 노드만 선택
                if ( separation <= startSeparation ||
                     separation <= Vector3.Distance( firstPosition, entrancePosition ) ||
                     Vector3.Distance( firstPosition, secondPosition ) <= arriveDistance )
                    continue;
                if ( _checker.TryGetPathDetails( firstPosition, secondPosition,
                    out _, out Vector3 outgoing, out _ ) == false )
                    continue;
                //입구와 첫 노드가 겹치면 어느 출발 방향이든 입구에서 멀어짐
                float alignment = away.sqrMagnitude > 0f ? Vector3.Dot( away, outgoing ) : 1f;
                if ( alignment <= 0f ||
                     ( approach.Value == bestApproach && alignment <= bestAlignment ) )
                    continue;
                //B에서 입구로 복귀할 때 A를 제외하여 A->B->A 왕복을 방지
                //다른 복귀 연결이 없는 조합은 후보에서 제외
                if ( _planner.TryFind( to, goalIndex, _route, from ) == false )
                    continue;

                //다음 후보 탐색에서 _route가 바뀌기 전에 최선 경로 전체를 복사
                SaveEntranceRoute( from, _route, results );
                bestApproach = approach.Value;
                bestAlignment = alignment;
                found = true;
            }
        }

        if ( found == false )
            return Fail( "입구 반대 방향으로 우회한 뒤 A를 재방문하지 않고 입구에 복귀할 수 있는 노드가 없습니다." );
        return true;
    }

    /// <summary>
    /// 최소 이동 거리를 만족하는 경유 노드와 전체 경로 선정
    /// </summary>
    /// <param name="start">현재 위치</param>
    /// <param name="destination">최종 목적지</param>
    /// <param name="minimumDistance">최소 이동 거리</param>
    /// <param name="maximumDistance">이동 거리 상한</param>
    /// <param name="results">선정한 전체 이동 순서</param>
    /// <returns>경로 선정 성공 여부</returns>
    public bool TrySelectDistanceDetour (
        Vector3 start, Vector3 destination,
        float minimumDistance, float maximumDistance,
        List<RoutePointData> results )
    {
        results.Clear( );

        //기존 구성에서 현재 NavMesh의 위치, 연결과 기본 경로 검사
        if ( TryBuild( start, destination, _entranceRoute ) == false )
            return false;

        float directLength = GetRouteLength( _route );		//기본 경로 길이
        if ( directLength > maximumDistance )
            return Fail( "기본 도주 경로가 이동 제한시간에 비해 너무 깁니다." );

        if ( directLength >= minimumDistance )
        {
            results.AddRange( _entranceRoute );
#if UNITY_EDITOR
            DebugSelection = $"기본 경로 / 전체 거리: {directLength:F2} / 최소: {minimumDistance:F2}";
#endif
            return true;
        }

        int startIndex = _nodeLookup.Count;		//임시 출발 지점 번호
        int goalIndex = startIndex + 1;		//임시 목적지 번호
        float bestLength = float.PositiveInfinity;		//선정한 전체 경로 길이
        bool found = false;		//거리 조건을 만족하는 경로 유무

        for ( int via = 0; via < _nodeLookup.Count; via++ )
        {
            if ( _planner.TryFind( startIndex, via, _route ) == false ||
                 _planner.TryFind( via, goalIndex, _detourReturnRoute ) == false )
                continue;

            //방을 빠져나오는 데 필요한 노드 재방문 허용
            float length = GetRouteLength( _route ) +
                GetRouteLength( _detourReturnRoute );		//두 구간의 전체 길이
            if ( length < minimumDistance ||
                 length > maximumDistance || length >= bestLength )
                continue;

            //다음 후보 계산 전에 현재 최선 경로 전체 복사
            results.Clear( );
            AppendRoute( _route, results );
            AppendRoute( _detourReturnRoute, results );
            bestLength = length;
            found = true;
#if UNITY_EDITOR
            DebugSelection = $"경유: {_nodeLookup [ via ].name} / 전체 거리: {length:F2} / 최소: {minimumDistance:F2}";
#endif
        }

        if ( found == false )
            return Fail( "현관까지 연결되지만 최소 도주 거리를 만족하는 경유 경로가 없습니다." );
        return true;
    }

    /// <summary>
    /// 탐색 결과의 연결 비용 합산
    /// </summary>
    /// <param name="route">출발과 도착을 포함한 노드 순서</param>
    /// <returns>전체 이동 경로 길이</returns>
    float GetRouteLength ( List<int> route )
    {
        float length = 0f;		//누적 이동 거리
        for ( int i = 1; i < route.Count; i++ )
            length += _data.GetConnections( route [ i - 1 ] ) [ route [ i ] ];
        return length;
    }

    /// <summary>
    /// 출발 지점을 제외한 탐색 결과를 이동 목록에 추가
    /// </summary>
    /// <param name="route">출발과 도착을 포함한 노드 순서</param>
    /// <param name="results">전체 이동 목록</param>
    void AppendRoute ( List<int> route, List<RoutePointData> results )
    {
        //현재 위치와 두 번째 구간의 중복 경유 지점 제외
        for ( int i = 1; i < route.Count; i++ )
        {
            int index = route [ i ];		//이동할 노드 번호
            RouteNode node = index < _nodeLookup.Count
                ? _nodeLookup [ index ] : null;		//임시 목적지는 원본 노드 없음
            results.Add( new RoutePointData( _data.GetPosition( index ), node ) );
        }
    }

    /// <summary>
    /// 접근 노드와 입구 복귀 경로를 이동 순서로 저장
    /// </summary>
    /// <param name="first">먼저 통과할 접근 노드 번호</param>
    /// <param name="returnRoute">B부터 입구까지의 노드 번호 순서</param>
    /// <param name="results">전체 이동 경유점 목록</param>
    void SaveEntranceRoute (
        int first, List<int> returnRoute, List<RoutePointData> results )
    {
        results.Clear( );
        results.Add( new RoutePointData(
            _data.GetPosition( first ), _nodeLookup [ first ] ) );

        //복귀 경로는 B를 포함하므로 A 뒤에 그대로 연결
        foreach ( int index in returnRoute )
        {
            //임시 입구는 원본 노드 없이 마지막 목적지로 보관
            RouteNode node = index < _nodeLookup.Count
                ? _nodeLookup [ index ] : null;
            results.Add( new RoutePointData( _data.GetPosition( index ), node ) );
        }
    }

    /// <summary>
    /// 선택한 두 경유점의 남은 이동 경로 구성
    /// </summary>
    /// <param name="start">현재 이동 위치</param>
    /// <param name="first">처음 선정한 첫 경유점</param>
    /// <param name="destination">처음 선정한 도주 완료 경유점</param>
    /// <param name="firstPassed">첫 경유점 통과 여부</param>
    /// <param name="results">남은 이동 경유점</param>
    /// <returns>남은 경로 구성 여부</returns>
    public bool TryBuildConnectedNodes (
        Vector3 start, RoutePointData first, RoutePointData destination,
        bool firstPassed, List<RoutePointData> results )
    {
        results.Clear( );
        _failureReason = string.Empty;
        _canRetry = false;

        if ( CheckNode( destination ) == false ||
             ( firstPassed == false && CheckNode( first ) == false ) )
            return Fail( "선택한 도주 노드가 제거, 비활성화되거나 이동했습니다.", canRetry: true );

        if ( firstPassed == false )
        {
            _graph.GetConnections( first.Node, _connections );
            if ( _connections.Contains( destination.Node ) == false )
                return Fail( "선택한 두 도주 노드의 연결이 해제되었습니다.", canRetry: true );
        }

        if ( _checker.TryGetPosition(
            start, _sampleDist, out Vector3 currentPosition ) == false )
            return Fail( "현재 위치 주변에서 NavMesh를 찾지 못했습니다.", canRetry: true );

        Vector3 nextPosition = firstPassed == true ? destination.Position : first.Position;
        if ( _checker.TryGetDistance( currentPosition, nextPosition, out _ ) == false ||
             ( firstPassed == false &&
               _checker.TryGetDistance( first.Position, destination.Position, out _ ) == false ) )
            return Fail( $"선택한 도주 경로 검사 실패 / {_checker.FailureReason}", canRetry: true );

        if ( firstPassed == false )
            results.Add( first );
        results.Add( destination );
        return true;
    }

    #endregion

    #region ----- 검사 -----

    /// <summary>
    /// 현재 위치에서 다음 경유점까지 이동 가능한지 확인
    /// </summary>
    /// <param name="start">현재 이동 위치</param>
    /// <param name="points">선정한 전체 경유점 목록</param>
    /// <param name="index">다음 경유점 번호</param>
    /// <returns>현재 구간의 이동 가능 여부</returns>
    public bool CheckMoveSegment (
        Vector3 start, List<RoutePointData> points, int index )
    {
        _failureReason = string.Empty;
        _canRetry = false;

        //선정 당시의 노드와 단방향 연결을 유지하는지 먼저 확인
        if ( CheckSegment( points, index ) == false )
            return Fail( "선정한 경유점 또는 연결이 변경됐습니다.", canRetry: true );

        //재시도는 현재 위치에서 다음 미통과 지점까지의 완전 경로만 검사
        //전체 목록과 진행 번호는 호출자가 유지하며 새 우회 노드를 선정하지 않음
        if ( _checker.TryGetPosition( start, _sampleDist, out Vector3 position ) == false ||
             _checker.TryGetDistance( position, points [ index ].Position, out _ ) == false )
            return Fail( "다음 경유점까지 완전한 경로가 없습니다.", canRetry: true );

        return true;
    }

    /// <summary>
    /// 경유점까지 도착 허용 거리 안에서 이동 가능한지 확인
    /// </summary>
    /// <param name="position">현재 월드 위치</param>
    /// <param name="destination">검사를 마친 경유점 위치</param>
    /// <param name="arriveDistance">도착 허용 거리</param>
    /// <returns>완전 경로의 길이가 허용 거리 이내인지 여부</returns>
    public bool CheckArrival (
        Vector3 position, Vector3 destination, float arriveDistance )
    {
        //에이전트가 경로를 보유하지 않을 때만 호출하여 도달 가능 여부 재확인
        if ( _checker.TryGetPosition( position, _sampleDist, out Vector3 start ) == false )
            return false;

        float positionOffset = Vector3.Distance( position, start );

        //같은 NavMesh 지점은 별도의 이동 경로가 필요하지 않음
        if ( start.Equals( destination ) )
            return positionOffset <= arriveDistance;

        if ( _checker.TryGetDistance( start, destination, out float distance ) == false )
            return false;

        //NavMesh로 위치가 보정된 거리도 포함해 다른 높이의 지점을 도착으로 넘기지 않음
        return distance + positionOffset <= arriveDistance;
    }

    /// <summary>
    /// 경유점의 원본 노드 상태 확인
    /// </summary>
    /// <param name="point">검사할 경유점</param>
    /// <returns>노드 상태와 위치가 유지되는지 여부</returns>
    public bool CheckNode ( RoutePointData point )
    {
        //최종 목적지는 노드가 없는 고정 위치
        if ( point.HasNode == false )
            return true;

        if ( _graph.Contains( point.Node ) == false )
            return false;

        //NavMesh에 보정된 이동 위치가 아닌 원본 위치끼리 비교
        return point.Node.Position.Equals( point.NodePosition );
    }

    /// <summary>
    /// 현재 구간의 노드와 단방향 연결 확인
    /// </summary>
    /// <param name="points">이동 중인 경유점 목록</param>
    /// <param name="index">목표 경유점 번호</param>
    /// <returns>현재 구간의 사용 가능 여부</returns>
    public bool CheckSegment ( List<RoutePointData> points, int index )
    {
        if ( _graph == null || _graph.isActiveAndEnabled == false ||
             index < 0 || index >= points.Count )
            return false;

        RoutePointData current = points [ index ];
        if ( CheckNode( current ) == false )
            return false;

        //첫 구간은 현재 위치에서 첫 노드로 접근하므로 이전 노드가 없음
        if ( index == 0 )
            return true;

        RoutePointData previous = points [ index - 1 ];
        if ( CheckNode( previous ) == false )
            return false;

        //마지막 노드에서 최종 목적지로 가는 구간은 씬의 연결이 아님
        if ( previous.HasNode == false || current.HasNode == false )
            return true;

        //A->B 연결만 검사하며 B->A 연결로 대신 통과시키지 않음
        _graph.GetConnections( previous.Node, _connections );
        return _connections.Contains( current.Node );
    }


    /// <summary>
    /// 유한한 수인지 확인
    /// </summary>
    /// <param name="value">검사할 값</param>
    /// <returns>유효한 수 여부</returns>
    bool IsFinite ( float value )
    {
        return float.IsNaN( value ) == false && float.IsInfinity( value ) == false;
    }

    /// <summary>
    /// 위치 좌표의 유효성 확인
    /// </summary>
    /// <param name="position">검사할 위치</param>
    /// <returns>모든 좌표의 유효 여부</returns>
    bool CheckPosition ( Vector3 position )
    {
        return IsFinite( position.x ) && IsFinite( position.y ) && IsFinite( position.z );
    }

    /// <summary>
    /// 경로 구성 기능과 거리 설정 확인
    /// </summary>
    /// <param name="reason">잘못된 설정의 설명, 정상이면 빈 문자열</param>
    /// <returns>설정의 유효 여부</returns>
    public bool CheckSettings ( out string reason )
    {
        reason = string.Empty;

        if ( _graph == null || _checker == null )
            reason = "경로 그래프와 검사기 연결을 확인해 주세요.";
        else if ( IsFinite( _sampleDist ) == false || _sampleDist <= 0f )
            reason = "NavMesh 탐색 거리는 유한한 양수여야 합니다.";
        else if ( IsFinite( _connectDist ) == false || _connectDist <= 0f )
            reason = "후보 노드 연결 거리는 유한한 양수여야 합니다.";

        return reason.Length == 0;
    }

    #endregion
}
