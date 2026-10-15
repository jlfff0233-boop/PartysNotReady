using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 경로 탐색 데이터 - 노드 위치와 연결 비용 보관
/// </summary>
public class RouteSearchData
{
    /// <summary>
    /// 노드별 이동 위치 목록
    /// </summary>
    List<Vector3> _positions = new( );
    /// <summary>
    /// 노드별 연결과 비용 딕셔너리 목록
    /// </summary>
    List<Dictionary<int, float>> _connections = new( );

    /// <summary>
    /// 노드 개수
    /// </summary>
    public int Count => _positions.Count;


    /// <summary>
    /// 위치와 연결을 비우고 계산 공간 재사용
    /// </summary>
    public void Clear ()
    {
        _positions.Clear( );

        //딕셔너리는 남겨 두되 이전 연결과 비용은 모두 제거
        foreach ( Dictionary<int, float> connections in _connections )
            connections.Clear( );
    }

    /// <summary>
    /// 이동 위치를 노드로 추가
    /// </summary>
    /// <param name="position">검사를 마친 NavMesh 위치</param>
    /// <returns>추가한 노드 번호, 잘못된 위치이면 -1</returns>
    public int AddNode ( Vector3 position )
    {
        if ( IsFinite( position.x ) == false ||
             IsFinite( position.y ) == false ||
             IsFinite( position.z ) == false )
            return -1;

        int index = _positions.Count;

        _positions.Add( position );
        //기존 공간이 부족할 때만 새 연결 목록 생성
        if ( index >= _connections.Count )
            _connections.Add( new Dictionary<int, float>( ) );
        return index;
    }

    /// <summary>
    /// 단방향 연결과 이동 비용 추가
    /// </summary>
    /// <param name="from">출발 노드 번호</param>
    /// <param name="to">도착 노드 번호</param>
    /// <param name="distance">완전 경로의 길이</param>
    /// <returns>연결 추가 여부</returns>
    public bool AddConnection ( int from, int to, float distance )
    {
        if ( Contains( from ) == false ||
             Contains( to ) == false || from == to )
            return false;

        if ( IsFinite( distance ) == false || distance < 0f )
            return false;

        float directDistance = Vector3.Distance(
            _positions [ from ], _positions [ to ] );

        if ( IsFinite( directDistance ) == false )
            return false;

        //경로 끝의 허용 오차로 비용이 직선거리보다 작아지는 경우 보정
        //A*의 직선거리 추정값이 실제 연결 비용을 초과하지 않도록 유지
        float cost = Mathf.Max( distance, directDistance );

        //노드별 연결과 비용 딕셔너리 
        Dictionary<int, float> connections = _connections [ from ];

        //같은 방향의 연결이 중복되면 더 짧은 비용 유지
        if ( connections.TryGetValue( to, out float previous ) )
            cost = Mathf.Min( previous, cost );

        connections [ to ] = cost;
        return true;
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
    /// 노드 번호의 포함 여부 확인
    /// </summary>
    /// <param name="index">확인할 노드 번호</param>
    /// <returns>포함 여부</returns>
    public bool Contains ( int index )
    {
        return index >= 0 && index < Count;
    }

    /// <summary>
    /// 노드 위치 조회
    /// </summary>
    /// <param name="index">유효한 노드 번호</param>
    /// <returns>노드의 이동 위치</returns>
    public Vector3 GetPosition ( int index )
    {
        return _positions [ index ];
    }

    /// <summary>
    /// 노드의 연결 비용 조회
    /// </summary>
    /// <param name="index">유효한 노드 번호</param>
    /// <returns>연결된 노드 번호와 이동 비용</returns>
    public IReadOnlyDictionary<int, float> GetConnections ( int index )
    {
        return _connections [ index ];
    }

}
