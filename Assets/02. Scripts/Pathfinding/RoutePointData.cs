using UnityEngine;

/// <summary>
/// 경유점 데이터 - 이동 위치와 원본 노드 보관
/// </summary>
public struct RoutePointData
{
    Vector3 _position;        //NavMesh에서 확인한 이동 위치
    RouteNode _node;        //경유점의 원본 노드
    Vector3 _nodePosition;        //경로 구성 당시의 노드 위치
    bool _hasNode;        //원본 노드가 있는 경유점인지 여부

    /// <summary>
    /// NavMesh에서 확인한 이동 위치
    /// </summary>
    public Vector3 Position => _position;

    /// <summary>
    /// 경유점의 원본 노드
    /// </summary>
    public RouteNode Node => _node;

    /// <summary>
    /// 경로 구성 당시의 노드 위치
    /// </summary>
    public Vector3 NodePosition => _nodePosition;

    /// <summary>
    /// 원본 노드가 있는 경유점인지 여부
    /// </summary>
    public bool HasNode => _hasNode;


    /// <summary>
    /// 이동 위치와 노드 상태 보관
    /// </summary>
    /// <param name="position">검사를 마친 이동 위치</param>
    /// <param name="node">원본 노드, 최종 목적지는 null</param>
    public RoutePointData ( Vector3 position, RouteNode node )
    {
        _position = position;
        _node = node;

        //나중에 노드가 파괴돼도 처음부터 노드가 없는 목적지와 구분
        _hasNode = node != null;
        _nodePosition = _hasNode ? node.Position : default;
    }
}
