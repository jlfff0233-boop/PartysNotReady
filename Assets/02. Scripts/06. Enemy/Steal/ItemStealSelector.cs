using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 절도 대상 선택 - 방에 있는 아이템 검사, 가까운 대상 선정
/// </summary>
public class ItemStealSelector
{
    ItemDataMap _itemMap;     //절도 가능 아이템 맵
    IngredientStockManager _stock;		//등록된 공급원 조회
    /// <summary>
    /// 방에 등록된 개별 대상 목록
    /// </summary>
    List<IRoomTarget> _targets = new( );
    /// <summary>
    /// 이번 플레이에 등록된 공급원 목록
    /// </summary>
    List<IngredientSource> _sources = new( );


    /// <summary>
    /// 아이템 맵 연결
    /// </summary>
    /// <param name="itemMap">아이템 맵</param>
    /// <param name="stock">공급원 재고 중재자</param>
    public ItemStealSelector ( ItemDataMap itemMap, IngredientStockManager stock )
    {
        _itemMap = itemMap;
        _stock = stock;
    }


    #region ----- 조회 -----

    /// <summary>
    /// 방 대상 목록 조회
    /// </summary>
    /// <param name="room">조회할 방</param>
    void GetTargets ( Room room )
    {
        RoomRegister.GetTargets( room, _targets );
        if ( _stock != null )
            _stock.GetSources( _sources );
        else
            _sources.Clear( );
    }

    /// <summary>
    /// 아이템 조회
    /// </summary>
    /// <param name="target">방 대상</param>
    /// <param name="item">조회한 아이템</param>
    /// <returns>조회 성공 여부</returns>
    bool TryGetItem ( IRoomTarget target, out ItemCarry item )
    {
        item = null;

        if ( target == null )
            return false;

        //등록된 대상도 조회 참여 상태를 다시 확인
        if ( target.IsRoomTargetActive == false )
            return false;

        Transform targetTransform = target.TargetTransform;
        if ( targetTransform == null )
            return false;

        //방 대상과 운반 기능은 같은 루트에서 조회
        if ( targetTransform.TryGetComponent( out item ) == false )
            return false;

        return CheckItem( item );
    }


    /// <summary>
    /// 가까운 절도 대상 선택
    /// </summary>
    /// <param name="room">조회할 방</param>
    /// <param name="position">거리 기준 위치</param>
    /// <param name="remainingCapacity">남은 수용량</param>
    /// <param name="excluded">이번 수집의 제외 대상</param>
    /// <param name="excludedSources">접근 또는 획득에 실패한 공급원</param>
    /// <param name="selected">선택한 절도 대상</param>
    /// <returns>선택 성공 여부</returns>
    public bool TrySelect (
        Room room, Vector3 position,
        int remainingCapacity,
        HashSet<ItemCarry> excluded,
        HashSet<IngredientSource> excludedSources,
        out ItemStealTargetData selected )
    {
        selected = default;
        bool found = false;

        if ( remainingCapacity <= 0 )
            return false;

        //운반이나 방 이동으로 달라진 현재 위치 반영
        GetTargets( room );
        float nearestSqrDist = float.PositiveInfinity;

        foreach ( IRoomTarget target in _targets )
        {
            if ( TryGetItem( target, out ItemCarry candidate ) == false )
                continue;

            //이번 수집에서 접근 등에 실패한 대상은 재선택하지 않음
            if ( excluded != null && excluded.Contains( candidate ) )
                continue;

            //들어가지 않는 물건은 건너뛰고 다음 후보 확인
            if ( candidate.ItemData.CapacityCost > remainingCapacity )
            {
                continue;
            }

            //거리 순서만 비교하며 실제 경로는 접근 단계에서 확인
            float sqrDist =
                ( candidate.transform.position - position ).sqrMagnitude;

            if ( sqrDist >= nearestSqrDist )
                continue;

            //더 가까운 유효 후보로 교체
            nearestSqrDist = sqrDist;
            selected = new ItemStealTargetData( candidate );
            found = true;
        }

        foreach ( IngredientSource source in _sources )
        {
            if ( CheckSource( room, source ) == false ||
                 excludedSources.Contains( source ) == true ||
                 source.SupplyItemData.CapacityCost > remainingCapacity )
                continue;

            float sqrDist = ( source.StealPoint.position - position ).sqrMagnitude;
            if ( sqrDist >= nearestSqrDist )
                continue;

            nearestSqrDist = sqrDist;
            selected = new ItemStealTargetData( source );
            found = true;
        }

        return found;
    }


    /// <summary>
    /// 방의 절도 대상 유무 확인
    /// </summary>
    /// <param name="room">조회할 방</param>
    /// <returns>대상 존재 여부</returns>
    public bool HasTarget ( Room room )
    {
        //이전 조회 목록을 비우고 현재 방의 대상으로 갱신
        GetTargets( room );

        //수용량과 접근 실패 여부는 제외하고 방의 대상 유무만 확인
        foreach ( IRoomTarget target in _targets )
        {
            if ( TryGetItem( target, out _ ) == true )
                return true;
        }

        foreach ( IngredientSource source in _sources )
        {
            if ( CheckSource( room, source ) == true )
                return true;
        }

        return false;
    }

    #endregion

    #region ----- 대상 검사 -----

    /// <summary>
    /// 아이템 절도 조건 확인
    /// </summary>
    /// <param name="item">검사할 아이템</param>
    /// <returns>절도 가능 여부</returns>
    bool CheckItem ( ItemCarry item )
    {
        if ( item == null || item.isActiveAndEnabled == false )
            return false;

        //플레이어와 다른 적이 보유한 물건 모두 제외
        if ( item.IsHold == true )
            return false;

        //공급원 본체와 하위 배치물을 개별 아이템으로 운반하지 않음
        foreach ( IngredientSource source in _sources )
        {
            if ( source.Contains( item.transform ) == true )
                return false;
        }

        //종류와 용량을 확인할 수 없는 물건 제외
        ItemData data = item.ItemData;
        if ( data == null || data.CapacityCost <= 0 )
            return false;

        //손실 처리가 가능한 아이템만 절도 대상으로 선택
        if ( item.TryGetComponent( out ItemLoss loss ) == false ||
             loss.isActiveAndEnabled == false ||
             loss.IsLost == true )
            return false;

        //운반 가능 여부와 별개로 허용 맵에 등록된 종류만 선택
        if ( _itemMap == null || _itemMap.CheckRegister( data ) == false )
            return false;

        //사건 진행, 사후 처리와 정상 기준 갱신 대기 대상 제외
        if ( item.TryGetComponent( out AnomalyTarget anomaly ) &&
             anomaly.NormalLocked == true )
            return false;

        //숨김과 복구 대기 대상 제외
        if ( item.TryGetComponent( out PresenceController presence ) )
        {
            if ( presence.IsPresent == false ||
                 presence.IsVisible == false ||
                 presence.IsRestoring == true )
                return false;
        }

        return true;
    }

    /// <summary>
    /// 대상 상태와 방 소속 재확인
    /// </summary>
    /// <param name="room">수집 중인 방</param>
    /// <param name="item">검사할 아이템</param>
    /// <returns>선택 유지 여부</returns>
    public bool CanSelect ( Room room, ItemCarry item )
    {
        if ( room == null || room.isActiveAndEnabled == false ||
             item == null )
            return false;

        //접근 중 보유되거나 변칙이 적용된 경우 선택 해제
        IRoomTarget target = item.GetComponent<IRoomTarget>( );

        if ( TryGetItem( target, out ItemCarry currentItem ) == false ||
             currentItem != item )
            return false;

        //최초 선택 위치 대신 현재 위치로 방 소속 재확인
        if ( RoomRegister.TryGetRoom(
            target.TargetTransform.position, out Room currentRoom ) == false )
            return false;

        return currentRoom == room;
    }

    /// <summary>
    /// 공급원 상태와 활동 방 소속 확인
    /// </summary>
    /// <param name="room">수집 중인 방</param>
    /// <param name="source">검사할 공급원</param>
    /// <returns>공급원 절도 가능 여부</returns>
    bool CheckSource ( Room room, IngredientSource source )
    {
        if ( room == null || room.isActiveAndEnabled == false ||
             source == null || source.CanSteal == false ||
             source.SuppliesLossItem == false )
            return false;

        ItemData data = source.SupplyItemData;
        if ( data.CapacityCost <= 0 || _itemMap == null ||
             _itemMap.CheckRegister( data ) == false )
            return false;

        return RoomRegister.TryGetRoom( source.SupplyRoot.position, out Room currentRoom ) == true &&
            currentRoom == room;
    }

    /// <summary>
    /// 개별 아이템 또는 공급원 재확인
    /// </summary>
    /// <param name="room">수집 중인 방</param>
    /// <param name="target">검사할 절도 대상</param>
    /// <returns>선택 유지 여부</returns>
    public bool CanSelect ( Room room, ItemStealTargetData target )
    {
        return target.IsSource == true
            ? CheckSource( room, target.Source )
            : CanSelect( room, target.Item );
    }

    #endregion
}
