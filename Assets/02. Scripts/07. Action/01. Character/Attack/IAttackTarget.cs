using UnityEngine;

/// <summary>
/// 캐릭터 공격 대상
/// </summary>
public interface IAttackTarget
{
    /// <summary>
    /// 공격 대상 트랜스폼
    /// </summary>
    Transform TargetTransform { get; }

    /// <summary>
    /// 피격 가능 여부
    /// </summary>
    bool CanTakeHit { get; }
}