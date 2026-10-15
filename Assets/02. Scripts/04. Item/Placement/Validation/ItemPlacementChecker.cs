using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 아이템 배치 검사 - 배치 상태와 충돌체 검사, 공통 표면/공간 검사 연결
/// </summary>
public static class ItemPlacementChecker
{

    /// <summary>
    /// 목적지에서 주변 장애물과 겹치는지 확인
    /// </summary>
    /// <param name="root">검사할 아이템 루트</param>
    /// <param name="colliders">아이템에 속한 활성 일반 충돌체 목록</param>
    /// <param name="pose">목적지의 루트 위치와 회전</param>
    /// <param name="otherRoot">함께 이동하여 현재 위치 검사에서 제외할 상대 루트, 없으면 null</param>
    /// <param name="obstacleMask">장애물 검사 레이어</param>
    /// <param name="contactTolerance">형상을 축소하여 허용할 접촉 거리</param>
    /// <param name="reason">실패 사유, 성공 시 빈 문자열</param>
    /// <returns>장애물과 겹치지 않는지 여부</returns>
    public static bool IsClear (
        Transform root, List<Collider> colliders, Pose pose,
        Transform otherRoot, LayerMask obstacleMask,
        float contactTolerance, out string reason )
    {
        if ( CheckColliders( root, colliders, out reason ) == false )
            return false;

        if ( otherRoot != null &&
             ( root.IsChildOf( otherRoot ) || otherRoot.IsChildOf( root ) ) )
        {
            reason = "서로 독립된 두 아이템 루트가 필요합니다.";
            return false;
        }

        //함께 이동할 두 아이템의 현재 위치는 장애물에서 제외
        return PlacementSpaceChecker.IsClear(
            root, colliders, pose, root, otherRoot,
            obstacleMask, contactTolerance, out reason );
    }

    /// <summary>
    /// 목적지에서 아이템의 중심과 가장자리를 표면이 받치는지 확인
    /// </summary>
    /// <param name="root">아이템 루트</param>
    /// <param name="colliders">아이템에 속한 활성 일반 충돌체 목록</param>
    /// <param name="pose">배치 계산으로 얻은 루트의 월드 위치와 회전</param>
    /// <param name="support">목적지에서 확인한 실제 지지 표면</param>
    /// <param name="tolerance">표면 높이 허용 오차</param>
    /// <param name="reason">실패 사유, 성공 시 빈 문자열</param>
    /// <returns>검사 지점이 모두 목적지 표면 안에 있는지 여부</returns>
    public static bool CheckSurfaceSupport (
        Transform root, List<Collider> colliders,
        Pose pose, RaycastHit support,
        float tolerance, out string reason )
    {
        if ( CheckColliders( root, colliders, out reason ) == false )
            return false;

        return PlacementChecker.CheckSurfaceSupport(
            root, colliders, pose, support, tolerance, out reason );
    }


    /// <summary>
    /// 배치 검사에 사용할 아이템 충돌체의 상태와 소속 확인
    /// </summary>
    /// <param name="root">활성 아이템 루트</param>
    /// <param name="colliders">검사할 충돌체 목록</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>아이템 충돌체 목록 유효 여부</returns>
    public static bool CheckColliders (
        Transform root, IReadOnlyList<Collider> colliders, out string reason )
    {
        reason = string.Empty;
        if ( root == null || root.gameObject.activeInHierarchy == false ||
             colliders == null || colliders.Count == 0 )
        {
            reason = "활성 아이템과 검사할 충돌체 목록이 필요합니다.";
            return false;
        }

        Rigidbody rigid = root.GetComponent<Rigidbody>( );
        if ( rigid == null )
        {
            reason = "아이템 루트의 Rigidbody가 필요합니다.";
            return false;
        }

        foreach ( Collider collider in colliders )
        {
            if ( collider == null || collider.enabled == false ||
                 collider.gameObject.activeInHierarchy == false || collider.isTrigger == true ||
                 collider.transform.IsChildOf( root ) == false ||
                 collider.attachedRigidbody != rigid )
            {
                reason = "아이템 Rigidbody에 속한 활성 일반 충돌체가 필요합니다.";
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 저장한 배치 상태를 현재 아이템에 복구할 수 있는지 확인
    /// </summary>
    /// <param name="root">현재 아이템 루트</param>
    /// <param name="state">검사할 배치 상태</param>
    /// <param name="reason">실패 사유, 성공 시 빈 문자열</param>
    /// <returns>배치 상태 유효 여부</returns>
    public static bool CheckPlacementState (
        Transform root, ItemPlacementState state, out string reason )
    {
        reason = string.Empty;

        if ( root == null || root.gameObject.activeInHierarchy == false ||
             state.Root == null || state.Root != root )
        {
            reason = "현재 활성 아이템에서 저장한 배치 상태가 필요합니다.";
            return false;
        }

        if ( state.HadParent && state.Parent == null )
        {
            reason = "저장한 부모가 삭제되어 배치 상태를 복구할 수 없습니다.";
            return false;
        }

        if ( state.Parent != null && state.Parent.IsChildOf( root ) )
        {
            reason = "저장한 부모가 현재 아이템 내부에 있어 복구할 수 없습니다.";
            return false;
        }

        if ( state.Parent != null &&
             state.Parent.gameObject.activeInHierarchy == false )
        {
            reason = "저장한 부모가 비활성 상태여서 배치 상태를 복구할 수 없습니다.";
            return false;
        }

        if ( state.HadSurface && state.Surface == null )
        {
            reason = "저장한 지지 표면이 삭제되어 배치 기록을 복구할 수 없습니다.";
            return false;
        }

        if ( ColliderCalculator.CheckFinite( state.Position ) == false ||
             ColliderCalculator.CheckRotation( state.Rotation ) == false ||
             ColliderCalculator.CheckFinite( state.LocalScale ) == false ||
             ColliderCalculator.CheckFinite( state.LocalNormal ) == false )
        {
            reason = "저장한 위치, 회전, 배율 또는 표면 법선이 유효하지 않습니다.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 배치 정보의 참조와 적용할 값 확인
    /// </summary>
    /// <param name="root">현재 아이템 루트</param>
    /// <param name="data">검사를 마친 배치 정보</param>
    /// <param name="reason">실패 사유, 성공 시 빈 문자열</param>
    /// <returns>배치 정보 적용 가능 여부</returns>
    public static bool CheckPlacementData (
        Transform root, PlacementData data, out string reason )
    {
        reason = string.Empty;

        if ( root == null || root.gameObject.activeInHierarchy == false )
        {
            reason = "활성 아이템 뷰가 필요합니다.";
            return false;
        }

        //자기 자신이나 비활성 부모로는 배치할 수 없음
        if ( data.Parent != null &&
             ( data.Parent.IsChildOf( root ) ||
               data.Parent.gameObject.activeInHierarchy == false ) )
        {
            reason = "아이템 외부의 활성 배치 부모가 필요합니다.";
            return false;
        }

        PlacementTarget target = data.Target;

        if ( target == null || target.isActiveAndEnabled == false )
        {
            reason = "활성 목적지 배치 대상이 필요합니다.";
            return false;
        }

        Collider surface = target.SurfaceCollider;

        if ( surface == null || surface.enabled == false ||
             surface.gameObject.activeInHierarchy == false ||
             surface.isTrigger ||
             surface.transform.IsChildOf( root ) )
        {
            reason = "아이템 외부의 활성 일반 지지 표면이 필요합니다.";
            return false;
        }

        if ( ColliderCalculator.CheckFinite( data.Position ) == false ||
             ColliderCalculator.CheckRotation( data.Rotation ) == false ||
             ColliderCalculator.CheckFinite( data.SurfaceNormal ) == false ||
             ColliderCalculator.CheckFinite(
                 data.SurfaceNormal.sqrMagnitude ) == false ||
             data.SurfaceNormal.sqrMagnitude < 0.000001f )
        {
            reason = "배치 위치, 회전과 표면 방향을 확인해 주세요.";
            return false;
        }

        return true;
    }
}
