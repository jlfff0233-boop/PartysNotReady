using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 요구 적 난동 대상과 접근 위치 선택
/// </summary>
public class DemandAttackPositionSelector
{
    const int DirectionCount = 8;		//아이템 주변 접근 방향 수

    CharacterMover _mover;		//접근 경로 검사
    ItemAttacker _attacker;		//공격 충돌체 조건
    float _approachDist;		//아이템 표면에서 접근 위치까지의 거리
    float _sampleDist;		//NavMesh 위치 보정 범위

    /// <summary>
    /// 이번 탐색의 활성 아이템 목록
    /// </summary>
    List<ItemCarry> _items = new( );

    /// <summary>
    /// 현재 아이템의 충돌체 목록
    /// </summary>
    List<Collider> _colliders = new( );

    /// <summary>
    /// 접근 위치 선택에 필요한 조건 연결
    /// </summary>
    /// <param name="mover">초기화된 이동 모듈</param>
    /// <param name="attacker">아이템 명중 검사</param>
    /// <param name="approachDist">아이템 표면과 접근 위치의 거리</param>
    /// <param name="sampleDist">NavMesh 보정 범위</param>
    public DemandAttackPositionSelector (
        CharacterMover mover, ItemAttacker attacker, float approachDist, float sampleDist )
    {
        _mover = mover;
        _attacker = attacker;
        _approachDist = approachDist;
        _sampleDist = sampleDist;
    }

    /// <summary>
    /// 직접 지정한 난동 지점의 접근 위치와 공격 방향 조회
    /// </summary>
    /// <param name="point">직접 지정한 지점</param>
    /// <param name="sampleDist">현재 NavMesh 보정 범위</param>
    /// <param name="position">접근 위치</param>
    /// <param name="rotation">공격 방향</param>
    /// <returns>위치 선택 성공 여부</returns>
    public bool TrySelectAssigned (
        Transform point, float sampleDist, out Vector3 position, out Quaternion rotation )
    {
        position = default;
        rotation = Quaternion.identity;

        if ( point == null || point.gameObject.activeInHierarchy == false )
            return false;

        Vector3 forward = point.forward;
        forward.y = 0f;
        if ( forward.sqrMagnitude <= 0.0001f ||
             _mover.PathChecker.TryGetPosition(
                 point.position, sampleDist, out position ) == false ||
             _mover.PathChecker.TryGetDistance( _mover.Position, position, out _ ) == false )
            return false;

        rotation = Quaternion.LookRotation( forward );
        return true;
    }

    /// <summary>
    /// 공격할 아이템과 접근 위치, 바라볼 방향 조회
    /// </summary>
    /// <param name="room">탐색할 방</param>
    /// <param name="excluded">이번 난동의 재선택 제외 아이템</param>
    /// <param name="target">선택한 아이템</param>
    /// <param name="position">접근 위치</param>
    /// <param name="rotation">공격 방향</param>
    /// <returns>선택 성공 여부</returns>
    public bool TrySelect (
        Room room, HashSet<ItemCarry> excluded, out ItemCarry target,
        out Vector3 position, out Quaternion rotation )
    {
        target = null;
        position = default;
        rotation = Quaternion.identity;
        if ( room == null || room.isActiveAndEnabled == false || _mover.CanNavigate == false )
            return false;

        ItemRegister.GetActive( _items );
        float bestDistance = float.PositiveInfinity;

        foreach ( ItemCarry item in _items )
        {
            if ( item.IsHold == true || excluded.Contains( item ) == true ||
                 item.gameObject.scene != room.gameObject.scene ||
                 room.Contains( item.transform.position ) == false )
                continue;

            item.GetComponentsInChildren<Collider>( false, _colliders );
            foreach ( Collider collider in _colliders )
            {
                //다른 자식 아이템의 충돌체는 부모 아이템 후보에서 제외
                if ( collider.GetComponentInParent<ItemCarry>( ) != item ||
                     _attacker.IsTargetCollider( collider ) == false )
                    continue;

                for ( int i = 0; i < DirectionCount; i++ )
                {
                    float angle = i * Mathf.PI * 2f / DirectionCount;
                    Vector3 direction = new( Mathf.Cos( angle ), 0f, Mathf.Sin( angle ) );
                    Bounds bounds = collider.bounds;
                    Vector3 outside = bounds.center + direction *
                        ( bounds.extents.magnitude + _approachDist );
                    //충돌체 종류에 제한이 없는 Bounds 표면을 접근 기준으로 사용
                    Vector3 surface = bounds.ClosestPoint( outside );
                    Vector3 probe = surface + direction * _approachDist;
                    probe.y = _mover.Position.y;

                    if ( _mover.PathChecker.TryGetPosition(
                        probe, _sampleDist, out Vector3 candidate ) == false )
                        continue;

                    if ( room.Contains( candidate ) == false ||
                         _mover.PathChecker.TryGetDistance(
                             _mover.Position, candidate, out float distance ) == false ||
                         distance >= bestDistance )
                        continue;

                    Vector3 forward = surface - candidate;
                    forward.y = 0f;
                    if ( forward.sqrMagnitude <= 0.0001f )
                        continue;

                    bestDistance = distance;
                    target = item;
                    position = candidate;
                    rotation = Quaternion.LookRotation( forward );
                }
            }
        }

        _items.Clear( );
        _colliders.Clear( );
        return target != null;
    }
}
