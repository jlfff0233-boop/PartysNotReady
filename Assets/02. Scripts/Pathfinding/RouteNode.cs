using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 경로 노드 - 위치와 인접 연결 제공
/// </summary>
public class RouteNode : MonoBehaviour
{
    [Header( "----- 연결 -----" )]
    [SerializeField] RouteNode [ ] _connections;     //이 노드에서 이동할 수 있는 노드


    /// <summary>
    /// 실행 중 노드 아이디
    /// </summary>
    public int Id => GetInstanceID( );

    /// <summary>
    /// 노드의 월드 위치
    /// </summary>
    public Vector3 Position => transform.position;


    /// <summary>
    /// 활성 연결 노드 조회
    /// </summary>
    /// <param name="results">조회 결과를 받을 목록</param>
    public void GetConnections ( List<RouteNode> results )
    {
        results.Clear( );

        if ( isActiveAndEnabled == false || _connections == null )
            return;

        foreach ( RouteNode node in _connections )
        {
            //자기 자신과 제거되거나 비활성화된 연결 제외
            if ( node == null || node == this ||
                 node.isActiveAndEnabled == false )
                continue;

            //중복 연결로 같은 경로를 여러 번 검사하지 않도록 제외
            if ( results.Contains( node ) == false )
                results.Add( node );
        }
    }


    /// <summary>
    /// 연결 방향 화살표 그리기
    /// </summary>
    /// <param name="destination">연결 대상의 월드 위치</param>
    void DrawConnection ( Vector3 destination )
    {
        Vector3 offset = destination - Position;
        float distance = offset.magnitude;

        if ( distance <= 0.001f )
            return;

        Vector3 direction = offset / distance;
        Vector3 side = Vector3.Cross( direction, Vector3.up );

        //수직 연결도 화살표 폭을 계산할 수 있도록 기준 축 변경
        if ( side.sqrMagnitude <= 0.001f )
            side = Vector3.Cross( direction, Vector3.right );

        side.Normalize( );

        Vector3 tip = Position + offset * 0.7f;
        float size = Mathf.Min( 0.3f, distance * 0.15f );
        Vector3 tail = tip - direction * size;

        Gizmos.DrawLine( Position, destination );
        Gizmos.DrawLine( tip, tail + side * size * 0.5f );
        Gizmos.DrawLine( tip, tail - side * size * 0.5f );
    }

    private void OnDrawGizmosSelected ()
    {
        Color previousColor = Gizmos.color;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere( Position, 0.15f );

        if ( _connections != null )
        {
            foreach ( RouteNode node in _connections )
            {
                if ( node == null || node == this )
                    continue;

                //비활성 연결도 설정을 확인할 수 있도록 회색으로 구분
                Gizmos.color = node.isActiveAndEnabled
                    ? Color.yellow : Color.gray;

                DrawConnection( node.Position );
            }
        }

        Gizmos.color = previousColor;
    }

}
