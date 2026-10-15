using System.Collections.Generic;
using UnityEngine;
using System;

/// <summary>
/// 아이템 교체 모듈 - 두 아이템의 배치 검사, 교체 적용과 실패 복구
/// </summary>
public class ItemSwapper
{

    float _supportDistance;     //모듈 생성 시 복사한 표면 탐색 거리
    float _surfaceGap;     //목적지 표면과 둘 간격
    float _surfaceTolerance;     //표면 높이 허용 오차
    LayerMask _obstacleMask;     //주변 장애물 검사 레이어
    float _contactTolerance;     //공간 검사 형상을 줄일 거리

    List<Collider> _collidersA = new( );        //첫 번째 아이템의 검사 충돌체
    List<Collider> _collidersB = new( );        //두 번째 아이템의 검사 충돌체

    bool _isSwapping;     //배치 중 콜백에 의한 중복 교체 방지

    /// <summary>
    /// 교체 검사에 사용할 설정 복사
    /// </summary>
    /// <param name="settings">표면과 공간 검사 설정</param>
    public ItemSwapper ( SwapSettings settings )
    {
        if ( settings == null )
            throw new ArgumentNullException( nameof( settings ) );

        //외부 설정 변경으로 교체 도중 검사 기준이 달라지지 않도록 복사
        _supportDistance = settings.SupportDistance;
        _surfaceGap = settings.SurfaceGap;
        _surfaceTolerance = settings.SurfaceTolerance;
        _obstacleMask = settings.ObstacleMask;
        _contactTolerance = settings.ContactTolerance;
    }

    #region ----- 배치 정보 조회 -----

    /// <summary>
    /// 아이템의 충돌체와 현재 지지 표면 조회
    /// </summary>
    /// <param name="item">조회할 아이템</param>
    /// <param name="placement">현재 지지해야 하는 배치 대상</param>
    /// <param name="colliders">조회한 충돌체를 받을 목록</param>
    /// <param name="support">실제로 확인한 현재 지지 표면</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>배치 정보 조회 성공 여부</returns>
    bool TryGetSource (
        ItemCarry item, PlacementTarget placement,
        List<Collider> colliders,
        out RaycastHit support, out string reason )
    {
        support = default;

        if ( CheckTarget( item, placement, out reason ) == false )
            return false;

        ItemWorldController view = item.GetComponent<ItemWorldController>( );

        if ( view == null || view.isActiveAndEnabled == false )
        {
            reason = "활성 아이템 뷰가 필요합니다.";
            return false;
        }

        //외형 뷰 유무와 관계없이 아이템에 속한 활성 충돌체 조회
        if ( view.TryGetPlacementColliders( colliders, out reason ) == false )
            return false;

        if ( ItemPlacementChecker.CheckColliders(
            item.transform, colliders, out reason ) == false )
            return false;

        //고정된 벽 배치는 기존 배치 방향을 우선 사용
        //일반 물건은 아래 방향으로 현재 지지 표면 확인
        if ( view.TryGetPlacementSupport(
            Vector3.down, item.PlacementRotationOffset,
            _supportDistance, out support ) == false )
        {
            reason = $"{item.name}: 현재 지지 표면을 찾지 못했습니다.";
            return false;
        }

        //배치 대상 참조만 믿지 않고 실제로 닿아 있는 표면과 비교
        if ( support.collider != placement.SurfaceCollider )
        {
            reason = $"{item.name}: 현재 지지 표면이 기준 배치 대상과 다릅니다.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 상대 자리의 배치 위치 계산과 공간 검사
    /// </summary>
    /// <param name="item">이동할 아이템</param>
    /// <param name="colliders">이동할 아이템의 충돌체 목록</param>
    /// <param name="support">상대 아이템이 있던 지지 표면</param>
    /// <param name="otherRoot">현재 장애물 검사에서 제외할 상대 아이템 루트</param>
    /// <param name="pose">검사를 통과한 목적지 위치와 회전</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>목적지 계산과 검사 성공 여부</returns>
    bool TryGetDestination (
        ItemCarry item, List<Collider> colliders,
        RaycastHit support, Transform otherRoot,
        out Pose pose, out string reason )
    {
        pose = default;

        //상대 아이템의 회전이 아닌 이동할 아이템 자신의 배치 보정을 사용
        if ( ItemPlacementCalculator.TryGetPose(
            item.transform, colliders, support,
            item.PlacementRotationOffset, _surfaceGap,
            out Pose candidate, out reason ) == false )
            return false;

        if ( ItemPlacementChecker.CheckSurfaceSupport(
            item.transform, colliders, candidate, support,
            _surfaceTolerance, out reason ) == false )
            return false;

        //두 아이템의 현재 충돌체는 제외하고 주변 환경만 검사
        if ( ItemPlacementChecker.IsClear(
            item.transform, colliders, candidate, otherRoot,
            _obstacleMask, _contactTolerance, out reason ) == false )
            return false;

        pose = candidate;
        return true;
    }

    #endregion

    #region ----- 배치 복구 -----

    /// <summary>
    /// 아이템의 위치, 회전과 물리 상태 복구
    /// </summary>
    /// <param name="view">복구할 아이템 뷰</param>
    /// <param name="physics">복구할 물리 모듈</param>
    /// <param name="placementState">저장한 배치 상태</param>
    /// <param name="physicsState">저장한 물리 상태</param>
    /// <param name="reason">복구 실패 사유</param>
    /// <returns>배치와 물리 상태 복구 성공 여부</returns>
    bool TryRestoreItem (
        ItemWorldController view, ItemPhysics physics,
        ItemPlacementState placementState,
        ItemPhysicsState physicsState, out string reason )
    {
        reason = string.Empty;
        bool placementRestored = false;
        bool physicsRestored = false;

        try
        {
            if ( view == null )
            {
                reason = "복구할 아이템 뷰가 사라졌습니다.";
            }
            else if ( physics.CheckPlacementState( out reason ) )
            {
                //위치와 회전을 복구하는 동안 물리 이동 방지
                physics.DisablePhysics( );

                placementRestored = view.TryRestorePlacementState(
                    placementState, out reason );
            }
        }
        catch ( Exception exception )
        {
            reason = $"배치 복구 중 예외: {exception.Message}";
            Debug.LogException( exception, view );
        }

        //위치와 회전 복구가 실패해도 물리 복구는 별도로 시도
        //중간에 고정된 물리 상태를 그대로 남기지 않음
        try
        {
            physicsRestored = physics.TryRestorePlacementState(
                physicsState, out string physicsReason );

            if ( physicsRestored == false )
                reason += $"\n물리 복구 실패: {physicsReason}";
        }
        catch ( Exception exception )
        {
            reason += $"\n물리 복구 중 예외: {exception.Message}";
            Debug.LogException( exception, view );
        }

        return placementRestored && physicsRestored;
    }

    #endregion

    #region ----- 교체 요청 -----

    /// <summary>
    /// 두 아이템의 교체 목적지 계산
    /// </summary>
    /// <param name="itemA">첫 번째 아이템</param>
    /// <param name="placementA">첫 번째 아이템의 기준 배치 대상</param>
    /// <param name="itemB">두 번째 아이템</param>
    /// <param name="placementB">두 번째 아이템의 기준 배치 대상</param>
    /// <param name="placements">두 아이템에 적용할 배치 정보</param>
    /// <param name="reason">실패 사유, 성공 시 빈 문자열</param>
    /// <returns>두 목적지의 계산과 검사 성공 여부</returns>
    public bool TryGetPlacements (
        ItemCarry itemA, PlacementTarget placementA,
        ItemCarry itemB, PlacementTarget placementB,
        out SwapPlacementData placements,
        out string reason )
    {
        placements = default;
        reason = string.Empty;

        if ( CheckSettings( out reason ) == false ||
             CheckPair(
                 itemA, placementA, itemB, placementB, out reason ) == false )
            return false;

        //현재 배치 대상과 실제 지지 표면이 일치하는지 확인
        if ( TryGetSource(
            itemA, placementA, _collidersA,
            out RaycastHit supportA, out reason ) == false ||
             TryGetSource(
            itemB, placementB, _collidersB,
            out RaycastHit supportB, out reason ) == false )
            return false;

        //상대가 있던 표면에 각 아이템 자신의 크기와 회전 보정을 적용
        if ( TryGetDestination(
            itemA, _collidersA, supportB, itemB.transform,
            out Pose poseA, out reason ) == false ||
             TryGetDestination(
            itemB, _collidersB, supportA, itemA.transform,
            out Pose poseB, out reason ) == false )
            return false;

        //주변 장애물과 별개로 두 아이템이 목적지에서 서로 겹치는지도 검사
        if ( PlacementSpaceChecker.IsPairClear(
            itemA.transform, _collidersA, poseA,
            itemB.transform, _collidersB, poseB,
            _contactTolerance, out reason ) == false )
            return false;

        //A의 목적지는 B의 배치 대상이며, 실제 검사한 B 표면 법선을 사용
        //고정 여부는 목적지 배치 대상이 결정
        if ( placementB.TryCreateForcedPlacement(
            poseA, supportB.normal,
            out PlacementData resultA, out reason ) == false ||
             placementA.TryCreateForcedPlacement(
            poseB, supportA.normal,
            out PlacementData resultB, out reason ) == false )
            return false;

        //한쪽 결과만 노출하지 않고 양쪽 성공 후 함께 반환
        placements = new SwapPlacementData( resultA, resultB );
        return true;
    }

    /// <summary>
    /// 교체 목적지 조건을 확인하고 아이템 배치 적용
    /// </summary>
    /// <param name="view">배치할 아이템 뷰</param>
    /// <param name="data">교체 목적지 정보</param>
    /// <param name="reason">배치 실패 사유</param>
    /// <returns>배치 적용 성공 여부</returns>
    bool TryApplyPlacement ( ItemWorldController view, PlacementData data, out string reason )
    {
        reason = string.Empty;

        //교체는 부모에서 분리해 검사 당시 월드 크기를 유지
        //일반 배치에서도 사용하는 View에는 이 제한을 두지 않음
        if ( data.Parent != null )
        {
            reason = "교체 배치의 부모는 지정하지 않아야 합니다.";
            return false;
        }

        return view.TryApplyPlacement( data, out reason );
    }

    /// <summary>
    /// 두 아이템 배치 적용과 실패 복구
    /// </summary>
    /// <param name="itemA">첫 번째 아이템</param>
    /// <param name="placementA">첫 번째 아이템의 기준 배치 대상</param>
    /// <param name="itemB">두 번째 아이템</param>
    /// <param name="placementB">두 번째 아이템의 기준 배치 대상</param>
    /// <param name="reason">교체 또는 복구 실패 사유</param>
    bool TrySwapItems (
        ItemCarry itemA, PlacementTarget placementA,
        ItemCarry itemB, PlacementTarget placementB,
        out string reason )
    {
        //실제 적용 요청마다 현재 상태와 목적지를 새로 검사
        if ( TryGetPlacements(
            itemA, placementA, itemB, placementB,
            out SwapPlacementData placements, out reason ) == false )
            return false;

        PlacementData destinationA = placements.DestinationA;
        PlacementData destinationB = placements.DestinationB;

        ItemWorldController viewA = itemA.GetComponent<ItemWorldController>( );
        ItemWorldController viewB = itemB.GetComponent<ItemWorldController>( );
        ItemPhysics physicsA = viewA.GetPhysics( );
        ItemPhysics physicsB = viewB.GetPhysics( );

        //양쪽 상태를 모두 저장하기 전에는 아무것도 이동하지 않음
        if ( viewA.TrySavePlacementState(
            out ItemPlacementState placementStateA, out reason ) == false ||
             viewB.TrySavePlacementState(
            out ItemPlacementState placementStateB, out reason ) == false ||
             physicsA.TrySavePlacementState(
            out ItemPhysicsState physicsStateA, out reason ) == false ||
             physicsB.TrySavePlacementState(
            out ItemPhysicsState physicsStateB, out reason ) == false )
            return false;

        bool applied = false;

        try
        {
            //첫 번째 아이템의 부모 변경 콜백이 실행되기 전에
            //두 아이템 모두 기존 운동을 정지
            physicsA.DisablePhysics( );
            physicsB.DisablePhysics( );

            if ( TryApplyPlacement( viewA, destinationA, out reason ) )
            {
                //A 적용 중 B가 비활성화되거나 보유 상태가 될 수 있음
                if ( CheckPair(
                    itemA, placementA,
                    itemB, placementB, out reason ) )
                {
                    if ( TryApplyPlacement( viewB, destinationB, out reason ) )
                    {
                        applied = CheckAppliedPair(
                            itemA, destinationA,
                            itemB, destinationB, out reason );
                    }
                }
            }
        }
        catch ( Exception exception )
        {
            reason = $"교체 적용 중 예외: {exception.Message}";
            Debug.LogException( exception, itemA );
        }

        if ( applied == false )
        {
            //첫 번째 복구 결과와 관계없이 두 번째도 반드시 시도
            bool restoredA = TryRestoreItem(
                viewA, physicsA, placementStateA,
                physicsStateA, out string restoreReasonA );

            bool restoredB = TryRestoreItem(
                viewB, physicsB, placementStateB,
                physicsStateB, out string restoreReasonB );

            if ( restoredA == false )
                reason += $"\n첫 번째 아이템 복구 실패: {restoreReasonA}";

            if ( restoredB == false )
                reason += $"\n두 번째 아이템 복구 실패: {restoreReasonB}";

            return false;
        }

        //양쪽 배치 검사가 끝난 뒤에만 투척 상태와 감쇠 설정 정리
        //이전에 정리하면 교체 실패 시 기존 투척 상태로 복구할 수 없음
        itemA.CompleteForcedPlacement( );
        itemB.CompleteForcedPlacement( );

        //강제 교체는 플레이어의 배치 행동이 아니므로 OnPlaced 발행 없음
        reason = string.Empty;
        return true;
    }

    /// <summary>
    /// 두 아이템의 위치를 교체하고 적용 실패 시 양쪽 복구
    /// </summary>
    /// <param name="itemA">첫 번째 아이템</param>
    /// <param name="placementA">첫 번째 아이템의 기준 배치 대상</param>
    /// <param name="itemB">두 번째 아이템</param>
    /// <param name="placementB">두 번째 아이템의 기준 배치 대상</param>
    /// <param name="reason">교체 또는 복구 실패 사유</param>
    /// <returns>양쪽 교체 성공 여부</returns>
    public bool TrySwap (
        ItemCarry itemA, PlacementTarget placementA,
        ItemCarry itemB, PlacementTarget placementB,
        out string reason )
    {
        reason = string.Empty;

        if ( _isSwapping )
        {
            reason = "이미 두 아이템의 교체를 처리하고 있습니다.";
            return false;
        }

        _isSwapping = true;

        try
        {
            return TrySwapItems( itemA, placementA, itemB, placementB, out reason );
        }
        finally
        {
            //중간 반환이나 예외가 발생해도 다음 요청을 막지 않음
            _isSwapping = false;
        }
    }

    #endregion

    #region ----- 검사 -----

    /// <summary>
    /// 아이템과 기준 배치 대상의 사용 가능 여부 확인
    /// </summary>
    /// <param name="item">검사할 아이템</param>
    /// <param name="placement">기준 배치 대상</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>대상 사용 가능 여부</returns>
    bool CheckTarget (
        ItemCarry item, PlacementTarget placement, out string reason )
    {
        reason = string.Empty;

        if ( item == null || item.isActiveAndEnabled == false || item.IsHold )
        {
            reason = "아무도 들고 있지 않은 활성 아이템이 필요합니다.";
            return false;
        }

        if ( placement == null || placement.isActiveAndEnabled == false )
        {
            reason = "활성 기준 배치 대상이 필요합니다.";
            return false;
        }

        Collider surface = placement.SurfaceCollider;

        if ( surface == null || surface.enabled == false ||
             surface.gameObject.activeInHierarchy == false || surface.isTrigger )
        {
            reason = "기준 배치 대상의 활성 일반 표면 충돌체가 필요합니다.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 서로 교체할 두 아이템과 배치 대상의 관계 확인
    /// </summary>
    /// <param name="itemA">첫 번째 아이템</param>
    /// <param name="placementA">첫 번째 기준 배치 대상</param>
    /// <param name="itemB">두 번째 아이템</param>
    /// <param name="placementB">두 번째 기준 배치 대상</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>교체할 수 있는 대상 관계 여부</returns>
    bool CheckPair (
        ItemCarry itemA, PlacementTarget placementA,
        ItemCarry itemB, PlacementTarget placementB, out string reason )
    {
        if ( CheckTarget( itemA, placementA, out reason ) == false ||
             CheckTarget( itemB, placementB, out reason ) == false )
            return false;

        Transform rootA = itemA.transform;
        Transform rootB = itemB.transform;

        if ( rootA.IsChildOf( rootB ) || rootB.IsChildOf( rootA ) )
        {
            reason = "서로 독립된 두 아이템이 필요합니다.";
            return false;
        }

        if ( placementA == placementB )
        {
            reason = "서로 다른 배치 대상에 놓인 두 아이템이 필요합니다.";
            return false;
        }

        Transform surfaceA = placementA.SurfaceCollider.transform;
        Transform surfaceB = placementB.SurfaceCollider.transform;

        //이동할 아이템에 붙어 있는 표면은 목적지로 사용할 수 없음
        if ( surfaceA.IsChildOf( rootA ) || surfaceA.IsChildOf( rootB ) ||
             surfaceB.IsChildOf( rootA ) || surfaceB.IsChildOf( rootB ) )
        {
            reason = "두 아이템 외부의 지지 표면이 필요합니다.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 표면과 공간 검사 설정의 유효성 확인
    /// </summary>
    /// <param name="reason">실패 사유</param>
    /// <returns>설정 유효 여부</returns>
    bool CheckSettings ( out string reason )
    {
        reason = string.Empty;

        if ( ColliderCalculator.CheckFinite( _supportDistance ) == false ||
             _supportDistance < 0f ||
             ColliderCalculator.CheckFinite( _surfaceGap ) == false ||
             _surfaceGap < 0f ||
             ColliderCalculator.CheckFinite( _surfaceTolerance ) == false ||
             _surfaceTolerance < 0f ||
             ColliderCalculator.CheckFinite( _contactTolerance ) == false ||
             _contactTolerance < 0f )
        {
            reason = "표면과 접촉 검사 거리는 유한한 0 이상의 값이어야 합니다.";
            return false;
        }

        if ( _obstacleMask.value == 0 )
        {
            reason = "장애물 검사 레이어가 지정되지 않았습니다.";
            return false;
        }

        return true;
    }


    /// <summary>
    /// 목적지 배치와 지지 표면 / 공간 확인
    /// </summary>
    /// <param name="item">배치를 적용한 아이템</param>
    /// <param name="destination">적용해야 하는 배치 정보</param>
    /// <param name="colliders">현재 충돌체를 받을 목록</param>
    /// <param name="otherRoot">장애물 검사에서 제외할 상대 아이템</param>
    /// <param name="reason">검사 실패 사유</param>
    /// <returns>배치 적용 결과의 유효 여부</returns>
    bool CheckAppliedPlacement (
        ItemCarry item, PlacementData destination,
        List<Collider> colliders, Transform otherRoot,
        out string reason )
    {
        if ( TryGetSource(
            item, destination.Target, colliders,
            out RaycastHit support, out reason ) == false )
            return false;

        Transform root = item.transform;

        //부모 변경 콜백에서 위치나 회전이 바뀐 경우 성공으로 처리하지 않음
        if ( root.parent != null ||
             Vector3.Distance( root.position, destination.Position ) > 0.0001f ||
             Quaternion.Angle( root.rotation, destination.Rotation ) > 0.01f )
        {
            reason = "배치 적용 중 아이템의 부모 또는 위치나 회전이 변경되었습니다.";
            return false;
        }

        Rigidbody body = root.GetComponent<Rigidbody>( );

        if ( body == null ||
             body.isKinematic != destination.IsFixed ||
             body.useGravity != ( destination.IsFixed == false ) )
        {
            reason = "목적지의 고정 또는 중력 설정이 적용되지 않았습니다.";
            return false;
        }

        Pose pose = new Pose( root.position, root.rotation );

        //실제로 적용된 형상으로 표면 경계와 주변 공간을 다시 확인
        if ( ItemPlacementChecker.CheckSurfaceSupport(
            root, colliders, pose, support,
            _surfaceTolerance, out reason ) == false )
            return false;

        return ItemPlacementChecker.IsClear(
            root, colliders, pose, otherRoot,
            _obstacleMask, _contactTolerance, out reason );
    }

    /// <summary>
    /// 두 아이템의 배치 결과와 최종 상호 겹침 확인
    /// </summary>
    /// <param name="itemA">첫 번째 아이템</param>
    /// <param name="destinationA">첫 번째 목적지</param>
    /// <param name="itemB">두 번째 아이템</param>
    /// <param name="destinationB">두 번째 목적지</param>
    /// <param name="reason">검사 실패 사유</param>
    /// <returns>양쪽 배치 결과의 유효 여부</returns>
    bool CheckAppliedPair (
        ItemCarry itemA, PlacementData destinationA,
        ItemCarry itemB, PlacementData destinationB,
        out string reason )
    {
        if ( CheckPair(
            itemA, destinationA.Target,
            itemB, destinationB.Target, out reason ) == false )
            return false;

        if ( CheckAppliedPlacement(
            itemA, destinationA, _collidersA,
            itemB.transform, out reason ) == false ||
             CheckAppliedPlacement(
            itemB, destinationB, _collidersB,
            itemA.transform, out reason ) == false )
            return false;

        return PlacementSpaceChecker.IsPairClear(
            itemA.transform, _collidersA,
            new Pose( itemA.transform.position, itemA.transform.rotation ),
            itemB.transform, _collidersB,
            new Pose( itemB.transform.position, itemB.transform.rotation ),
            _contactTolerance, out reason );
    }

    #endregion

}
