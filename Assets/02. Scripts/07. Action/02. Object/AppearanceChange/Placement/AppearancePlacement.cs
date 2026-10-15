using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 외형 배치 계산 - 표면에 맞출 이동량과 배치 범위 검사
/// </summary>
public static class AppearancePlacement
{

    /// <summary>
    /// 외형을 표면에 맞출 이동량 계산
    /// </summary>
    /// <param name="view">위치와 회전 및 크기를 적용한 외형 인스턴스</param>
    /// <param name="surfacePoint">현재 지지 표면 위의 월드 위치</param>
    /// <param name="surfaceNormal">표면에서 물건 쪽을 향하는 월드 법선</param>
    /// <param name="surfaceGap">표면과 외형 사이에 둘 월드 간격</param>
    /// <param name="offset">표면 법선 방향의 월드 이동량</param>
    /// <param name="reason">실패 사유, 성공 시 빈 문자열</param>
    /// <returns>이동량 계산 성공 여부</returns>
    public static bool TryGetOffset (
        AppearanceComponents view, Vector3 surfacePoint,
        Vector3 surfaceNormal, float surfaceGap,
        out Vector3 offset, out string reason )
    {
        offset = Vector3.zero;
        //비활성 외형에서도 실제 배치에 쓰일 일반 충돌체만 수집
        if ( AppearanceChecker.TryGetPlacementColliders(
            view, out List<Collider> colliders, out reason ) == false )
            return false;

        Pose pose = new Pose( view.transform.position, view.transform.rotation );
        return PlacementCalculator.TryGetOffset(
            view.transform, colliders, pose, surfacePoint, surfaceNormal,
            surfaceGap, out offset, out reason );
    }

    /// <summary>
    /// 외형의 중심과 가장자리가 표면 안에 있는지 확인
    /// </summary>
    /// <param name="view">위치 보정을 마친 비활성 외형</param>
    /// <param name="support">현재 지지 표면 정보</param>
    /// <param name="tolerance">표면 높이 허용 오차</param>
    /// <param name="reason">배치 불가 사유</param>
    /// <returns>외형을 받칠 표면이 충분한지 여부</returns>
    public static bool CheckSurfaceSupport (
        AppearanceComponents view, RaycastHit support, float tolerance, out string reason )
    {
        if ( AppearanceChecker.TryGetPlacementColliders(
            view, out List<Collider> colliders, out reason ) == false )
            return false;

        Pose pose = new Pose( view.transform.position, view.transform.rotation );
        return PlacementChecker.CheckSurfaceSupport(
            view.transform, colliders, pose, support, tolerance, out reason );
    }
}
