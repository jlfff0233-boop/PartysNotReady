using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 방 - 영역, 잠복 지점 및 연결된 방 제공
/// </summary>
[DisallowMultipleComponent]
public class Room : MonoBehaviour
{
    [Header( "----- 표시 -----" )]
    [SerializeField] string _displayName;     //장소 표시 이름


    [Header( "----- 방 영역 -----" )]
    [SerializeField] Vector3 _center = new( 0f, 1.5f, 0f );     //영역 중심
    [SerializeField] Vector3 _size = new( 10f, 3f, 10f );     //영역 크기

    [Header( "----- 잠복 -----" )]
    [SerializeField] Transform _hidePoint;     //잠복 중 재배치 지점

    [Header( "----- 연결 -----" )]
    [SerializeField] Room [ ] _connectedRooms;     //이동 가능한 다른 방

    /// <summary>
    /// 장소 표시 이름
    /// </summary>
    public string DisplayName => _displayName;

    /// <summary>
    /// 잠복 중 재배치할 지점
    /// </summary>
    public Transform HidePoint => _hidePoint;


    private void OnEnable ()
    {
        RoomRegister.RegisterRoom( this );
    }

    private void OnDisable ()
    {
        RoomRegister.UnregisterRoom( this );
    }


    /// <summary>
    /// 조건에 맞는 활성 연결 방 조회
    /// </summary>
    /// <param name="results">조회 결과를 받을 목록</param>
    /// <param name="requireHidePoint">잠복 지점 필수 여부, 기존 적 조회는 true</param>
    public void GetConnectedRooms ( List<Room> results, bool requireHidePoint = true )
    {
        results.Clear( );

        if ( _connectedRooms == null )
            return;

        foreach ( Room room in _connectedRooms )
        {
            if ( room == null || room == this )
                continue;

            if ( room.isActiveAndEnabled == false ||
                 ( requireHidePoint && room.HidePoint == null ) )
                continue;

            //중복 연결은 한 번만 반환
            if ( results.Contains( room ) == false )
                results.Add( room );
        }
    }


    /// <summary>
    /// 방 내부 수평 후보의 바닥 탐색 시작점과 수직 범위 조회
    /// </summary>
    /// <param name="position">방 상단 위의 월드 탐색 시작점</param>
    /// <param name="distance">방 하단까지의 수직 탐색 거리</param>
    /// <returns>위치 조회 성공 여부</returns>
    public bool TryGetRandomFloorProbe ( out Vector3 position , out float distance )
    {
        position = default;
        distance = 0f;

        if ( isActiveAndEnabled == false || HasValidArea( ) == false )
            return false;

        Vector3 halfSize = _size * 0.5f;
        Vector3 localPosition = _center + new Vector3(
            Random.Range( -halfSize.x, halfSize.x ),
            0f,
            Random.Range( -halfSize.z, halfSize.z ) );

        //회전과 크기를 포함한 기존 방 영역 설정 사용
        position = transform.TransformPoint( localPosition );
        Vector3 center = transform.TransformPoint( _center );
        float height = Mathf.Abs( transform.TransformVector( Vector3.right * halfSize.x ).y ) +
                       Mathf.Abs( transform.TransformVector( Vector3.up * halfSize.y ).y ) +
                       Mathf.Abs( transform.TransformVector( Vector3.forward * halfSize.z ).y );
        if ( height <= 0f )
            return false;

        //월드 수직 방향으로 탐색하고 실제 충돌점의 방 포함 여부는 호출자가 확인
        const float margin = 0.01f;
        position.y = center.y + height + margin;
        distance = ( height + margin ) * 2f;
        return true;
    }

    /// <summary>
    /// 월드 위치가 방 영역 안에 있는지 확인
    /// </summary>
    /// <param name="position">검사할 월드 위치</param>
    /// <returns>영역 내부 여부</returns>
    public bool Contains ( Vector3 position )
    {
        if ( isActiveAndEnabled == false || HasValidArea( ) == false )
            return false;

        //방의 회전과 크기를 반영해 로컬 좌표에서 검사
        Vector3 offset = transform.InverseTransformPoint( position ) - _center;
        Vector3 halfSize = _size * 0.5f;

        //맞닿은 영역의 경계가 양쪽에 동시에 포함되지 않도록
        //최소 경계는 포함하고 최대 경계는 제외
        return offset.x >= -halfSize.x && offset.x < halfSize.x &&
               offset.y >= -halfSize.y && offset.y < halfSize.y &&
               offset.z >= -halfSize.z && offset.z < halfSize.z;
    }


    /// <summary>
    /// 방 영역 설정의 유효성 확인
    /// </summary>
    /// <returns>모든 축의 크기가 양수이면 true</returns>
    bool HasValidArea ()
    {
        return _size.x > 0f &&
               _size.y > 0f &&
               _size.z > 0f;
    }

    private void OnDrawGizmosSelected ()
    {
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube( _center, _size );

        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }

}
