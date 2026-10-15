using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 공통 배치 검사 - 지정 표면의 지지 범위 확인
/// </summary>
public static class PlacementChecker
{

    /// <summary>
    /// 후보 위치의 실제 표면 확인
    /// </summary>
    /// <param name="surfaceCollider">배치 표면 콜라이더</param>
    /// <param name="position">배치 후보 월드 위치</param>
    /// <param name="hit">표면 충돌 정보</param>
    /// <returns>표면을 찾았는지 여부</returns>
    public static bool TryGetSurfaceHit (
        Collider surfaceCollider, Vector3 position, out RaycastHit hit )
    {
        //배치 대상 collider의 월드 Bounds 가져오기
        Bounds worldBounds = surfaceCollider.bounds;
        //배치 대상 오브젝트의 로컬 위쪽 방향 가져오기
        Vector3 surfaceUp = surfaceCollider.transform.up;

        //배치 표면 전체를 통과할 수 있는 레이 거리 계산
        float rayDistance = worldBounds.extents.magnitude + 0.1f;

        //후보 위치에서 surfaceUp 방향으로 rayDistance만큼 떨어진 시작점
        Vector3 origin = position + surfaceUp * rayDistance;

        //위에서 아래(-surfaceUp) 방향으로 레이 생성
        Ray ray = new Ray( origin, -surfaceUp );

        //표면과 충돌하는지 확인
        return surfaceCollider.Raycast( ray, out hit, rayDistance * 2f );
    }

    /// <summary>
    /// 지정 방향으로 배치 표면 확인
    /// </summary>
    /// <param name="surfaceCollider">확인할 표면</param>
    /// <param name="position">표면 위 검사 위치</param>
    /// <param name="normal">표면 바깥 방향</param>
    /// <param name="distance">표면 앞뒤 탐색 거리</param>
    /// <param name="hit">표면 충돌 정보</param>
    public static bool TryGetSurfaceHit (
        Collider surfaceCollider, Vector3 position, Vector3 normal,
        float distance, out RaycastHit hit )
    {
        Ray ray = new Ray( position + normal * distance, -normal );
        return surfaceCollider.Raycast( ray, out hit, distance * 2f );
    }


    /// <summary>
    /// 중심과 가장자리의 표면 지지 확인
    /// </summary>
    /// <param name="surface">현재 지지 표면</param>
    /// <param name="point">표면 기준 위치</param>
    /// <param name="rotation">Y축이 표면 법선인 검사 회전</param>
    /// <param name="bounds">표면 축으로 계산한 충돌체 영역</param>
    /// <param name="tolerance">표면 높이 허용 오차</param>
    public static bool HasSurfaceSupport (
        Collider surface, Vector3 point, Quaternion rotation,
        Bounds bounds, float tolerance )
    {
        if ( surface == null || surface.enabled == false ||
             surface.gameObject.activeInHierarchy == false || surface.isTrigger == true )
            return false;

        Vector3 normal = rotation * Vector3.up;
        float distance = 0.01f + tolerance;

        //이동의 지지 검사와 같은 9개 지점을 표면 축으로 돌려 벽에도 적용
        //다른 콜라이더나 아래쪽 표면으로 넘어가 경계 밖을 허용하지 않음
        for ( int x = -1; x <= 1; x++ )
        {
            for ( int z = -1; z <= 1; z++ )
            {
                Vector3 local = new Vector3(
                    bounds.center.x + bounds.extents.x * x, 0f,
                    bounds.center.z + bounds.extents.z * z );
                Vector3 position = point + rotation * local;
                if ( TryGetSurfaceHit( surface, position, normal, distance, out RaycastHit hit ) == false ||
                     Vector3.Dot( hit.normal, normal ) < 0.9999f ||
                     Mathf.Abs( Vector3.Dot( hit.point - point, normal ) ) > tolerance )
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 배치 영역의 지지 표면 검사
    /// </summary>
    /// <param name="root">아이템 루트</param>
    /// <param name="colliders">아이템에 속한 활성 일반 충돌체 목록</param>
    /// <param name="pose">배치 계산으로 얻은 루트의 월드 위치와 회전</param>
    /// <param name="support">목적지에서 확인한 실제 지지 표면</param>
    /// <param name="tolerance">표면 높이 허용 오차</param>
    /// <param name="reason">실패 사유, 성공 시 빈 문자열</param>
    /// <returns>검사 지점이 모두 목적지 표면 안에 있는지 여부</returns>
    public static bool CheckSurfaceSupport (
        Transform root, IReadOnlyList<Collider> colliders,
        Pose pose, RaycastHit support,
        float tolerance, out string reason )
    {
        reason = string.Empty;

        if ( ColliderCalculator.CheckFinite( tolerance ) == false ||
             tolerance < 0f )
        {
            reason = "표면 높이 허용 오차는 유한한 0 이상의 값이어야 합니다.";
            return false;
        }

        //아이템을 움직이지 않고 목적지에서 차지할 표면 방향 범위 계산
        if ( PlacementCalculator.TryGetSurfaceBounds(
            root, colliders, pose, support,
            out Quaternion surfaceRotation, out Bounds bounds,
            out reason ) == false )
            return false;

        //표면의 최신 위치와 회전을 물리 조회에 반영
        Physics.SyncTransforms( );

        //기존 9개 지점 검사를 재사용하며 지정한 표면만 확인
        //벽에서도 표면 법선을 기준으로 검사하므로 아래 방향에 한정되지 않음
        float checkTolerance = Mathf.Max( tolerance, 0.0001f );

        if ( HasSurfaceSupport(
            support.collider, support.point, surfaceRotation,
            bounds, checkTolerance ) == false )
        {
            reason = "아이템의 중심 또는 가장자리를 목적지 표면이 받치지 못합니다.";
            return false;
        }

        return true;
    }
}
