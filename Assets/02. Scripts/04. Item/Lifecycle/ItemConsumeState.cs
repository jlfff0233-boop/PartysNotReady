/// <summary>
/// 소비 비활성화 전 아이템 복구 상태
/// </summary>
public struct ItemConsumeState
{
    ItemPlacementState _placement;        //비활성화 전 배치 상태
    ItemPhysicsState _physics;        //비활성화 전 물리 상태
    bool _wasCarryLocked;        //비활성화 전 운반 잠금 여부

    /// <summary>
    /// 비활성화 전 배치 상태
    /// </summary>
    public ItemPlacementState Placement
    {
        get => _placement;
        set => _placement = value;
    }

    /// <summary>
    /// 비활성화 전 물리 상태
    /// </summary>
    public ItemPhysicsState Physics
    {
        get => _physics;
        set => _physics = value;
    }

    /// <summary>
    /// 비활성화 전 운반 잠금 여부
    /// </summary>
    public bool WasCarryLocked
    {
        get => _wasCarryLocked;
        set => _wasCarryLocked = value;
    }
}
