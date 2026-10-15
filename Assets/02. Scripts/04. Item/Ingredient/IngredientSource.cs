using System;
using TMPro;
using UnityEngine;

/// <summary>
/// 재료 공급원 - 풀 대여와 보유 전달, 재고 표시
/// </summary>
public class IngredientSource : MonoBehaviour
{
    [SerializeField] IngredientStockData _data;     //공급원 재료 설정

    [Header("----- 컴포넌트 -----")]
    [SerializeField] ItemCarry _itemPrefab;     //공급원에서 생성할 재료 프리팹
    [SerializeField] ProgressBarHud _refillHud;     //재보충 게이지
    [SerializeField] TMP_Text _stockText;       //현재 재고와 최대 재고 표시
    [SerializeField] Transform _supplyRoot;		//씬에 배치된 공급원 상태 확인 루트
    [SerializeField] Transform _stealPoint;		//적이 실제로 재료를 꺼낼 접근 지점

    PoolingManager _pool;		//재료 프리팹의 풀 대여
    IngredientStockManager _stock;     //재고 중재자
    ProductionManager _productManager;		//공급 그릇의 제작 등록

    bool _isReady;     //공급원 등록 완료 여부
    uint _stockVersion;		//재고 연결 회차
    int _shownCount = -1;		//마지막 표시 수량
    int _shownMaxCount = -1;		//마지막 표시 최대 수량

    /// <summary>
    /// 공급 아이템 정보
    /// </summary>
    public ItemData SupplyItemData => _itemPrefab.ItemData;

    /// <summary>
    /// 공급원 상태 확인 루트
    /// </summary>
    public Transform SupplyRoot => _supplyRoot != null ? _supplyRoot : transform;

    /// <summary>
    /// 절도 접근 지점
    /// </summary>
    public Transform StealPoint => _stealPoint;

    /// <summary>
    /// 음식 공급 여부
    /// </summary>
    public bool SuppliesFood => _itemPrefab.GetComponent<Food>( ) != null;

    /// <summary>
    /// 손실 가능한 아이템 공급 여부
    /// </summary>
    public bool SuppliesLossItem => _itemPrefab.TryGetComponent( out ItemLoss loss ) == true &&
        loss.enabled == true;

    /// <summary>
    /// 적의 공급원 절도 가능 여부
    /// </summary>
    public bool CanSteal => CanTake == true && _stealPoint != null &&
        _stealPoint.gameObject.activeInHierarchy == true;

    /// <summary>
    /// 획득 가능한 재료 존재 여부
    /// </summary>
    public bool CanTake =>
        _isReady && isActiveAndEnabled &&
        CanSupply( ) == true &&
        _stock.HasAvailableStock( this );


    /// <summary>
    /// 공급원 재고 등록과 초기 풀 객체 준비
    /// </summary>
    /// <param name="stock">재고 중재자</param>
    /// <returns>초기화 완료 여부</returns>
    public bool Init ( IngredientStockManager stock )
    {
        if ( _isReady == true )
            return _stock == stock;

        if ( stock == null || _data == null ||
             _itemPrefab == null || _itemPrefab.ItemData == null ||
             _itemPrefab.GetComponent<ItemPoolHandler>( ) == null )
            return false;

        ItemWorldController view = _itemPrefab.GetComponent<ItemWorldController>( );

        if ( view.CheckConfiguration( out string reason ) == false )
        {
            Debug.LogError(
                $"[재료 공급원 초기화 실패] {name} / " +
                $"{_itemPrefab.name}: {reason}", this );
            return false;
        }

        GameManager game = GameManager.Instance;
        if ( game == null || game.IsReady == false )
            return false;

        if ( stock.TryRegisterSource( this, _data ) == false )
            return false;

        _pool = game.Pool;
        _stock = stock;
        _stockVersion++;
        _pool.PreparePool( _itemPrefab.gameObject );
        _isReady = true;
        SetStockCount( _data.MaxCount, _data.MaxCount );
        SetRefillProgress( false, 0f );
        return true;
    }


    /// <summary>
    /// 풀에서 비활성 재료를 대여하고 데이터 준비
    /// </summary>
    /// <returns>준비한 객체 또는 없음</returns>
    ItemCarry RentItem ()
    {
        GameObject rented = _pool.GetFromPool( _itemPrefab.gameObject, gameObject.scene );
        ItemCarry item = rented.GetComponent<ItemCarry>( );

        if ( item.TryPreparePoolRent( _itemPrefab.ItemData ) == false )
        {
            Debug.LogError( $"[재료 대여 실패] {name} / 아이템 구성을 확인해 주세요.", this );
            _stock.QueueReturn( item );
            return null;
        }

        item.transform.SetPositionAndRotation( transform.position, transform.rotation );

        ItemPoolHandler handler = item.GetComponent<ItemPoolHandler>( );
        if ( handler.TryPreparePoolRent(
            _productManager, out string reason ) == false )
        {
            Debug.LogError( $"[공급 아이템 준비 실패] {name} / {reason}", this );
            _stock.QueueReturn( item );
            return null;
        }

        return item;
    }

    /// <summary>
    /// 이번 플레이의 제작 관리자 연결
    /// </summary>
    /// <param name="manager">제작 관리자</param>
    public void BindProduction ( ProductionManager manager )
    {
        _productManager = manager;
    }


    /// <summary>
    /// 예약한 객체를 활성화하고 실제 획득 확정
    /// </summary>
    /// <param name="item">예약된 재료</param>
    /// <param name="canAcquire">요청자의 활동 유지 확인</param>
    /// <param name="acquire">실제 보유 요청</param>
    /// <param name="isHolding">요청자의 실제 보유 확인</param>
    /// <returns>집기 성공 여부</returns>
    bool TryDeliver (
        ItemCarry item, Func<bool> canAcquire,
        Func<ItemCarry, bool> acquire, Func<ItemCarry, bool> isHolding )
    {
        int instanceId = item.gameObject.GetInstanceID( );     //활성화 중 소실돼도 예약 취소
        IngredientStockManager stock = _stock;		//활성화 콜백 이후에도 예약 정리
        uint version = _stockVersion;		//종료와 재시작을 구분할 연결 회차
        bool confirmed = false;		//획득 확정 여부
        bool wasTaken = false;		//확보 직후 해제된 물건도 공급 수량에 반영

        Action<HoldType, HoldType> handleTaken = ( previous, current ) =>
        {
            if ( previous != HoldType.None || current == HoldType.None )
                return;

            wasTaken = true;
            if ( _isReady == true && _stockVersion == version && confirmed == false )
                confirmed = stock.TryConfirmTaken( this, item );
        };
        item.OnHoldChanged += handleTaken;

        try
        {
            item.gameObject.SetActive( true );

            bool taken = item != null && item.isActiveAndEnabled == true &&
                item.IsHold == false && _isReady == true && _stockVersion == version &&
                isActiveAndEnabled == true && CanSupply( ) == true &&
                canAcquire( ) == true && acquire( item ) == true;

            //자신이 예약한 마지막 재고는 가용 수량으로 다시 검사하지 않음
            if ( confirmed == false && _isReady == true && _stockVersion == version &&
                 item != null && ( wasTaken == true || item.IsHold == true ) )
                confirmed = stock.TryConfirmTaken( this, item );

            return taken == true && confirmed == true &&
                _isReady == true && _stockVersion == version &&
                canAcquire( ) == true && isHolding( item ) == true;
        }
        finally
        {
            if ( item != null )
            {
                item.OnHoldChanged -= handleTaken;

                //보유 알림에서 즉시 해제됐어도 실제 공급은 취소하지 않음
                if ( confirmed == false && _isReady == true && _stockVersion == version &&
                     ( wasTaken == true || item.IsHold == true ) )
                    confirmed = stock.TryConfirmTaken( this, item );
            }

            //실패와 예외 모두 예약을 취소하고 반환 대기에 등록
            if ( confirmed == false && _stockVersion == version )
                stock.TryCancelReserve( this, instanceId );
        }
    }

    /// <summary>
    /// 재료 전달
    /// </summary>
    /// <param name="holder">플레이어의 보유 처리</param>
    /// <param name="holdPoint">보유 지점</param>
    /// <returns>실제 집기 성공 여부</returns>
    public bool TryTake ( ItemHolder holder, Transform holdPoint )
    {
        if ( CanTake == false || holder == null || holder.HasItem ||
             holdPoint == null || holdPoint.gameObject.activeInHierarchy == false )
            return false;

        ItemCarry item = RentItem( );
        if ( item == null )
            return false;

        if ( _stock.TryReserve( this, item ) == false )
        {
            _stock.QueueReturn( item );
            return false;
        }

        return TryDeliver( item,
            () => holdPoint != null && holdPoint.gameObject.activeInHierarchy == true,
            candidate => holder.TryPickUp( candidate, holdPoint ), holder.IsHolding );
    }

    /// <summary>
    /// 공급원에서 적에게 실제 아이템 한 개 전달
    /// </summary>
    /// <param name="canAcquire">적의 활동 유지 확인</param>
    /// <param name="acquire">적의 실제 보유 요청</param>
    /// <param name="isHolding">적의 실제 보유 확인</param>
    /// <param name="item">전달을 시도한 아이템</param>
    /// <returns>재고 확정과 실제 보유 성공 여부</returns>
    public bool TrySteal (
        Func<bool> canAcquire, Func<ItemCarry, bool> acquire,
        Func<ItemCarry, bool> isHolding, out ItemCarry item )
    {
        item = null;
        if ( CanSteal == false || canAcquire( ) == false )
            return false;

        item = RentItem( );
        if ( item == null )
            return false;

        if ( _stock.TryReserve( this, item ) == false )
        {
            _stock.QueueReturn( item );
            return false;
        }

        item.transform.SetPositionAndRotation( _stealPoint.position, _stealPoint.rotation );
        return TryDeliver( item, canAcquire, acquire, isHolding );
    }

    /// <summary>
    /// 공급원에 속한 배치 객체 확인
    /// </summary>
    /// <param name="target">조회할 객체 위치</param>
    /// <returns>공급원 루트 또는 하위 객체 여부</returns>
    public bool Contains ( Transform target )
    {
        return target != null && ( target.IsChildOf( transform ) == true ||
            target.IsChildOf( SupplyRoot ) == true );
    }

    /// <summary>
    /// 바뀐 재고 수량만 표시
    /// </summary>
    /// <param name="currentCount">현재 수량</param>
    /// <param name="maxCount">최대 수량</param>
    public void SetStockCount ( int currentCount, int maxCount )
    {
        if ( _stockText == null ||
             ( _shownCount == currentCount && _shownMaxCount == maxCount ) )
            return;

        _shownCount = currentCount;
        _shownMaxCount = maxCount;
        _stockText.text = $"{currentCount} / {maxCount}";
    }

    /// <summary>
    /// 재보충 게이지 표시
    /// </summary>
    /// <param name="visible">재보충 진행 여부</param>
    /// <param name="progress">완료 진행률</param>
    public void SetRefillProgress ( bool visible, float progress )
    {
        if ( _refillHud == null )
            return;
        _refillHud.SetProgress( progress );
        _refillHud.SetVisible( visible );
    }


    /// <summary>
    /// 공급원의 재고 연결 해제
    /// </summary>
    /// <param name="stock">연결을 해제할 재고 중재자</param>
    public void ClearStock ( IngredientStockManager stock )
    {
        if ( _stock != stock )
            return;

        _isReady = false;
        _stockVersion++;
        SetRefillProgress( false, 0f );
        if ( _stockText != null )
            _stockText.text = string.Empty;
        _shownCount = -1;
        _shownMaxCount = -1;
        _stock = null;
        _pool = null;
        _productManager = null;
    }
    /// <summary>
    /// 공급원 자체 조작 중 재료 공급 허용 여부
    /// </summary>
    /// <returns>재료 공급 가능 여부</returns>
    bool CanSupply ()
    {
        return CheckSupplyState( transform ) == true &&
            ( SupplyRoot == transform || CheckSupplyState( SupplyRoot ) == true );
    }

    /// <summary>
    /// 공급원 배치 객체의 활성과 조작 상태 확인
    /// </summary>
    /// <param name="root">상태를 확인할 배치 루트</param>
    /// <returns>공급 상태 유지 여부</returns>
    bool CheckSupplyState ( Transform root )
    {
        if ( root.gameObject.activeInHierarchy == false )
            return false;

        //공급원 자신 또는 부모에 연결된 사건 대상 확인
        AnomalyTarget target = root.GetComponentInParent<AnomalyTarget>( );

        if ( target != null && target.NormalLocked == true )
            return false;

        //운반 중인 공급원에서 재료를 꺼내지 않음
        ItemCarry carry = root.GetComponentInParent<ItemCarry>( );

        if ( carry != null && carry.IsHold == true )
            return false;

        PresenceController presence = root.GetComponentInParent<PresenceController>( );
        if ( presence != null && ( presence.IsPresent == false ||
             presence.IsVisible == false || presence.IsRestoring == true ) )
            return false;

        return true;
    }

#if UNITY_EDITOR
    /// <summary>
    /// 등록된 공급원의 재고 상태 조회
    /// </summary>
    /// <returns>현재 재고와 재보충 상태</returns>
    public string GetStockStatus ()
    {
        return _stock != null ? _stock.GetDebugStockStatus( this ) : "재고 연결 없음";
    }

    /// <summary>
    /// 재고와 재보충 상태 기록
    /// </summary>
    [ContextMenu( "Debug/재고 상태 확인" )]
    void TestStockStatus ()
    {
        string status = _stock != null
            ? _stock.GetDebugStockStatus( this ) : "플레이 중 등록된 공급원이 필요합니다.";
        Debug.Log( $"[재고 확인] {name} / {status}", this );
        PlayDebugHistory.Record( "재고 확인", this, status );
    }

    /// <summary>
    /// 공급원 재고 소진과 리필 시작
    /// </summary>
    [ContextMenu( "Debug/재고 소진 및 리필 시작" )]
    void TestConsumeStock ()
    {
        string reason = "플레이 중 등록된 공급원이 필요합니다.";
        bool consumed = _stock != null && _stock.TryDebugConsumeStock( this, out reason );
        Debug.Log( $"[재고 소비 테스트] {name} / {consumed} / {reason}", this );
        PlayDebugHistory.Record( "재고 소비 테스트", this, reason );
    }
#endif

}
