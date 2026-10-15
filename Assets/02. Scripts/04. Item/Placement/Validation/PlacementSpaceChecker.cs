using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 공통 배치 공간 검사 - 현재 또는 목적지 형상과 장애물 겹침 확인
/// </summary>
public static class PlacementSpaceChecker
{

    #region ----- 형상 조회 -----

    /// <summary>
    /// 박스와 겹치는 주변 Collider 조회
    /// </summary>
    /// <param name="target">형상의 Transform</param>
    /// <param name="root">현재 형상의 기준 루트</param>
    /// <param name="pose">검사할 루트의 월드 위치와 회전</param>
    /// <param name="localCenter">로컬 중심</param>
    /// <param name="localSize">로컬 크기</param>
    /// <param name="scale">월드 배율의 절댓값</param>
    /// <param name="mask">장애물 레이어</param>
    /// <param name="tolerance">접촉 허용 거리</param>
    /// <param name="overlaps">조회한 주변 충돌체 목록</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>유효한 박스로 조회했는지 여부</returns>
    static bool TryGetBoxOverlaps (
        Transform target , Transform root , Pose pose , Vector3 localCenter , Vector3 localSize ,
        Vector3 scale , LayerMask mask , float tolerance ,
        out Collider [ ] overlaps , out string reason )
    {
        overlaps = null;
        reason = string.Empty;

        Vector3 center = ColliderCalculator.GetPosition (
            root , pose , target.TransformPoint ( localCenter ) );
        Quaternion rotation = ColliderCalculator.GetRotationDifference (
            root , pose.rotation ) * target.rotation;
        Vector3 halfExtents = ColliderCalculator.GetBoxHalfExtents ( localSize , scale );

        if ( ColliderCalculator.CheckFinite ( center ) == false ||
             ColliderCalculator.CheckExtent ( halfExtents.x , tolerance ) == false ||
             ColliderCalculator.CheckExtent ( halfExtents.y , tolerance ) == false ||
             ColliderCalculator.CheckExtent ( halfExtents.z , tolerance ) == false )
            return Fail ( "박스 중심 또는 크기가 유효하지 않습니다." , out reason );

        //지지 표면과 닿기만 한 경우를 겹침으로 판단하지 않도록 축소
        halfExtents -= Vector3.one * tolerance;

        overlaps = Physics.OverlapBox (
            center , halfExtents , rotation , mask ,
            QueryTriggerInteraction.Ignore );
        return true;
    }

    /// <summary>
    /// 충돌체와 겹치는 주변 Collider 조회
    /// </summary>
    /// <param name="collider">비활성 상태에서도 형상을 읽을 충돌체</param>
    /// <param name="root">현재 형상의 기준 루트</param>
    /// <param name="pose">검사할 루트의 월드 위치와 회전</param>
    /// <param name="mask">장애물 레이어</param>
    /// <param name="tolerance">접촉 허용 거리</param>
    /// <param name="overlaps">조회한 주변 충돌체 목록</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>유효한 형상으로 조회했는지 여부</returns>
    static bool TryGetOverlaps (
        Collider collider , Transform root , Pose pose , LayerMask mask , float tolerance ,
        out Collider [ ] overlaps , out string reason )
    {
        overlaps = null;
        reason = string.Empty;

        Transform target = collider.transform;
        Quaternion difference = ColliderCalculator.GetRotationDifference ( root , pose.rotation );
        Vector3 scale = ColliderCalculator.GetScale ( target );

        switch ( collider )
        {
            case BoxCollider:
            case MeshCollider:
                //메시는 실제 표면 대신 감싸는 박스로 검사하므로 빈 부분도 막힐 수 있음
                if ( ColliderCalculator.TryGetBox (
                    collider , out Vector3 localCenter , out Vector3 localSize , out reason ) == false )
                    return false;
                return TryGetBoxOverlaps (
                    target , root , pose , localCenter , localSize , scale , mask , tolerance ,
                    out overlaps , out reason );

            case SphereCollider sphere:
                {
                    Vector3 center = ColliderCalculator.GetPosition (
                        root , pose , target.TransformPoint ( sphere.center ) );
                    float radius = ColliderCalculator.GetSphereRadius ( sphere , scale );

                    if ( ColliderCalculator.CheckFinite ( center ) == false ||
                         ColliderCalculator.CheckExtent ( radius , tolerance ) == false )
                        return Fail ( "구체 중심 또는 반지름이 유효하지 않습니다." , out reason );

                    overlaps = Physics.OverlapSphere (
                        center , radius - tolerance , mask ,
                        QueryTriggerInteraction.Ignore );
                    return true;
                }

            case CapsuleCollider capsule:
                {
                    if ( ColliderCalculator.TryGetCapsule (
                        capsule , scale , out Vector3 capsuleCenter ,
                        out float radius , out float height , out Vector3 worldAxis ,
                        out float halfSegment , out reason ) == false )
                        return false;
                    Vector3 center = ColliderCalculator.GetPosition ( root , pose , capsuleCenter );

                    if ( ColliderCalculator.CheckFinite ( center ) == false ||
                         ColliderCalculator.CheckExtent ( radius , tolerance ) == false ||
                         ColliderCalculator.CheckExtent ( height , 0f ) == false )
                        return Fail ( "캡슐 중심 또는 크기가 유효하지 않습니다." , out reason );

                    //OverlapCapsule에 필요한 양 끝 구체의 중심을 계산
                    Vector3 offset = difference * worldAxis * halfSegment;

                    if ( ColliderCalculator.CheckFinite ( center + offset ) == false ||
                         ColliderCalculator.CheckFinite ( center - offset ) == false )
                        return Fail ( "캡슐 끝점이 유효하지 않습니다." , out reason );

                    overlaps = Physics.OverlapCapsule (
                        center + offset , center - offset ,
                        radius - tolerance , mask , QueryTriggerInteraction.Ignore );
                    return true;
                }

            default:
                return Fail ( "지원하지 않는 Collider 종류입니다." , out reason );
        }
    }

    #endregion

    #region ----- 공간 검사 -----

    /// <summary>
    /// 배치 후보 위치의 아이템 충돌 여부 확인
    /// </summary>
    /// <param name="position">표면 배치 위치</param>
    /// <param name="rotation">배치 회전</param>
    /// <param name="placementBoundsSize">아이템 축 기준 크기</param>
    /// <param name="surfaceNormal">표면 바깥 방향</param>
    /// <param name="itemLayerMask">공간 검사 레이어</param>
    /// <param name="surfaceCollider">배치 표면 충돌체</param>
    /// <returns>배치 가능 여부</returns>
    public static bool IsPlacementAvailable (
        Vector3 position ,
        Quaternion rotation ,
        Vector3 placementBoundsSize ,
        Vector3 surfaceNormal ,
        LayerMask itemLayerMask ,
        Collider surfaceCollider )
    {
        float radius = PlacementCalculator.GetItemExtent (
            placementBoundsSize , rotation , surfaceNormal );
        Vector3 center = position + surfaceNormal * radius;
        Vector3 halfExtents = placementBoundsSize * 0.5f;

        Collider [ ] overlaps = Physics.OverlapBox (
            center , halfExtents , rotation , itemLayerMask , QueryTriggerInteraction.Ignore );

        foreach ( Collider overlap in overlaps )
        {
            if ( overlap != surfaceCollider )
                return false;
        }

        return true;
    }

    /// <summary>
    /// 지정 위치와 회전을 적용한 충돌체와 주변 장애물의 겹침 확인
    /// </summary>
    /// <param name="root">충돌체 형상의 기준 루트, 비활성 루트도 허용</param>
    /// <param name="colliders">기능별로 선정한 검사 충돌체 목록</param>
    /// <param name="pose">검사할 루트의 월드 위치와 회전</param>
    /// <param name="ignoreRoot">장애물에서 제외할 관리 루트</param>
    /// <param name="otherRoot">함께 이동할 두 번째 제외 루트, 없으면 null</param>
    /// <param name="mask">장애물 레이어</param>
    /// <param name="tolerance">형상을 축소하여 허용할 접촉 거리</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>장애물과 겹치지 않는지 여부</returns>
    public static bool IsClear (
        Transform root , IReadOnlyList<Collider> colliders , Pose pose ,
        Transform ignoreRoot , Transform otherRoot , LayerMask mask ,
        float tolerance , out string reason )
    {
        reason = string.Empty;
        if ( root == null || colliders == null || colliders.Count == 0 ||
             ColliderCalculator.CheckFinite ( root.position ) == false ||
             ColliderCalculator.CheckRotation ( root.rotation ) == false ||
             ColliderCalculator.CheckFinite ( pose.position ) == false ||
             ColliderCalculator.CheckRotation ( pose.rotation ) == false ||
             mask.value == 0 || ColliderCalculator.CheckFinite ( tolerance ) == false ||
             tolerance < 0f )
            return Fail ( "검사 형상, 위치와 회전, 장애물 레이어와 접촉 거리를 확인해 주세요." , out reason );

        Physics.SyncTransforms ( );
        foreach ( Collider collider in colliders )
        {
            //비활성 충돌체도 형상 계산에 포함
            if ( collider == null || collider.transform.IsChildOf ( root ) == false )
                return Fail ( "형상 루트에 속한 충돌체가 필요합니다." , out reason );

            if ( ColliderCalculator.CheckTransform ( collider.transform , out reason ) == false )
                return false;

            if ( TryGetOverlaps (
                collider , root , pose , mask , tolerance ,
                out Collider [ ] overlaps , out reason ) == false )
                return false;

            foreach ( Collider obstacle in overlaps )
            {
                if ( obstacle == null )
                    continue;
                Transform target = obstacle.transform;
                if ( ( ignoreRoot != null && target.IsChildOf ( ignoreRoot ) ) ||
                     ( otherRoot != null && target.IsChildOf ( otherRoot ) ) )
                    continue;

                return Fail (
                    $"{collider.name}: {obstacle.name}과 겹쳐 배치할 수 없습니다." , out reason );
            }
        }

        return true;
    }

    #endregion

    #region ----- 대상 간 검사 -----

    /// <summary>
    /// 상대 기준으로 목적지 겹침 검사
    /// </summary>
    /// <param name="root">검사 형상의 현재 루트</param>
    /// <param name="colliders">검사할 충돌체 목록</param>
    /// <param name="pose">검사 형상의 목적지 위치와 회전</param>
    /// <param name="otherRoot">상대 형상의 현재 루트</param>
    /// <param name="otherColliders">겹침 결과에서 확인할 상대 충돌체 집합</param>
    /// <param name="otherPose">상대 형상의 목적지 위치와 회전</param>
    /// <param name="tolerance">형상을 축소하여 허용할 접촉 거리</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>상대 충돌체와 겹치지 않는지 여부</returns>
    static bool CheckAgainstCurrent (
        Transform root , IReadOnlyList<Collider> colliders , Pose pose ,
        Transform otherRoot , HashSet<Collider> otherColliders ,
        Pose otherPose , float tolerance , out string reason )
    {
        reason = string.Empty;

        Quaternion otherDifference =
            ColliderCalculator.GetRotationDifference (
                otherRoot , otherPose.rotation );

        Quaternion inverse = Quaternion.Inverse ( otherDifference );

        //상대의 목적지를 현재 위치로 되돌리는 변환을 양쪽에 동일하게 적용
        //상대 충돌체는 현재 위치 그대로 두고 검사 형상의 위치와 회전만 환산
        Vector3 position = otherRoot.position +
            inverse * ( pose.position - otherPose.position );
        Quaternion rotation = inverse * pose.rotation.normalized;

        if ( ColliderCalculator.CheckFinite ( position ) == false ||
             ColliderCalculator.CheckRotation ( rotation ) == false )
            return Fail ( "두 목적지를 비교할 위치나 회전이 유효하지 않습니다." , out reason );

        Pose queryPose = new Pose ( position , rotation.normalized );

        foreach ( Collider collider in colliders )
        {
            //기존 박스, 구체, 캡슐과 메시의 Overlap 검사를 그대로 사용
            //대상 간 검사는 레이어 설정과 관계없이 상대 충돌체를 확인
            if ( TryGetOverlaps (
                collider , root , queryPose , ~0 , tolerance ,
                out Collider [ ] overlaps , out reason ) == false )
                return false;

            foreach ( Collider obstacle in overlaps )
            {
                //환산한 위치의 주변 환경은 이번 비교와 관계없음
                //지정한 상대 아이템의 충돌체만 겹침 결과로 인정
                if ( obstacle == null ||
                     otherColliders.Contains ( obstacle ) == false )
                    continue;

                return Fail (
                    $"{collider.name}: 최종 목적지에서 " +
                    $"{obstacle.name}과 겹칩니다." , out reason );
            }
        }

        return true;
    }

    /// <summary>
    /// 두 목적지의 충돌 영역 겹침 검사
    /// </summary>
    /// <param name="rootA">첫 번째 형상의 현재 루트</param>
    /// <param name="collidersA">첫 번째 형상의 활성 일반 충돌체 목록</param>
    /// <param name="poseA">첫 번째 루트의 목적지 위치와 회전</param>
    /// <param name="rootB">두 번째 형상의 현재 루트</param>
    /// <param name="collidersB">두 번째 형상의 활성 일반 충돌체 목록</param>
    /// <param name="poseB">두 번째 루트의 목적지 위치와 회전</param>
    /// <param name="tolerance">검사 형상을 축소하여 허용할 접촉 거리</param>
    /// <param name="reason">실패 사유, 성공 시 빈 문자열</param>
    /// <returns>두 목적지 형상이 서로 겹치지 않는지 여부</returns>
    public static bool IsPairClear (
        Transform rootA , IReadOnlyList<Collider> collidersA , Pose poseA ,
        Transform rootB , IReadOnlyList<Collider> collidersB , Pose poseB ,
        float tolerance , out string reason )
    {
        reason = string.Empty;

        if ( CheckPairTarget (
            rootA , collidersA , poseA , out reason ) == false ||
             CheckPairTarget (
            rootB , collidersB , poseB , out reason ) == false )
            return false;

        if ( rootA.IsChildOf ( rootB ) || rootB.IsChildOf ( rootA ) )
            return Fail ( "서로 독립된 두 형상 루트가 필요합니다." , out reason );

        if ( ColliderCalculator.CheckFinite ( tolerance ) == false ||
             tolerance < 0f )
            return Fail ( "접촉 허용 거리는 유한한 0 이상의 값이어야 합니다." , out reason );

        //상대의 현재 충돌체를 물리 조회에서 찾아야 하므로 최신 위치 반영
        Physics.SyncTransforms ( );

        HashSet<Collider> targetsA = new HashSet<Collider> ( collidersA );
        HashSet<Collider> targetsB = new HashSet<Collider> ( collidersB );

        //메시 외접 박스와 접촉 거리 적용 때문에 검사 방향에 따라
        //판정이 달라질 수 있으므로 양쪽 모두 통과해야 배치를 허용
        if ( CheckAgainstCurrent (
            rootA , collidersA , poseA ,
            rootB , targetsB , poseB , tolerance , out reason ) == false )
            return false;

        return CheckAgainstCurrent (
            rootB , collidersB , poseB ,
            rootA , targetsA , poseA , tolerance , out reason );
    }

    #endregion

    #region ----- 상태 검사 -----

    /// <summary>
    /// 목적지 비교에 사용할 형상과 현재 물리 조회 가능 여부 확인
    /// </summary>
    /// <param name="root">현재 형상 루트</param>
    /// <param name="colliders">비교할 활성 일반 충돌체 목록</param>
    /// <param name="pose">목적지 루트 위치와 회전</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>목적지 비교 가능 여부</returns>
    static bool CheckPairTarget (
        Transform root , IReadOnlyList<Collider> colliders ,
        Pose pose , out string reason )
    {
        reason = string.Empty;

        if ( root == null || colliders == null || colliders.Count == 0 )
            return Fail ( "비교할 루트와 충돌체 목록이 필요합니다." , out reason );

        if ( ColliderCalculator.CheckFinite ( root.position ) == false ||
             ColliderCalculator.CheckRotation ( root.rotation ) == false ||
             ColliderCalculator.CheckFinite ( pose.position ) == false ||
             ColliderCalculator.CheckRotation ( pose.rotation ) == false )
            return Fail ( "현재 위치 / 회전과 목적지 위치 / 회전을 확인해 주세요." , out reason );

        foreach ( Collider collider in colliders )
        {
            //이 검사는 상대의 현재 충돌체를 Overlap 결과에서 찾아 비교
            //물리 조회에는 활성 충돌체가 필요
            if ( collider == null ||
                 collider.enabled == false ||
                 collider.gameObject.activeInHierarchy == false ||
                 collider.isTrigger == true ||
                 collider.transform.IsChildOf ( root ) == false )
            {
                return Fail (
                    "비교할 루트에 속한 활성 일반 충돌체가 필요합니다." ,
                    out reason );
            }

            if ( ColliderCalculator.CheckTransform (
                collider.transform , out reason ) == false )
                return false;
        }

        return true;
    }

    /// <summary>
    /// 검사 실패 사유 반환
    /// </summary>
    /// <param name="message">실패 설명</param>
    /// <param name="reason">반환할 실패 사유</param>
    /// <returns>검사 실패</returns>
    static bool Fail ( string message , out string reason )
    {
        reason = message;
        return false;
    }

    #endregion

}
