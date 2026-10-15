using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// 아이템 이동 모듈 - 주변 목적지 계산과 바닥 및 공간 검사
/// </summary>
public static class ItemDisplacement
{
    const float FloorGap = 0.01f;       //바닥 감지 오차
    const float HeightTolerance = 0.005f;       //지지점 높이 허용 오차
    const float MinUpDot = 0.9999f;     //수평 표면 판정 기준


    #region ----- 목적지 탐색 -----

    /// <summary>
    /// 현재 위치 주변의 유효한 이동 목적지 계산
    /// </summary>
    /// <param name="root">아이템 루트</param>
    /// <param name="bounds">현재 월드 충돌 영역</param>
    /// <param name="settings">탐색 설정</param>
    /// <param name="position">선정한 루트 월드 위치</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>목적지 선정 성공 여부</returns>
    public static bool TryFindPosition (
        Transform root, Bounds bounds, ItemDisplacementSettings settings,
        out Vector3 position, out string reason )
    {
        position = default;
        reason = null;

        if ( settings == null || settings.IsValid == false )
        {
            reason = "이동 거리, 탐색 횟수, 바닥 마스크와 탐색 높이를 확인해주세요.";
            return false;
        }

        Vector3 originalPos = root.position;

        for ( int i = 0; i < settings.TryCount; i++ )
        {
            float angle = Random.Range( 0f, Mathf.PI * 2f );
            float dist = Random.Range( settings.MinDist, settings.MaxDist );

            Vector3 offset = new Vector3(
                Mathf.Cos( angle ) * dist, 0f, Mathf.Sin( angle ) * dist );

            Vector3 origin = bounds.center + offset;
            origin.y = bounds.min.y + settings.RayHeight;

            if ( TryGetFloor(
                root, origin,
                settings.RayHeight + settings.SearchDepth,
                settings.FloorMask, out RaycastHit floor ) == false )
            {
                reason = "후보 위치에서 수평 바닥을 찾지 못했습니다.";
                continue;
            }

            //피벗 위치와 관계없이 콜라이더 밑면을 바닥에 맞춤
            Vector3 movement = offset;
            movement.y = floor.point.y + FloorGap - bounds.min.y;

            Bounds movedBounds = new Bounds(
                bounds.center + movement, bounds.size );

            //중심과 가장자리의 높이가 맞지 않으면 다음 후보 탐색
            if ( HasSupport(
                root, movedBounds, floor.point.y, settings.FloorMask ) == false )
            {
                reason = "후보 위치의 바닥이 물체 영역을 받치지 못합니다.";
                continue;
            }

            if ( IsSpaceAvailable( root, movedBounds ) == false )
            {
                reason = "후보 위치가 다른 물체와 겹칩니다.";
                continue;
            }

            position = originalPos + movement;
            reason = null;
            return true;
        }

        reason = $"목적지 탐색 {settings.TryCount}회 실패 / 마지막 사유: {reason}";
        return false;
    }

    #endregion

    #region ----- 바닥/공간 검사 -----

    /// <summary>
    /// 지정 지점 아래에서 가장 가까운 수평 바닥 조회
    /// </summary>
    /// <param name="root">충돌 제외 대상</param>
    /// <param name="origin">레이 시작 위치</param>
    /// <param name="dist">검사 거리</param>
    /// <param name="floorMask">바닥 레이어</param>
    /// <param name="hit">선택한 바닥 정보</param>
    /// <returns>수평 바닥 존재 여부</returns>
    static bool TryGetFloor (
        Transform root, Vector3 origin, float dist,
        LayerMask floorMask, out RaycastHit hit )
    {
        hit = default;

        RaycastHit [ ] hits = Physics.RaycastAll(
            origin, Vector3.down, dist, floorMask, QueryTriggerInteraction.Ignore );

        float nearestDist = float.MaxValue;

        foreach ( RaycastHit candidate in hits )
        {
            if ( candidate.collider.transform.IsChildOf( root ) )
                continue;

            if ( candidate.distance >= nearestDist )
                continue;

            nearestDist = candidate.distance;
            hit = candidate;
        }

        //가장 가까운 바닥이 경사면이면 아래 표면으로 건너뛰지 않음
        return hit.collider != null &&
               Vector3.Dot( hit.normal, Vector3.up ) >= MinUpDot;
    }

    /// <summary>
    /// 바닥 지지 여부 확인
    /// </summary>
    /// <param name="root">충돌 제외 대상</param>
    /// <param name="bounds">목적지 충돌 영역</param>
    /// <param name="floorY">기준 바닥 높이</param>
    /// <param name="floorMask">바닥 레이어</param>
    /// <returns>검사 지점의 지지 높이 일치 여부</returns>
    static bool HasSupport (
        Transform root, Bounds bounds, float floorY,
        LayerMask floorMask )
    {
        //중심과 가장자리 8곳을 검사
        for ( int x = -1; x <= 1; x++ )
        {
            for ( int z = -1; z <= 1; z++ )
            {
                Vector3 origin = new Vector3(
                    bounds.center.x + bounds.extents.x * x,
                    floorY + FloorGap + HeightTolerance,
                    bounds.center.z + bounds.extents.z * z );

                if ( TryGetFloor(
                    root, origin, FloorGap + HeightTolerance * 2f,
                    floorMask, out RaycastHit hit ) == false )
                    return false;

                if ( Mathf.Abs( hit.point.y - floorY ) > HeightTolerance )
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 목적지 영역의 다른 물체 겹침 확인
    /// </summary>
    /// <param name="root">자기 충돌 제외 대상</param>
    /// <param name="bounds">목적지 충돌 영역</param>
    /// <returns>다른 물체가 없는지 여부</returns>
    static bool IsSpaceAvailable ( Transform root, Bounds bounds )
    {
        //벽과 캐릭터까지 포함해 실제 충돌 영역 검사
        Collider [ ] overlaps = Physics.OverlapBox(
            bounds.center, bounds.extents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore );

        foreach ( Collider collider in overlaps )
        {
            if ( collider.transform.IsChildOf( root ) )
                continue;

            return false;
        }

        return true;
    }

    #endregion


    /// <summary>
    /// 유한한 양수인지 확인
    /// </summary>
    /// <param name="value">검사할 값</param>
    /// <returns>유한한 양수 여부</returns>
    static bool IsPositive ( float value )
    {
        return float.IsNaN( value ) == false &&
               float.IsInfinity( value ) == false &&
               value > 0f;
    }


}
