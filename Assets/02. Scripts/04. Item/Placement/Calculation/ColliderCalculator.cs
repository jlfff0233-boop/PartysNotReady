using UnityEngine;

/// <summary>
/// 공통 충돌체 계산 - 크기, 중심과 방향별 거리 계산
/// </summary>
public static class ColliderCalculator
{

    /// <summary>
    /// 충돌체의 월드 배율 절댓값 계산
    /// </summary>
    /// <param name="target">형상의 Transform</param>
    /// <returns>월드 배율 절댓값</returns>
    public static Vector3 GetScale ( Transform target )
    {
        Vector3 scale = target.lossyScale;
        return new Vector3( Mathf.Abs( scale.x ), Mathf.Abs( scale.y ), Mathf.Abs( scale.z ) );
    }

    /// <summary>
    /// 박스의 월드 반크기 계산
    /// </summary>
    /// <param name="size">로컬 전체 크기</param>
    /// <param name="scale">월드 배율 절댓값</param>
    /// <returns>각 축의 월드 반크기</returns>
    public static Vector3 GetBoxHalfExtents ( Vector3 size, Vector3 scale )
    {
        return Vector3.Scale( size, scale ) * 0.5f;
    }

    /// <summary>
    /// 구체의 월드 반지름 계산
    /// </summary>
    /// <param name="collider">구체 충돌체</param>
    /// <param name="scale">월드 배율 절댓값</param>
    /// <returns>월드 반지름</returns>
    public static float GetSphereRadius ( SphereCollider collider, Vector3 scale )
    {
        //Unity 구체 충돌체는 가장 큰 축의 배율로 반지름을 확대
        return collider.radius * Mathf.Max( scale.x, Mathf.Max( scale.y, scale.z ) );
    }

    /// <summary>
    /// 캡슐의 월드 크기와 중심선 계산
    /// </summary>
    /// <param name="collider">캡슐 충돌체</param>
    /// <param name="scale">월드 배율 절댓값</param>
    /// <param name="center">월드 중심</param>
    /// <param name="radius">월드 반지름</param>
    /// <param name="height">월드 전체 높이</param>
    /// <param name="axis">월드 중심선 방향</param>
    /// <param name="halfSegment">중심선의 절반 길이</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>형상 계산 성공 여부</returns>
    public static bool TryGetCapsule (
        CapsuleCollider collider, Vector3 scale, out Vector3 center,
        out float radius, out float height, out Vector3 axis,
        out float halfSegment, out string reason )
    {
        center = default;
        radius = height = halfSegment = 0f;
        axis = default;
        reason = string.Empty;

        int direction = collider.direction;
        if ( direction < 0 || direction > 2 )
            return Fail( "캡슐 방향이 유효하지 않습니다.", out reason );

        center = collider.transform.TransformPoint( collider.center );
        //길이 방향은 높이에, 나머지 두 축 중 큰 배율은 반지름에 적용
        radius = collider.radius * Mathf.Max(
            scale [ ( direction + 1 ) % 3 ], scale [ ( direction + 2 ) % 3 ] );
        height = collider.height * scale [ direction ];

        Vector3 localAxis = direction == 0 ? Vector3.right
            : direction == 1 ? Vector3.up : Vector3.forward;
        axis = collider.transform.rotation * localAxis;
        //끝 구체를 제외한 중심선 길이. 높이가 지름보다 작으면 중심선은 0
        halfSegment = Mathf.Max( 0f, height * 0.5f - radius );
        return true;
    }

    /// <summary>
    /// 박스 또는 메시를 감싸는 박스의 중심과 크기 조회
    /// </summary>
    /// <param name="collider">박스 또는 메시 충돌체</param>
    /// <param name="center">로컬 중심</param>
    /// <param name="size">로컬 크기</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>형상 조회 성공 여부</returns>
    public static bool TryGetBox (
        Collider collider, out Vector3 center, out Vector3 size, out string reason )
    {
        center = size = default;
        reason = string.Empty;
        if ( collider is BoxCollider box )
        {
            center = box.center;
            size = box.size;
            return true;
        }

        if ( collider is MeshCollider mesh )
        {
            if ( mesh.sharedMesh == null || mesh.convex == false )
                return Fail( "유효한 Convex MeshCollider가 필요합니다.", out reason );

            //배치와 공간 검사는 동일한 메시 외접 박스를 사용
            Bounds bounds = mesh.sharedMesh.bounds;
            center = bounds.center;
            size = bounds.size;
            return true;
        }

        return Fail( "지원하지 않는 Collider 종류입니다.", out reason );
    }

    /// <summary>
    /// 지정한 방향의 박스 반경 계산
    /// </summary>
    /// <param name="target">박스의 Transform</param>
    /// <param name="localSize">박스의 로컬 크기</param>
    /// <param name="normal">길이가 1인 거리 계산 방향</param>
    /// <returns>지정한 방향으로 중심에서 끝까지의 거리</returns>
    static float GetBoxExtent (
        Transform target, Vector3 localSize, Vector3 normal )
    {
        Vector3 half = localSize * 0.5f;

        Vector3 x = target.TransformVector( Vector3.right * half.x );
        Vector3 y = target.TransformVector( Vector3.up * half.y );
        Vector3 z = target.TransformVector( Vector3.forward * half.z );

        return Mathf.Abs( Vector3.Dot( x, normal ) ) +
               Mathf.Abs( Vector3.Dot( y, normal ) ) +
               Mathf.Abs( Vector3.Dot( z, normal ) );
    }

    /// <summary>
    /// 지정한 방향에서 충돌체의 가장 가까운 지점까지 거리 계산
    /// </summary>
    /// <param name="collider">형상을 조회할 충돌체</param>
    /// <param name="surfacePoint">거리 기준이 되는 표면 위치</param>
    /// <param name="normal">길이가 1인 거리 계산 방향</param>
    /// <param name="minimum">기준점에서 계산한 최저 거리, 반대 방향은 음수</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>형상 계산 성공 여부</returns>
    public static bool TryGetMinimum (
         Collider collider, Vector3 surfacePoint, Vector3 normal,
         out float minimum, out string reason )
    {
        minimum = 0f;
        reason = string.Empty;

        if ( collider == null )
            return Fail( "충돌체가 없습니다.", out reason );

        if ( CheckTransform( collider.transform, out reason ) == false )
            return false;

        Transform target = collider.transform;
        Vector3 scale = GetScale( target );

        if ( CheckFinite( scale ) == false ||
             scale.x <= 0f || scale.y <= 0f || scale.z <= 0f )
            return Fail( "Transform의 월드 배율이 유효하지 않습니다.", out reason );

        Vector3 center;
        float extent;

        switch ( collider )
        {
            case BoxCollider:
            case MeshCollider:
                if ( TryGetBox(
                    collider, out Vector3 localCenter, out Vector3 localSize, out reason ) == false )
                    return false;
                center = target.TransformPoint( localCenter );
                extent = GetBoxExtent( target, localSize, normal );
                break;

            case SphereCollider sphere:
                center = target.TransformPoint( sphere.center );
                extent = GetSphereRadius( sphere, scale );
                break;

            case CapsuleCollider capsule:
                {
                    if ( TryGetCapsule(
                        capsule, scale, out Vector3 capsuleCenter,
                        out float radius, out float height, out Vector3 worldAxis,
                        out float halfSegment, out reason ) == false )
                        return false;
                    center = capsuleCenter;

                    if ( CheckFinite( radius ) == false || radius <= 0f ||
                         CheckFinite( height ) == false || height <= 0f )
                        return Fail( "캡슐 크기가 유효하지 않습니다.", out reason );

                    //캡슐 중심선의 투영 길이에 끝 구체 반지름을 더함
                    extent = Mathf.Abs( Vector3.Dot( worldAxis, normal ) ) *
                        halfSegment + radius;
                    break;
                }

            default:
                return Fail( "지원하지 않는 Collider 종류입니다.", out reason );
        }

        if ( CheckFinite( center ) == false ||
             CheckFinite( extent ) == false || extent <= 0f )
            return Fail( "충돌체 중심 또는 형상 크기가 유효하지 않습니다.", out reason );

        //표면 위치를 기준으로 계산하여 큰 월드 좌표의 오차를 줄임
        minimum = Vector3.Dot( center - surfacePoint, normal ) - extent;

        if ( CheckFinite( minimum ) == false )
            return Fail( "표면 기준 거리가 유효하지 않습니다.", out reason );

        return true;
    }

    /// <summary>
    /// 목적지 기준 월드 지점 계산
    /// </summary>
    /// <param name="root">현재 기준 루트</param>
    /// <param name="pose">목적지 루트 위치와 회전, 월드 크기는 유지</param>
    /// <param name="point">현재 형상의 월드 지점</param>
    /// <returns>목적지에서의 월드 지점</returns>
    public static Vector3 GetPosition ( Transform root, Pose pose, Vector3 point )
    {
        //현재 위치와 회전 검사는 원래 지점을 사용해 불필요한 좌표 오차 방지
        if ( root.position.Equals( pose.position ) && root.rotation.Equals( pose.rotation ) )
            return point;

        Quaternion difference = GetRotationDifference( root, pose.rotation );
        //현재 루트에서 떨어진 거리에 회전 차이를 적용한 뒤 목적지로 이동
        return pose.position + difference * ( point - root.position );
    }

    /// <summary>
    /// 목적지와 현재 회전의 차이 계산
    /// </summary>
    /// <param name="root">현재 기준 루트</param>
    /// <param name="rotation">목적지 회전</param>
    /// <returns>현재 월드 방향에 적용할 회전 차이</returns>
    public static Quaternion GetRotationDifference ( Transform root, Quaternion rotation )
    {
        //현재 회전과 같으면 충돌체 형상의 방향 유지
        if ( root.rotation.Equals( rotation ) )
            return Quaternion.identity;

        return rotation.normalized * Quaternion.Inverse( root.rotation );
    }

    /// <summary>
    /// 정규화할 수 있는 회전값인지 확인
    /// </summary>
    /// <param name="rotation">검사할 회전</param>
    /// <returns>유한하고 길이가 충분한 회전값 여부</returns>
    public static bool CheckRotation ( Quaternion rotation )
    {
        float lengthSquared =
            rotation.x * rotation.x +
            rotation.y * rotation.y +
            rotation.z * rotation.z +
            rotation.w * rotation.w;

        return CheckFinite( lengthSquared ) == true &&
               lengthSquared > 0.000001f;
    }

    /// <summary>
    /// 세 축이 서로 직각을 유지하는지 확인
    /// </summary>
    /// <param name="x">변환된 X축</param>
    /// <param name="y">변환된 Y축</param>
    /// <param name="z">변환된 Z축</param>
    /// <returns>축 기울어짐이 허용 범위 안인지 여부</returns>
    public static bool CheckAxes ( Vector3 x, Vector3 y, Vector3 z )
    {
        x.Normalize( );
        y.Normalize( );
        z.Normalize( );

        return ( Mathf.Abs( Vector3.Dot( x, y ) ) > 0.001f ||
                 Mathf.Abs( Vector3.Dot( y, z ) ) > 0.001f ||
                 Mathf.Abs( Vector3.Dot( z, x ) ) > 0.001f ) == false;
    }

    /// <summary>
    /// 크기와 축 방향을 계산할 수 있는지 확인
    /// </summary>
    /// <param name="target">확인할 Transform</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>형상을 계산할 수 있는지 여부</returns>
    public static bool CheckTransform ( Transform target, out string reason )
    {
        reason = string.Empty;

        Vector3 x = target.TransformVector( Vector3.right );
        Vector3 y = target.TransformVector( Vector3.up );
        Vector3 z = target.TransformVector( Vector3.forward );

        if ( CheckFinite( x ) == false ||
             CheckFinite( y ) == false ||
             CheckFinite( z ) == false ||
             CheckFinite( x.magnitude ) == false || x.magnitude <= 0f ||
             CheckFinite( y.magnitude ) == false || y.magnitude <= 0f ||
             CheckFinite( z.magnitude ) == false || z.magnitude <= 0f )
            return Fail( "Transform의 월드 배율이 유효하지 않습니다.", out reason );

        //부모 배율과 자식 회전으로 세 축이 직각을 잃은 경우 거절
        if ( CheckAxes( x, y, z ) == false )
        {
            return Fail(
                "Transform 축이 직교하지 않습니다. 부모 배율과 자식 회전을 확인해 주세요.",
                out reason );
        }

        return true;
    }

    /// <summary>
    /// 벡터에 NaN이나 무한대가 없는지 확인
    /// </summary>
    /// <param name="value">검사할 벡터</param>
    /// <returns>유한한 벡터 여부</returns>
    public static bool CheckFinite ( Vector3 value )
    {
        return CheckFinite( value.x ) &&
               CheckFinite( value.y ) &&
               CheckFinite( value.z );
    }

    /// <summary>
    /// NaN이나 무한대가 아닌 값인지 확인
    /// </summary>
    /// <param name="value">검사할 값</param>
    /// <returns>유한한 값 여부</returns>
    public static bool CheckFinite ( float value )
    {
        return float.IsNaN( value ) == false &&
               float.IsInfinity( value ) == false;
    }

    /// <summary>
    /// 접촉 오차를 제외해도 크기가 남는지 확인
    /// </summary>
    /// <param name="value">검사할 크기</param>
    /// <param name="tolerance">접촉 허용 거리, 생략하면 양수 여부만 검사</param>
    /// <returns>조회 가능한 크기 여부</returns>
    public static bool CheckExtent ( float value, float tolerance = 0f )
    {
        return CheckFinite( value ) == true && value > tolerance;
    }

    /// <summary>
    /// 검사 실패 사유 반환
    /// </summary>
    /// <param name="message">실패 설명</param>
    /// <param name="reason">반환할 실패 사유</param>
    /// <returns>항상 false</returns>
    static bool Fail ( string message, out string reason )
    {
        reason = message;
        return false;
    }

}
