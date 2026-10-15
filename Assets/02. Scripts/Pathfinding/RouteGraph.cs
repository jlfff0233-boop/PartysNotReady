using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 경로 그래프 - 노드 목록, 연결 관리
/// </summary>
public class RouteGraph : MonoBehaviour
{
    [Header( "----- 노드 -----" )]
    [SerializeField] RouteNode [ ] _nodes;     //이 그래프에서 사용하는 노드

    /// <summary>
    /// 인스펙터의 노드 등록 칸 수
    /// </summary>
    public int RegisteredCount => _nodes == null ? 0 : _nodes.Length;


    /// <summary>
    /// 활성 노드 목록 조회
    /// </summary>
    /// <param name="results">조회 결과를 받을 목록</param>
    public void GetNodes ( List<RouteNode> results )
    {
        results.Clear( );

        if ( isActiveAndEnabled == false || _nodes == null )
            return;

        foreach ( RouteNode node in _nodes )
        {
            if ( node == null || node.isActiveAndEnabled == false )
                continue;

            //같은 노드가 여러 번 할당돼도 한 번만 반환
            if ( results.Contains( node ) == false )
                results.Add( node );
        }
    }

    /// <summary>
    /// 그래프 내부의 연결 노드 조회
    /// </summary>
    /// <param name="node">연결을 조회할 노드</param>
    /// <param name="results">조회 결과를 받을 목록</param>
    public void GetConnections (
        RouteNode node, List<RouteNode> results )
    {
        results.Clear( );

        if ( Contains( node ) == false )
            return;

        node.GetConnections( results );

        //다른 그래프의 노드를 실수로 연결해도 탐색 범위를 벗어나지 않음
        for ( int i = results.Count - 1; i >= 0; i-- )
        {
            if ( Contains( results [ i ] ) == false )
                results.RemoveAt( i );
        }

        //설정된 연결만 반환하며 실제 통행 가능 여부는 경로 검사에서 판단
    }


    /// <summary>
    /// 활성 노드의 그래프 포함 여부 확인
    /// </summary>
    /// <param name="node">확인할 노드</param>
    /// <returns>포함 여부</returns>
    public bool Contains ( RouteNode node )
    {
        if ( isActiveAndEnabled == false || _nodes == null ||
             node == null || node.isActiveAndEnabled == false )
            return false;

        foreach ( RouteNode candidate in _nodes )
        {
            if ( candidate == node )
                return true;
        }

        return false;
    }

}
