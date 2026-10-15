using UnityEngine;

/// <summary>
/// 낙하 변칙 사건 모델 - 발생 조건, 밀기 방향, 재배치 해결 판정
/// </summary>
public class ItemDropEvent : AnomalyEvent
{
    /// <summary>
    /// 낙하 변칙 사건의 해결 시간과 사후 복구 시간 설정
    /// </summary>
    /// <param name="times">해결 시간과 사후 복구 시간</param>
    public ItemDropEvent ( AnomalyEventTimes times ) : base ( times )
    {
    }

    /// <summary>
    /// 가까운 가장자리 방향 계산
    /// </summary>
    /// <param name="surfaceBounds">배치 표면의 월드 영역</param>
    /// <param name="itemBounds">아이템의 월드 영역</param>
    /// <returns>가까운 가장자리의 수평 방향</returns>
    public Vector3 GetPushDirection ( Bounds surfaceBounds, Bounds itemBounds )
    {
        Vector3 center = itemBounds.center;

        //아이템 전체가 표면 밖으로 나가는 데 필요한 거리를 네 방향으로 비교
        float nearestDist = center.x - surfaceBounds.min.x + itemBounds.extents.x;
        Vector3 direction = Vector3.left;
        float dist = surfaceBounds.max.x - center.x + itemBounds.extents.x;

        if ( dist < nearestDist )
        {
            nearestDist = dist;
            direction = Vector3.right;
        }

        dist = center.z - surfaceBounds.min.z + itemBounds.extents.z;

        if ( dist < nearestDist )
        {
            nearestDist = dist;
            direction = Vector3.back;
        }

        dist = surfaceBounds.max.z - center.z + itemBounds.extents.z;

        if ( dist < nearestDist )
            direction = Vector3.forward;

        return direction;
    }

    /// <summary>
    /// 재배치 결과 반영
    /// </summary>
    /// <param name="isPlaced">배치 완료 여부</param>
    /// <returns>해결 또는 사후 복구 성공 여부</returns>
    public bool TryPlace ( bool isPlaced )
    {
        //사후 처리 만료가 재배치 완료보다 우선
        if ( TryExpire ( ) == true )
            return false;

        //원래 표면으로 돌아가지 않아도 허용된 대상에 배치하면 완료
        return TryComplete ( isPlaced );
    }

    /// <summary>
    /// 표면에 따른 낙하 가능 여부 확인
    /// </summary>
    /// <param name="isFloor">바닥 여부</param>
    /// <param name="isWall">벽 여부</param>
    /// <param name="normal">현재 지지 표면 방향</param>
    /// <returns>낙하 가능 여부</returns>
    public bool CanDrop ( bool isFloor, bool isWall, Vector3 normal )
    {
        //바닥에서는 추가 낙하 사건을 발생시키지 않음
        if ( isFloor == true )
            return false;

        if ( isWall == true )
            return true;

        //테이블, 의자, 박스 등의 윗면만 밀기 대상으로 사용
        return Vector3.Dot( normal, Vector3.up ) >= 0.7f;
    }
}
