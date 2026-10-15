using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 공통 배치 계산 - 표면에 맞출 위치, 회전과 범위 계산
/// </summary>
public static class PlacementCalculator
{
    #region ----- 방향 계산 -----

    /// <summary>
    /// 배치 회전 계산
    /// </summary>
    /// <param name="surfaceCollider">배치 표면 콜라이더</param>
    /// <param name="normal">배치 표면의 노멀 벡터</param>
    /// <returns>회전값</returns>
    public static Quaternion GetPlacementRotation (
        Collider surfaceCollider, Vector3 normal )
    {
        Transform surface = surfaceCollider.transform;

        //배치 회전에 사용할 전방 방향을 표면에 맞게 보정
        Vector3 forward = Vector3.ProjectOnPlane(
            Mathf.Abs( normal.y ) < 0.5f ? Vector3.up : surface.forward, normal );

        //보정한 값이 사실상 0이라 기준으로 쓸 수 없으면
        if ( forward.sqrMagnitude < 0.0001f )
            //right를 대신 표면에 눕혀서 배치 방향으로 사용
            forward = Vector3.ProjectOnPlane( surface.right, normal );

        //보정한 forward와 normal을 기준으로 최종 회전값 생성
        return Quaternion.LookRotation( forward.normalized, normal );
    }

    /// <summary>
    /// 배치 회전에서 충돌체의 가장 낮은 지점 계산
    /// </summary>
    /// <param name="root">형상 기준 루트</param>
    /// <param name="colliders">기능별로 선정한 검사 충돌체 목록</param>
    /// <param name="rotation">목적지의 아이템 회전</param>
    /// <param name="normal">길이가 1인 표면 법선</param>
    /// <param name="minimum">아이템 피벗 기준 최저 거리</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>최저 거리 계산 성공 여부</returns>
    static bool TryGetMinimum (
        Transform root, IReadOnlyList<Collider> colliders,
        Quaternion rotation, Vector3 normal,
        out float minimum, out string reason )
    {
        minimum = float.PositiveInfinity;
        reason = string.Empty;

        //현재 회전에서 목적지 회전으로 바뀌는 차이만 계산
        Quaternion difference = ColliderCalculator.GetRotationDifference( root, rotation );

        //충돌체를 실제로 회전시키는 대신 검사 방향을 반대로 회전
        //현재 형상을 그대로 읽어 목적지에서의 높이를 계산할 수 있음
        Vector3 direction = Quaternion.Inverse( difference ) * normal;
        direction.Normalize( );

        foreach ( Collider collider in colliders )
        {
            //활성 상태와 소속 정책은 호출한 기능에서 확인
            //형상 유효성과 방향별 거리는 공통 충돌체 계산에서 확인
            //기존 박스, 구체, 캡슐과 메시의 거리 계산을 재사용
            //지원하지 않는 형상이 하나라도 있으면 전체 배치 계산 실패
            if ( ColliderCalculator.TryGetMinimum(
                collider, root.position, direction,
                out float distance, out reason ) == false )
                return false;

            minimum = Mathf.Min( minimum, distance );
        }

        return true;
    }

    #endregion

    #region ----- 표면 맞춤 -----

    /// <summary>
    /// 콜라이더 바닥까지의 거리 계산
    /// </summary>
    /// <param name="bounds">배치 회전 적용 후 콜라이더 영역</param>
    /// <param name="position">배치 피벗 위치</param>
    /// <param name="up">배치 표면의 위쪽 방향</param>
    /// <returns>피벗에서 콜라이더 바닥까지의 거리</returns>
    static float GetBottomOffset ( Bounds bounds, Vector3 position, Vector3 up )
    {
        float centerOffset = Vector3.Dot( bounds.center - position, up );
        Vector3 absoluteUp = new Vector3(
            Mathf.Abs( up.x ), Mathf.Abs( up.y ), Mathf.Abs( up.z ) );

        float boundsRadius = Vector3.Dot( bounds.extents, absoluteUp );
        return boundsRadius - centerOffset;
    }

    /// <summary>
    /// 콜라이더 바닥을 반영한 배치 위치 계산
    /// </summary>
    /// <param name="bounds">배치 회전 적용 후 콜라이더 영역</param>
    /// <param name="position">배치 피벗 위치</param>
    /// <param name="surfaceNormal">아이템 회전과 독립적인 배치 표면 방향</param>
    /// <returns>콜라이더 바닥 높이가 반영된 배치 위치</returns>
    public static Vector3 GetPlacementPosition (
        Bounds bounds, Vector3 position, Vector3 surfaceNormal )
    {
        Vector3 up = surfaceNormal;
        return position + up * GetBottomOffset( bounds, position, up );
    }

    /// <summary>
    /// 표면에 맞출 이동량 계산
    /// </summary>
    /// <param name="root">현재 형상의 기준 루트</param>
    /// <param name="colliders">검사에 참여할 충돌체 목록</param>
    /// <param name="pose">검사할 루트의 월드 위치와 회전</param>
    /// <param name="surfacePoint">목적지 표면 위치</param>
    /// <param name="surfaceNormal">표면 바깥 방향</param>
    /// <param name="surfaceGap">표면과 아이템 사이의 월드 간격</param>
    /// <param name="offset">표면 법선 방향 이동량</param>
    /// <param name="reason">실패 사유</param>
    public static bool TryGetOffset (
        Transform root, IReadOnlyList<Collider> colliders, Pose pose,
        Vector3 surfacePoint, Vector3 surfaceNormal, float surfaceGap,
        out Vector3 offset, out string reason )
    {
        offset = Vector3.zero;
        reason = string.Empty;
        if ( root == null || colliders == null || colliders.Count == 0 ||
             ColliderCalculator.CheckFinite( root.position ) == false ||
             ColliderCalculator.CheckRotation( root.rotation ) == false ||
             ColliderCalculator.CheckFinite( pose.position ) == false ||
             ColliderCalculator.CheckRotation( pose.rotation ) == false ||
             ColliderCalculator.CheckFinite( surfacePoint ) == false ||
             ColliderCalculator.CheckFinite( surfaceNormal.sqrMagnitude ) == false ||
             surfaceNormal.sqrMagnitude < 0.000001f ||
             ColliderCalculator.CheckFinite( surfaceGap ) == false || surfaceGap < 0f )
        {
            reason = "배치 형상, 위치와 회전, 표면 방향과 간격을 확인해 주세요.";
            return false;
        }

        Vector3 normal = surfaceNormal.normalized;
        if ( TryGetMinimum(
            root, colliders, pose.rotation.normalized, normal,
            out float minimum, out reason ) == false )
            return false;

        //피벗 기준 최저 거리를 표면 기준으로 변환한 뒤 법선 방향만 보정
        minimum += Vector3.Dot( pose.position - surfacePoint, normal );
        offset = normal * ( surfaceGap - minimum );
        if ( ColliderCalculator.CheckFinite( offset ) == false )
        {
            offset = Vector3.zero;
            reason = "계산한 배치 이동량이 유효하지 않습니다.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 지정 표면에 놓을 아이템 위치와 회전 계산
    /// </summary>
    /// <param name="root">형상 기준 루트</param>
    /// <param name="colliders">기능별로 선정한 검사 충돌체 목록</param>
    /// <param name="support">목적지에서 확인한 실제 지지 표면</param>
    /// <param name="rotationOffset">아이템 자신의 배치 회전 보정</param>
    /// <param name="surfaceGap">표면과 아이템 사이의 월드 간격</param>
    /// <param name="pose">계산한 루트의 월드 위치와 회전</param>
    /// <param name="reason">실패 사유, 성공 시 빈 문자열</param>
    /// <returns>배치 위치와 회전 계산 성공 여부</returns>
    public static bool TryGetPose (
        Transform root, IReadOnlyList<Collider> colliders,
        RaycastHit support, Quaternion rotationOffset,
        float surfaceGap, out Pose pose, out string reason )
    {
        pose = default;

        if ( CheckInput(
            root, colliders, support, rotationOffset,
            surfaceGap, out reason ) == false )
            return false;

        Vector3 normal = support.normal.normalized;

        //목적지 표면 방향에 이동할 아이템 자신의 회전 보정을 적용
        Quaternion rotation = GetPlacementRotation(
            support.collider, normal ) * rotationOffset.normalized;

        if ( ColliderCalculator.CheckRotation( rotation ) == false )
        {
            reason = "계산한 배치 회전이 유효하지 않습니다.";
            return false;
        }

        rotation = rotation.normalized;

        Pose candidate = new Pose( support.point, rotation );
        if ( TryGetOffset(
            root, colliders, candidate, support.point, normal, surfaceGap,
            out Vector3 offset, out reason ) == false )
            return false;

        Vector3 position = candidate.position + offset;
        if ( ColliderCalculator.CheckFinite( position ) == false )
        {
            reason = "계산한 배치 위치가 유효하지 않습니다.";
            return false;
        }

        //Transform과 Rigidbody는 변경하지 않고 계산 결과만 반환
        pose = new Pose( position, rotation );
        return true;
    }

    #endregion

    #region ----- 범위 계산 -----

    /// <summary>
    /// 충돌 영역의 지정 방향 반경 계산
    /// </summary>
    /// <param name="bounds">대상 범위</param>
    /// <param name="axis">확인할 월드 방향</param>
    /// <returns>지정 축 방향 반경</returns>
    public static float GetBoundsExtent ( Bounds bounds, Vector3 axis )
    {
        //방향이 반대여도 같은 크기로 계산하기 위해서 양수로 변환
        Vector3 direction = new Vector3(
            Mathf.Abs( axis.x ), Mathf.Abs( axis.y ), Mathf.Abs( axis.z ) );

        //Bounds의 중심에서 지정 방향으로 얼마나 뻗어 있는지 계산
        return Vector3.Dot( bounds.extents, direction );
    }

    /// <summary>
    /// 회전한 아이템의 지정 방향 반경 계산
    /// </summary>
    /// <param name="size">아이템 축 기준 크기</param>
    /// <param name="rotation">검사할 회전</param>
    /// <param name="direction">월드 검사 방향</param>
    /// <returns>지정 방향 반경</returns>
    public static float GetItemExtent ( Vector3 size, Quaternion rotation, Vector3 direction )
    {
        Vector3 localDirection = Quaternion.Inverse( rotation ) * direction;
        Vector3 absoluteDirection = new Vector3(
            Mathf.Abs( localDirection.x ),
            Mathf.Abs( localDirection.y ),
            Mathf.Abs( localDirection.z ) );
        return Vector3.Dot( size * 0.5f, absoluteDirection );
    }

    /// <summary>
    /// 표면 기준 배치 영역 계산
    /// </summary>
    /// <param name="root">형상 기준 루트</param>
    /// <param name="colliders">기능별로 선정한 검사 충돌체 목록</param>
    /// <param name="pose">검사할 루트의 월드 위치와 회전</param>
    /// <param name="support">목적지 지지 표면</param>
    /// <param name="surfaceRotation">Y축이 표면 법선인 검사 회전</param>
    /// <param name="bounds">표면 위치 기준 좌우와 앞뒤 범위, 높이는 0</param>
    /// <param name="reason">실패 사유, 성공 시 빈 문자열</param>
    /// <returns>표면 방향 범위 계산 성공 여부</returns>
    public static bool TryGetSurfaceBounds (
        Transform root, IReadOnlyList<Collider> colliders,
        Pose pose, RaycastHit support,
        out Quaternion surfaceRotation, out Bounds bounds,
        out string reason )
    {
        surfaceRotation = Quaternion.identity;
        bounds = default;

        if ( CheckInput(
            root, colliders, support, pose.rotation,
            0f, out reason ) == false )
            return false;

        if ( ColliderCalculator.CheckFinite( pose.position ) == false )
        {
            reason = "검사할 배치 위치가 유효하지 않습니다.";
            return false;
        }

        //아이템 회전과 구분하여 표면의 좌우와 앞뒤 방향 구성
        Quaternion rotation = GetPlacementRotation(
            support.collider, support.normal.normalized );

        if ( ColliderCalculator.CheckRotation( rotation ) == false )
        {
            reason = "표면 범위를 검사할 회전이 유효하지 않습니다.";
            return false;
        }

        rotation = rotation.normalized;
        Vector3 right = rotation * Vector3.right;
        Vector3 forward = rotation * Vector3.forward;
        Quaternion itemRotation = pose.rotation.normalized;

        //반대 방향의 최저 거리에 부호를 바꾸면 정방향 최고 거리가 됨
        //각 계산은 모든 충돌체를 포함하므로 대표 충돌체 밖의 형상도 반영
        if ( TryGetMinimum(
            root, colliders, itemRotation, right,
            out float minX, out reason ) == false ||
             TryGetMinimum(
            root, colliders, itemRotation, -right,
            out float negativeMaxX, out reason ) == false ||
             TryGetMinimum(
            root, colliders, itemRotation, forward,
            out float minZ, out reason ) == false ||
             TryGetMinimum(
            root, colliders, itemRotation, -forward,
            out float negativeMaxZ, out reason ) == false )
            return false;

        //피벗 기준 거리를 목적지 표면 위치 기준 거리로 변환
        Vector3 offset = pose.position - support.point;
        float offsetX = Vector3.Dot( offset, right );
        float offsetZ = Vector3.Dot( offset, forward );

        Vector3 min = new Vector3(
            minX + offsetX, 0f, minZ + offsetZ );
        Vector3 max = new Vector3(
            -negativeMaxX + offsetX, 0f, -negativeMaxZ + offsetZ );

        Vector3 size = max - min;
        Vector3 center = min + size * 0.5f;

        if ( ColliderCalculator.CheckFinite( size ) == false ||
             ColliderCalculator.CheckFinite( center ) == false ||
             size.x <= 0f || size.z <= 0f )
        {
            reason = "표면 경계를 검사할 아이템 범위가 유효하지 않습니다.";
            return false;
        }

        surfaceRotation = rotation;
        bounds = new Bounds( center, size );
        return true;
    }

    #endregion

    #region ----- 상태 검사 -----

    /// <summary>
    /// 배치 위치와 회전 계산에 필요한 입력 확인
    /// </summary>
    /// <param name="root">형상 기준 루트</param>
    /// <param name="colliders">계산할 충돌체 목록</param>
    /// <param name="support">목적지 지지 표면</param>
    /// <param name="rotationOffset">아이템 배치 회전 보정</param>
    /// <param name="surfaceGap">표면과 아이템 사이의 간격</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>입력 유효 여부</returns>
    static bool CheckInput (
        Transform root, IReadOnlyList<Collider> colliders,
        RaycastHit support, Quaternion rotationOffset,
        float surfaceGap, out string reason )
    {
        reason = string.Empty;

        if ( root == null ||
             colliders == null || colliders.Count == 0 )
        {
            reason = "기준 루트와 배치에 사용할 충돌체 목록이 필요합니다.";
            return false;
        }

        if ( ColliderCalculator.CheckFinite( root.position ) == false ||
             ColliderCalculator.CheckRotation( root.rotation ) == false ||
             ColliderCalculator.CheckRotation( rotationOffset ) == false )
        {
            reason = "아이템 위치와 배치 회전 보정을 확인해 주세요.";
            return false;
        }

        Collider surface = support.collider;

        if ( surface == null || surface.enabled == false ||
             surface.gameObject.activeInHierarchy == false ||
             surface.isTrigger == true ||
             surface.transform.IsChildOf( root ) == true )
        {
            reason = "아이템 외부의 활성 일반 지지 표면이 필요합니다.";
            return false;
        }

        if ( ColliderCalculator.CheckFinite( support.point ) == false ||
             ColliderCalculator.CheckFinite( support.normal ) == false ||
             ColliderCalculator.CheckFinite(
                 support.normal.sqrMagnitude ) == false ||
             support.normal.sqrMagnitude < 0.000001f )
        {
            reason = "지지 표면의 위치와 법선이 유효하지 않습니다.";
            return false;
        }

        if ( ColliderCalculator.CheckFinite( surfaceGap ) == false ||
             surfaceGap < 0f )
        {
            reason = "표면 간격은 유한한 0 이상의 값이어야 합니다.";
            return false;
        }

        return true;
    }

    #endregion

}
