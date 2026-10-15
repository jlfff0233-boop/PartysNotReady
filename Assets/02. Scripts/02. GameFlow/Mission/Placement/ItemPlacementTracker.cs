using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

/// <summary>
/// 일반 아이템 배치 추적 - 대여 회차별 기록과 배치 알림 관리
/// </summary>
public class ItemPlacementTracker : IDisposable
{
    Scene _scene;		//추적할 플레이 씬
    bool _isSubscribed;		//아이템 등록과 상태 알림 연결 여부
    bool _isRecheckingItems;		//일괄 재검사 진행 여부

    /// <summary>
    /// 아이템 데이터별 검증된 배치 미션 설정
    /// </summary>
    IReadOnlyDictionary<ItemData, ItemPlacementMissionData> _itemSetups;

    /// <summary>
    /// 아이템별 대여 회차와 배치 기록
    /// </summary>
    Dictionary<ItemCarry, ItemPlacementMissionRecord> _items = new( );

    /// <summary>
    /// 일괄 재검사 중 사용할 배치 기록 목록
    /// </summary>
    List<ItemPlacementMissionRecord> _itemRecords = new( );

    /// <summary>
    /// 추적 시작 시 등록할 활성 아이템 목록
    /// </summary>
    List<ItemCarry> _itemBuffer = new( );

    /// <summary>
    /// 추적 중인 아이템 수
    /// </summary>
    public int TrackedItemCount => _items.Count;

    /// <summary>
    /// 준비 목표 집계 갱신 요청
    /// </summary>
    public event Action OnChanged;

    /// <summary>
    /// 직접 배치 완료 알림(미션, 대여 회차별 결과 ID)
    /// </summary>
    public event Action<MissionData, string> OnValidPlacement;

    /// <summary>
    /// 추적할 씬과 검증된 배치 설정 연결
    /// </summary>
    /// <param name="scene">플레이 씬</param>
    /// <param name="setups">아이템 데이터별 배치 설정</param>
    public ItemPlacementTracker (
        Scene scene, IReadOnlyDictionary<ItemData, ItemPlacementMissionData> setups )
    {
        _scene = scene;
        _itemSetups = setups;
    }

    /// <summary>
    /// 아이템 등록 알림 연결과 기존 아이템 추적 재개
    /// </summary>
    public void Begin ()
    {
        if ( _isSubscribed == true )
            return;

        _isSubscribed = true;
        if ( _itemSetups.Count == 0 )
            return;

        ItemRegister.OnRegistered += RegisterItemPlacement;
        ItemRegister.GetActive( _itemBuffer );
        foreach ( ItemCarry item in _itemBuffer )
            RegisterItemPlacement( item );
        _itemBuffer.Clear( );
    }

    /// <summary>
    /// 알림을 해제하고 같은 대여 회차의 기록은 재개를 위해 유지
    /// </summary>
    public void Dispose ()
    {
        if ( _isSubscribed == false )
            return;

        _isSubscribed = false;
        ItemRegister.OnRegistered -= RegisterItemPlacement;
        foreach ( ItemPlacementMissionRecord record in _items.Values )
            record.Unsubscribe( );
    }

    /// <summary>
    /// 일반 아이템 배치 추적 등록
    /// </summary>
    /// <param name="item">등록할 아이템</param>
    void RegisterItemPlacement ( ItemCarry item )
    {
        if ( _isSubscribed == false ||
             item == null ||
             item.isActiveAndEnabled == false ||
             item.gameObject.scene != _scene ||
             item.ItemData == null ||
             _itemSetups.TryGetValue(
                 item.ItemData, out ItemPlacementMissionData setup ) == false )
            return;

        if ( _items.TryGetValue(
            item, out ItemPlacementMissionRecord record ) == true )
        {
            bool isNewRent =
                record.PoolRentVersion != item.PoolRentVersion;		//새 대여 회차 여부

            if ( isNewRent == true || record.Setup != setup )
            {
                record.Unsubscribe( );

                //같은 회차의 설정 변경에서는 기존 물건 ID 유지
                string nextResultId = isNewRent == true
                    ? Guid.NewGuid( ).ToString( "N" )
                    : record.ResultId;

                record = new(
                    item, setup, nextResultId,
                    RecheckItemPlacement );

                _items [ item ] = record;
            }

            record.Subscribe( );
            return;
        }

        //최초 추적 시에만 물건 ID 생성
        string resultId = Guid.NewGuid( ).ToString( "N" );

        record = new(
            item, setup, resultId,
            RecheckItemPlacement );

        _items.Add( item, record );
        record.Subscribe( );
    }

    /// <summary>
    /// 일반 아이템 추적 기록 제거
    /// </summary>
    /// <param name="record">제거할 배치 기록</param>
    void RemoveItemPlacement (
        ItemPlacementMissionRecord record )
    {
        record.Unsubscribe( );

        //파괴된 Unity 객체도 딕셔너리 키 참조로 제거
        _items.Remove( record.Item );
    }

    /// <summary>
    /// 일반 아이템 정착 확인과 결과 전달
    /// </summary>
    /// <param name="record">검사할 배치 기록</param>
    void RecheckItemPlacement (
        ItemPlacementMissionRecord record )
    {
        if ( _isSubscribed == false )
            return;

        ItemCarry item = record.Item;

        //스냅샷에 남은 이전 기록은 새 기록을 변경하지 않음
        if ( _items.TryGetValue(
            item, out ItemPlacementMissionRecord current ) == false ||
             current != record )
            return;

        if ( item == null )
        {
            RemoveItemPlacement( record );

            if ( _isRecheckingItems == false )
                OnChanged?.Invoke( );

            return;
        }

        if ( item.isActiveAndEnabled == false )
        {
            //재활성화할 때 같은 기록과 물건 ID 재사용
            record.Unsubscribe( );

            if ( _isRecheckingItems == false )
                OnChanged?.Invoke( );

            return;
        }

        //대여 회차나 아이템 종류가 바뀌면 이전 요청으로 인정하지 않음
        if ( item.PoolRentVersion != record.PoolRentVersion ||
             item.ItemData != record.Setup.ItemData )
        {
            record.Unsubscribe( );
            RegisterItemPlacement( item );

            if ( _isRecheckingItems == false )
                OnChanged?.Invoke( );

            return;
        }

        bool isPlaced = record.CheckPlacement( );

        //개별 이벤트 또는
        //배치 성공 전달 직전에만 즉시 갱신
        if ( _isRecheckingItems == false || isPlaced == true )
            OnChanged?.Invoke( );

        if ( isPlaced == false || _isSubscribed == false )
            return;

        //집계 갱신 후 미션에 배치 결과 전달
        OnValidPlacement?.Invoke(
            record.Setup.PlacementMission, record.ResultId );
    }

    /// <summary>
    /// 일반 아이템 배치 대기와 추적 수명 재검사
    /// </summary>
    public void RecheckAll ()
    {
        if ( _isSubscribed == false || _isRecheckingItems == true )
            return;

        _isRecheckingItems = true;

        try
        {
            //결과 콜백 중 등록 목록이 바뀌어도 순회 유지
            _itemRecords.Clear( );

            foreach ( ItemPlacementMissionRecord record in _items.Values )
                _itemRecords.Add( record );

            foreach ( ItemPlacementMissionRecord record in _itemRecords )
            {
                if ( _isSubscribed == false )
                    break;

                RecheckItemPlacement( record );
            }
        }
        finally
        {
            _itemRecords.Clear( );
            _isRecheckingItems = false;
        }
    }
}
