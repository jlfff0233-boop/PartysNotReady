using System;
using UnityEngine;

/// <summary>
/// 운반 대상 - 들기와 내려놓기
/// </summary>
public interface ICarryable
{
    /// <summary>
    /// 현재 보유 방식
    /// </summary>
    HoldType CurrentType { get; }

    /// <summary>
    /// 아이템 축 기준 배치 판정 크기
    /// </summary>
    public Vector3 PlacementBoundsSize { get; }

    /// <summary>
    /// 표면 기준 배치 회전에 곱할 아이템 방향 보정
    /// </summary>
    public Quaternion PlacementRotationOffset { get; }

    /// <summary>
    /// 보유 방식 변경 알림
    /// </summary>
    event Action<HoldType, HoldType> OnHoldChanged;

    /// <summary>
    /// 비활성화에 따른 사용 중단 알림
    /// </summary>
    event Action OnUnavailable;


    /// <summary>
    /// 아이템 들기
    /// </summary>
    /// <param name="holdPoint">아이템 들기 지점</param>
    /// <param name="type">보유 방식</param>
    /// <param name="ownerColliders">월드 보유 중 충돌을 제외할 보유자 콜라이더</param>
    /// <returns>들기 성공 여부</returns>
    public bool TryPickUp (
        Transform holdPoint,
        HoldType type = HoldType.View,
        Collider [ ] ownerColliders = null );

    /// <summary>
    /// 아이템 내려놓기
    /// </summary>
    /// <param name="placementData">배치 정보</param>
    /// <returns>배치 성공 여부</returns>
    public bool TryPutDown ( PlacementData placementData );

    /// <summary>
    /// 보유 해제
    /// </summary>
    /// <returns>해제 성공 여부</returns>
    bool TryRelease ();

}
