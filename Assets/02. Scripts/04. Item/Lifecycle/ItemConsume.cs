/// <summary>
/// 아이템 소비 실패에 대비한 배치와 물리 상태 저장, 복구
/// </summary>
class ItemConsume
{
    ItemWorldController _view;		//복구할 아이템 뷰

    /// <summary>
    /// 소비 상태를 관리할 뷰 연결
    /// </summary>
    /// <param name="view">초기화된 아이템 뷰</param>
    public ItemConsume ( ItemWorldController view )
    {
        _view = view;
    }


    /// <summary>
    /// 소비 실패에 대비한 배치와 물리 상태 저장
    /// </summary>
    /// <param name="state">저장한 상태이며 운반 잠금은 호출자가 기록</param>
    /// <param name="reason">저장 실패 사유</param>
    /// <returns>전체 상태 저장 여부</returns>
    public bool TrySaveState ( out ItemConsumeState state, out string reason )
    {
        state = default;
        ItemPhysics physics = _view.GetPhysics( );

        if ( _view.TrySavePlacementState(
                 out ItemPlacementState placement, out reason ) == false )
            return false;

        //프로퍼티 대신 지역 변수로 저장 결과를 받은 뒤 복구 상태에 반영
        state.Placement = placement;
        bool saved = physics.TrySavePlacementState(
            out ItemPhysicsState physicsState, out reason );
        state.Physics = physicsState;
        return saved;
    }

    /// <summary>
    /// 저장한 배치와 물리 상태를 각각 복구
    /// </summary>
    /// <param name="state">복구할 기존 상태</param>
    /// <param name="reason">배치 또는 물리 복구 실패 사유</param>
    /// <returns>전체 상태 복구 여부</returns>
    public bool TryRestoreState ( ItemConsumeState state, out string reason )
    {
        ItemPhysics physics = _view.GetPhysics( );
        _view.DisablePhysics( );

		//배치 복구에 실패해도 물리 상태 복구는 시도
        bool placementRestored = _view.TryRestorePlacementState(
            state.Placement, out string placementReason );
        bool physicsRestored = physics.TryRestorePlacementState(
            state.Physics, out string physicsReason );

        reason = placementRestored && physicsRestored
            ? string.Empty
            : $"배치: {placementReason}\n물리: {physicsReason}";

        return placementRestored && physicsRestored;
    }

    /// <summary>
    /// 배치와 물리 상태의 저장 가능 여부 검사
    /// </summary>
    /// <param name="reason">검사 실패 사유</param>
    /// <returns>소비 준비 여부</returns>
    public bool CheckState ( out string reason )
    {
        return _view.TrySavePlacementState( out _, out reason ) &&
            _view.GetPhysics( ).TrySavePlacementState( out _, out reason );
    }

}
