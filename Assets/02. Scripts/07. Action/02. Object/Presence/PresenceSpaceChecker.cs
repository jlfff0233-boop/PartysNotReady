using UnityEngine;

/// <summary>
/// 등장 공간 검사 - 콜라이더별 영역 계산, 다른 물체와 겹침 확인
/// </summary>
public static class PresenceSpaceChecker
{

    #region ----- 공간 검사 -----

    /// <summary>
    /// 박스 영역의 빈 공간 확인
    /// </summary>
    /// <param name="target">콜라이더의 Transform</param>
    /// <param name="center">로컬 중심</param>
    /// <param name="size">로컬 크기</param>
    /// <param name="scale">월드 배율의 절댓값</param>
    /// <param name="mask">장애물 레이어</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>공간 확보 여부</returns>
    static bool CheckBox (
        Transform target, Vector3 center, Vector3 size,
        Vector3 scale, LayerMask mask, out string reason )
    {
        Vector3 worldCenter = target.TransformPoint( center );
        //전체 크기를 월드 반크기로 변환해 회전된 박스 검사에 사용
        Vector3 halfExtents = ColliderCalculator.GetBoxHalfExtents( size, scale );

        if ( ColliderCalculator.CheckFinite( worldCenter ) == false ||
             ColliderCalculator.CheckExtent( halfExtents.x ) == false ||
             ColliderCalculator.CheckExtent( halfExtents.y ) == false ||
             ColliderCalculator.CheckExtent( halfExtents.z ) == false )
            return Fail( "박스 중심 또는 크기가 유효하지 않습니다.", out reason );

        bool blocked = Physics.CheckBox(
            worldCenter, halfExtents, target.rotation, mask,
            QueryTriggerInteraction.Ignore );
        reason = blocked ? "등장 공간에 다른 물체가 있습니다." : string.Empty;
        return blocked == false;
    }

    /// <summary>
    /// 구체 영역의 빈 공간 확인
    /// </summary>
    /// <param name="collider">검사할 구체 콜라이더</param>
    /// <param name="scale">월드 배율의 절댓값</param>
    /// <param name="mask">장애물 레이어</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>공간 확보 여부</returns>
    static bool CheckSphere (
        SphereCollider collider, Vector3 scale,
        LayerMask mask, out string reason )
    {
        Vector3 center = collider.transform.TransformPoint( collider.center );
        //구체 반지름은 가장 큰 축의 배율 적용
        float radius = ColliderCalculator.GetSphereRadius( collider, scale );

        if ( ColliderCalculator.CheckFinite( center ) == false ||
             ColliderCalculator.CheckExtent( radius ) == false )
            return Fail( "구체 중심 또는 반지름이 유효하지 않습니다.", out reason );

        bool blocked = Physics.CheckSphere(
            center, radius, mask, QueryTriggerInteraction.Ignore );
        reason = blocked ? "등장 공간에 다른 물체가 있습니다." : string.Empty;
        return blocked == false;
    }

    /// <summary>
    /// 캡슐 영역의 빈 공간 확인
    /// </summary>
    /// <param name="collider">검사할 캡슐 콜라이더</param>
    /// <param name="scale">월드 배율의 절댓값</param>
    /// <param name="mask">장애물 레이어</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>공간 확보 여부</returns>
    static bool CheckCapsule (
        CapsuleCollider collider, Vector3 scale,
        LayerMask mask, out string reason )
    {
        int axis = collider.direction;
        if ( axis < 0 || axis > 2 || ColliderCalculator.CheckExtent( collider.height ) == false )
            return Fail( "캡슐 축 또는 높이가 유효하지 않습니다.", out reason );

        if ( ColliderCalculator.TryGetCapsule(
            collider, scale, out Vector3 center, out float radius,
            out float height, out Vector3 worldAxis,
            out float halfSegment, out reason ) == false )
            return false;

        if ( ColliderCalculator.CheckFinite( center ) == false ||
             ColliderCalculator.CheckExtent( radius ) == false ||
             ColliderCalculator.CheckExtent( height ) == false )
            return Fail( "캡슐 중심 또는 크기가 유효하지 않습니다.", out reason );

        //검사 끝점은 양 끝 구체의 중심
        Vector3 offset = worldAxis * halfSegment;
        if ( ColliderCalculator.CheckFinite( center + offset ) == false ||
             ColliderCalculator.CheckFinite( center - offset ) == false )
            return Fail( "캡슐 끝점이 유효하지 않습니다.", out reason );

        bool blocked = Physics.CheckCapsule(
            center + offset, center - offset, radius, mask,
            QueryTriggerInteraction.Ignore );
        reason = blocked ? "등장 공간에 다른 물체가 있습니다." : string.Empty;
        return blocked == false;
    }

    /// <summary>
    /// 콜라이더 종류에 맞춰 등장 공간 확인
    /// </summary>
    /// <param name="collider">등장할 물건의 콜라이더</param>
    /// <param name="obstacleMask">등장을 막는 물체의 레이어</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>등장 공간이 비어 있는지 여부</returns>
    public static bool IsClear (
        Collider collider, LayerMask obstacleMask, out string reason )
    {
        if ( collider == null )
            return Fail( "검사할 콜라이더가 없습니다.", out reason );
        if ( obstacleMask.value == 0 )
            return Fail( "장애물 레이어가 지정되지 않았습니다.", out reason );

        Transform target = collider.transform;
        Vector3 scale = ColliderCalculator.GetScale( target );
        if ( ColliderCalculator.CheckExtent( scale.x ) == false ||
             ColliderCalculator.CheckExtent( scale.y ) == false ||
             ColliderCalculator.CheckExtent( scale.z ) == false )
            return Fail( "콜라이더 배율이 유효하지 않습니다.", out reason );

        //회전과 비균일 부모 배율로 축이 기울어진 구조는 근사 검사하지 않음
        Vector3 x = target.TransformVector( Vector3.right );
        Vector3 y = target.TransformVector( Vector3.up );
        Vector3 z = target.TransformVector( Vector3.forward );
        if ( ColliderCalculator.CheckAxes( x, y, z ) == false )
            return Fail( "부모 배율과 회전으로 검사 축이 기울어졌습니다. 부모 배율을 균일하게 설정해 주세요.", out reason );

        //주변 물체의 Transform 변경을 물리 검사에 반영
        Physics.SyncTransforms( );

        //숨겨진 콜라이더의 형상 데이터로 검사하며 대상 자체는 켜지 않음
        switch ( collider )
        {
            case BoxCollider box:
                return CheckBox( target, box.center, box.size, scale, obstacleMask, out reason );
            case SphereCollider sphere:
                return CheckSphere( sphere, scale, obstacleMask, out reason );
            case CapsuleCollider capsule:
                return CheckCapsule( capsule, scale, obstacleMask, out reason );
            case MeshCollider mesh:
                if ( mesh.sharedMesh == null )
                    return Fail( "MeshCollider에 Mesh가 없습니다.", out reason );

                //Mesh는 실제 면 대신 외접 박스로 검사하므로 빈 부분도 막힐 수 있음
                Bounds bounds = mesh.sharedMesh.bounds;
                return CheckBox( target, bounds.center, bounds.size, scale, obstacleMask, out reason );
            default:
                return Fail( "지원하지 않는 콜라이더 종류입니다.", out reason );
        }
    }

    #endregion


    /// <summary>
    /// 검사 실패 사유 전달
    /// </summary>
    /// <param name="message">실패 내용</param>
    /// <param name="reason">반환할 실패 사유</param>
    /// <returns>항상 false</returns>
    static bool Fail ( string message, out string reason )
    {
        reason = message;
        return false;
    }


}
