using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 외형 공간 검사 - 후보 외형과 주변 장애물의 겹침 확인
/// </summary>
public static class AppearanceSpaceChecker
{

    /// <summary>
    /// 후보 외형을 놓을 공간이 있는지 확인
    /// </summary>
    /// <param name="view">최종 위치와 보정값을 적용한 외형 인스턴스</param>
    /// <param name="itemRoot">자기 충돌체를 제외할 아이템 관리 루트</param>
    /// <param name="obstacleMask">검사할 장애물 레이어</param>
    /// <param name="contactTolerance">단순 접촉을 허용할 월드 거리</param>
    /// <param name="reason">실패 사유, 성공 시 빈 문자열</param>
    /// <returns>외형을 적용할 공간이 있는지 여부</returns>
    public static bool IsClear (
        AppearanceComponents view, Transform itemRoot,
        LayerMask obstacleMask, float contactTolerance,
        out string reason )
    {
        reason = string.Empty;
        if ( itemRoot == null || view == null ||
             view.transform == itemRoot || view.transform.IsChildOf( itemRoot ) == false )
            return Fail( "검사할 외형은 아이템 루트 아래의 자식이어야 합니다.", out reason );

        if ( AppearanceChecker.TryGetPlacementColliders(
            view, out List<Collider> colliders, out reason ) == false )
            return false;

        //현재 외형과 보관 외형을 포함한 자기 아이템의 충돌체는 장애물에서 제외
        Pose pose = new Pose( view.transform.position, view.transform.rotation );
        return PlacementSpaceChecker.IsClear(
            view.transform, colliders, pose, itemRoot, null,
            obstacleMask, contactTolerance, out reason );
    }

    /// <summary>
    /// 검사 실패 사유 반환
    /// </summary>
    /// <param name="message">실패 설명</param>
    /// <param name="reason">반환할 실패 사유</param>
    /// <returns>검사 실패</returns>
    static bool Fail ( string message, out string reason )
    {
        reason = message;
        return false;
    }
}
