using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 경로 탐색기 - 연결 비용 기반 A* 경로 계산
/// </summary>
public class RoutePlanner
{
    RouteSearchData _data;     //이번 탐색에 사용할 그래프

    /// <summary>
    /// 탐색 대기 노드 목록(노드 번호)
    /// </summary>
    List<int> _openNodes = new( );
    /// <summary>
    /// 최소 비용 딕셔너리(노드 번호, 누적 비용)
    /// </summary>
    Dictionary<int, float> _costs = new( );
    /// <summary>
    /// 이전 노드 딕셔너리(노드 번호, 이전 노드 번호)
    /// </summary>
    Dictionary<int, int> _previous = new( );


    /// <summary>
    /// 탐색 데이터 연결
    /// </summary>
    /// <param name="data">위치와 연결 비용을 구성한 데이터</param>
    public RoutePlanner ( RouteSearchData data )
    {
        _data = data;
    }


    /// <summary>
    /// 목표까지의 예상 거리 계산
    /// </summary>
    /// <param name="node">현재 노드 번호</param>
    /// <param name="goal">목표 노드 번호</param>
    /// <returns>목표까지의 직선거리</returns>
    float EstimateDistance ( int node, int goal )
    {
        return Vector3.Distance(
            _data.GetPosition( node ),
            _data.GetPosition( goal ) );
    }

    /// <summary>
    /// 예상 총비용이 가장 작은 노드 선택
    /// </summary>
    /// <param name="goal">목표 노드 번호</param>
    /// <returns>선택한 노드 번호</returns>
    int TakeNextNode ( int goal )
    {
        int bestIndex = 0;
        int bestNode = _openNodes [ 0 ];

        //G: 출발점부터 누적 비용, H: 목표까지 예상 거리
        //F = G + H가 가장 작은 노드부터 확인
        double bestScore = ( double ) _costs [ bestNode ] +
                           EstimateDistance( bestNode, goal );

        for ( int i = 1; i < _openNodes.Count; i++ )
        {
            int node = _openNodes [ i ];
            double score = ( double ) _costs [ node ] +
                           EstimateDistance( node, goal );

            if ( score >= bestScore )
                continue;

            bestIndex = i;
            bestNode = node;
            bestScore = score;
        }

        //주요 분기점 위주의 작은 그래프이므로 목록에서 직접 선택
        _openNodes.RemoveAt( bestIndex );
        return bestNode;
    }

    /// <summary>
    /// 연결된 노드의 최소 비용 갱신
    /// </summary>
    /// <param name="current">현재 노드 번호</param>
    /// <param name="excludedNode">제외할 노드 번호, -1이면 제외 없음</param>
    void UpdateConnections ( int current, int excludedNode )
    {
        foreach ( KeyValuePair<int, float> connection
                 in _data.GetConnections( current ) )
        {
            int next = connection.Key;
            //이번 요청에서 제외한 노드는 탐색 목록에도 등록하지 않음
            if ( next == excludedNode )
                continue;

            float nextCost = _costs [ current ] + connection.Value;

            //누적 비용이 표현 범위를 넘은 경로는 사용하지 않음
            if ( float.IsInfinity( nextCost ) || float.IsNaN( nextCost ) )
                continue;

            //더 짧은 경로를 발견한 경우에만 비용과 이전 노드 변경
            if ( _costs.TryGetValue( next, out float previousCost ) &&
                 nextCost >= previousCost )
                continue;

            _costs [ next ] = nextCost;
            _previous [ next ] = current;

            //이미 확인한 노드도 더 짧은 경로가 발견되면 다시 검사
            if ( _openNodes.Contains( next ) == false )
                _openNodes.Add( next );
        }
    }


    /// <summary>
    /// 이전 노드를 따라 이동 순서 구성
    /// </summary>
    /// <param name="goal">도착한 목표 노드 번호</param>
    /// <param name="results">경로 결과 목록</param>
    void BuildRoute ( int goal, List<int> results )
    {
        int current = goal;
        results.Add( current );

        //목표에서 출발점까지 거슬러 올라가며 경로 수집
        while ( _previous.TryGetValue( current, out int previous ) )
        {
            current = previous;
            results.Add( current );
        }

        results.Reverse( );
    }

    /// <summary>
    /// 출발 노드부터 목표 노드까지 경로 계산
    /// </summary>
    /// <param name="start">출발 노드 번호</param>
    /// <param name="goal">목표 노드 번호</param>
    /// <param name="results">출발과 목표를 포함한 이동 순서</param>
    /// <param name="excludedNode">제외할 노드 번호, -1이면 제외 없음</param>
    /// <returns>경로 탐색 성공 여부</returns>
    public bool TryFind (
        int start, int goal, List<int> results, int excludedNode = -1 )
    {
        results.Clear( );
        _openNodes.Clear( );
        _costs.Clear( );
        _previous.Clear( );

        if ( _data == null ||
             _data.Contains( start ) == false ||
             _data.Contains( goal ) == false )
            return false;

        //제외 조건이 없는 기존 호출은 그대로 계산
        //출발이나 목표 자체를 제외한 요청은 경로를 만들 수 없음
        if ( excludedNode != -1 &&
             ( _data.Contains( excludedNode ) == false ||
               start == excludedNode || goal == excludedNode ) )
            return false;

        //출발점의 누적 비용은 0부터 시작
        _costs [ start ] = 0f;
        _openNodes.Add( start );

        while ( _openNodes.Count > 0 )
        {
            int current = TakeNextNode( goal );

            if ( current == goal )
            {
                BuildRoute( goal, results );
                return true;
            }

            UpdateConnections( current, excludedNode );
        }

        //연결이 끊겼으면 부분 경로를 성공 결과로 반환하지 않음
        return false;
    }

}
