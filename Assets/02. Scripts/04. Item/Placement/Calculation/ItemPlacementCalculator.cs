using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 아이템 배치 계산 - 충돌체 소속 검사와 공통 배치 계산 연결
/// </summary>
public static class ItemPlacementCalculator
{

    /// <summary>
    /// 지정 표면에 놓을 아이템 위치와 회전 계산
    /// </summary>
    /// <param name="root">아이템 루트</param>
    /// <param name="colliders">아이템에 속한 활성 일반 충돌체 목록</param>
    /// <param name="support">목적지에서 확인한 실제 지지 표면</param>
    /// <param name="rotationOffset">아이템 자신의 배치 회전 보정</param>
    /// <param name="surfaceGap">표면과 아이템 사이의 월드 간격</param>
    /// <param name="pose">계산한 루트의 월드 위치와 회전</param>
    /// <param name="reason">실패 사유, 성공 시 빈 문자열</param>
    /// <returns>배치 위치와 회전 계산 성공 여부</returns>
    public static bool TryGetPose (
        Transform root, List<Collider> colliders,
        RaycastHit support, Quaternion rotationOffset,
        float surfaceGap, out Pose pose, out string reason )
    {
        pose = default;
        //아이템 소속과 활성 상태를 확인한 뒤 공통 계산에 전달
        if ( ItemPlacementChecker.CheckColliders( root, colliders, out reason ) == false )
            return false;

        return PlacementCalculator.TryGetPose(
            root, colliders, support, rotationOffset, surfaceGap, out pose, out reason );
    }

    /// <summary>
    /// 표면 기준 배치 영역 계산
    /// </summary>
    /// <param name="root">아이템 루트</param>
    /// <param name="colliders">아이템에 속한 활성 일반 충돌체 목록</param>
    /// <param name="pose">검사할 루트의 월드 위치와 회전</param>
    /// <param name="support">목적지 지지 표면</param>
    /// <param name="surfaceRotation">Y축이 표면 법선인 검사 회전</param>
    /// <param name="bounds">표면 위치 기준 좌우와 앞뒤 범위, 높이는 0</param>
    /// <param name="reason">실패 사유, 성공 시 빈 문자열</param>
    /// <returns>표면 방향 범위 계산 성공 여부</returns>
    public static bool TryGetSurfaceBounds (
        Transform root, List<Collider> colliders,
        Pose pose, RaycastHit support,
        out Quaternion surfaceRotation, out Bounds bounds,
        out string reason )
    {
        surfaceRotation = Quaternion.identity;
        bounds = default;
        if ( ItemPlacementChecker.CheckColliders( root, colliders, out reason ) == false )
            return false;

        return PlacementCalculator.TryGetSurfaceBounds(
            root, colliders, pose, support, out surfaceRotation, out bounds, out reason );
    }

}
