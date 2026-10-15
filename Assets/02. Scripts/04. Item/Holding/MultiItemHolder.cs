using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 복수 아이템 보유 모듈 - 확보, 회수와 용량 관리
/// </summary>
public class MultiItemHolder : IDisposable
{
    ItemCapacityModel _capacity;     //수용량 모델
    ItemHolder _pendingHolder;     //확보 중인 보유 모듈

    bool _isExecuting;     //보유 처리 중
    bool _isDisposed;     //사용 종료 여부

    /// <summary>
    /// 보유 정보 딕셔너리(아이템, 운반 정보)
    /// </summary>
    Dictionary<ICarryable, MultiItemHoldData> _items = new( );

    /// <summary>
    /// 보유 개수
    /// </summary>
    public int Count => _items.Count;

    /// <summary>
    /// 사용 중인 용량
    /// </summary>
    public int UsedCapacity => _capacity.UsedCapacity;

    /// <summary>
    /// 남은 수용량
    /// </summary>
    public int RemainingCapacity => _capacity.RemainingCapacity;


    /// <summary>
    /// 최대 수용량 설정
    /// </summary>
    /// <param name="capacity">최대 수용량</param>
    public MultiItemHolder ( int capacity )
    {
        _capacity = new ItemCapacityModel( capacity );
    }


    #region ----- 보유 정리 -----

    /// <summary>
    /// 보유 정보 제거
    /// </summary>
    /// <param name="item">제거할 아이템</param>
    void RemoveItem ( ICarryable item )
    {
        if ( _items.TryGetValue( item, out MultiItemHoldData data ) == false )
            return;

        //해제 중 발생하는 알림에서 중복 차감하지 않도록 먼저 제거
        _items.Remove( item );
        data.Holder.OnHoldEnded -= HandleHoldEnded;

        _capacity.TryRemove( data.CapacityCost );

        //이미 회수된 물건은 건드리지 않고 남은 보유만 해제
        data.Holder.Dispose( );
    }

    /// <summary>
    /// 회수된 아이템 정리
    /// </summary>
    /// <param name="item">보유가 끝난 아이템</param>
    void HandleHoldEnded ( ICarryable item )
    {
        //보유 해제와 비활성화 모두 해당 물건의 용량만 차감
        RemoveItem( item );
    }

    /// <summary>
    /// 모든 보유 아이템 정리
    /// </summary>
    void ReleaseItems ()
    {
        //물건 해제 중 다른 물건의 알림이 발생해도 순회 목록 유지
        List<ICarryable> items = new( _items.Keys );

        foreach ( ICarryable item in items )
            RemoveItem( item );
    }

    /// <summary>
    /// 모든 아이템 내려놓기
    /// </summary>
    /// <returns>정리 요청 처리 여부</returns>
    public bool TryReleaseAll ()
    {
        if ( _isDisposed || _isExecuting )
            return false;

        //해제 알림에서 새 물건을 추가하지 못하도록 차단
        _isExecuting = true;

        ReleaseItems( );

        _isExecuting = false;
        return true;
    }

    /// <summary>
    /// 보유 종료
    /// </summary>
    public void Dispose ()
    {
        if ( _isDisposed == true )
            return;

        _isDisposed = true;

        //확보 알림 도중 종료돼도 임시 보유를 남기지 않음
        _pendingHolder?.Dispose( );
        ReleaseItems( );
    }

    #endregion

    #region ----- 확보 -----

    /// <summary>
    /// 아이템 확보
    /// </summary>
    /// <param name="item">확보할 아이템</param>
    /// <param name="holdPoint">보유 지점</param>
    /// <param name="ownerColliders">보유자 충돌체</param>
    /// <returns>확보 성공 여부</returns>
    public bool TryAdd (
        ItemCarry item, Transform holdPoint,
        Collider [ ] ownerColliders )
    {
        if ( _isDisposed == true || _isExecuting == true || CanAdd( item ) == false )
            return false;

        //확보 당시 용량 보관
        int capacityCost = item.ItemData.CapacityCost;
        ItemHolder holder = new( HoldType.World );
        bool isRegistered = false;

        _isExecuting = true;
        _pendingHolder = holder;

        try
        {
            //부모 변경과 충돌 처리, 실제 보유 검사는 기존 모듈 사용
            if ( holder.TryPickUp( item, holdPoint, ownerColliders ) == false )
                return false;

            //집기 알림에서 이 모듈이 종료됐는지 재확인
            if ( _isDisposed )
                return false;

            //실제로 확보한 물건만 사용 용량에 반영
            if ( _capacity.TryAdd( capacityCost ) == false )
                return false;

            _items.Add( item, new MultiItemHoldData( holder, capacityCost ) );
            holder.OnHoldEnded += HandleHoldEnded;

            isRegistered = true;
            return true;
        }
        finally
        {
            _pendingHolder = null;

            //목록에 등록하지 못한 물건의 임시 보유 정리
            if ( isRegistered == false )
                holder.Dispose( );

            _isExecuting = false;
        }
    }

    #endregion

    #region ----- 조회/검사 -----

    /// <summary>
    /// 실제 보유 아이템 조회
    /// </summary>
    /// <param name="results">조회 결과</param>
    public void GetItems ( List<ItemCarry> results )
    {
        results.Clear( );

        foreach ( KeyValuePair<ICarryable, MultiItemHoldData> pair in _items )
        {
            if ( pair.Key is not ItemCarry item || item == null )
                continue;

            //손실 처리 전에도 보유 방식과 부모가 일치하는지 확인
            if ( pair.Value.Holder.IsHolding( item ) == true )
                results.Add( item );
        }
    }

    /// <summary>
    /// 추가 가능 여부 확인
    /// </summary>
    /// <param name="item">확인할 아이템</param>
    /// <returns>추가 가능 여부</returns>
    bool CanAdd ( ItemCarry item )
    {
        if ( item == null || item.isActiveAndEnabled == false )
            return false;

        //다른 보유자의 물건과 중복 등록 제외
        if ( item.IsHold == true || _items.ContainsKey( item ) )
            return false;

        ItemData data = item.ItemData;
        if ( data == null )
            return false;

        return _capacity.CanAdd( data.CapacityCost );
    }

    /// <summary>
    /// 지정한 아이템의 실제 보유 여부 확인
    /// </summary>
    /// <param name="item">확인할 아이템</param>
    /// <returns>보유 여부</returns>
    public bool IsHolding ( ItemCarry item )
    {
        if ( _isDisposed || item == null )
            return false;

        if ( _items.TryGetValue(
            item, out MultiItemHoldData data ) == false )
            return false;

        //목록에 남아 있어도 보유 방식과 부모가 달라졌다면 제외
        return data.Holder.IsHolding( item );
    }

    #endregion
}
