using UnityEngine;

/// <summary>
/// 방 대상 인터페이스 - 위치와 조회 참여 여부 제공
/// </summary>
public interface IRoomTarget
{
    /// <summary>
    /// 방 소속을 판단할 위치
    /// </summary>
    Transform TargetTransform { get; }

    /// <summary>
    /// 방 대상 조회 참여 여부
    /// </summary>
    bool IsRoomTargetActive { get; }
}